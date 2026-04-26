using OpenApparatus.Topology;

namespace OpenApparatus.Geometry;

/// <summary>
/// Strategy that builds a thick-walled room mesh from a <see cref="Room"/> shape.
/// One implementation per <see cref="IRoomShape"/> kind (e.g. <see cref="RectangleShape"/>).
///
/// The result is a <see cref="MeshData"/> with three submeshes
/// (<see cref="SubmeshIndex.Floor"/>, <see cref="SubmeshIndex.Walls"/>,
/// <see cref="SubmeshIndex.Ceiling"/>).
///
/// In v1 the builder produces a fully-closed room — every wall present, no doorways.
/// Doorways and shared-wall ownership are introduced in milestone A3 and onward.
/// </summary>
public interface IShapeGeometryBuilder
{
    /// <summary>
    /// Build the geometry for one room.
    /// </summary>
    /// <param name="room">The room to mesh. Its <see cref="Room.Shape"/> must match
    /// the implementation's expected shape type.</param>
    /// <param name="wallThickness">Wall thickness, taken inward from the room's footprint
    /// (so the visible interior is (W − 2t) × (D − 2t) for a rectangle).</param>
    /// <param name="wallHeight">Height of the wall in world units. Floor at y=0,
    /// ceiling at y=wallHeight.</param>
    MeshData Build(Room room, float wallThickness, float wallHeight);
}
