namespace OpenApparatus.Tests;

public class SeededRandomTests
{
    [Fact]
    public void SameSeed_ProducesIdenticalSequence()
    {
        var a = new SeededRandom(42);
        var b = new SeededRandom(42);

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(a.NextInt(1_000_000), b.NextInt(1_000_000));
        }
    }

    [Fact]
    public void DifferentSeeds_DivergeInFirstHundredDraws()
    {
        var a = new SeededRandom(1);
        var b = new SeededRandom(2);
        bool differed = false;

        for (int i = 0; i < 100 && !differed; i++)
        {
            if (a.NextInt(1_000_000) != b.NextInt(1_000_000)) differed = true;
        }

        Assert.True(differed, "Two different seeds should diverge well within 100 draws.");
    }

    [Fact]
    public void Shuffle_IsDeterministicForSeed()
    {
        var listA = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
        var listB = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

        new SeededRandom(7).Shuffle(listA);
        new SeededRandom(7).Shuffle(listB);

        Assert.Equal(listA, listB);
    }

    [Fact]
    public void Pick_ReturnsAnElementOfTheList()
    {
        var rng = new SeededRandom(0);
        var items = new[] { "a", "b", "c", "d" };

        for (int i = 0; i < 50; i++)
        {
            Assert.Contains(rng.Pick(items), items);
        }
    }

    [Fact]
    public void NextInt_ZeroOrNegativeMax_Throws()
    {
        var rng = new SeededRandom(0);
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(-1));
    }
}
