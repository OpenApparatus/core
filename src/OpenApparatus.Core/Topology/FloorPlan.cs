using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenApparatus.Topology;

/// <summary>
/// The output of <see cref="IFloorPlanGenerator.Generate"/>: a set of cells and the
/// adjacencies between them (and to the outside). This is the engine-agnostic
/// description of a floor plan; the geometry layer turns it into meshes.
/// </summary>
public sealed class FloorPlan
{
    public IReadOnlyList<Cell> Cells { get; }
    public IReadOnlyList<Adjacency> Adjacencies { get; }

    public FloorPlan(IReadOnlyList<Cell> cells, IReadOnlyList<Adjacency> adjacencies)
    {
        Cells = cells ?? throw new ArgumentNullException(nameof(cells));
        Adjacencies = adjacencies ?? throw new ArgumentNullException(nameof(adjacencies));
    }

    /// <summary>Bounds in world coordinates encompassing every cell.</summary>
    public Bounds2D GetWorldBounds()
    {
        if (Cells.Count == 0)
            throw new InvalidOperationException("Floor plan has no cells.");
        var b = Cells[0].GetWorldBounds();
        for (int i = 1; i < Cells.Count; i++)
            b = Bounds2D.Union(b, Cells[i].GetWorldBounds());
        return b;
    }

    /// <summary>All adjacencies that touch <paramref name="cell"/> (internal or outer).</summary>
    public IEnumerable<Adjacency> GetAdjacenciesOf(Cell cell)
    {
        for (int i = 0; i < Adjacencies.Count; i++)
        {
            var a = Adjacencies[i];
            if (a.CellA == cell || a.CellB == cell) yield return a;
        }
    }

    /// <summary>All adjacencies between two cells (excludes outer boundaries).</summary>
    public IEnumerable<Adjacency> GetInternalAdjacencies() =>
        Adjacencies.Where(a => a.IsInternal);

    /// <summary>All adjacencies between a cell and the outside.</summary>
    public IEnumerable<Adjacency> GetOuterAdjacencies() =>
        Adjacencies.Where(a => a.IsOuter);
}
