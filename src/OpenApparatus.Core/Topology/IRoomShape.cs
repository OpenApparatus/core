using System.Collections.Generic;

namespace OpenApparatus.Topology;

/// <summary>
/// The 2D footprint of a room in room-local coordinates (XZ plane).
/// Implementations expose the boundary as an ordered list of <see cref="EdgeSegment"/>s,
/// CCW from above, starting at a deterministic vertex (so two equal shapes always have
/// the same edge ordering — this is required for golden-master tests).
/// </summary>
public interface IRoomShape
{
    /// <summary>
    /// The shape's outline as a closed polyline in room-local coordinates,
    /// CCW from above (the room interior is on the left of each edge).
    /// Ordering is shape-specific but deterministic.
    /// </summary>
    IReadOnlyList<EdgeSegment> GetOutline();

    /// <summary>Axis-aligned bounding box of the outline in room-local coordinates.</summary>
    Bounds2D GetLocalBounds();
}
