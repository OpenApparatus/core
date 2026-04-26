using System;
using System.Collections.Generic;
using System.Numerics;

namespace OpenApparatus.Topology.Generators;

/// <summary>
/// Tiles an axis-aligned <c>floorWidth × floorLength</c> grid of square tiles
/// (each <see cref="TileSize"/> on a side) with:
///   1. <see cref="RectangleRoomCount"/> randomly-placed 1×2 dominoes (rectangle rooms),
///   2. then 1×1 squares filling every remaining tile.
///
/// The output is a <see cref="MultiRoomEnvironment"/> whose adjacencies are all initially
/// <see cref="Passage.Closed"/>; pass it to an <see cref="IPassageAssigner"/> to
/// open doors.
/// </summary>
public sealed class GridDominoGenerator : IMultiRoomEnvironmentGenerator
{
    /// <summary>Number of tiles along the +X axis.</summary>
    public int FloorWidthCells { get; set; } = 4;

    /// <summary>Number of tiles along the +Z axis.</summary>
    public int FloorLengthCells { get; set; } = 4;

    /// <summary>How many 1×2 rectangle rooms to place before filling with squares.</summary>
    public int RectangleRoomCount { get; set; } = 0;

    /// <summary>Side length of one grid tile (and therefore one square room) in world units.</summary>
    public float TileSize { get; set; } = 3.5f;

    /// <summary>Cap on the random-retry loop when a domino placement collides.</summary>
    public int MaxPlacementRetries { get; set; } = 256;

    /// <summary>
    /// Controls whether each rectangle is laid length-wise (1×2 along +Z),
    /// width-wise (2×1 along +X), or randomly per-rectangle (the default).
    /// </summary>
    public RectangleOrientation Orientation { get; set; } = RectangleOrientation.Random;

    public MultiRoomEnvironment Generate(SeededRandom rng)
    {
        if (FloorWidthCells <= 0) throw new InvalidOperationException("FloorWidthCells must be positive.");
        if (FloorLengthCells <= 0) throw new InvalidOperationException("FloorLengthCells must be positive.");
        if (RectangleRoomCount < 0) throw new InvalidOperationException("RectangleRoomCount must be non-negative.");
        int totalTiles = FloorWidthCells * FloorLengthCells;
        if (RectangleRoomCount * 2 > totalTiles)
            throw new InvalidOperationException(
                $"Cannot fit {RectangleRoomCount} rectangle rooms (= {RectangleRoomCount * 2} tiles) " +
                $"into a {FloorWidthCells}×{FloorLengthCells} grid (= {totalTiles} tiles).");

        // grid[x, z] = -1 if unoccupied, else the room id.
        int[,] grid = new int[FloorWidthCells, FloorLengthCells];
        for (int x = 0; x < FloorWidthCells; x++)
            for (int z = 0; z < FloorLengthCells; z++)
                grid[x, z] = -1;

        var layouts = new List<RoomLayout>();

        // 1) Place rectangle rooms (dominoes).
        for (int i = 0; i < RectangleRoomCount; i++)
        {
            if (!TryPlaceDomino(grid, layouts, rng))
                throw new InvalidOperationException(
                    $"Failed to place rectangle room #{i} after {MaxPlacementRetries} retries. " +
                    $"Grid is too crowded for {RectangleRoomCount} rectangles.");
        }

        // 2) Fill remaining tiles with 1×1 squares.
        for (int x = 0; x < FloorWidthCells; x++)
            for (int z = 0; z < FloorLengthCells; z++)
            {
                if (grid[x, z] != -1) continue;
                int id = layouts.Count;
                grid[x, z] = id;
                layouts.Add(new RoomLayout(id, RoomType.Square,
                    new Vector2Int(x, z), new Vector2Int(1, 1)));
            }

        // 3) Materialize rooms.
        var rooms = new Room[layouts.Count];
        for (int i = 0; i < layouts.Count; i++)
            rooms[i] = MakeRoom(layouts[i]);

        // 4) Build adjacencies by walking the grid.
        var adjacencies = BuildAdjacencies(grid, rooms);

        return new MultiRoomEnvironment(rooms, adjacencies);
    }

