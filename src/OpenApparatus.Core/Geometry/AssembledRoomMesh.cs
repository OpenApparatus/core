using OpenApparatus.Topology;

namespace OpenApparatus.Geometry;

/// <summary>
/// One room's combined mesh as produced by <see cref="MultiRoomEnvironmentMeshAssembler"/>:
/// the original <see cref="Topology.Room"/> alongside its assembled <see cref="MeshData"/>
/// (floor + ceiling + assigned walls, one per submesh).
/// </summary>
public sealed record AssembledRoomMesh(Room Room, MeshData Mesh);
