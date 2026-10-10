using System;
using System.Collections.Generic;

namespace BusinessTourFiveRealms;

// Pure generation policy, shared by the runtime and the offline regression
// tests. No Unity or game objects are touched by this class.
internal static class FiveRealmsLayoutRules
{
    internal const int RegionCount = 5;
    internal const int MaximumTraps = 2;

    internal static int[] RegionStarts(int cellCount)
    {
        if (cellCount < RegionCount) throw new ArgumentOutOfRangeException(nameof(cellCount));
        // Match theme boundaries to the five visible corners. The two longer
        // sides of the native 32-cell graph are separated for visual balance.
        if (cellCount == 32) return new[] { 0, 7, 13, 20, 26, 32 };
        var starts = new int[RegionCount + 1];
        for (int region = 0; region <= RegionCount; region++)
            starts[region] = (int)Math.Round(region * cellCount / (double)RegionCount,
                MidpointRounding.AwayFromZero);
        return starts;
    }

    internal static int[] SelectTrapCells(IReadOnlyList<IReadOnlyList<int>> regions,
        uint seed, int existingTraps)
    {
        if (existingTraps < 0 || existingTraps > MaximumTraps)
            throw new ArgumentOutOfRangeException(nameof(existingTraps));
        var eligible = new List<int>();
        for (int region = 0; region < regions.Count; region++)
            if (regions[region] != null && regions[region].Count > 0) eligible.Add(region);
        uint state = seed == 0 ? 0xA341316Cu : seed;
        var selected = new List<int>();
        // Shuffle eligible regions: traps are on distinct sides, not always
        // in the first two regions. Existing native traps consume the budget.
        for (int index = 0; index < eligible.Count &&
            selected.Count + existingTraps < MaximumTraps; index++)
        {
            int other = index + Next(ref state, eligible.Count - index);
            (eligible[index], eligible[other]) = (eligible[other], eligible[index]);
            var candidates = regions[eligible[index]];
            selected.Add(candidates[Next(ref state, candidates.Count)]);
        }
        return selected.ToArray();
    }

    private static int Next(ref uint state, int exclusiveMax)
    {
        if (exclusiveMax <= 1) return 0;
        uint x = state;
        x ^= x << 13;
        x ^= x >> 17;
        x ^= x << 5;
        state = x;
        return (int)(x % (uint)exclusiveMax);
    }
}
