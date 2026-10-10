using BusinessTourFiveRealms;

TopologySourceCell[] source = ClassicFixture();
TopologyExpansionPlan plan = FivePlayerTopologyRules.CreateClassicCandidate(source);
Require(plan.Cells.Count == 40, "32 preserved cells plus eight additions");
Require(plan.Cells.Select(cell => cell.CellId).SequenceEqual(Enumerable.Range(0, 40)), "unique ordered IDs");
Require(FivePlayerTopologyRules.RegionStarts().SequenceEqual(new[] { 0, 8, 16, 24, 32, 40 }), "five equal eight-cell sides");
Require(FiveRealmsLayoutRules.RegionStarts(40).SequenceEqual(FivePlayerTopologyRules.RegionStarts()), "existing region policy agrees for 40 cells");
Require(source.Count(cell => cell.Kind == TopologyCellKind.City) == 20, "inspected native map has 20 cities");
Require(source.Where(cell => cell.Kind == TopologyCellKind.Resort).Select(cell => cell.CellId)
    .SequenceEqual(new[] { 4, 14, 18, 25 }), "inspected native four-resort inventory");
Require(source.Where(cell => cell.Kind == TopologyCellKind.Chance).Select(cell => cell.CellId)
    .SequenceEqual(new[] { 12, 20, 28 }), "inspected native three-Chance inventory");

for (int id = 0; id < 32; id++)
{
    TopologyCellPlan cell = plan.Cells[id];
    TopologySourceCell original = source[id];
    Require(!cell.Appended && cell.TemplateCellId == id, "original complete native template retained");
    Require(cell.Kind == original.Kind && cell.LineId == original.LineId &&
        cell.NativeSuitId == original.NativeSuitId && cell.DecorationId == original.DecorationId,
        "old kind, line, suit and decoration unchanged");
    Require(cell.NextCellId == (id == 31 ? 32 : original.NextCellIds[0]), "only old closing edge changes");
    Require(original.NextCellIds[0] == (id + 1) % 32, "source graph not mutated");
}

TopologyCellPlan[] added = plan.Cells.Where(cell => cell.Appended).ToArray();
Require(added.Length == 8 && added.All(cell => cell.LineId == 5 && cell.SideIndex == 4), "new logical side five");
Require(added.Select(cell => cell.TemplateCellId).SequenceEqual(new[] { 12, 9, 10, 11, 12, 13, 14, 15 }), "complete native-template clone instructions");
Require(added.Count(cell => cell.Kind == TopologyCellKind.Chance) == 2 &&
    added.Count(cell => cell.Kind == TopologyCellKind.Resort) == 1 &&
    added.Count(cell => cell.Kind == TopologyCellKind.City) == 3 &&
    added.Count(cell => cell.Kind == TopologyCellKind.RogueTrap) == 2, "candidate added-side inventory");
Require(plan.Cells.Count(cell => cell.Kind == TopologyCellKind.Resort) == 5, "fifth resort added, none removed");
Require(plan.Cells.Count(cell => cell.Kind == TopologyCellKind.City) == 23, "three cities added, none removed");
Require(plan.Cells.Count(cell => cell.Kind == TopologyCellKind.RogueTrap) == 2, "maximum two traps, exclusively new cells");
Require(plan.Cells.Count(cell => cell.Kind == TopologyCellKind.Start) == 1, "no extra Start/lap rewards");
Require(added[0].Kind == TopologyCellKind.Chance && added[0].TemplateCellId == 12, "new corner clones Chance rather than Start");
Require(plan.Cells.Count(cell => cell.Kind == TopologyCellKind.Island) == 1 &&
    plan.Cells.Count(cell => cell.Kind == TopologyCellKind.Tournament) == 1 &&
    plan.Cells.Count(cell => cell.Kind == TopologyCellKind.TravelNextTurn) == 1 &&
    plan.Cells.Count(cell => cell.Kind == TopologyCellKind.Tax) == 1, "native services not duplicated");

TopologyCellPlan resort = added.Single(cell => cell.Kind == TopologyCellKind.Resort);
Require(resort.TemplateCellId == 14 && resort.NativeSuitId == 3 && resort.DecorationId == 11,
    "resort reuses valid native suit/decoration and complete economy template");
Require(added.Where(cell => cell.Kind == TopologyCellKind.City).All(cell =>
    cell.NativeSuitId == null && cell.ExpansionCityGroup == FivePlayerTopologyRules.ExpansionCityGroup),
    "new city suit unresolved, independent group explicitly required");
Require(added.Where(cell => cell.Kind == TopologyCellKind.RogueTrap).All(cell =>
    cell.NativeSuitId == 1 && cell.DecorationId == 1 && cell.ExpansionCityGroup == null),
    "traps excluded from ownable city groups");
Require(added.Where(cell => cell.Kind == TopologyCellKind.Chance).All(cell =>
    cell.NativeSuitId == 1 && cell.DecorationId == 1), "Chance has valid Service metadata");

