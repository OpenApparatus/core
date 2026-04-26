using System.Linq;
using System.Numerics;
using OpenApparatus.Topology;

namespace OpenApparatus.Tests.Topology;

public class AdjacencyAndFloorPlanTests
{
    static Cell MakeCell(int id, float x, float z) =>
        new(id, new RectangleShape(1, 1), new Vector2(x, z), RoomType.Square);

    [Fact]
    public void Adjacency_DefaultsToClosedPassage()
    {
        var a = MakeCell(0, 0, 0);
        var b = MakeCell(1, 1, 0);
        var seg = new EdgeSegment(new Vector2(1, 0), new Vector2(1, 1));
        var adj = new Adjacency(a, b, seg);
        Assert.IsType<Passage.Closed>(adj.Passage);
    }

    [Fact]
    public void Adjacency_IsInternal_WhenBothCellsPresent()
    {
        var a = MakeCell(0, 0, 0);
        var b = MakeCell(1, 1, 0);
        var seg = new EdgeSegment(new Vector2(1, 0), new Vector2(1, 1));
        var adj = new Adjacency(a, b, seg);
        Assert.True(adj.IsInternal);
        Assert.False(adj.IsOuter);
    }

    [Fact]
    public void Adjacency_IsOuter_WhenCellBNull()
    {
        var a = MakeCell(0, 0, 0);
        var seg = new EdgeSegment(new Vector2(0, 0), new Vector2(1, 0));
        var adj = new Adjacency(a, null, seg);
        Assert.True(adj.IsOuter);
        Assert.False(adj.IsInternal);
    }

    [Fact]
    public void Adjacency_Other_ReturnsTheOtherCell()
    {
        var a = MakeCell(0, 0, 0);
        var b = MakeCell(1, 1, 0);
        var seg = new EdgeSegment(new Vector2(1, 0), new Vector2(1, 1));
        var adj = new Adjacency(a, b, seg);
        Assert.Same(b, adj.Other(a));
        Assert.Same(a, adj.Other(b));
    }

    [Fact]
    public void Adjacency_Other_OfUnrelatedCell_Throws()
    {
        var a = MakeCell(0, 0, 0);
        var b = MakeCell(1, 1, 0);
        var c = MakeCell(2, 2, 0);
        var seg = new EdgeSegment(new Vector2(1, 0), new Vector2(1, 1));
        var adj = new Adjacency(a, b, seg);
        Assert.Throws<ArgumentException>(() => adj.Other(c));
    }

    [Fact]
    public void FloorPlan_GetWorldBounds_EncompassesAllCells()
    {
        var a = MakeCell(0, 0, 0);
        var b = MakeCell(1, 5, 7);
        var plan = new FloorPlan(new[] { a, b }, []);
        var bounds = plan.GetWorldBounds();
        Assert.Equal(new Vector2(0, 0), bounds.Min);
        Assert.Equal(new Vector2(6, 8), bounds.Max);
    }

    [Fact]
    public void FloorPlan_FilterAdjacencies()
    {
        var a = MakeCell(0, 0, 0);
        var b = MakeCell(1, 1, 0);
        var seg1 = new EdgeSegment(new Vector2(1, 0), new Vector2(1, 1));
        var seg2 = new EdgeSegment(new Vector2(0, 0), new Vector2(1, 0));
        var inner = new Adjacency(a, b, seg1);
        var outer = new Adjacency(a, null, seg2);
        var plan = new FloorPlan(new[] { a, b }, new[] { inner, outer });

        Assert.Single(plan.GetInternalAdjacencies());
        Assert.Single(plan.GetOuterAdjacencies());
        Assert.Equal(2, plan.GetAdjacenciesOf(a).Count());
        Assert.Single(plan.GetAdjacenciesOf(b));
    }
}
