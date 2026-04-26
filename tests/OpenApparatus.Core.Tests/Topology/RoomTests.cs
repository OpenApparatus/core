using System.Numerics;
using OpenApparatus.Topology;

namespace OpenApparatus.Tests.Topology;

public class RoomTests
{
    [Fact]
    public void WorldOutline_TranslatesByPosition()
    {
        var room = new Room(0, new RectangleShape(2, 3), new Vector2(10, 20), RoomType.Square);
        var outline = room.GetWorldOutline();

        // South edge translated by Position.
        Assert.Equal(new Vector2(10, 20), outline[0].Start);
        Assert.Equal(new Vector2(12, 20), outline[0].End);
    }

    [Fact]
    public void WorldOutline_AtOriginWithNoRotation_EqualsLocalOutline()
    {
        var shape = new RectangleShape(2, 3);
        var room = new Room(0, shape, Vector2.Zero, RoomType.Square);

        var local = shape.GetOutline();
        var world = room.GetWorldOutline();
        for (int i = 0; i < local.Count; i++)
            Assert.Equal(local[i], world[i]);
    }

    [Fact]
    public void WorldBounds_TranslatesByPosition()
    {
        var room = new Room(0, new RectangleShape(2, 3), new Vector2(5, 7), RoomType.Square);
        var b = room.GetWorldBounds();
        Assert.Equal(new Vector2(5, 7), b.Min);
        Assert.Equal(new Vector2(7, 10), b.Max);
    }

    [Fact]
    public void WorldOutline_With90DegreeRotation_RotatesAllEdges()
    {
        var room = new Room(0, new RectangleShape(2, 3), Vector2.Zero, RoomType.Square, rotation: 90f);
        var outline = room.GetWorldOutline();

        // (W,0) under +90° around Y → (0, W). The east edge in local space
        // was (2,0) → (2,3); after 90° rotation it becomes (0,2) → (-3,2).
        Assert.Equal(new Vector2(0, 2), outline[1].Start, new Vector2Comparer(1e-4f));
        Assert.Equal(new Vector2(-3, 2), outline[1].End, new Vector2Comparer(1e-4f));
    }

    [Fact]
    public void Constructor_NullShape_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Room(0, null!, Vector2.Zero, RoomType.Square));
    }

    sealed class Vector2Comparer(float epsilon) : IEqualityComparer<Vector2>
    {
        public bool Equals(Vector2 x, Vector2 y) =>
            MathF.Abs(x.X - y.X) < epsilon && MathF.Abs(x.Y - y.Y) < epsilon;
        public int GetHashCode(Vector2 obj) => obj.GetHashCode();
    }
}
