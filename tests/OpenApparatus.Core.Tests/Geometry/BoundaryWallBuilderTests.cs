using System.Linq;
using System.Numerics;
using OpenApparatus.Geometry;
using OpenApparatus.Topology;

namespace OpenApparatus.Tests.Geometry;

public class BoundaryWallBuilderTests
{
    static Room CellAt(int id, float x, float z, float w = 1, float d = 1) =>
        new(id, new RectangleShape(w, d), new Vector2(x, z), RoomType.Square);

    /// <summary>
    /// Adjacency between two unit rooms along an east-west boundary at world x=1, z∈[0,1].
    /// Segment direction: -Z (so RoomA — the lower-id room on the +N side — is on the +X side).
    /// Convention is "RoomA on the left of seg.Start→seg.End"; we set RoomA to the right room
    /// (id=0) and the segment direction to make RoomA-side = +N.
    /// </summary>
    static (Room a, Room b, Adjacency adj) MakeInternalAdjacency(Passage passage)
    {
        // RoomA on the +X side, RoomB on the -X side. Segment runs from (1,1) to (1,0)
        // (going -Z). Walking S→E, "left" (i.e. +N) is +X = RoomA's interior. ✓
        var a = CellAt(0, 1, 0);
        var b = CellAt(1, 0, 0);
        var seg = new EdgeSegment(new Vector2(1, 1), new Vector2(1, 0));
        return (a, b, new Adjacency(a, b, seg, passage));
    }

    [Fact]
    public void OpenPassage_ProducesNoTriangles()
    {
        var (_, _, adj) = MakeInternalAdjacency(Passage.Open.Instance);
        var mesh = new BoundaryWallBuilder().Build(adj, 0.2f, 3f);
        Assert.Equal(SubmeshIndex.Count, mesh.SubmeshCount);
        Assert.Equal(0, mesh.TotalTriangleCount);
    }

    [Fact]
    public void ClosedPassage_Produces6Faces()
    {
        var (_, _, adj) = MakeInternalAdjacency(Passage.Closed.Instance);
        var mesh = new BoundaryWallBuilder().Build(adj, 0.2f, 3f);
        // 6 faces × 2 triangles = 12 triangles, all in submesh 1.
        Assert.Equal(12, mesh.TriangleCount(SubmeshIndex.Walls));
        Assert.Equal(0, mesh.TriangleCount(SubmeshIndex.Floor));
        Assert.Equal(0, mesh.TriangleCount(SubmeshIndex.Ceiling));
    }

    [Fact]
    public void ClosedPassage_HasCorrectFaceNormals()
    {
        // Segment from (1,1) to (1,0) → direction -Z → "left" (RoomA side) = +X.
        // Expected normals on the 6 faces: +X, -X, +Y, -Y, +Z (start cap, opposite of dir),
        // -Z (end cap, dir).
        var (_, _, adj) = MakeInternalAdjacency(Passage.Closed.Instance);
        var mesh = new BoundaryWallBuilder().Build(adj, 0.2f, 3f);

        // Each face has 4 verts with the same normal; collect unique normals.
        var uniqueNormals = mesh.Normals
            .Select(n => new Vector3(MathF.Round(n.X, 3), MathF.Round(n.Y, 3), MathF.Round(n.Z, 3)))
            .Distinct()
            .ToHashSet();

        Assert.Contains(new Vector3(+1, 0, 0), uniqueNormals); // RoomA face
        Assert.Contains(new Vector3(-1, 0, 0), uniqueNormals); // RoomB face
        Assert.Contains(new Vector3( 0,+1, 0), uniqueNormals); // Top
        Assert.Contains(new Vector3( 0,-1, 0), uniqueNormals); // Bottom
        Assert.Contains(new Vector3( 0, 0,+1), uniqueNormals); // Start cap (opposite of direction -Z)
        Assert.Contains(new Vector3( 0, 0,-1), uniqueNormals); // End cap (along direction -Z)
        Assert.Equal(6, uniqueNormals.Count);
    }

