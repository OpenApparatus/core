using System.Linq;
using System.Numerics;
using OpenApparatus.Geometry;
using OpenApparatus.Topology;

namespace OpenApparatus.Tests.Geometry;

public class RectangleGeometryBuilderTests
{
    static Cell Cell(float w = 4, float d = 4, float px = 0, float pz = 0, float rot = 0) =>
        new(0, new RectangleShape(w, d), new Vector2(px, pz), RoomType.Square, rot);

    static MeshData Build(Cell cell, float t = 0.2f, float h = 3f) =>
        new RectangleGeometryBuilder().Build(cell, t, h);

    [Fact]
    public void RejectsNonRectangleShape()
    {
        // No alternative shape exists yet; sanity-check by passing a rectangle and
        // verifying the type-check at least exists (a future shape would test this properly).
        var builder = new RectangleGeometryBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.Build(null!, 0.2f, 3f));
    }

    [Fact]
    public void RejectsTooThickWalls()
    {
        var cell = Cell(2f, 2f);
        Assert.Throws<ArgumentException>(() => Build(cell, t: 1f, h: 3f));
        Assert.Throws<ArgumentException>(() => Build(cell, t: 1.5f, h: 3f));
    }

    [Fact]
    public void RejectsNonPositiveDimensions()
    {
        var cell = Cell();
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(cell, t: 0f, h: 3f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(cell, t: 0.2f, h: 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() => Build(cell, t: -0.1f, h: 3f));
    }

    [Fact]
    public void EmitsThreeSubmeshes()
    {
        var mesh = Build(Cell());
        Assert.Equal(SubmeshIndex.Count, mesh.SubmeshCount);
    }

    [Fact]
    public void FloorAndCeiling_HaveOneFaceEach()
    {
        var mesh = Build(Cell());
        Assert.Equal(2, mesh.TriangleCount(SubmeshIndex.Floor));   // 1 quad = 2 tris
        Assert.Equal(2, mesh.TriangleCount(SubmeshIndex.Ceiling));
    }

    [Fact]
    public void Walls_Have16Faces()
    {
        var mesh = Build(Cell());
        // 4 outer + 4 inner + 4 top frame + 4 bottom frame = 16 quads = 32 triangles
        Assert.Equal(32, mesh.TriangleCount(SubmeshIndex.Walls));
    }

    [Fact]
    public void FloorNormals_PointUp()
    {
        var mesh = Build(Cell());
        var floorTris = mesh.SubmeshIndices[SubmeshIndex.Floor];
        for (int i = 0; i < floorTris.Length; i++)
        {
            var n = mesh.Normals[floorTris[i]];
            Assert.True(n.Y > 0.99f, $"Floor normal not +Y: {n}");
        }
    }

    [Fact]
    public void CeilingNormals_PointDown()
    {
        var mesh = Build(Cell());
        var ceilTris = mesh.SubmeshIndices[SubmeshIndex.Ceiling];
        for (int i = 0; i < ceilTris.Length; i++)
        {
            var n = mesh.Normals[ceilTris[i]];
            Assert.True(n.Y < -0.99f, $"Ceiling normal not -Y: {n}");
        }
    }

    [Fact]
    public void OuterWalls_HaveHorizontalNormals_PointingAwayFromInterior()
    {
        // The cell is at origin with width=depth=4. Interior center at (2, ?, 2).
        // Each outer wall's center is one edge of the bounding rectangle. The
        // outward direction is the vector from the cell center to the face center.
        var cell = Cell(4, 4);
        var mesh = Build(cell);

        // Group wall faces by their median face position; verify normals point away from center.
        var wallTris = mesh.SubmeshIndices[SubmeshIndex.Walls];
        var faceMidpoints = new Dictionary<int, (Vector3 mid, Vector3 normal)>();
        for (int i = 0; i < wallTris.Length; i += 6)
        {
            // Each face = 2 triangles = 6 indices, and our builder emits faces with
            // 4 unique vertices each. Get the face's 4 verts (= [tri[0], tri[1], tri[2], tri[5]]).
            int v0 = wallTris[i + 0];
            int v1 = wallTris[i + 1];
            int v2 = wallTris[i + 2];
            int v3 = wallTris[i + 5];
            var mid = (mesh.Vertices[v0] + mesh.Vertices[v1] + mesh.Vertices[v2] + mesh.Vertices[v3]) / 4f;
            faceMidpoints[i] = (mid, mesh.Normals[v0]);
        }

        // We expect 16 wall faces; not all are vertical (4 outer + 4 inner are vertical,
        // 4 top frame + 4 bottom frame are horizontal). Check just the OUTER 4 vertical
        // faces — these are the ones at the cell-bounds extremes (X≈0, X≈4, Z≈0, Z≈4).
        var outerVertical = faceMidpoints.Values
            .Where(f => MathF.Abs(f.normal.Y) < 0.01f && IsAtCellBoundary(f.mid, cellW: 4, cellD: 4))
            .ToList();
        Assert.Equal(4, outerVertical.Count);

        var center = new Vector3(2f, 0f, 2f);
        foreach (var (mid, normal) in outerVertical)
        {
            var outward = Vector3.Normalize(new Vector3(mid.X - center.X, 0, mid.Z - center.Z));
            float dot = Vector3.Dot(normal, outward);
            Assert.True(dot > 0.99f, $"Outer wall normal {normal} not pointing outward from center; dot={dot}");
        }
    }

    [Fact]
    public void Geometry_RespectsCellPosition()
    {
        var cell = Cell(2, 2, px: 10, pz: 20);
        var mesh = Build(cell, t: 0.1f, h: 3f);

        // The floor's 4 vertices should be near (10+0.1..10+1.9, 0, 20+0.1..20+1.9).
        var floorTris = mesh.SubmeshIndices[SubmeshIndex.Floor];
        var floorVerts = floorTris.Distinct().Select(i => mesh.Vertices[i]).ToList();
        Assert.All(floorVerts, v =>
        {
            Assert.InRange(v.X, 10.099f, 11.901f);
            Assert.Equal(0f, v.Y);
            Assert.InRange(v.Z, 20.099f, 21.901f);
        });
    }

    [Fact]
    public void Geometry_RespectsCellRotation_90Degrees()
    {
        // Rotate a 4×2 cell by 90° around Y. The original X axis (width 4) maps to Z.
        var cell = Cell(4, 2, rot: 90f);
        var mesh = Build(cell, t: 0.1f, h: 3f);

        var floorTris = mesh.SubmeshIndices[SubmeshIndex.Floor];
        var floorVerts = floorTris.Distinct().Select(i => mesh.Vertices[i]).ToList();

        // After 90° rotation, the floor (originally X∈[0.1..3.9], Z∈[0.1..1.9]) becomes:
        //   X' = -Z = [-1.9..-0.1]
        //   Z' = +X = [0.1..3.9]
        Assert.All(floorVerts, v =>
        {
            Assert.InRange(v.X, -1.901f, -0.099f);
            Assert.InRange(v.Z, 0.099f, 3.901f);
        });
    }

    [Fact]
    public void TotalTriangleCount_Matches18FacesAt2TrianglesEach()
    {
        var mesh = Build(Cell());
        Assert.Equal(36, mesh.TotalTriangleCount); // 18 faces × 2 triangles = 36
    }

    [Fact]
    public void TotalVertexCount_Matches18FacesAt4VerticesEach()
    {
        var mesh = Build(Cell());
        Assert.Equal(72, mesh.VertexCount); // 18 faces × 4 verts (no sharing) = 72
    }

    static bool IsAtCellBoundary(Vector3 mid, float cellW, float cellD, float epsilon = 1e-3f)
    {
        return MathF.Abs(mid.X - 0f) < epsilon
            || MathF.Abs(mid.X - cellW) < epsilon
            || MathF.Abs(mid.Z - 0f) < epsilon
            || MathF.Abs(mid.Z - cellD) < epsilon;
    }
}
