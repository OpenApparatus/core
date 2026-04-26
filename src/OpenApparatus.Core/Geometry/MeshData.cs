using System;
using System.Numerics;

namespace OpenApparatus.Geometry;

/// <summary>
/// Engine-agnostic mesh representation. Consumers (Unity adapter, Avalonia
/// studio's OBJ export, etc.) convert this into their respective renderable type.
///
/// Vertex attributes are parallel arrays (vertex[i] has normal[i] and uv0[i]).
/// Submeshes are slices of one shared vertex/normal/UV array, each with their
/// own triangle index list — this matches the conventions of Unity's Mesh,
/// Three.js BufferGeometry, glTF primitives, and most other modern engines.
///
/// Triangle winding: CCW when viewed from the side the normal points to.
/// </summary>
public sealed class MeshData
{
    public Vector3[] Vertices { get; }
    public Vector3[] Normals { get; }
    public Vector2[] Uv0 { get; }

    /// <summary>One triangle index list per submesh. Indices reference the global vertex array.</summary>
    public int[][] SubmeshIndices { get; }

    public MeshData(Vector3[] vertices, Vector3[] normals, Vector2[] uv0, int[][] submeshIndices)
    {
        Vertices = vertices ?? throw new ArgumentNullException(nameof(vertices));
        Normals = normals ?? throw new ArgumentNullException(nameof(normals));
        Uv0 = uv0 ?? throw new ArgumentNullException(nameof(uv0));
        SubmeshIndices = submeshIndices ?? throw new ArgumentNullException(nameof(submeshIndices));

        if (normals.Length != vertices.Length)
            throw new ArgumentException("Normals length must match vertices length.");
        if (uv0.Length != vertices.Length)
            throw new ArgumentException("Uv0 length must match vertices length.");
    }

    public int VertexCount => Vertices.Length;
    public int SubmeshCount => SubmeshIndices.Length;
    public int TriangleCount(int submeshIndex) => SubmeshIndices[submeshIndex].Length / 3;
    public int TotalTriangleCount
    {
        get
        {
            int total = 0;
            for (int i = 0; i < SubmeshIndices.Length; i++)
                total += SubmeshIndices[i].Length / 3;
            return total;
        }
    }
}
