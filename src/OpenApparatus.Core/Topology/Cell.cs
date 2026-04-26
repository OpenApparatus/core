using System;
using System.Collections.Generic;
using System.Numerics;

namespace OpenApparatus.Topology;

/// <summary>
/// A single room / region of a floor plan. Owns a <see cref="ICellShape"/> outline,
/// a world-space placement, and a classification used by generators.
///
/// The shape lives in cell-local coordinates with the local origin defined by the
/// shape implementation. World-space outline is computed by translating + rotating
/// the local outline by <see cref="Position"/> and <see cref="Rotation"/>.
/// </summary>
public sealed class Cell
{
    public int Id { get; }
    public ICellShape Shape { get; }

    /// <summary>World XZ position of the cell's local-origin point.</summary>
    public Vector2 Position { get; }

    /// <summary>Rotation about the +Y axis, in degrees. CCW positive (looking down).</summary>
    public float Rotation { get; }

    /// <summary>Generator-assigned room type. Drives downstream behavior (e.g. entrance preference).</summary>
    public RoomType RoomType { get; }

    public Cell(int id, ICellShape shape, Vector2 position, RoomType roomType, float rotation = 0f)
    {
        Id = id;
        Shape = shape ?? throw new ArgumentNullException(nameof(shape));
        Position = position;
        Rotation = rotation;
        RoomType = roomType;
    }

    /// <summary>The cell's outline transformed into world coordinates.</summary>
    public IReadOnlyList<EdgeSegment> GetWorldOutline()
    {
        var local = Shape.GetOutline();
        if (Rotation == 0f && Position == Vector2.Zero) return local;

        var rotated = new EdgeSegment[local.Count];
        for (int i = 0; i < local.Count; i++)
            rotated[i] = TransformEdge(local[i]);
        return rotated;
    }

    /// <summary>
    /// Transform a cell-local 2D point (in XZ) into a world-space 3D point at the
    /// given <paramref name="y"/>, applying <see cref="Position"/> and <see cref="Rotation"/>.
    /// </summary>
    public System.Numerics.Vector3 LocalToWorld(System.Numerics.Vector2 cellLocal, float y)
    {
        if (Rotation == 0f)
            return new System.Numerics.Vector3(cellLocal.X + Position.X, y, cellLocal.Y + Position.Y);

        float rad = Rotation * MathF.PI / 180f;
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);
        float rx = cellLocal.X * cos - cellLocal.Y * sin;
        float rz = cellLocal.X * sin + cellLocal.Y * cos;
        return new System.Numerics.Vector3(rx + Position.X, y, rz + Position.Y);
    }

    /// <summary>The cell's bounds in world coordinates (axis-aligned even if shape is rotated).</summary>
    public Bounds2D GetWorldBounds()
    {
        var local = Shape.GetLocalBounds();
        if (Rotation == 0f) return new Bounds2D(local.Min + Position, local.Max + Position);

        // Encapsulate the rotated corners.
        Vector2 c0 = TransformPoint(local.Min);
        Vector2 c1 = TransformPoint(new Vector2(local.Max.X, local.Min.Y));
        Vector2 c2 = TransformPoint(local.Max);
        Vector2 c3 = TransformPoint(new Vector2(local.Min.X, local.Max.Y));
        var b = new Bounds2D(c0, c0);
        b = b.Encapsulate(c1).Encapsulate(c2).Encapsulate(c3);
        return b;
    }

    EdgeSegment TransformEdge(EdgeSegment e) =>
        new(TransformPoint(e.Start), TransformPoint(e.End));

    Vector2 TransformPoint(Vector2 local)
    {
        if (Rotation == 0f) return local + Position;
        float rad = Rotation * MathF.PI / 180f;
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);
        var rotated = new Vector2(
            local.X * cos - local.Y * sin,
            local.X * sin + local.Y * cos);
        return rotated + Position;
    }

    public override string ToString() => $"Cell #{Id} {RoomType} @ ({Position.X:F2},{Position.Y:F2})";
}
