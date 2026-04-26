using System;
using System.Numerics;

namespace OpenApparatus;

/// <summary>
/// An axis-aligned bounding box in the 2D XZ plane. Used for cell-local outline
/// extents, world-space floor-plan extents, and quick adjacency screening.
/// </summary>
public readonly struct Bounds2D : IEquatable<Bounds2D>
{
    public Vector2 Min { get; }
    public Vector2 Max { get; }

    public Bounds2D(Vector2 min, Vector2 max)
    {
        if (max.X < min.X || max.Y < min.Y)
            throw new ArgumentException("Bounds2D max must be component-wise >= min.");
        Min = min;
        Max = max;
    }

    public Vector2 Size => Max - Min;
    public Vector2 Center => (Min + Max) * 0.5f;
    public float Width => Max.X - Min.X;
    public float Depth => Max.Y - Min.Y;
    public float Area => Width * Depth;

    /// <summary>True if <paramref name="point"/> is within these bounds (inclusive).</summary>
    public bool Contains(Vector2 point) =>
        point.X >= Min.X && point.X <= Max.X &&
        point.Y >= Min.Y && point.Y <= Max.Y;

    /// <summary>Smallest bounds containing both inputs.</summary>
    public static Bounds2D Union(Bounds2D a, Bounds2D b) => new(
        Vector2.Min(a.Min, b.Min),
        Vector2.Max(a.Max, b.Max));

    /// <summary>Smallest bounds containing the original plus <paramref name="point"/>.</summary>
    public Bounds2D Encapsulate(Vector2 point) => new(
        Vector2.Min(Min, point),
        Vector2.Max(Max, point));

    public bool Equals(Bounds2D other) => Min.Equals(other.Min) && Max.Equals(other.Max);
    public override bool Equals(object? obj) => obj is Bounds2D other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Min, Max);
    public override string ToString() => $"[{Min.X:F3},{Min.Y:F3}]..[{Max.X:F3},{Max.Y:F3}]";

    public static bool operator ==(Bounds2D a, Bounds2D b) => a.Equals(b);
    public static bool operator !=(Bounds2D a, Bounds2D b) => !a.Equals(b);
}
