using System.Numerics;
using OpenApparatus.Geometry;

namespace OpenApparatus.Tests.Geometry;

public class MeshDataTests
{
    [Fact]
    public void Constructor_RequiresMatchingArrayLengths()
    {
        var verts = new[] { Vector3.Zero, Vector3.UnitX };
        var normalsBad = new[] { Vector3.UnitY };
        var uvs = new[] { Vector2.Zero, Vector2.One };
        Assert.Throws<ArgumentException>(() =>
            new MeshData(verts, normalsBad, uvs, new[] { new[] { 0, 1, 0 } }));
    }

    [Fact]
    public void Constructor_NullArgs_Throw()
    {
        var verts = new[] { Vector3.Zero };
        Assert.Throws<ArgumentNullException>(() => new MeshData(null!, [], [], []));
        Assert.Throws<ArgumentNullException>(() => new MeshData(verts, null!, [], []));
    }

    [Fact]
    public void Builder_EmptyMesh_ProducesValidEmptyData()
    {
        var mesh = new MeshDataBuilder().ToMeshData();
        Assert.Equal(0, mesh.VertexCount);
        Assert.Equal(0, mesh.SubmeshCount);
    }

    [Fact]
    public void Builder_SingleQuad_ProducesFourVertsAndOneSubmeshOfTwoTriangles()
    {
        var b = new MeshDataBuilder();
        b.AddQuadAutoUv(0,
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(1, 0, 1),
            new Vector3(0, 0, 1));
        // (b-a) = +X, (c-a) = (1,0,1), cross = (0,−1,0) ⇒ normal −Y. Just verify shape.
        var mesh = b.ToMeshData();
        Assert.Equal(4, mesh.VertexCount);
        Assert.Equal(1, mesh.SubmeshCount);
        Assert.Equal(2, mesh.TriangleCount(0));
    }

    [Fact]
    public void Builder_NormalIsUnitLength()
    {
        var b = new MeshDataBuilder();
        b.AddQuadAutoUv(0,
            new Vector3(0, 0, 0),
            new Vector3(0, 0, 1),
            new Vector3(1, 0, 1),
            new Vector3(1, 0, 0));
        var mesh = b.ToMeshData();
        for (int i = 0; i < mesh.Normals.Length; i++)
            Assert.Equal(1f, mesh.Normals[i].Length(), precision: 4);
    }

    [Fact]
    public void Builder_AllVerticesOnQuad_ShareSameNormal()
    {
        var b = new MeshDataBuilder();
        b.AddQuadAutoUv(0,
            new Vector3(0, 0, 0),
            new Vector3(0, 0, 1),
            new Vector3(1, 0, 1),
            new Vector3(1, 0, 0));
        var mesh = b.ToMeshData();
        for (int i = 1; i < 4; i++)
            Assert.Equal(mesh.Normals[0], mesh.Normals[i]);
    }

    [Fact]
    public void Builder_RejectsSparseSubmeshIndices()
    {
        var b = new MeshDataBuilder();
        // submesh 0 and 2 used, 1 skipped — should throw.
        b.AddQuadAutoUv(0, Vector3.Zero, Vector3.UnitX, Vector3.UnitY + Vector3.UnitX, Vector3.UnitY);
        b.AddQuadAutoUv(2, Vector3.Zero, Vector3.UnitX, Vector3.UnitY + Vector3.UnitX, Vector3.UnitY);
        Assert.Throws<InvalidOperationException>(() => b.ToMeshData());
    }
}
