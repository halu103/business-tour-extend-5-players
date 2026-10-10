using BusinessTourFiveRealms;

var regions = new IReadOnlyList<int>[]
{
    new[] { 1, 2, 3 }, new[] { 7, 8, 9 }, new[] { 13, 14 },
    new[] { 20, 21, 22 }, new[] { 26, 27, 28 }
};
var observedRegions = new HashSet<int>();
Require(FiveRealmsLayoutRules.RegionStarts(32).SequenceEqual(new[] { 0, 7, 13, 20, 26, 32 }),
    "theme boundaries match all five visible corners");
for (int cellCount = 5; cellCount <= 128; cellCount++)
{
    int[] starts = FiveRealmsLayoutRules.RegionStarts(cellCount);
    Require(starts.Length == 6 && starts[0] == 0 && starts[^1] == cellCount,
        "region partition covers the entire ring");
    Require(starts.Zip(starts.Skip(1), (a, b) => b > a).All(x => x),
        "regions are nonempty and never overlap");
}
for (uint seed = 0; seed < 10000; seed++)
{
    for (int existing = 0; existing <= 2; existing++)
    {
        int[] first = FiveRealmsLayoutRules.SelectTrapCells(regions, seed, existing);
        int[] second = FiveRealmsLayoutRules.SelectTrapCells(regions, seed, existing);
        Require(first.SequenceEqual(second), "generation is deterministic");
        Require(first.Length + existing == 2, "hard two-trap budget including existing traps");
        Require(first.Distinct().Count() == first.Length, "no duplicate cells");
        var sides = first.Select(cell => Array.FindIndex(regions, r => r.Contains(cell))).ToArray();
        Require(sides.All(side => side >= 0), "only eligible cities");
        Require(sides.Distinct().Count() == sides.Length, "distinct regions");
        foreach (int side in sides) observedRegions.Add(side);
    }
}
Require(observedRegions.Count == 5, "all five regions can receive traps");
Require(FiveRealmsLayoutRules.SelectTrapCells(Array.Empty<IReadOnlyList<int>>(), 1, 0).Length == 0,
    "no eligible regions gives no traps");
var sparse = new IReadOnlyList<int>[] { Array.Empty<int>(), new[] { 7 }, Array.Empty<int>() };
Require(FiveRealmsLayoutRules.SelectTrapCells(sparse, 1, 0).SequenceEqual(new[] { 7 }),
    "sparse map gives one trap without invalid placement");
foreach (int invalid in new[] { -1, 3 })
{
    bool rejected = false;
    try { FiveRealmsLayoutRules.SelectTrapCells(regions, 1, invalid); }
    catch (ArgumentOutOfRangeException) { rejected = true; }
    Require(rejected, "reject invalid existing-trap budget");
}
Console.WriteLine("PASS: 30,000 seeded layouts, max 2 traps, existing-trap budget, eligibility and deterministic regions.");

static void Require(bool value, string description)
{
    if (!value) throw new InvalidOperationException("FAIL: " + description);
}
