using System.Numerics;
using OpenApparatus.Topology;

namespace OpenApparatus.Tests.Topology;

public class RectangleShapeTests
{
    [Fact]
    public void Outline_HasFourEdges_OrderedSouthEastNorthWest()
    {
        var s = new RectangleShape(2f, 3f);
        var o = s.GetOutline();

        Assert.Equal(4, o.Count);
        // South: (0,0) -> (W,0)
        Assert.Equal(new Vector2(0, 0), o[0].Start);
        Assert.Equal(new Vector2(2, 0), o[0].End);
        // East: (W,0) -> (W,D)
        Assert.Equal(new Vector2(2, 0), o[1].Start);
        Assert.Equal(new Vector2(2, 3), o[1].End);
        // North: (W,D) -> (0,D)
        Assert.Equal(new Vector2(2, 3), o[2].Start);
        Assert.Equal(new Vector2(0, 3), o[2].End);
        // West: (0,D) -> (0,0)
        Assert.Equal(new Vector2(0, 3), o[3].Start);
        Assert.Equal(new Vector2(0, 0), o[3].End);
    }

    [Fact]
    public void Outline_IsClosed()
    {
        var s = new RectangleShape(5f, 5f);
        var o = s.GetOutline();
        Assert.Equal(o[0].Start, o[3].End);
    }

    [Fact]
    public void LocalBounds_StartsAtOrigin()
    {
        var s = new RectangleShape(4f, 7f);
        var b = s.GetLocalBounds();
        Assert.Equal(Vector2.Zero, b.Min);
        Assert.Equal(new Vector2(4, 7), b.Max);
    }

    [Fact]
    public void Constructor_RejectsZeroOrNegativeDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RectangleShape(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RectangleShape(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RectangleShape(-1, 1));
    }
}
