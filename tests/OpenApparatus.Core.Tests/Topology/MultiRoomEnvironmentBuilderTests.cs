using System.Linq;
using OpenApparatus.Topology;

namespace OpenApparatus.Tests.Topology;

public class MultiRoomEnvironmentBuilderTests
{
    [Fact]
    public void EmptyGridProducesEmptyEnvironment()
    {
        // 2x2 grid with all empty (-1) tiles → no rooms, no adjacencies.
        var grid = new int[,] { { -1, -1 }, { -1, -1 } };
        var env = MultiRoomEnvironmentBuilder.FromGrid(grid, 1f);
        Assert.Empty(env.Rooms);
        Assert.Empty(env.Adjacencies);
    }

    [Fact]
    public void SingleRoomProducesOneRoomAndFourOuterAdjacencies()
    {
        // 2x2 grid filled with one room (id 0). 4 outer sides → 4 outer adjacencies.
        var grid = new int[,] { { 0, 0 }, { 0, 0 } };
        var env = MultiRoomEnvironmentBuilder.FromGrid(grid, 1f);

        Assert.Single(env.Rooms);
        Assert.Equal(0, env.Rooms[0].Id);
        Assert.Equal(4, env.GetOuterAdjacencies().Count());
        Assert.Empty(env.GetInternalAdjacencies());
    }

    [Fact]
    public void TwoAdjacentRoomsHaveOneInternalAndSixOuterAdjacencies()
    {
        // Two side-by-side 1x1 rooms.
        var grid = new int[,] { { 0 }, { 1 } };
        var env = MultiRoomEnvironmentBuilder.FromGrid(grid, 1f);

        Assert.Equal(2, env.Rooms.Count);
        Assert.Single(env.GetInternalAdjacencies());
        // 6 outer (3 per cell × 2 cells).
        Assert.Equal(6, env.GetOuterAdjacencies().Count());
    }

    [Fact]
    public void EmptyTileBetweenRoomsProducesOuterOnlyAdjacencies()
    {
        // 3x1 grid: room 0, empty, room 1. They are NOT directly adjacent.
        var grid = new int[,] { { 0 }, { -1 }, { 1 } };
        var env = MultiRoomEnvironmentBuilder.FromGrid(grid, 1f);

        Assert.Equal(2, env.Rooms.Count);
        Assert.Empty(env.GetInternalAdjacencies());
        // Each room has 4 outer sides (one of which faces the empty tile).
        Assert.Equal(8, env.GetOuterAdjacencies().Count());
    }

    [Fact]
    public void NonRectangularRoomThrows()
    {
        // L-shape: room 0 occupies (0,0), (1,0), (0,1) but NOT (1,1).
        var grid = new int[,] { { 0, 0 }, { 0, -1 } };
        Assert.Throws<InvalidOperationException>(() =>
            MultiRoomEnvironmentBuilder.FromGrid(grid, 1f));
    }

    [Fact]
    public void DisconnectedSameIdTilesThrow()
    {
        // Two separate tiles with the same id form a non-rectangular set (bbox has the gap).
        var grid = new int[,] { { 0, -1 }, { -1, 0 } };
        Assert.Throws<InvalidOperationException>(() =>
            MultiRoomEnvironmentBuilder.FromGrid(grid, 1f));
    }

    [Fact]
    public void RoomTypeIsSquareForSingleTileRectangleOtherwise()
    {
        var grid = new int[,] { { 0, 1, 1 } };  // 1x1 square (id 0) and 1x2 rectangle (id 1)
        var env = MultiRoomEnvironmentBuilder.FromGrid(grid, 1f);

        var byId = env.Rooms.ToDictionary(r => r.Id);
        Assert.Equal(RoomType.Square, byId[0].RoomType);
        Assert.Equal(RoomType.Rectangle, byId[1].RoomType);
    }

    [Fact]
    public void LargerRectangularRoomIsSupported()
    {
        // 3x2 room (id 0) — uniform. Should produce a 3x2 RectangleShape.
        var grid = new int[,] { { 0, 0 }, { 0, 0 }, { 0, 0 } };
        var env = MultiRoomEnvironmentBuilder.FromGrid(grid, 1f);

        Assert.Single(env.Rooms);
        var rect = (RectangleShape)env.Rooms[0].Shape;
        Assert.Equal(3f, rect.Width);
        Assert.Equal(2f, rect.Depth);
    }

    [Fact]
    public void AdjacenciesDefaultToClosedPassage()
    {
        var grid = new int[,] { { 0 }, { 1 } };
        var env = MultiRoomEnvironmentBuilder.FromGrid(grid, 1f);
        Assert.All(env.Adjacencies, a => Assert.IsType<Passage.Closed>(a.Passage));
    }

    [Fact]
    public void NullGridThrows()
    {
        Assert.Throws<ArgumentNullException>(() =>
            MultiRoomEnvironmentBuilder.FromGrid(null!, 1f));
    }

    [Fact]
    public void NonPositiveTileSizeThrows()
    {
        var grid = new int[,] { { 0 } };
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MultiRoomEnvironmentBuilder.FromGrid(grid, 0f));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MultiRoomEnvironmentBuilder.FromGrid(grid, -1f));
    }

    [Fact]
    public void TileSizeAffectsRoomPositionAndDimensions()
    {
        // 2-wide × 1-deep grid; room 0 at tile (x=1, z=0).
        var grid = new int[,] { { -1 }, { 0 } };
        var env = MultiRoomEnvironmentBuilder.FromGrid(grid, 3.5f);

        var room = env.Rooms.Single();
        // Position = (xMin * tileSize, zMin * tileSize) = (1 * 3.5, 0 * 3.5) = (3.5, 0).
        Assert.Equal(3.5f, room.Position.X, precision: 4);
        Assert.Equal(0f, room.Position.Y, precision: 4);
        var rect = (RectangleShape)room.Shape;
        Assert.Equal(3.5f, rect.Width);
        Assert.Equal(3.5f, rect.Depth);
    }
}