    bool TryPlaceDomino(int[,] grid, List<RoomLayout> rooms, SeededRandom rng)
    {
        for (int attempt = 0; attempt < MaxPlacementRetries; attempt++)
        {
            int x = rng.NextInt(FloorWidthCells);
            int z = rng.NextInt(FloorLengthCells);
            // horizontal = true means rectangle spans +X (a 2x1, "width-wise" placement);
            // horizontal = false means it spans +Z (1x2, "length-wise" placement).
            bool horizontal = Orientation switch
            {
                RectangleOrientation.WidthWise => true,
                RectangleOrientation.LengthWise => false,
                _ => rng.NextBool(),
            };

            if (grid[x, z] != -1) continue;

            int x2 = horizontal ? x + 1 : x;
            int z2 = horizontal ? z : z + 1;
            if (x2 >= FloorWidthCells || z2 >= FloorLengthCells) continue;
            if (grid[x2, z2] != -1) continue;

            int id = rooms.Count;
            grid[x, z] = id;
            grid[x2, z2] = id;
            var size = horizontal ? new Vector2Int(2, 1) : new Vector2Int(1, 2);
            rooms.Add(new RoomLayout(id, RoomType.Rectangle, new Vector2Int(x, z), size));
            return true;
        }
        return false;
    }

    Room MakeRoom(RoomLayout room)
    {
        var shape = new RectangleShape(room.SizeTiles.X * TileSize, room.SizeTiles.Z * TileSize);
        var pos = new Vector2(room.OriginTile.X * TileSize, room.OriginTile.Z * TileSize);
        return new Room(room.Id, shape, pos, room.Type);
    }

    /// <summary>
    /// Walks the grid and emits one adjacency per directed boundary segment between
    /// distinct rooms (or between a room and the outside). Multiple grid-room-pairs
    /// between the same two rooms are collapsed into one adjacency with the merged
    /// world segment.
    /// </summary>
    Adjacency[] BuildAdjacencies(int[,] grid, Room[] rooms)
    {
        // key = (smallerRoomId, largerRoomId) for internal, or (roomId, -1 - outerSide) for outer
        // value = list of unmerged grid-edge segments (in world coords, CCW-from-RoomA direction)
        var bins = new Dictionary<(int, int, int), List<EdgeSegment>>();

        // Visit each boundary exactly once:
        //   * For internal boundaries, walk only the +X and +Z directions — every internal
        //     edge is shared by two grid rooms, and this picks the side with the smaller
        //     coordinate, avoiding double-counting.
        //   * For outer boundaries, the room on the opposite-coordinate edge of the grid
        //     also needs its -X / -Z outer side accounted for.
        for (int x = 0; x < FloorWidthCells; x++)
            for (int z = 0; z < FloorLengthCells; z++)
            {
                int self = grid[x, z];
                ConsiderEdge(self, x, z, +1, 0, grid, bins); // east — internal or outer
                ConsiderEdge(self, x, z, 0, +1, grid, bins); // north — internal or outer
                if (x == 0) ConsiderEdge(self, x, z, -1, 0, grid, bins); // west outer only
                if (z == 0) ConsiderEdge(self, x, z, 0, -1, grid, bins); // south outer only
            }

        var result = new List<Adjacency>(bins.Count);
        foreach (var kvp in bins)
        {
            var (idA, idB, _) = kvp.Key;
            var merged = MergeColinearSegments(kvp.Value);
            foreach (var seg in merged)
            {
                Room a = rooms[idA];
                Room? b = idB >= 0 ? rooms[idB] : null;
                result.Add(new Adjacency(a, b, seg));
            }
        }
        return result.ToArray();
    }

