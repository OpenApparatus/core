using System;

namespace OpenApparatus.Topology;

/// <summary>
/// A boundary segment between two cells, or between one cell and the outside.
/// Adjacencies are produced by an <see cref="IFloorPlanGenerator"/> (always with
/// <see cref="Passage.Closed"/> initially) and then assigned passages by an
/// <see cref="IPassageAssigner"/> to form the floor plan's connectivity.
/// </summary>
public sealed class Adjacency
{
    public Cell CellA { get; }

    /// <summary>Null when this is an outer-boundary adjacency (cell ↔ outside).</summary>
    public Cell? CellB { get; }

    /// <summary>
    /// The boundary segment in world XZ. Direction follows the CCW outline convention:
    /// walking Start → End, CellA is on the left and CellB (or outside) is on the right.
    /// </summary>
    public EdgeSegment SharedSegment { get; }

    /// <summary>Mutable — set by the passage assigner. Defaults to <see cref="Passage.Closed.Instance"/>.</summary>
    public Passage Passage { get; set; }

    public Adjacency(Cell cellA, Cell? cellB, EdgeSegment sharedSegment, Passage? initialPassage = null)
    {
        CellA = cellA ?? throw new ArgumentNullException(nameof(cellA));
        CellB = cellB;
        SharedSegment = sharedSegment;
        Passage = initialPassage ?? Passage.Closed.Instance;
    }

    /// <summary>True if this connects two cells (false for outer-boundary adjacencies).</summary>
    public bool IsInternal => CellB is not null;

    /// <summary>True if this connects one cell to the outside (false for inter-cell adjacencies).</summary>
    public bool IsOuter => CellB is null;

    /// <summary>Returns the other cell across this adjacency, or null if outer.</summary>
    public Cell? Other(Cell cell)
    {
        if (cell == CellA) return CellB;
        if (cell == CellB) return CellA;
        throw new ArgumentException($"Cell #{cell.Id} is not part of this adjacency.", nameof(cell));
    }

    public override string ToString()
    {
        string b = CellB is null ? "outside" : $"#{CellB.Id}";
        return $"#{CellA.Id} <-> {b} : {Passage} along {SharedSegment}";
    }
}
