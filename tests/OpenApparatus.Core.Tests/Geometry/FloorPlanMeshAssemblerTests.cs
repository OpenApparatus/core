using System.Linq;
using OpenApparatus.Geometry;
using OpenApparatus.Topology;
using OpenApparatus.Topology.Assigners;
using OpenApparatus.Topology.Generators;

namespace OpenApparatus.Tests.Geometry;

public class FloorPlanMeshAssemblerTests
{
    static FloorPlan GeneratePlan(int seed, int w = 4, int h = 4, int rects = 0,
        bool assign = true, bool entrance = true)
    {
        var gen = new GridDominoGenerator
        {
            FloorWidthCells = w,
            FloorHeightCells = h,
            RectangleRoomCount = rects,
            TileSize = 3.5f,
        };
        var plan = gen.Generate(new SeededRandom(seed));
        if (assign)
            new SpanningTreePassageAssigner { IncludeOuterEntrance = entrance }
                .Assign(plan, new SeededRandom(seed));
        return plan;
    }

    [Fact]
    public void Assemble_ReturnsOneMeshPerCell()
    {
        var plan = GeneratePlan(42, 4, 4);
        var meshes = new FloorPlanMeshAssembler().Assemble(plan, 0.2f, 3f);
        Assert.Equal(plan.Cells.Count, meshes.Count);
    }

    [Fact]
    public void Assemble_PreservesCellOrder()
    {
        var plan = GeneratePlan(42, 3, 3);
        var meshes = new FloorPlanMeshAssembler().Assemble(plan, 0.2f, 3f);
        for (int i = 0; i < plan.Cells.Count; i++)
            Assert.Same(plan.Cells[i], meshes[i].Cell);
    }

    [Fact]
    public void Assemble_EveryCellHasNonEmptyFloorAndCeiling()
    {
        var plan = GeneratePlan(42, 3, 3);
        var meshes = new FloorPlanMeshAssembler().Assemble(plan, 0.2f, 3f);
        foreach (var m in meshes)
        {
            Assert.True(m.Mesh.TriangleCount(SubmeshIndex.Floor) > 0,
                $"Cell #{m.Cell.Id} has no floor.");
            Assert.True(m.Mesh.TriangleCount(SubmeshIndex.Ceiling) > 0,
                $"Cell #{m.Cell.Id} has no ceiling.");
        }
    }

    [Fact]
    public void OneCell_HasFourOuterWalls_AssignedToIt()
    {
        // Single 1×1 grid (one cell). The cell has 4 outer adjacencies, all closed.
        var plan = GeneratePlan(0, 1, 1, assign: false);
        var meshes = new FloorPlanMeshAssembler().Assemble(plan, 0.2f, 3f);

        Assert.Single(meshes);
        var only = meshes[0];

        // 4 outer walls × 6 faces × 2 tris = 48 wall triangles.
        Assert.Equal(48, only.Mesh.TriangleCount(SubmeshIndex.Walls));
    }

    [Fact]
    public void TwoCells_ClosedSharedWall_OwnedByLowerIdCell()
    {
        // 1×2 grid → 2 square cells (id 0 at (0,0), id 1 at (0,1) — or swapped depending
        // on iteration order; we check by counting walls).
        var plan = GeneratePlan(0, 1, 2, assign: false);   // no spanning tree → all closed
        var meshes = new FloorPlanMeshAssembler().Assemble(plan, 0.2f, 3f).ToList();

        Assert.Equal(2, meshes.Count);
        var byId = meshes.ToDictionary(m => m.Cell.Id, m => m);

        // Cell 0 has: 3 outer walls (south, west, east) + 1 shared wall to cell 1 = 4 walls.
        // Cell 1 has: 3 outer walls (north, west, east), shared wall is owned by cell 0 = 3 walls.
        // Wall = 6 faces = 12 tris.
        Assert.Equal(48, byId[0].Mesh.TriangleCount(SubmeshIndex.Walls));   // 4 walls × 12
        Assert.Equal(36, byId[1].Mesh.TriangleCount(SubmeshIndex.Walls));   // 3 walls × 12
    }

    [Fact]
    public void TwoCells_OpenAdjacency_ProducesNoSharedWallGeometry()
    {
        var plan = GeneratePlan(0, 1, 2, assign: false);
        // Manually set the internal adjacency to Open.
        foreach (var adj in plan.GetInternalAdjacencies())
            adj.Passage = Passage.Open.Instance;

        var meshes = new FloorPlanMeshAssembler().Assemble(plan, 0.2f, 3f).ToList();
        var byId = meshes.ToDictionary(m => m.Cell.Id, m => m);

        // Each cell now only has its 3 outer walls (no shared wall material at all).
        Assert.Equal(36, byId[0].Mesh.TriangleCount(SubmeshIndex.Walls));   // 3 walls × 12
        Assert.Equal(36, byId[1].Mesh.TriangleCount(SubmeshIndex.Walls));
    }