    void ConsiderEdge(int self, int x, int z, int dx, int dz, int[,] grid,
        Dictionary<(int, int, int), List<EdgeSegment>> bins)
    {
        int nx = x + dx;
        int nz = z + dz;
        bool inBounds = nx >= 0 && nx < FloorWidthCells && nz >= 0 && nz < FloorLengthCells;
        int neighborId = inBounds ? grid[nx, nz] : -1;

        if (inBounds && neighborId == self) return;     // same room → not an adjacency

        // Compute the directed shared segment in world coordinates (CCW around `self`).
        var seg = GridEdgeSegment(x, z, dx, dz);

        // Bin key: lower id first for internal; for outer (no neighbor), use side
        // direction in the third slot to keep separate sides distinct.
        (int, int, int) key;
        EdgeSegment binSeg;
        if (inBounds)
        {
            // Two rooms share this edge; record under (min, max). To preserve a consistent
            // CCW direction (RoomA on left), if `self` is the larger id, we'd reverse the
            // segment. Pick RoomA = smaller id.
            int idA = System.Math.Min(self, neighborId);
            int idB = System.Math.Max(self, neighborId);
            // The seg we computed is CCW around `self`. If self is the larger id, that means
            // RoomA = neighbor — reverse the segment so RoomA is on the left.
            binSeg = (idA == self) ? seg : seg.Reversed();
            key = (idA, idB, 0);
        }
        else
        {
            // Outer adjacency: RoomA = self, RoomB = null.
            // Use direction in key slot 3 to bin by which outer side we're on.
            int dirCode = DirCode(dx, dz);
            key = (self, -1, dirCode);
            binSeg = seg;
        }

        if (!bins.TryGetValue(key, out var list))
        {
            list = new List<EdgeSegment>();
            bins[key] = list;
        }
        list.Add(binSeg);
    }

    static int DirCode(int dx, int dz) => (dx + 2) * 10 + (dz + 2);   // any unique mapping

    /// <summary>
    /// Returns the world-space edge segment of grid room (x, z) on the side
    /// indicated by (dx, dz). The direction is CCW around (x, z).
    /// </summary>
    EdgeSegment GridEdgeSegment(int x, int z, int dx, int dz)
    {
        float ts = TileSize;
        if (dx == +1) return new(new(ts * (x + 1), ts * z),       new(ts * (x + 1), ts * (z + 1))); // east, +Z
        if (dx == -1) return new(new(ts * x,       ts * (z + 1)), new(ts * x,       ts * z));       // west, -Z
        if (dz == +1) return new(new(ts * (x + 1), ts * (z + 1)), new(ts * x,       ts * (z + 1))); // north, -X
        if (dz == -1) return new(new(ts * x,       ts * z),       new(ts * (x + 1), ts * z));       // south, +X
        throw new ArgumentException("Direction must be one unit step.");
    }

    /// <summary>
    /// Merges segments that share an endpoint AND a direction into single longer ones.
    /// All input segments in any one call are guaranteed colinear by the binning logic above.
    /// </summary>
    static List<EdgeSegment> MergeColinearSegments(List<EdgeSegment> segs)
    {
        if (segs.Count <= 1) return segs;
        // Sort along the segment direction. Take the first segment's direction as canonical.
        var dir = segs[0].Direction;
        // Project Start onto dir for sorting key.
        segs.Sort((a, b) =>
            Vector2.Dot(a.Start, dir).CompareTo(Vector2.Dot(b.Start, dir)));

        var result = new List<EdgeSegment>();
        var current = segs[0];
        const float EPS = 1e-4f;
        for (int i = 1; i < segs.Count; i++)
        {
            var next = segs[i];
            // Adjacent? current.End == next.Start
            if ((current.End - next.Start).LengthSquared() < EPS * EPS)
                current = new EdgeSegment(current.Start, next.End);
            else
            {
                result.Add(current);
                current = next;
            }
        }
        result.Add(current);
        return result;
    }

    readonly record struct Vector2Int(int X, int Z);
    readonly record struct RoomLayout(int Id, RoomType Type, Vector2Int OriginTile, Vector2Int SizeTiles);
}
