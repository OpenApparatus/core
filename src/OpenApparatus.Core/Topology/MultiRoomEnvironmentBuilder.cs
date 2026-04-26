using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace OpenApparatus.Topology;

/// <summary>
/// Constructs a <see cref="MultiRoomEnvironment"/> from an authored tile grid.
/// This is the editor-driven counterpart to <see cref="IMultiRoomEnvironmentGenerator"/>:
/// the user lays out which tiles belong to which room, the builder turns that
/// grid into the materialized environment with adjacencies derived from it.
///
/// Grid convention:
///   • <c>grid[x, z] == -1</c> → empty tile (no room there; counts as "outside")
///   • <c>grid[x, z] &gt;= 0</c>  → that tile belongs to room with that id
///
/// In v1, every room must occupy a contiguous, axis-aligned rectangular set of
/// tiles. Non-rectangular tile sets throw — polygon-shaped rooms come later.
///
/// All adjacencies in the returned environment are <see cref="Passage.Closed"/>;
/// pass it to an <see cref="IPassageAssigner"/> or mutate the passages in the
/// editor to define connectivity.
/// </summary>
public static class MultiRoomEnvironmentBuilder
{
    public static MultiRoomEnvironment FromGrid(int[,] grid, float tileSize)
    {
        if (grid is null) throw new ArgumentNullException(nameof(grid));
        if (tileSize <= 0f) throw new ArgumentOutOfRangeException(nameof(tileSize));

        int width = grid.GetLength(0);
        int length = grid.GetLength(1);

        // 1. Group tiles by room id.
        var roomTiles = new Dictionary<int, List<(int x, int z)>>();
        for (int x = 0; x < width; x++)
            for (int z = 0; z < length; z++)
            {
                int id = grid[x, z];
                if (id < 0) continue;
                if (!roomTiles.TryGetValue(id, out var list))
                {
                    list = new List<(int, int)>();
                    roomTiles[id] = list;
                }
                list.Add((x, z));
            }

        // 2. Validate rectangular + materialize Room objects in id order.
        var rooms = new List<Room>(roomTiles.Count);
        foreach (var kvp in roomTiles.OrderBy(k => k.Key))
        {
            int id = kvp.Key;
            var tiles = kvp.Value;
            int xMin = int.MaxValue, xMax = int.MinValue;
            int zMin = int.MaxValue, zMax = int.MinValue;
            for (int i = 0; i < tiles.Count; i++)
            {
                if (tiles[i].x < xMin) xMin = tiles[i].x;
                if (tiles[i].x > xMax) xMax = tiles[i].x;
                if (tiles[i].z < zMin) zMin = tiles[i].z;
                if (tiles[i].z > zMax) zMax = tiles[i].z;
            }
            int bboxW = xMax - xMin + 1;
            int bboxD = zMax - zMin + 1;
            int bboxArea = bboxW * bboxD;
            if (tiles.Count != bboxArea)
                throw new InvalidOperationException(
                    $"Room {id} occupies {tiles.Count} tiles but its bounding box ({bboxW}x{bboxD}) " +
                    $"contains {bboxArea}. v1 only supports rectangular rooms; non-rectangular shapes " +
                    "will be supported when PolygonShape lands.");

            var shape = new RectangleShape(bboxW * tileSize, bboxD * tileSize);
            var pos = new Vector2(xMin * tileSize, zMin * tileSize);
            // Type classification: 1x1 = Square, anything else = Rectangle. This keeps the
            // existing PreferEntranceRoomType behavior meaningful for editor-built environments.
            var roomType = (bboxW == 1 && bboxD == 1) ? RoomType.Square : RoomType.Rectangle;
            rooms.Add(new Room(id, shape, pos, roomType));
        }

        // 3. Compute adjacencies by walking the grid.
        var roomById = rooms.ToDictionary(r => r.Id);
        var adjacencies = BuildAdjacencies(grid, width, length, roomById, tileSize);

        return new MultiRoomEnvironment(rooms, adjacencies);
    }

