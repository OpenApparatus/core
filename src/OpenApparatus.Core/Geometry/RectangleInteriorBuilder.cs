using System;
using System.Numerics;
using OpenApparatus.Topology;

namespace OpenApparatus.Geometry;

/// <summary>
/// Builds the floor + ceiling of a rectangle cell for the production pipeline.
/// Uses the "split convention": walls live AT the cell outline (centered with
/// thickness t/2 on each side), so the interior occupies
/// (t/2, t/2)..(W − t/2, D − t/2). The walls themselves are added separately
/// by <see cref="BoundaryWallBuilder"/>; the assembler combines the two.
///
/// This is the production-side counterpart to the all-walls-included
/// <see cref="RectangleGeometryBuilder"/> (kept for isolated-cell preview).
/// </summary>
public sealed class RectangleInteriorBuilder
{
    /// <summary>
    /// Returns a MeshData with floor (submesh 0) + ceiling (submesh 2) populated;
    /// walls (submesh 1) is present but empty so the result composes cleanly with
    /// per-adjacency wall meshes.
    /// </summary>
    public MeshData Build(Cell cell, float wallThickness, float wallHeight)
    {
        if (cell is null) throw new ArgumentNullException(nameof(cell));
        if (cell.Shape is not RectangleShape rect)
            throw new InvalidOperationException(
                $"RectangleInteriorBuilder requires a RectangleShape; got {cell.Shape.GetType().Name}.");
        if (wallThickness <= 0f) throw new ArgumentOutOfRangeException(nameof(wallThickness));
        if (wallHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(wallHeight));
        if (wallThickness >= rect.Width || wallThickness >= rect.Depth)
            throw new ArgumentException(
                $"Wall thickness {wallThickness} too large for {rect.Width}×{rect.Depth} cell " +
                $"(would leave no interior).");

        float W = rect.Width;
        float D = rect.Depth;
        float h = wallHeight;
        float t2 = wallThickness * 0.5f;

        var b = new MeshDataBuilder();

        // Floor: y=0, normal +Y. CCW-from-+Y order is (xMin,zMin), (xMin,zMax), (xMax,zMax), (xMax,zMin).
        b.AddQuadAutoUv(SubmeshIndex.Floor,
            cell.LocalToWorld(new Vector2(t2,     t2),     0f),
            cell.LocalToWorld(new Vector2(t2,     D - t2), 0f),
            cell.LocalToWorld(new Vector2(W - t2, D - t2), 0f),
            cell.LocalToWorld(new Vector2(W - t2, t2),     0f));

        // Ceiling: y=h, normal −Y. Reversed winding from floor.
        b.AddQuadAutoUv(SubmeshIndex.Ceiling,
            cell.LocalToWorld(new Vector2(t2,     t2),     h),
            cell.LocalToWorld(new Vector2(W - t2, t2),     h),
            cell.LocalToWorld(new Vector2(W - t2, D - t2), h),
            cell.LocalToWorld(new Vector2(t2,     D - t2), h));

        b.EnsureSubmeshCount(SubmeshIndex.Count);
        return b.ToMeshData();
    }
}
