using System.Numerics;

namespace OpenApparatus.Tests;

public class Bounds2DTests
{
    [Fact]
    public void Constructor_RejectsInvertedBounds()
    {
        Assert.Throws<ArgumentException>(() =>
            new Bounds2D(new Vector2(5, 0), new Vector2(0, 5)));
    }

    [Fact]
    public void Size_IsMaxMinusMin()
    {
        var b = new Bounds2D(new Vector2(1, 2), new Vector2(4, 7));
        Assert.Equal(new Vector2(3, 5), b.Size);
    }

    [Fact]
    public void Center_IsHalfwayBetweenMinAndMax()
    {
        var b = new Bounds2D(new Vector2(0, 0), new Vector2(10, 6));
        Assert.Equal(new Vector2(5, 3), b.Center);
    }

    [Fact]
    public void Contains_IsInclusiveOfBoundary()
    {
        var b = new Bounds2D(new Vector2(0, 0), new Vector2(10, 10));
        Assert.True(b.Contains(new Vector2(0, 0)));
        Assert.True(b.Contains(new Vector2(10, 10)));
        Assert.True(b.Contains(new Vector2(5, 5)));
        Assert.False(b.Contains(new Vector2(11, 5)));
        Assert.False(b.Contains(new Vector2(5, -1)));
    }

    [Fact]
    public void Union_WrapsBothInputs()
    {
        var a = new Bounds2D(new Vector2(0, 0), new Vector2(2, 2));
        var b = new Bounds2D(new Vector2(3, 4), new Vector2(5, 6));
        var u = Bounds2D.Union(a, b);
        Assert.Equal(new Vector2(0, 0), u.Min);
        Assert.Equal(new Vector2(5, 6), u.Max);
    }

    [Fact]
    public void Encapsulate_ExtendsBoundsToIncludePoint()
    {
        var b = new Bounds2D(new Vector2(0, 0), new Vector2(2, 2));
        var e = b.Encapsulate(new Vector2(-1, 5));
        Assert.Equal(new Vector2(-1, 0), e.Min);
        Assert.Equal(new Vector2(2, 5), e.Max);
    }

    [Fact]
    public void Area_OfUnitBox_IsOne()
    {
        var b = new Bounds2D(new Vector2(0, 0), new Vector2(1, 1));
        Assert.Equal(1f, b.Area);
    }
}