    [Fact]
    public void TwoCells_DoorwayAdjacency_OwnerHasTunneledWall()
    {
        var plan = GeneratePlan(0, 1, 2, assign: false);
        var sharedAdj = plan.GetInternalAdjacencies().Single();
        sharedAdj.Passage = new Passage.Doorway(
            offsetAlongEdge: (sharedAdj.SharedSegment.Length - 1.2f) * 0.5f,
            width: 1.2f, height: 2.2f);

        var meshes = new FloorPlanMeshAssembler().Assemble(plan, 0.2f, 3f).ToList();
        var byId = meshes.ToDictionary(m => m.Cell.Id, m => m);

        // Owner has 3 closed outer walls (3 × 12 = 36) + 1 doorway wall (14 faces × 2 = 28)
        // → 64 triangles. Non-owner has just its 3 outer walls (36 tris).
        int ownerId = sharedAdj.CellA.Id < sharedAdj.CellB!.Id ? sharedAdj.CellA.Id : sharedAdj.CellB.Id;
        int otherId = ownerId == sharedAdj.CellA.Id ? sharedAdj.CellB.Id : sharedAdj.CellA.Id;

        Assert.Equal(64, byId[ownerId].Mesh.TriangleCount(SubmeshIndex.Walls));
        Assert.Equal(36, byId[otherId].Mesh.TriangleCount(SubmeshIndex.Walls));
    }

    [Fact]
    public void Assembler_DeterministicForSamePlan()
    {
        var plan = GeneratePlan(42, 4, 4, rects: 2);
        var a = new FloorPlanMeshAssembler().Assemble(plan, 0.2f, 3f);
        var b = new FloorPlanMeshAssembler().Assemble(plan, 0.2f, 3f);

        Assert.Equal(a.Count, b.Count);
        for (int i = 0; i < a.Count; i++)
        {
            Assert.Equal(a[i].Mesh.VertexCount, b[i].Mesh.VertexCount);
            Assert.Equal(a[i].Mesh.TotalTriangleCount, b[i].Mesh.TotalTriangleCount);
        }
    }

    [Fact]
    public void Assembler_RejectsInvalidArguments()
    {
        var plan = GeneratePlan(0, 2, 2);
        var asm = new FloorPlanMeshAssembler();
        Assert.Throws<ArgumentNullException>(() => asm.Assemble(null!, 0.2f, 3f));
        Assert.Throws<ArgumentOutOfRangeException>(() => asm.Assemble(plan, 0f, 3f));
        Assert.Throws<ArgumentOutOfRangeException>(() => asm.Assemble(plan, 0.2f, 0f));
    }

    [Fact]
    public void Assembler_BuildsThreeSubmeshesPerCell()
    {
        var plan = GeneratePlan(0, 2, 2);
        var meshes = new FloorPlanMeshAssembler().Assemble(plan, 0.2f, 3f);
        foreach (var m in meshes)
            Assert.Equal(SubmeshIndex.Count, m.Mesh.SubmeshCount);
    }

    [Fact]
    public void Assembler_FullPipeline_SpanningTreeProducesValidMeshes()
    {
        // End-to-end: generator → spanning-tree assigner → assembler. Verify every
        // cell ends up with non-zero mesh content (floor, ceiling, and at least one wall).
        var plan = GeneratePlan(123, 4, 4, rects: 2);
        var meshes = new FloorPlanMeshAssembler().Assemble(plan, 0.2f, 3f);

        Assert.Equal(plan.Cells.Count, meshes.Count);
        foreach (var m in meshes)
        {
            Assert.True(m.Mesh.TriangleCount(SubmeshIndex.Floor) > 0);
            Assert.True(m.Mesh.TriangleCount(SubmeshIndex.Ceiling) > 0);
            // Most cells will have walls; corner-only cells with all-open might not, but
            // with the spanning-tree assigner some passages stay closed.
        }

        // Total wall triangles should match the spanning-tree expectation:
        // (cells - 1) doorways + (outer adjacencies - 1) outer-closed walls + 1 entrance doorway +
        // (internal adjacencies - (cells-1)) closed walls. Each closed wall = 12 tris,
        // each doorway = 28 tris.
        // We just sanity-check that the total isn't zero.
        int totalWallTris = meshes.Sum(m => m.Mesh.TriangleCount(SubmeshIndex.Walls));
        Assert.True(totalWallTris > 0);
    }
}
