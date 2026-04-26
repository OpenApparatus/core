using OpenApparatus.Topology;

namespace OpenApparatus.Geometry;

/// <summary>
/// One cell's combined mesh as produced by <see cref="FloorPlanMeshAssembler"/>:
/// the original <see cref="Topology.Cell"/> alongside its assembled <see cref="MeshData"/>
/// (floor + ceiling + assigned walls, one per submesh).
/// </summary>
public sealed record AssembledCellMesh(Cell Cell, MeshData Mesh);
