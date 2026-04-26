using System;

namespace OpenApparatus.Topology;

/// <summary>
/// What lies along the boundary an <see cref="Adjacency"/> represents. Sealed
/// hierarchy — extend by adding a new nested type, never by inheriting outside this file.
/// </summary>
public abstract class Passage
{
    private Passage() { }   // restrict subclasses to nested types only

    /// <summary>A solid wall — no opening between the two rooms (or to the outside).</summary>
    public sealed class Closed : Passage
    {
        public static readonly Closed Instance = new();
        Closed() { }
        public override string ToString() => "Closed";
    }

    /// <summary>No wall at all. The two rooms share floor space (e.g. T-maze internal arms).</summary>
    public sealed class Open : Passage
    {
        public static readonly Open Instance = new();
        Open() { }
        public override string ToString() => "Open";
    }

    /// <summary>
    /// A solid wall with a rectangular doorway cut into it. The door starts at
    /// <see cref="OffsetAlongEdge"/> measured from the shared segment's Start, has the
    /// given <see cref="Width"/>, and rises from the floor to <see cref="Height"/>.
    /// </summary>
    public sealed class Doorway : Passage
    {
        public float OffsetAlongEdge { get; }
        public float Width { get; }
        public float Height { get; }

        public Doorway(float offsetAlongEdge, float width, float height)
        {
            if (offsetAlongEdge < 0f) throw new ArgumentOutOfRangeException(nameof(offsetAlongEdge));
            if (width <= 0f) throw new ArgumentOutOfRangeException(nameof(width));
            if (height <= 0f) throw new ArgumentOutOfRangeException(nameof(height));
            OffsetAlongEdge = offsetAlongEdge;
            Width = width;
            Height = height;
        }

        public override string ToString() =>
            $"Doorway(offset={OffsetAlongEdge:F2}, w={Width:F2}, h={Height:F2})";
    }
}
