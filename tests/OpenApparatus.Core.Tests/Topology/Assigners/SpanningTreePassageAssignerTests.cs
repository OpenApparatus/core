using System.Collections.Generic;
using System.Linq;
using OpenApparatus.Topology;
using OpenApparatus.Topology.Assigners;
using OpenApparatus.Topology.Generators;

namespace OpenApparatus.Tests.Topology.Assigners;

public class SpanningTreePassageAssignerTests
{
    static (MultiRoomEnvironment plan, GridDominoGenerator gen) MakePlan(
        int seed, int w = 4, int h = 4, int rects = 0, float tile = 1f)
    {
        var gen = new GridDominoGenerator
        {
            FloorWidthCells = w,
            FloorLengthCells = h,
            RectangleRoomCount = rects,
            TileSize = tile,
        };
        return (gen.Generate(new SeededRandom(seed)), gen);
    }

    static SpanningTreePassageAssigner Default(bool entrance = true) =>
        new() { IncludeOuterEntrance = entrance };

    [Fact]
    public void DoorwayCount_OnInternalEdges_EqualsCellCountMinusOne()
    {
        var (plan, _) = MakePlan(42, 4, 4);
        new SpanningTreePassageAssigner { IncludeOuterEntrance = false }
            .Assign(plan, new SeededRandom(7));

        int internalDoorways = plan.GetInternalAdjacencies()
            .Count(a => a.Passage is Passage.Doorway);

        Assert.Equal(plan.Rooms.Count - 1, internalDoorways);
    }

    [Fact]
    public void Doorways_FormConnectedSpanningTree()
    {
        var (plan, _) = MakePlan(42, 5, 5, rects: 3);
        new SpanningTreePassageAssigner { IncludeOuterEntrance = false }
            .Assign(plan, new SeededRandom(11));

        // BFS from room 0 over doorway edges and verify we reach every room.
        var graph = BuildDoorwayGraph(plan);
        var visited = new HashSet<int> { plan.Rooms[0].Id };
        var queue = new Queue<int>(new[] { plan.Rooms[0].Id });
        while (queue.Count > 0)
        {
            int curr = queue.Dequeue();
            if (!graph.TryGetValue(curr, out var nbrs)) continue;
            foreach (var n in nbrs)
                if (visited.Add(n)) queue.Enqueue(n);
        }
        Assert.Equal(plan.Rooms.Count, visited.Count);
    }

    [Fact]
    public void DoorwayGraph_HasNoCycles()
    {
        // A connected acyclic graph with N nodes has exactly N-1 edges. Any spanning
        // tree must satisfy this; if we ever ship more edges, there's a cycle.
        var (plan, _) = MakePlan(7, 4, 4, rects: 2);
        new SpanningTreePassageAssigner { IncludeOuterEntrance = false }
            .Assign(plan, new SeededRandom(7));

        int internalDoorways = plan.GetInternalAdjacencies()
            .Count(a => a.Passage is Passage.Doorway);
        Assert.Equal(plan.Rooms.Count - 1, internalDoorways);
    }

    [Fact]
    public void IncludeOuterEntrance_AddsExactlyOneOuterDoorway()
    {
        var (plan, _) = MakePlan(42);
        Default(entrance: true).Assign(plan, new SeededRandom(7));

        int outerDoorways = plan.GetOuterAdjacencies()
            .Count(a => a.Passage is Passage.Doorway);
        Assert.Equal(1, outerDoorways);
    }

    [Fact]
    public void NoEntrance_LeavesAllOuterClosed()
    {
        var (plan, _) = MakePlan(42);
        Default(entrance: false).Assign(plan, new SeededRandom(7));

        Assert.All(plan.GetOuterAdjacencies(),
            a => Assert.IsType<Passage.Closed>(a.Passage));
    }

    [Fact]
    public void DeterministicForSeed()
    {
        var (planA, _) = MakePlan(42);
        var (planB, _) = MakePlan(42);

        Default().Assign(planA, new SeededRandom(99));
        Default().Assign(planB, new SeededRandom(99));

        var typesA = planA.Adjacencies.Select(a => a.Passage.GetType().Name).ToList();
        var typesB = planB.Adjacencies.Select(a => a.Passage.GetType().Name).ToList();
        Assert.Equal(typesA, typesB);
    }

    [Fact]
    public void AssignIsIdempotent_ReassigningProducesSameStructure()
    {
        var (plan, _) = MakePlan(42);
        var assigner = Default();

        assigner.Assign(plan, new SeededRandom(1));
        var firstPass = plan.Adjacencies.Select(a => a.Passage.GetType().Name).ToList();

        assigner.Assign(plan, new SeededRandom(1));
        var secondPass = plan.Adjacencies.Select(a => a.Passage.GetType().Name).ToList();

        Assert.Equal(firstPass, secondPass);
    }

    [Fact]
    public void PreferEntranceRoomType_PicksMatchingRoomWhenAvailable()
    {
        // 3×3 grid with 1 rectangle → 8 rooms total (1 rect + 7 squares). At least
        // some boundary rooms will be squares; we ask for a Square entrance.
        var (plan, _) = MakePlan(42, 3, 3, rects: 1);
        var assigner = new SpanningTreePassageAssigner
        {
            IncludeOuterEntrance = true,
            PreferEntranceRoomType = RoomType.Square,
        };
        assigner.Assign(plan, new SeededRandom(7));

        var entrance = plan.GetOuterAdjacencies().Single(a => a.Passage is Passage.Doorway);
        Assert.Equal(RoomType.Square, entrance.RoomA.RoomType);
    }

    [Fact]
    public void Assign_NullArgs_Throw()
    {
        var assigner = Default();
        var (plan, _) = MakePlan(0);
        Assert.Throws<ArgumentNullException>(() => assigner.Assign(null!, new SeededRandom(0)));
        Assert.Throws<ArgumentNullException>(() => assigner.Assign(plan, null!));
    }

    static Dictionary<int, List<int>> BuildDoorwayGraph(MultiRoomEnvironment plan)
    {
        var g = new Dictionary<int, List<int>>();
        foreach (var adj in plan.GetInternalAdjacencies())
        {
            if (adj.Passage is not Passage.Doorway) continue;
            int a = adj.RoomA.Id, b = adj.RoomB!.Id;
            if (!g.TryGetValue(a, out var la)) g[a] = la = new();
            if (!g.TryGetValue(b, out var lb)) g[b] = lb = new();
            la.Add(b);
            lb.Add(a);
        }
        return g;
    }
}