    /// <summary>
    /// Walks the grid and produces one adjacency per inter-room shared boundary segment
    /// (for distinct rooms) or per outer-boundary segment (for empty / out-of-bounds neighbors).
    /// Each internal boundary is visited exactly once. Outer boundaries are visited per side.
    /// </summary>
    static Adjacency[] BuildAdjacencies(int[,] grid, int width, int length,
        Dictionary<int, Room> roomById, float tileSize)
    {
        // Bin segments by (idA, idB-or-outer-side) to merge colinear runs into single segments.
        // Internal: key = (min(idA, idB), max(idA, idB), 0)
        // Outer:    key = (idA, -1, dirCode)
        var bins = new Dictionary<(int, int, int), List<EdgeSegment>>();

        for (int x = 0; x < width; x++)
            for (int z = 0; z < length; z++)
            {
                int self = grid[x, z];
                if (self < 0) continue;

                // +X and +Z handle both internal and outer (empty / out-of-bounds neighbor).
                Consider(self, x, z, +1, 0, grid, width, length, tileSize, bins);
                Consider(self, x, z, 0, +1, grid, width, length, tileSize, bins);
                // -X and -Z only emit outer-edge adjacencies — internal ones are picked up
                // from the other side.
                ConsiderOuterOnly(self, x, z, -1, 0, grid, width, length, tileSize, bins);
                ConsiderOuterOnly(self, x, z, 0, -1, grid, width, length, tileSize, bins);
            }

        var result = new List<Adjacency>(bins.Count);
        foreach (var kvp in bins)
        {
            var (idA, idB, _) = kvp.Key;
            var merged = MergeColinearSegments(kvp.Value);
            foreach (var seg in merged)
            {
                Room a = roomById[idA];
                Room? b = idB >= 0 ? roomById[idB] : null;
                result.Add(new Adjacency(a, b, seg));
            }
        }
        return result.ToArray();
    }

    static void Consider(int self, int x, int z, int dx, int dz,
        int[,] grid, int width, int length, float tileSize,
        Dictionary<(int, int, int), List<EdgeSegment>> bins)
    {
        int nx = x + dx, nz = z + dz;
        bool inBounds = nx >= 0 && nx < width && nz >= 0 && nz < length;
        int neighborId = inBounds ? grid[nx, nz] : -1;

        if (inBounds && neighborId == self) return; // same room

        var seg = GridEdgeSegment(x, z, dx, dz, tileSize);

        (int, int, int) key;
        EdgeSegment binSeg;
        if (inBounds && neighborId >= 0)
        {
            // Internal adjacency between two rooms.
            int idA = System.Math.Min(self, neighborId);
            int idB = System.Math.Max(self, neighborId);
            binSeg = (idA == self) ? seg : seg.Reversed();
            key = (idA, idB, 0);
        }
        else
        {
            // Outer: out-of-bounds OR empty tile.
            key = (self, -1, DirCode(dx, dz));
            binSeg = seg;
        }
        AddToBin(bins, key, binSeg);
    }

    static void ConsiderOuterOnly(int self, int x, int z, int dx, int dz,
        int[,] grid, int width, int length, float tileSize,
        Dictionary<(int, int, int), List<EdgeSegment>> bins)
    {
        int nx = x + dx, nz = z + dz;
        bool inBounds = nx >= 0 && nx < width && nz >= 0 && nz < length;
        int neighborId = inBounds ? grid[nx, nz] : -1;

        // Skip if neighbor is a room (that adjacency is recorded from the other side).
        if (inBounds && neighborId >= 0) return;

        var seg = GridEdgeSegment(x, z, dx, dz, tileSize);
        var key = (self, -1, DirCode(dx, dz));
        AddToBin(bins, key, seg);
    }

    static void AddToBin(Dictionary<(int, int, int), List<EdgeSegment>> bins,
        (int, int, int) key, EdgeSegment seg)
    {
        if (!bins.TryGetValue(key, out var list))
        {
            list = new List<EdgeSegment>();
            bins[key] = list;
        }
        list.Add(seg);
    }

    static int DirCode(int dx, int dz) => (dx + 2) * 10 + (dz + 2);

    /// <summary>
    /// Returns the world-space edge segment of grid tile (x, z) on the side
    /// indicated by (dx, dz). Direction is CCW around the tile.
    /// </summary>
    static EdgeSegment GridEdgeSegment(int x, int z, int dx, int dz, float tileSize)
    {
        float ts = tileSize;
        if (dx == +1) return new(new(ts * (x + 1), ts * z),       new(ts * (x + 1), ts * (z + 1)));
        if (dx == -1) return new(new(ts * x,       ts * (z + 1)), new(ts * x,       ts * z));
        if (dz == +1) return new(new(ts * (x + 1), ts * (z + 1)), new(ts * x,       ts * (z + 1)));
        if (dz == -1) return new(new(ts * x,       ts * z),       new(ts * (x + 1), ts * z));
        throw new ArgumentException("Direction must be one unit step.");
    }

    /// <summary>Merge segments that share an endpoint AND a direction into one longer segment.</summary>
    static List<EdgeSegment> MergeColinearSegments(List<EdgeSegment> segs)
    {
        if (segs.Count <= 1) return segs;
        var dir = segs[0].Direction;
        segs.Sort((a, b) => Vector2.Dot(a.Start, dir).CompareTo(Vector2.Dot(b.Start, dir)));

        var result = new List<EdgeSegment>();
        var current = segs[0];
        const float EPS = 1e-4f;
        for (int i = 1; i < segs.Count; i++)
        {
            var next = segs[i];
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
}
