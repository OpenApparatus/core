using System.Numerics;

namespace OpenApparatus.Tests;

public class EdgeSegmentTests
{
    [Fact]
    public void Length_OfHorizontalSegment_EqualsXDelta()
    {
        var s = new EdgeSegment(new Vector2(0, 0), new Vector2(5, 0));
        Assert.Equal(5f, s.Length, precision: 5);
    }

    [Fact]
    public void Direction_IsUnitVector()
    {
        var s = new EdgeSegment(new Vector2(1, 1), new Vector2(4, 5));
        Assert.Equal(1f, s.Direction.Length(), precision: 5);
    }

    [Fact]
    public void Normal_IsRotated90DegreesCcwFromDirection()
    {
        // Segment going +X (east). CCW 90° → +Y (north in 2D XZ-plane convention).
        var s = new EdgeSegment(new Vector2(0, 0), new Vector2(1, 0));
        Assert.Equal(new Vector2(0, 1), s.Normal);
    }

    [Fact]
    public void Midpoint_IsHalfway()
    {
        var s = new EdgeSegment(new Vector2(0, 0), new Vector2(10, 4));
        Assert.Equal(new Vector2(5, 2), s.Midpoint);
    }

    [Fact]
    public void Reversed_FlipsStartAndEnd()
    {
        var s = new EdgeSegment(new Vector2(0, 0), new Vector2(3, 4));
        var r = s.Reversed();
        Assert.Equal(s.Start, r.End);
        Assert.Equal(s.End, r.Start);
    }

    [Fact]
    public void ZeroLengthSegment_DirectionIsZero()
    {
        var s = new EdgeSegment(new Vector2(2, 2), new Vector2(2, 2));
        Assert.Equal(Vector2.Zero, s.Direction);
        Assert.Equal(0f, s.Length);
    }

    [Fact]
    public void Equality_ByValue()
    {
        var a = new EdgeSegment(new Vector2(1, 2), new Vector2(3, 4));
        var b = new EdgeSegment(new Vector2(1, 2), new Vector2(3, 4));
        var c = new EdgeSegment(new Vector2(0, 0), new Vector2(3, 4));
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.True(a == b);
        Assert.True(a != c);
    }
}
