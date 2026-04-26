using System;
using System.Collections.Generic;
using OpenApparatus.Topology;

namespace OpenApparatus.Geometry;

/// <summary>
/// Walks a <see cref="FloorPlan"/> and produces one merged <see cref="MeshData"/>
/// per cell. The result is a list of <see cref="AssembledCellMesh"/> in the same
/// order as <see cref="FloorPlan.Cells"/>.
///
/// Per cell, the assembler:
///   1. Builds the cell's interior (floor + ceiling) via the appropriate
///      <c>I*InteriorBuilder</c> for the cell's shape.
///   2. For each adjacency the cell touches, decides whether this cell owns the
///      wall (lower-id ownership for internal adjacencies; CellA owns its outer
///      adjacencies). Walls owned by this cell are built via
///      <see cref="BoundaryWallBuilder"/> and appended to its mesh parts.
///   3. Combines all parts with <see cref="MeshData.Combine"/>.
///
/// In v1 only <see cref="RectangleShape"/> is supported. Other shapes will
/// throw — extending support means adding a builder for that shape and a
/// dispatch case below.
///
/// Known limitation: at outer corners of a building, walls don't extend past
/// their segment ends, so a t/2 × t/2 square gap remains. To be fixed in a
/// subsequent milestone (corner posts, or wall length extension).
/// </summary>
public sealed class FloorPlanMeshAssembler
{
    readonly RectangleInteriorBuilder _rectInterior = new();
    readonly BoundaryWallBuilder _wallBuilder = new();

    public IReadOnlyList<AssembledCellMesh> Assemble(
        FloorPlan plan, float wallThickness, float wallHeight)
    {
        if (plan is null) throw new ArgumentNullException(nameof(plan));
        if (wallThickness <= 0f) throw new ArgumentOutOfRangeException(nameof(wallThickness));
        if (wallHeight <= 0f) throw new ArgumentOutOfRangeException(nameof(wallHeight));

        // Phase 1: cell interiors.
        // Bucket holds the parts (interior + assigned walls) for each cell.
        var partsByCellId = new Dictionary<int, List<MeshData>>(plan.Cells.Count);
        foreach (var cell in plan.Cells)
        {
            var interior = BuildInteriorFor(cell, wallThickness, wallHeight);
            partsByCellId[cell.Id] = new List<MeshData> { interior };
        }

        // Phase 2: assign each adjacency's wall to its owner cell.
        foreach (var adj in plan.Adjacencies)
        {
            int ownerId = ResolveWallOwner(adj);
            var wall = _wallBuilder.Build(adj, wallThickness, wallHeight);
            partsByCellId[ownerId].Add(wall);
        }

        // Phase 3: combine each cell's parts.
        var result = new AssembledCellMesh[plan.Cells.Count];
        for (int i = 0; i < plan.Cells.Count; i++)
        {
            var cell = plan.Cells[i];
            var combined = MeshData.Combine(partsByCellId[cell.Id]);
            result[i] = new AssembledCellMesh(cell, combined);
        }
        return result;
    }

    MeshData BuildInteriorFor(Cell cell, float t, float h)
    {
        return cell.Shape switch
        {
            RectangleShape => _rectInterior.Build(cell, t, h),
            _ => throw new InvalidOperationException(
                $"FloorPlanMeshAssembler does not yet support cell shape '{cell.Shape.GetType().Name}'. " +
                "Add an interior builder + a dispatch case in BuildInteriorFor."),
        };
    }

    /// <summary>
    /// The cell whose mesh will contain the wall geometry for this adjacency.
    /// • Outer adjacency: CellA (the only cell).
    /// • Internal adjacency: lower-id cell.
    /// </summary>
    static int ResolveWallOwner(Adjacency adj)
    {
        if (adj.IsOuter) return adj.CellA.Id;
        return adj.CellA.Id < adj.CellB!.Id ? adj.CellA.Id : adj.CellB.Id;
    }
}
