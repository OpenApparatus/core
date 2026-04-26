using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenApparatus.Topology.Assigners;

/// <summary>
/// Walks the floor plan's room adjacency graph and selects a random spanning tree —
/// the cheapest data structure that guarantees exactly one path between any two rooms.
/// Selected internal adjacencies become <see cref="Passage.Doorway"/>s; the rest stay
/// <see cref="Passage.Closed"/>. Optionally, one outer adjacency becomes a doorway too,
/// representing the entrance from outside the floor.
///
/// Algorithm: Kruskal-with-shuffled-edges. Conceptually equivalent to assigning each
/// edge a random weight and picking the minimum spanning tree, but cheaper.
/// </summary>
public sealed class SpanningTreePassageAssigner : IPassageAssigner
{
    public float DoorWidth { get; set; } = 1.2f;
    public float DoorHeight { get; set; } = 2.2f;

    /// <summary>If true, designates one outer adjacency as the floor's entrance doorway.</summary>
    public bool IncludeOuterEntrance { get; set; } = true;

    /// <summary>
    /// If non-null and <see cref="IncludeOuterEntrance"/> is true, the outer entrance
    /// is preferentially placed on a leaf room whose <see cref="Room.RoomType"/> matches.
    /// Falls back to any leaf if no matching room is on the boundary.
    /// </summary>
    public RoomType? PreferEntranceRoomType { get; set; }

    public void Assign(MultiRoomEnvironment plan, SeededRandom rng)
    {
        if (plan is null) throw new ArgumentNullException(nameof(plan));
        if (rng is null) throw new ArgumentNullException(nameof(rng));

        // Reset everything to closed first so the assigner is idempotent w.r.t. repeated calls.
        foreach (var adj in plan.Adjacencies)
            adj.Passage = Passage.Closed.Instance;

        // 1) Spanning tree over INTERNAL adjacencies.
        var internalEdges = plan.GetInternalAdjacencies().ToList();
        rng.Shuffle(internalEdges);

        var unionFind = new UnionFind(plan.Rooms.Count);
        // Map room.Id → contiguous index for the union-find.
        var idToIndex = new Dictionary<int, int>(plan.Rooms.Count);
        for (int i = 0; i < plan.Rooms.Count; i++)
            idToIndex[plan.Rooms[i].Id] = i;

        foreach (var adj in internalEdges)
        {
            int a = idToIndex[adj.RoomA.Id];
            int b = idToIndex[adj.RoomB!.Id];
            if (unionFind.Find(a) == unionFind.Find(b)) continue;   // would create a cycle
            unionFind.Union(a, b);
            adj.Passage = MakeDoorwayFor(adj);
        }

        // 2) Optional outer entrance.
        if (!IncludeOuterEntrance) return;

        var outerEdges = plan.GetOuterAdjacencies().ToList();
        if (outerEdges.Count == 0) return;

        // Prefer outer adjacencies whose room matches PreferEntranceRoomType, if any.
        Adjacency entrance;
        if (PreferEntranceRoomType is RoomType preferred)
        {
            var preferredOuters = outerEdges
                .Where(a => a.RoomA.RoomType == preferred)
                .ToList();
            entrance = preferredOuters.Count > 0
                ? rng.Pick(preferredOuters)
                : rng.Pick(outerEdges);
        }
        else
        {
            entrance = rng.Pick(outerEdges);
        }

        entrance.Passage = MakeDoorwayFor(entrance);
    }

    Passage.Doorway MakeDoorwayFor(Adjacency adj)
    {
        // Centered along the shared segment, clamped to the segment's length.
        float segLen = adj.SharedSegment.Length;
        float w = MathF.Min(DoorWidth, segLen);
        float offset = (segLen - w) * 0.5f;
        return new Passage.Doorway(offset, w, DoorHeight);
    }

    /// <summary>Disjoint-set with union-by-rank + path compression. Internal helper.</summary>
    sealed class UnionFind
    {
        readonly int[] _parent;
        readonly int[] _rank;

        public UnionFind(int n)
        {
            _parent = new int[n];
            _rank = new int[n];
            for (int i = 0; i < n; i++) _parent[i] = i;
        }

        public int Find(int x)
        {
            while (_parent[x] != x)
            {
                _parent[x] = _parent[_parent[x]]; // path compression
                x = _parent[x];
            }
            return x;
        }

        public void Union(int a, int b)
        {
            int ra = Find(a), rb = Find(b);
            if (ra == rb) return;
            if (_rank[ra] < _rank[rb]) (ra, rb) = (rb, ra);
            _parent[rb] = ra;
            if (_rank[ra] == _rank[rb]) _rank[ra]++;
        }
    }
}
