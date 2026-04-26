using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Concatenates several mesh-datas into one. Vertex/normal/UV arrays are appended;
    /// submesh triangles are merged by index, with indices offset by the running vertex count
    /// so each input's triangles continue to reference its own vertices.
    /// Submesh count of the result = max submesh count across inputs.
    /// </summary>
    public static MeshData Combine(IReadOnlyList<MeshData> parts)
    {
        if (parts is null) throw new ArgumentNullException(nameof(parts));
        if (parts.Count == 0)
            return new MeshData(Array.Empty<Vector3>(), Array.Empty<Vector3>(),
                Array.Empty<Vector2>(), Array.Empty<int[]>());

        int totalVerts = 0;
        int submeshCount = 0;
        for (int i = 0; i < parts.Count; i++)
        {
            totalVerts += parts[i].VertexCount;
            if (parts[i].SubmeshCount > submeshCount) submeshCount = parts[i].SubmeshCount;
        }

        var verts = new Vector3[totalVerts];
        var normals = new Vector3[totalVerts];
        var uv0 = new Vector2[totalVerts];
        var perSubmeshIndices = new List<int>[submeshCount];
        for (int s = 0; s < submeshCount; s++) perSubmeshIndices[s] = new List<int>();

        int vertOffset = 0;
        foreach (var part in parts)
        {
            Array.Copy(part.Vertices, 0, verts, vertOffset, part.VertexCount);
            Array.Copy(part.Normals, 0, normals, vertOffset, part.VertexCount);
            Array.Copy(part.Uv0, 0, uv0, vertOffset, part.VertexCount);

            for (int s = 0; s < part.SubmeshCount; s++)
            {
                var srcTris = part.SubmeshIndices[s];
                var dstList = perSubmeshIndices[s];
                for (int i = 0; i < srcTris.Length; i++)
                    dstList.Add(srcTris[i] + vertOffset);
            }

            vertOffset += part.VertexCount;
        }

        var submeshArrays = new int[submeshCount][];
        for (int s = 0; s < submeshCount; s++)
            submeshArrays[s] = perSubmeshIndices[s].ToArray();

        return new MeshData(verts, normals, uv0, submeshArrays);
    }
}
