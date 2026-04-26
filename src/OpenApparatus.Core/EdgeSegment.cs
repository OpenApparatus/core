using System;
using System.Numerics;

namespace OpenApparatus;

/// <summary>
/// A directed line segment in the 2D XZ plane. Used as the unit of cell-outline
/// description and inter-cell adjacency: one edge of a room outline, or the
/// shared boundary between two rooms.
///
/// Direction matters: walking from <see cref="Start"/> to <see cref="End"/>, the
/// "inside" of the cell or adjacency is on the left (CCW outline convention).
/// </summary>
public readonly struct EdgeSegment : IEquatable<EdgeSegment>
{
    public Vector2 Start { get; }
    public Vector2 End { get; }

    public EdgeSegment(Vector2 start, Vector2 end)
    {
        Start = start;
        End = end;
    }

    public Vector2 Midpoint => (Start + End) * 0.5f;
    public Vector2 Delta => End - Start;
    public float Length => Delta.Length();

    /// <summary>Unit vector from Start toward End. Undefined for zero-length segments.</summary>
    public Vector2 Direction
    {
        get
        {
            var d = Delta;
            float len = d.Length();
            return len > 0f ? d / len : Vector2.Zero;
        }
    }

    /// <summary>
    /// Unit vector 90° counter-clockwise from <see cref="Direction"/>. For an outline
    /// edge ordered CCW around the cell, this normal points OUTWARD (away from the cell's
    /// interior). For an adjacency segment with CellA on the left, it points toward CellB.
    /// </summary>
    public Vector2 Normal
    {
        get
        {
            var d = Direction;
            return new Vector2(-d.Y, d.X);
        }
    }

    /// <summary>Returns the same segment with Start and End swapped (and Direction/Normal flipped).</summary>
    public EdgeSegment Reversed() => new(End, Start);

    public bool Equals(EdgeSegment other) => Start.Equals(other.Start) && End.Equals(other.End);
    public override bool Equals(object? obj) => obj is EdgeSegment other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Start, End);
    public override string ToString() => $"({Start.X:F3},{Start.Y:F3}) -> ({End.X:F3},{End.Y:F3})";

    public static bool operator ==(EdgeSegment a, EdgeSegment b) => a.Equals(b);
    public static bool operator !=(EdgeSegment a, EdgeSegment b) => !a.Equals(b);
}
