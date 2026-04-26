using OpenApparatus.Topology;

namespace OpenApparatus.Tests.Topology;

public class PassageTests
{
    [Fact]
    public void Closed_IsSingleton()
    {
        Assert.Same(Passage.Closed.Instance, Passage.Closed.Instance);
    }

    [Fact]
    public void Open_IsSingleton()
    {
        Assert.Same(Passage.Open.Instance, Passage.Open.Instance);
    }

    [Fact]
    public void Doorway_PreservesParameters()
    {
        var d = new Passage.Doorway(offsetAlongEdge: 1.5f, width: 1.2f, height: 2.2f);
        Assert.Equal(1.5f, d.OffsetAlongEdge);
        Assert.Equal(1.2f, d.Width);
        Assert.Equal(2.2f, d.Height);
    }

    [Theory]
    [InlineData(-0.1f, 1f, 2f)]
    [InlineData(0f, 0f, 2f)]
    [InlineData(0f, -1f, 2f)]
    [InlineData(0f, 1f, 0f)]
    [InlineData(0f, 1f, -1f)]
    public void Doorway_RejectsInvalidParameters(float offset, float width, float height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Passage.Doorway(offset, width, height));
    }

    [Fact]
    public void PatternMatching_WorksOverPassageHierarchy()
    {
        Passage p1 = Passage.Closed.Instance;
        Passage p2 = Passage.Open.Instance;
        Passage p3 = new Passage.Doorway(0f, 1f, 2f);

        Assert.True(p1 is Passage.Closed);
        Assert.True(p2 is Passage.Open);
        Assert.True(p3 is Passage.Doorway);
    }
}
