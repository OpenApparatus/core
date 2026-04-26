using System;
using System.Collections.Generic;

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
    /// A solid wall with one or more rectangular openings cut into it. Each opening
    /// sits on the floor (y=0) and rises to the opening's height.
    /// </summary>
    public sealed class Doorway : Passage
    {
        public IReadOnlyList<Opening> Openings { get; }

        public Doorway(IReadOnlyList<Opening> openings)
        {
            if (openings is null) throw new ArgumentNullException(nameof(openings));
            if (openings.Count == 0)
                throw new ArgumentException("Doorway must have at least one opening.", nameof(openings));
            Openings = openings;
        }

        /// <summary>Convenience constructor for the common single-opening case.</summary>
        public Doorway(float offsetAlongEdge, float width, float height)
            : this(new[] { new Opening(offsetAlongEdge, width, height) })
        {
        }

        // Convenience accessors for single-opening doorways. Iterate Openings directly
        // for the multi-opening case.
        public float OffsetAlongEdge => Openings[0].OffsetAlongEdge;
        public float Width => Openings[0].Width;
        public float Height => Openings[0].Height;

        public override string ToString() =>
            Openings.Count == 1
                ? $"Doorway(offset={OffsetAlongEdge:F2}, w={Width:F2}, h={Height:F2})"
                : $"Doorway({Openings.Count} openings)";
    }
}

/// <summary>
/// One rectangular opening in a doorway wall. <see cref="OffsetAlongEdge"/> is
/// the distance from the shared segment's Start to the opening's left edge.
/// <see cref="SillHeight"/> is the bottom of the opening; 0 = a door (sits on
/// the floor), &gt;0 = a window (a wall panel below the opening). <see cref="Height"/>
/// is the top of the opening (head height), measured from the floor.
///
/// <para><see cref="HingeAtEnd"/> and <see cref="SwingNegative"/> are
/// presentation hints for door panels: they pick which end of the opening
/// the hinge sits on and which side of the wall the door swings into. The
/// geometric opening (the rectangular hole through the wall) is unaffected
/// — these flags are read by 2D editors and render-time door props in
/// downstream tools.</para>
/// </summary>
public readonly struct Opening : IEquatable<Opening>
{
    public float OffsetAlongEdge { get; }
    public float Width { get; }
    public float Height { get; }
    public float SillHeight { get; }
    public bool HingeAtEnd { get; }
    public bool SwingNegative { get; }

    public Opening(
        float offsetAlongEdge, float width, float height,
        float sillHeight = 0f,
        bool hingeAtEnd = false,
        bool swingNegative = false)
    {
        if (offsetAlongEdge < 0f) throw new ArgumentOutOfRangeException(nameof(offsetAlongEdge));
        if (width <= 0f) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0f) throw new ArgumentOutOfRangeException(nameof(height));
        if (sillHeight < 0f) throw new ArgumentOutOfRangeException(nameof(sillHeight));
        if (sillHeight >= height)
            throw new ArgumentOutOfRangeException(nameof(sillHeight),
                $"SillHeight ({sillHeight}) must be less than Height ({height}).");
        OffsetAlongEdge = offsetAlongEdge;
        Width = width;
        Height = height;
        SillHeight = sillHeight;
        HingeAtEnd = hingeAtEnd;
        SwingNegative = swingNegative;
    }

    /// <summary>True when <see cref="SillHeight"/> &gt; 0 — the opening floats above the floor.</summary>
    public bool IsWindow => SillHeight > 0f;

    /// <summary>Returns a copy with the named field replaced. Lets editors mutate
    /// one piece of state without rebuilding the whole struct in client code.</summary>
    public Opening With(
        float? offsetAlongEdge = null,
        float? width = null,
        float? height = null,
        float? sillHeight = null,
        bool? hingeAtEnd = null,
        bool? swingNegative = null)
        => new Opening(
            offsetAlongEdge ?? OffsetAlongEdge,
            width ?? Width,
            height ?? Height,
            sillHeight ?? SillHeight,
            hingeAtEnd ?? HingeAtEnd,
            swingNegative ?? SwingNegative);

    public bool Equals(Opening other) =>
        OffsetAlongEdge == other.OffsetAlongEdge && Width == other.Width
        && Height == other.Height && SillHeight == other.SillHeight
        && HingeAtEnd == other.HingeAtEnd && SwingNegative == other.SwingNegative;
    public override bool Equals(object? obj) => obj is Opening o && Equals(o);
    public override int GetHashCode() => HashCode.Combine(
        OffsetAlongEdge, Width, Height, SillHeight, HingeAtEnd, SwingNegative);
    public override string ToString() =>
        SillHeight > 0f
            ? $"({OffsetAlongEdge:F2}, w={Width:F2}, sill={SillHeight:F2}, h={Height:F2})"
            : $"({OffsetAlongEdge:F2}, w={Width:F2}, h={Height:F2})";
}