    [Fact]
    public void ClosedPassage_OuterAdjacency_Works()
    {
        // Outer adjacency (RoomB null) — geometry should still be a 6-face wall.
        var a = CellAt(0, 0, 0);
        var seg = new EdgeSegment(new Vector2(0, 0), new Vector2(1, 0));   // south outer edge
        var adj = new Adjacency(a, null, seg, Passage.Closed.Instance);
        var mesh = new BoundaryWallBuilder().Build(adj, 0.2f, 3f);
        Assert.Equal(12, mesh.TriangleCount(SubmeshIndex.Walls));
    }

    [Fact]
    public void DoorwayPassage_HasMoreFacesThanClosed()
    {
        var (_, _, adj) = MakeInternalAdjacency(
            new Passage.Doorway(offsetAlongEdge: 0.4f, width: 0.2f, height: 2.2f));
        var mesh = new BoundaryWallBuilder().Build(adj, 0.2f, 3f);
        // Centered door, lintel present, both jambs present.
        // Faces: 3 (RoomA: jamb-jamb-lintel) + 3 (RoomB) + 1 (top) + 2 (bottom split) +
        //        2 (caps) + 3 (tunnel: left, right, ceiling) = 14.
        Assert.Equal(14 * 2, mesh.TriangleCount(SubmeshIndex.Walls));
    }

    [Fact]
    public void DoorwayPassage_VertexCount_Matches14FacesAt4VertsEach()
    {
        var (_, _, adj) = MakeInternalAdjacency(
            new Passage.Doorway(offsetAlongEdge: 0.4f, width: 0.2f, height: 2.2f));
        var mesh = new BoundaryWallBuilder().Build(adj, 0.2f, 3f);
        Assert.Equal(14 * 4, mesh.VertexCount);
    }

    [Fact]
    public void DoorwayFlushWithStart_OmitsLeftJambAndLeftBottomAndLeftTunnelSide()
    {
        // doorOffset=0 → no left jamb on either side, no left bottom rim.
        // Tunnel left side stays (still bounds the door); right side stays; ceiling stays.
        // Faces: 2 (right-jamb each side) + 1 (top) + 1 (right bottom only) +
        //        2 (caps) + 3 (tunnel left/right/ceiling) = 9. Wait — both tunnel sides
        //        are still emitted because the door has finite width with two side walls.
        // Reconsider: 2 (right jamb each side) + 1 (top) + 1 (right bottom only) + 2 (caps) +
        //             3 (tunnel) + 0 (no lintel split) ... but lintel IS still present (door
        //             height < wall height). Lintel = 2 (one per side).
        // Total: 2 + 2 + 1 + 1 + 2 + 3 = 11 faces.
        var (_, _, adj) = MakeInternalAdjacency(
            new Passage.Doorway(offsetAlongEdge: 0f, width: 0.4f, height: 2.2f));
        var mesh = new BoundaryWallBuilder().Build(adj, 0.2f, 3f);
        Assert.Equal(11 * 2, mesh.TriangleCount(SubmeshIndex.Walls));
    }

    [Fact]
    public void DoorwayWithTwoOpenings_HasExpected22Faces()
    {
        // Two openings on a 1m wall. Both have lintels (height < wall height) and
        // are interior (non-flush with start or end), so we get the full set of
        // pieces the multi-opening builder emits.
        // Faces for N=2: 2 outer wall sections × 2 sides + 1 between section × 2 sides
        //                = 6
        //                + 2 lintels × 2 sides = 4
        //                + 1 top + 2 caps = 3
        //                + 3 bottom strips (2 between/around) = 3
        //                + 2 tunnels × 3 (left/right/ceiling) = 6
        //                Total = 22 (matches 8N + 6 with N = 2).
        var (_, _, adj) = MakeInternalAdjacency(new Passage.Doorway(new[]
        {
            new Opening(offsetAlongEdge: 0.15f, width: 0.2f, height: 2.2f),
            new Opening(offsetAlongEdge: 0.55f, width: 0.2f, height: 2.2f),
        }));
        var mesh = new BoundaryWallBuilder().Build(adj, 0.2f, 3f);
        Assert.Equal(22 * 2, mesh.TriangleCount(SubmeshIndex.Walls));
        Assert.Equal(22 * 4, mesh.VertexCount);
    }

