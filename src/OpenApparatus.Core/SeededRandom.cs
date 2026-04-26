using System;
using System.Collections.Generic;

namespace OpenApparatus;

/// <summary>
/// Deterministic random number source. The single RNG used throughout floor-plan
/// generation: every generator and passage assigner takes a <see cref="SeededRandom"/>,
/// never a raw <see cref="System.Random"/>. This keeps "same seed → same plan"
/// inviolable, which is the single most important reproducibility guarantee the
/// library makes.
/// </summary>
public sealed class SeededRandom
{
    readonly Random _random;

    /// <summary>The seed that was used to construct this instance.</summary>
    public int Seed { get; }

    public SeededRandom(int seed)
    {
        Seed = seed;
        _random = new Random(seed);
    }

    /// <summary>Uniform float in [0, 1).</summary>
    public float NextFloat() => (float)_random.NextDouble();

    /// <summary>Uniform integer in [0, maxExclusive).</summary>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        return _random.Next(maxExclusive);
    }

    /// <summary>Uniform integer in [minInclusive, maxExclusive).</summary>
    public int NextInt(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        return _random.Next(minInclusive, maxExclusive);
    }

    /// <summary>Fair coin flip.</summary>
    public bool NextBool() => _random.Next(2) == 1;

    /// <summary>Returns a uniformly-chosen element from <paramref name="items"/>.</summary>
    public T Pick<T>(IReadOnlyList<T> items)
    {
        if (items is null) throw new ArgumentNullException(nameof(items));
        if (items.Count == 0) throw new ArgumentException("List is empty.", nameof(items));
        return items[_random.Next(items.Count)];
    }

    /// <summary>In-place Fisher–Yates shuffle.</summary>
    public void Shuffle<T>(IList<T> items)
    {
        if (items is null) throw new ArgumentNullException(nameof(items));
        for (int i = items.Count - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