// Traverse from every cell through two full laps, preserving ordered IDs and
// crossing Start exactly once per lap; no forks, short loops or orphan cells.
for (int start = 0; start < 40; start++)
{
    int cursor = start;
    int passesStart = 0;
    var seen = new HashSet<int>();
    for (int step = 0; step < 80; step++)
    {
        Require(cursor == (start + step) % 40, "graph order matches the forty-cell ring");
        seen.Add(cursor);
        cursor = plan.Cells[cursor].NextCellId;
        if (cursor == 0) passesStart++;
    }
    Require(cursor == start && seen.Count == 40 && passesStart == 2, "two complete laps and exactly two Start crossings");
}
Require(Enumerable.Range(0, 5).All(side => plan.Cells.Count(cell => cell.SideIndex == side) == 8), "eight cells per logical side");
Require(!plan.IsRuntimeReady && plan.RequiredNativeWork.Count > 0, "no runtime-ready claim");
RequireThrows<InvalidOperationException>(() => plan.RequireRuntimeReady(), "runtime materialization gated");

for (int caseIndex = 0; caseIndex < 8; caseIndex++)
{
    TopologySourceCell[] invalid = ClassicFixture();
    if (caseIndex == 0) invalid = invalid.Take(31).ToArray();
    else if (caseIndex == 1) invalid[0] = null;
    else if (caseIndex == 2) invalid[2] = new TopologySourceCell(3, TopologyCellKind.City, 1, 10, 2, new[] { 3 });
    else if (caseIndex == 3) invalid[31] = new TopologySourceCell(31, TopologyCellKind.City, 4, 80, 9, new[] { 1 });
    else if (caseIndex == 4) invalid[12] = new TopologySourceCell(12, TopologyCellKind.Start, 2, 1, 1, new[] { 13 });
    else if (caseIndex == 5) invalid[14] = new TopologySourceCell(14, TopologyCellKind.Resort, 5, 3, 11, new[] { 15 });
    else if (caseIndex == 6) invalid[9] = new TopologySourceCell(9, TopologyCellKind.City, 2, 90, 4, new[] { 10 });
    else invalid[9] = new TopologySourceCell(9, TopologyCellKind.City, 2, 30, 4, new[] { 10, 15 });
    RequireThrows<ArgumentException>(() => FivePlayerTopologyRules.CreateClassicCandidate(invalid), "unsupported source rejected");
}
RequireThrows<ArgumentNullException>(() => FivePlayerTopologyRules.CreateClassicCandidate(null), "missing loaded source rejected");
int[] mutableLinks = { 1 };
var isolated = new TopologySourceCell(0, TopologyCellKind.Start, 1, 1, 1, mutableLinks);
mutableLinks[0] = 99;
Require(isolated.NextCellIds[0] == 1, "inventory defensively copies mutable next-cell arrays");
int[] boundaries = FivePlayerTopologyRules.RegionStarts();
boundaries[1] = 99;
Require(FivePlayerTopologyRules.RegionStarts()[1] == 8, "region boundaries cannot mutate policy");
RequireThrows<NotSupportedException>(() => ((IList<TopologyCellPlan>)plan.Cells)[0] = plan.Cells[1], "plan cells immutable");

Console.WriteLine("PASS: unwired 40-cell candidate, all 32 identities preserved, native template provenance, 5 resorts, 3 new cities/2 new traps, 40 two-lap walks, unsupported-source rejection and unresolved-suit runtime gate.");

static TopologySourceCell[] ClassicFixture()
{
    // Metadata extracted from the bundled ClassicMap, 2026-10-11. Prices and
    // rewards are deliberately not recreated here: the runtime must clone
    // each complete loaded native RawCellData, not this inventory fixture.
    int[] kinds = { 1, 0, 0, 0, 2, 0, 0, 0, 3, 0, 0, 0, 4, 0, 2, 0,
        5, 0, 2, 0, 4, 0, 0, 0, 6, 2, 0, 0, 4, 0, 7, 0 };
    return Enumerable.Range(0, 32).Select(id =>
    {
        int line = id / 8 + 1;
        TopologyCellKind kind = (TopologyCellKind)kinds[id];
        int suit = kind == TopologyCellKind.City ? (id / 4 + 1) * 10 :
            kind == TopologyCellKind.Resort ? line + 1 : 1;
        int decoration = kind == TopologyCellKind.City ? id / 4 + 2 :
            kind == TopologyCellKind.Resort ? line + 9 : 1;
        return new TopologySourceCell(id, kind, line, suit, decoration, new[] { (id + 1) % 32 });
    }).ToArray();
}

static void Require(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + description);
}

static void RequireThrows<TException>(Action action, string description) where TException : Exception
{
    try { action(); }
    catch (TException) { return; }
    throw new InvalidOperationException("FAIL: " + description);
}