    [Fact]
    public void DoorwayWithOverlappingOpenings_Throws()
    {
        // Opening 0 ends at 0.35; opening 1 starts at 0.30 — overlap.
        var (_, _, adj) = MakeInternalAdjacency(new Passage.Doorway(new[]
        {
            new Opening(0.15f, 0.2f, 2.2f),
            new Opening(0.30f, 0.2f, 2.2f),
        }));
        Assert.Throws<InvalidOperationException>(() =>
            new BoundaryWallBuilder().Build(adj, 0.2f, 3f));
    }

    [Fact]
    public void DoorwayThatExceedsWallLength_Throws()
    {
        var (_, _, adj) = MakeInternalAdjacency(
            new Passage.Doorway(offsetAlongEdge: 0.5f, width: 0.8f, height: 2.2f));
        Assert.Throws<InvalidOperationException>(() =>
            new BoundaryWallBuilder().Build(adj, 0.2f, 3f));
    }

    [Fact]
    public void DoorwayHigherThanWall_Throws()
    {
        var (_, _, adj) = MakeInternalAdjacency(
            new Passage.Doorway(offsetAlongEdge: 0.4f, width: 0.2f, height: 5f));
        Assert.Throws<InvalidOperationException>(() =>
            new BoundaryWallBuilder().Build(adj, 0.2f, 3f));
    }

    [Fact]
    public void Wall_IsCenteredOnSegment()
    {
        // Segment from (1,0) to (1,1); thickness 0.2 → wall fills x ∈ [0.9, 1.1].
        var a = CellAt(0, 0, 0);
        var seg = new EdgeSegment(new Vector2(1, 0), new Vector2(1, 1));
        var adj = new Adjacency(a, null, seg, Passage.Closed.Instance);
        var mesh = new BoundaryWallBuilder().Build(adj, 0.2f, 3f);

        var xs = mesh.Vertices.Select(v => v.X).ToList();
        Assert.Equal(0.9f, xs.Min(), precision: 4);
        Assert.Equal(1.1f, xs.Max(), precision: 4);
    }

    [Fact]
    public void NullAdjacency_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BoundaryWallBuilder().Build(null!, 0.2f, 3f));
    }

    [Fact]
    public void NonPositiveDimensions_Throw()
    {
        var (_, _, adj) = MakeInternalAdjacency(Passage.Closed.Instance);
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundaryWallBuilder().Build(adj, 0f, 3f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new BoundaryWallBuilder().Build(adj, 0.2f, 0f));
    }

    [Fact]
    public void Combine_MergesMultipleWallMeshes()
    {
        // Sanity-check MeshData.Combine on two boundary outputs.
        var (_, _, adj1) = MakeInternalAdjacency(Passage.Closed.Instance);
        var a = CellAt(0, 0, 0);
        var seg2 = new EdgeSegment(new Vector2(0, 0), new Vector2(1, 0));
        var adj2 = new Adjacency(a, null, seg2, Passage.Closed.Instance);

        var m1 = new BoundaryWallBuilder().Build(adj1, 0.2f, 3f);
        var m2 = new BoundaryWallBuilder().Build(adj2, 0.2f, 3f);
        var combined = MeshData.Combine(new[] { m1, m2 });

        Assert.Equal(m1.VertexCount + m2.VertexCount, combined.VertexCount);
        Assert.Equal(
            m1.TriangleCount(SubmeshIndex.Walls) + m2.TriangleCount(SubmeshIndex.Walls),
            combined.TriangleCount(SubmeshIndex.Walls));
    }
}
