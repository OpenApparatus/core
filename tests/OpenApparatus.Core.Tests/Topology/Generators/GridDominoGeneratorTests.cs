using System.Linq;
using OpenApparatus.Topology;
using OpenApparatus.Topology.Generators;

namespace OpenApparatus.Tests.Topology.Generators;

public class GridDominoGeneratorTests
{
    static GridDominoGenerator Default(int w = 4, int h = 4, int rects = 0, float tile = 1f) =>
        new() { FloorWidthCells = w, FloorLengthCells = h, RectangleRoomCount = rects, TileSize = tile };

    [Fact]
    public void EmptyGrid_FillsWithOneSquarePerTile()
    {
        var gen = Default(3, 3);
        var plan = gen.Generate(new SeededRandom(42));

        Assert.Equal(9, plan.Rooms.Count);
        Assert.All(plan.Rooms, c => Assert.Equal(RoomType.Square, c.RoomType));
    }

    [Fact]
    public void RectangleRooms_AreEmittedAsRectangleType()
    {
        var gen = Default(rects: 3);
        var plan = gen.Generate(new SeededRandom(42));

        int rects = plan.Rooms.Count(c => c.RoomType == RoomType.Rectangle);
        Assert.Equal(3, rects);
    }

    [Fact]
    public void RoomCount_EqualsRectanglesPlusRemainingTilesAsSquares()
    {
        var gen = Default(w: 4, h: 4, rects: 3);
        var plan = gen.Generate(new SeededRandom(42));

        // 4×4 = 16 tiles; 3 rectangles = 6 tiles; remaining 10 tiles = 10 squares.
        // Total rooms = 3 + 10 = 13.
        Assert.Equal(13, plan.Rooms.Count);
    }

    [Fact]
    public void DeterministicForSeed()
    {
        var gen = Default(rects: 2);
        var p1 = gen.Generate(new SeededRandom(7));
        var p2 = gen.Generate(new SeededRandom(7));

        Assert.Equal(p1.Rooms.Count, p2.Rooms.Count);
        for (int i = 0; i < p1.Rooms.Count; i++)
        {
            Assert.Equal(p1.Rooms[i].RoomType, p2.Rooms[i].RoomType);
            Assert.Equal(p1.Rooms[i].Position, p2.Rooms[i].Position);
        }
    }

    [Fact]
    public void TooManyRectangles_Throws()
    {
        // 2×2 grid = 4 tiles, can fit at most 2 dominoes; ask for 3.
        var gen = Default(w: 2, h: 2, rects: 3);
        Assert.Throws<InvalidOperationException>(() => gen.Generate(new SeededRandom(0)));
    }

    [Fact]
    public void InvalidGridDimensions_Throw()
    {
        Assert.Throws<InvalidOperationException>(() => Default(w: 0).Generate(new SeededRandom(0)));
        Assert.Throws<InvalidOperationException>(() => Default(h: 0).Generate(new SeededRandom(0)));
    }

    [Fact]
    public void NegativeRectangleCount_Throws()
    {
        var gen = Default(rects: -1);
        Assert.Throws<InvalidOperationException>(() => gen.Generate(new SeededRandom(0)));
    }

    [Fact]
    public void RectangleShapes_AreEither2x1Or1x2()
    {
        var gen = Default(rects: 4);
        var plan = gen.Generate(new SeededRandom(42));
        foreach (var c in plan.Rooms.Where(c => c.RoomType == RoomType.Rectangle))
        {
            var rect = (RectangleShape)c.Shape;
            float small = MathF.Min(rect.Width, rect.Depth);
            float big = MathF.Max(rect.Width, rect.Depth);
            Assert.Equal(1f, small, precision: 5);
            Assert.Equal(2f, big, precision: 5);
        }
    }

    [Fact]
    public void Orientation_LengthWise_AllRectanglesAre1x2()
    {
        var gen = Default(rects: 4);
        gen.Orientation = RectangleOrientation.LengthWise;
        var plan = gen.Generate(new SeededRandom(42));
        foreach (var c in plan.Rooms.Where(c => c.RoomType == RoomType.Rectangle))
        {
            var rect = (RectangleShape)c.Shape;
            Assert.Equal(1f, rect.Width, precision: 5);
            Assert.Equal(2f, rect.Depth, precision: 5);
        }
    }

    [Fact]
    public void Orientation_WidthWise_AllRectanglesAre2x1()
    {
        var gen = Default(rects: 4);
        gen.Orientation = RectangleOrientation.WidthWise;
        var plan = gen.Generate(new SeededRandom(42));
        foreach (var c in plan.Rooms.Where(c => c.RoomType == RoomType.Rectangle))
        {
            var rect = (RectangleShape)c.Shape;
            Assert.Equal(2f, rect.Width, precision: 5);
            Assert.Equal(1f, rect.Depth, precision: 5);
        }
    }

    [Fact]
    public void Adjacencies_ConnectActualNeighborsOnly()
    {
        var gen = Default(2, 2);
        var plan = gen.Generate(new SeededRandom(0));

        // 2×2 floor of 4 squares. Internal adjacencies = 4 (two horizontal pairs + two vertical).
        Assert.Equal(4, plan.GetInternalAdjacencies().Count());
        // Outer adjacencies: each of 4 rooms has 2 outer sides → 8 total.
        Assert.Equal(8, plan.GetOuterAdjacencies().Count());
    }

    [Fact]
    public void Adjacencies_DefaultToClosedPassage()
    {
        var gen = Default(rects: 1);
        var plan = gen.Generate(new SeededRandom(0));
        Assert.All(plan.Adjacencies, a => Assert.IsType<Passage.Closed>(a.Passage));
    }

    [Fact]
    public void Adjacencies_BetweenRoomsAreSinglePerPair_EvenWhenSpanningMultipleTiles()
    {
        // A vertical 1×2 rectangle at left, two squares right of it. The rectangle should
        // have ONE adjacency to each square (each a 1-tile-wide segment), not several
        // partial-segments. That's two separate adjacencies between the rectangle and the
        // two distinct squares — but only one per (rect, square) pair.
        var gen = Default(2, 2, rects: 1);
        // We can't force the orientation, so this test asserts the invariant "no two
        // adjacencies share the same pair of rooms" rather than the layout itself.
        var plan = gen.Generate(new SeededRandom(123));

        var seenPairs = new HashSet<(int, int)>();
        foreach (var adj in plan.GetInternalAdjacencies())
        {
            var key = (System.Math.Min(adj.RoomA.Id, adj.RoomB!.Id),
                       System.Math.Max(adj.RoomA.Id, adj.RoomB!.Id));
            Assert.True(seenPairs.Add(key),
                $"Duplicate adjacency between rooms {key.Item1} and {key.Item2}.");
        }
    }
}
