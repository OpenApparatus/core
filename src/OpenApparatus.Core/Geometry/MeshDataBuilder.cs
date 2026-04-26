using System;
using System.Collections.Generic;
using System.Numerics;

namespace OpenApparatus.Geometry;

/// <summary>
/// Incremental builder for <see cref="MeshData"/>. Owns growing vertex/normal/UV
/// lists plus per-submesh triangle index lists.
///
/// Vertices are appended per face (no sharing across faces — keeps normals sharp
/// at edges). Submesh indices are looked up by integer key; missing keys auto-create.
/// </summary>
public sealed class MeshDataBuilder
{
    readonly List<Vector3> _vertices = new();
    readonly List<Vector3> _normals = new();
    readonly List<Vector2> _uv0 = new();
    readonly Dictionary<int, List<int>> _submeshes = new();

    /// <summary>
    /// Adds a quad face with corners <paramref name="a"/>, <paramref name="b"/>,
    /// <paramref name="c"/>, <paramref name="d"/> in CCW order viewed from the
    /// side the normal points to. UVs are taken at the same indices.
    /// </summary>
    public void AddQuad(int submeshIndex,
        Vector3 a, Vector3 b, Vector3 c, Vector3 d,
        Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD)
    {
        var normal = Vector3.Normalize(Vector3.Cross(b - a, c - a));
        int baseIdx = _vertices.Count;

        _vertices.Add(a);
        _vertices.Add(b);
        _vertices.Add(c);
        _vertices.Add(d);

        _normals.Add(normal);
        _normals.Add(normal);
        _normals.Add(normal);
        _normals.Add(normal);

        _uv0.Add(uvA);
        _uv0.Add(uvB);
        _uv0.Add(uvC);
        _uv0.Add(uvD);

        var tris = GetOrAddSubmesh(submeshIndex);
        tris.Add(baseIdx + 0);
        tris.Add(baseIdx + 1);
        tris.Add(baseIdx + 2);
        tris.Add(baseIdx + 0);
        tris.Add(baseIdx + 2);
        tris.Add(baseIdx + 3);
    }

    /// <summary>
    /// Adds a quad face whose UVs are auto-generated from face-local distance:
    /// uvA=(0,0), uvB=(width,0), uvC=(width,height), uvD=(0,height) where
    /// width = |b-a| and height = |d-a|.
    /// </summary>
    public void AddQuadAutoUv(int submeshIndex, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        float width = (b - a).Length();
        float height = (d - a).Length();
        AddQuad(submeshIndex, a, b, c, d,
            new Vector2(0, 0),
            new Vector2(width, 0),
            new Vector2(width, height),
            new Vector2(0, height));
    }

    public MeshData ToMeshData()
    {
        // Output length = max submesh key + 1. Unused indices in the [0..max] range
        // are emitted as empty arrays — this lets a builder populate only submesh 1
        // (walls) while still producing a 3-submesh MeshData with the conventional
        // index layout (0=Floor, 1=Walls, 2=Ceiling) ready to be combined with other
        // partial MeshData later.
        int submeshCount = 0;
        foreach (var key in _submeshes.Keys)
        {
            if (key < 0)
                throw new InvalidOperationException($"Submesh indices must be non-negative; saw {key}.");
            if (key + 1 > submeshCount) submeshCount = key + 1;
        }

        var submeshArrays = new int[submeshCount][];
        for (int i = 0; i < submeshCount; i++)
            submeshArrays[i] = _submeshes.TryGetValue(i, out var list)
                ? list.ToArray()
                : Array.Empty<int>();

        return new MeshData(
            _vertices.ToArray(),
            _normals.ToArray(),
            _uv0.ToArray(),
            submeshArrays);
    }

    /// <summary>
    /// Forces the output to have at least <paramref name="submeshCount"/> submeshes,
    /// padding any unused indices with empty arrays. Useful when the builder hasn't
    /// touched all conventional submeshes but downstream code expects them.
    /// </summary>
    public void EnsureSubmeshCount(int submeshCount)
    {
        for (int i = 0; i < submeshCount; i++)
        {
            if (!_submeshes.ContainsKey(i))
                _submeshes[i] = new List<int>();
        }
    }

    List<int> GetOrAddSubmesh(int index)
    {
        if (!_submeshes.TryGetValue(index, out var list))
        {
            list = new List<int>();
            _submeshes[index] = list;
        }
        return list;
    }
}
