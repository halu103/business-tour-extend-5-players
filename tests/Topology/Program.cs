using BusinessTourFiveRealms;

TopologySourceCell[] source = ClassicFixture();
TopologySourceCell[] unchangedSource = ClassicFixture();
TopologyExpansionPlan plan = FivePlayerTopologyRules.CreateClassicCandidate(source);
Require(plan.Cells.Count == 40, "32 destination positions plus eight additions");
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
    int expectedTemplateId = id switch { 16 => 30, 28 => 8, 30 => 12, _ => id };
    TopologySourceCell template = source[expectedTemplateId];
    Require(!cell.Appended && cell.TemplateCellId == expectedTemplateId &&
        FivePlayerTopologyRules.OriginalTemplateCellId(id) == expectedTemplateId,
        "only the four approved original services can change role or complete template");
    Require(cell.Kind == (id == 12 ? TopologyCellKind.RogueTrap : template.Kind) && cell.LineId == original.LineId &&
        cell.NativeSuitId == template.NativeSuitId && cell.DecorationId == template.DecorationId,
        "replacement uses the selected native template while destination identity/line are retained");
    if (original.Kind == TopologyCellKind.City || original.Kind == TopologyCellKind.Resort)
        Require(cell.TemplateCellId == id && cell.Kind == original.Kind &&
            cell.NativeSuitId == original.NativeSuitId && cell.DecorationId == original.DecorationId,
            "every original ownable remains at its original ID with its exact native template and economy source");
    Require(original.Kind == unchangedSource[id].Kind && original.LineId == unchangedSource[id].LineId &&
        original.NativeSuitId == unchangedSource[id].NativeSuitId && original.DecorationId == unchangedSource[id].DecorationId,
        "source kind/line/suit/decoration inventory never mutated");
    Require(cell.NextCellId == (id == 31 ? 32 : original.NextCellIds[0]), "only old closing edge changes");
    Require(original.NextCellIds[0] == (id + 1) % 32, "source graph not mutated");
}

TopologyCellPlan[] added = plan.Cells.Where(cell => cell.Appended).ToArray();
Require(added.Length == 8 && added.All(cell => cell.LineId == 5 && cell.SideIndex == 4), "new logical side five");
Require(added.Select(cell => cell.TemplateCellId).SequenceEqual(new[] { 12, 9, 10, 11, 30, 16, 14, 12 }), "complete native-template clone instructions");
Require(added.Count(cell => cell.Kind == TopologyCellKind.Chance) == 1 &&
    added.Count(cell => cell.Kind == TopologyCellKind.Tax) == 1 &&
    added.Count(cell => cell.Kind == TopologyCellKind.Tournament) == 1 &&
    added.Count(cell => cell.Kind == TopologyCellKind.Resort) == 1 &&
    added.Count(cell => cell.Kind == TopologyCellKind.City) == 3 &&
    added.Count(cell => cell.Kind == TopologyCellKind.RogueTrap) == 1, "opposite-trap added-side inventory");
Require(plan.Cells.Count(cell => cell.Kind == TopologyCellKind.Resort) == 5, "fifth resort added, none removed");
Require(plan.Cells.Count(cell => cell.Kind == TopologyCellKind.City) == 23, "three cities added, none removed");
int[] traps = plan.Cells.Where(cell => cell.Kind == TopologyCellKind.RogueTrap).Select(cell => cell.CellId).ToArray();
Require(traps.SequenceEqual(new[] { 12, 32 }) && traps.SequenceEqual(FivePlayerTopologyRules.TrapCellIds()),
    "maximum two traps at the approved opposing cells");
Require(FivePlayerTopologyRules.AreOppositeTrapCells(traps[0], traps[1], 40) &&
    FivePlayerTopologyRules.AreOppositeTrapCells(traps[1], traps[0], 40), "traps are twenty cells apart in both directions");
Require(plan.Cells.Count(cell => cell.Kind == TopologyCellKind.Chance) == 3, "exactly three complete native Chance cells");
Require(plan.Cells.Count(cell => cell.Kind == TopologyCellKind.Start) == 1, "no extra Start/lap rewards");
Require(added[0].Kind == TopologyCellKind.RogueTrap && added[0].TemplateCellId == 12, "opposite corner trap clones Chance rather than Start");
Require(plan.Cells[37].Kind == TopologyCellKind.Tournament && plan.Cells[37].TemplateCellId == 16 &&
    plan.Cells[39].Kind == TopologyCellKind.Chance && plan.Cells[39].TemplateCellId == 12,
    "Tournament relocates intact and final Chance clones the native Chance template");
Require(plan.Cells.Count(cell => cell.Kind == TopologyCellKind.Island) == 2 &&
    plan.Cells.Count(cell => cell.Kind == TopologyCellKind.Tournament) == 1 &&
    plan.Cells.Count(cell => cell.Kind == TopologyCellKind.TravelNextTurn) == 1 &&
    plan.Cells.Count(cell => cell.Kind == TopologyCellKind.Tax) == 2, "two Islands/two Taxes retain native effects with one Tournament and Travel");

var inventory = new Dictionary<TopologyCellKind, int>
{
    [TopologyCellKind.Chance] = 3, [TopologyCellKind.Island] = 2, [TopologyCellKind.Tax] = 2,
    [TopologyCellKind.Resort] = 5, [TopologyCellKind.City] = 23, [TopologyCellKind.RogueTrap] = 2,
    [TopologyCellKind.Start] = 1, [TopologyCellKind.Tournament] = 1, [TopologyCellKind.TravelNextTurn] = 1
};
Require(inventory.Values.Sum() == 40 && Enum.GetValues<TopologyCellKind>().Length == inventory.Count,
    "requested inventory accounts for every cell kind and all forty destinations");
foreach (var expected in inventory)
    Require(plan.Cells.Count(cell => cell.Kind == expected.Key) == expected.Value, "exact requested count for " + expected.Key);

int[] islandIds = Ids(TopologyCellKind.Island);
int[] taxIds = Ids(TopologyCellKind.Tax);
Require(islandIds.SequenceEqual(new[] { 8, 28 }) && taxIds.SequenceEqual(new[] { 16, 36 }),
    "approved native service pair positions");
foreach (int[] pair in new[] { islandIds, taxIds, traps })
    Require(FivePlayerTopologyRules.AreHalfRingApart(pair[0], pair[1], 40) &&
        FivePlayerTopologyRules.AreHalfRingApart(pair[1], pair[0], 40), "each paired service is twenty cells apart in either direction");
int[] chanceIds = Ids(TopologyCellKind.Chance);
Require(chanceIds.SequenceEqual(new[] { 20, 30, 39 }), "approved three Chance positions");
int[] chanceGaps = chanceIds.Select((id, index) => (chanceIds[(index + 1) % chanceIds.Length] - id + 40) % 40).ToArray();
Require(chanceGaps.SequenceEqual(new[] { 10, 9, 21 }) && chanceGaps.All(gap => gap >= 9) && chanceGaps.Sum() == 40,
    "Chance cells are spread by at least nine graph steps including the closing gap");
Require(plan.Cells[28].TemplateCellId == 8 && plan.Cells[16].TemplateCellId == 30 &&
    plan.Cells[36].TemplateCellId == 30 && plan.Cells[30].TemplateCellId == 12 &&
    plan.Cells[24].TemplateCellId == 24 && plan.Cells[24].Kind == TopologyCellKind.TravelNextTurn,
    "Island/Tax/Chance replacements and original Travel use their complete native effect templates");
RequireThrows<ArgumentOutOfRangeException>(() => FivePlayerTopologyRules.OriginalTemplateCellId(-1), "prefix templates reject negative IDs");
RequireThrows<ArgumentOutOfRangeException>(() => FivePlayerTopologyRules.OriginalTemplateCellId(32), "prefix templates reject appended IDs");

int[] Ids(TopologyCellKind kind) => plan.Cells.Where(cell => cell.Kind == kind).Select(cell => cell.CellId).ToArray();

TopologyCellPlan resort = added.Single(cell => cell.Kind == TopologyCellKind.Resort);
Require(resort.TemplateCellId == 14 && resort.NativeSuitId == 3 && resort.DecorationId == 11,
    "resort reuses valid native suit/decoration and complete economy template");
Require(added.Where(cell => cell.Kind == TopologyCellKind.City).All(cell =>
    cell.NativeSuitId == null && cell.ExpansionCityGroup == FivePlayerTopologyRules.ExpansionCityGroup),
    "new city suit unresolved, independent group explicitly required");
foreach (TopologyCellPlan cell in plan.Cells)
{
    int resolved = FivePlayerTopologyRules.ResolveNativeSuit(cell, true);
    Require(resolved == (cell.NativeSuitId ?? 90), "only independent new cities receive suit90");
    if (!cell.NativeSuitId.HasValue)
    {
        bool refused = false;
        try { FivePlayerTopologyRules.ResolveNativeSuit(cell, false); }
        catch (InvalidOperationException) { refused = true; }
        Require(refused, "missing artwork support rejects new city group");
        Require(resolved != FivePlayerTopologyRules.NativeCityArtSuitId,
            "art alias never merges gameplay into original group30");
    }
    else Require(FivePlayerTopologyRules.ResolveNativeSuit(cell, false) == cell.NativeSuitId.Value,
        "native original/trap/resort suits never require an art override");
}
double[] resortTiers = { 25000, 50000, 100000, 200000, 400000 };
for (int count = 1; count <= 5; count++)
    Require(FivePlayerTopologyRules.ExpectedResortRent(25000, 2, count) == resortTiers[count - 1],
        "five-resort expected rent retains native base and multiplier");
Require(plan.Cells.Where(cell => cell.Kind == TopologyCellKind.RogueTrap).All(cell =>
    cell.NativeSuitId == 1 && cell.DecorationId == 1 && cell.ExpansionCityGroup == null),
    "traps excluded from ownable city groups");
Require(added.Where(cell => cell.Kind == TopologyCellKind.Chance).All(cell =>
    cell.NativeSuitId == 1 && cell.DecorationId == 1), "Chance has valid Service metadata");

for (int first = -1; first <= 40; first++)
    for (int second = -1; second <= 40; second++)
        Require(FivePlayerTopologyRules.AreOppositeTrapCells(first, second, 40) ==
            (first >= 0 && first < 40 && second >= 0 && second < 40 && Math.Abs(first - second) == 20),
            "only valid distinct half-ring trap pairs accepted");
Require(!FivePlayerTopologyRules.AreOppositeTrapCells(0, 2, 5) &&
    !FivePlayerTopologyRules.AreOppositeTrapCells(0, 0, 0), "odd or empty routes cannot claim a half-ring pair");

// A regular pentagon is not centrally symmetric, so equal-width tile centers
// cannot generally form an exact diameter. The chosen half-ring pair is also
// among the visually closest opposite pairs; never claim exact180 degrees.
foreach (int winding in new[] { -1, 1 })
{
    CanonicalBoardGeometry geometry = CanonicalBoardGeometry.Create(System.Numerics.Vector2.Zero, 1, 1, 40, winding);
    double selectedDot = System.Numerics.Vector2.Dot(System.Numerics.Vector2.Normalize(geometry.Cells[12].Center),
        System.Numerics.Vector2.Normalize(geometry.Cells[32].Center));
    double closestDot = Enumerable.Range(0, 20).Min(id => (double)System.Numerics.Vector2.Dot(
        System.Numerics.Vector2.Normalize(geometry.Cells[id].Center),
        System.Numerics.Vector2.Normalize(geometry.Cells[id + 20].Center)));
    Require(selectedDot <= closestDot + 0.000001d && selectedDot < -0.999d,
        "approved traps are among closest visual opposites for either winding");
}

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
int[] mutableTrapIds = FivePlayerTopologyRules.TrapCellIds();
mutableTrapIds[0] = 37;
Require(FivePlayerTopologyRules.TrapCellIds()[0] == 12, "trap pair cannot mutate policy");
RequireThrows<NotSupportedException>(() => ((IList<TopologyCellPlan>)plan.Cells)[0] = plan.Cells[1], "plan cells immutable");

// Source contracts tie this pure fixture to the native cloning boundary.
// They do not execute IL2CPP or establish live native effect behavior.
using var nativeStream = System.Reflection.Assembly.GetExecutingAssembly()
    .GetManifestResourceStream("NativeFivePlayerMapTopologySource.cs")
    ?? throw new InvalidOperationException("Missing native topology source contract resource.");
using var nativeReader = new StreamReader(nativeStream);
string nativeSource = nativeReader.ReadToEnd();
string prepareSource = MethodSource(nativeSource, "internal static Il2CppReferenceArray<RawCellData> Prepare(");
string cloneSource = MethodSource(nativeSource, "private static RawCellData CloneNativeRaw(");
string trapSource = MethodSource(nativeSource, "private static void ConfigureTrap(");
string prefixSource = MethodSource(nativeSource, "private static void ValidateOriginalPrefix(");
string templateSource = MethodSource(nativeSource, "private static void ValidateCompleteTemplateSettings(");
string graphSource = MethodSource(nativeSource, "private static void ValidatePreparedGraph(");
Require(cloneSource.Contains("JsonParser.Deserialize(serializedCell, Il2CppType.Of<CellData>())", StringComparison.Ordinal) &&
    cloneSource.Contains("SerializeCell(clone), serializedCell", StringComparison.Ordinal) &&
    cloneSource.Contains("new RawCellData(template.MetaPosition, clone)", StringComparison.Ordinal),
    "native serialization clones complete CellData and selected RawCellData metadata");
Require(prepareSource.Contains("RawCellData template = original[entry.TemplateCellId];", StringComparison.Ordinal) &&
    prepareSource.Contains("CloneNativeRaw(template, sourceJson[entry.TemplateCellId])", StringComparison.Ordinal) &&
    prepareSource.Contains("template._cellData._cellNameLocalizationKey", StringComparison.Ordinal) &&
    prepareSource.Contains("template._cellData._overridenCellName", StringComparison.Ordinal),
    "native creation and name checks use the selected complete template rather than destination data");
Require(trapSource.Contains("data.SetCellType(CellType.RogueTrap);", StringComparison.Ordinal) &&
    trapSource.Contains("data._trapSettings = new TrapSettings(durationTurns, releaseCost);", StringComparison.Ordinal) &&
    nativeSource.Split(".SetCellType(", StringSplitOptions.None).Length - 1 == 1 &&
    nativeSource.Split("_trapSettings =", StringSplitOptions.None).Length - 1 == 1 &&
    prepareSource.Contains("if (entry.Kind == TopologyCellKind.RogueTrap)", StringComparison.Ordinal),
    "only approved RogueTrap cells receive a native type/skin/trap-settings transition");
Require(prefixSource.Contains("FivePlayerTopologyRules.OriginalTemplateCellId(id)", StringComparison.Ordinal) &&
    prefixSource.Contains("entry.TemplateCellId != approvedTemplateId", StringComparison.Ordinal) &&
    prefixSource.Contains("(CellType)entry.Kind != approvedKind", StringComparison.Ordinal) &&
    prefixSource.Contains("entry.LineId != original[id]._cellData._lineId", StringComparison.Ordinal),
    "native prefix accepts only explicit approved service templates and retained destination lines");
Require(prefixSource.Contains("sourceCell._cellType == CellType.City || sourceCell._cellType == CellType.Resort", StringComparison.Ordinal) &&
    prefixSource.Contains("entry.TemplateCellId != id", StringComparison.Ordinal) &&
    prefixSource.Contains("clone._suitId != sourceCell._suitId", StringComparison.Ordinal) &&
    prefixSource.Contains("clone._decorationType != sourceCell._decorationType", StringComparison.Ordinal),
    "native prefix independently pins all original ownables to their native identity/group template");
Require(templateSource.Contains("CloneNativeRaw(template, sourceJson[entry.TemplateCellId])", StringComparison.Ordinal) &&
    templateSource.Contains("expected._cellId = entry.CellId;", StringComparison.Ordinal) &&
    templateSource.Contains("expected._lineId = entry.LineId;", StringComparison.Ordinal) &&
    templateSource.Contains("expected._nextCellsArray = new Il2CppStructArray<int>(new[] { entry.NextCellId });", StringComparison.Ordinal) &&
    templateSource.Contains("SerializeCell(prepared[id]._cellData), SerializeCell(expected)", StringComparison.Ordinal) &&
    templateSource.Contains("prepared[id].MetaPosition != template.MetaPosition", StringComparison.Ordinal),
    "complete native final settings compare against the actual template normalized to destination ID/line/edge");
Require(prefixSource.Contains("ValidateCompleteTemplateSettings(prepared, original, sourceJson, entry,", StringComparison.Ordinal) &&
    prepareSource.Contains("for (int id = original.Length; id < prepared.Length; id++)", StringComparison.Ordinal) &&
    prepareSource.Contains("ValidateCompleteTemplateSettings(prepared, original, sourceJson, plan.Cells[id],", StringComparison.Ordinal),
    "every original and appended cell receives complete native settings validation");
Require(!nativeSource.Contains("._taxSettings =", StringComparison.Ordinal) &&
    !nativeSource.Contains("._tournamentSettings =", StringComparison.Ordinal) &&
    !nativeSource.Contains("._travelSettings =", StringComparison.Ordinal) &&
    !nativeSource.Contains("._islandSettings =", StringComparison.Ordinal) &&
    !templateSource.Contains("clone._", StringComparison.Ordinal),
    "native Island/Tax/Tournament/Travel effect fields are retained through full clone rather than manually reconstructed");
Require(prepareSource.Contains("SerializeCell(original[id]._cellData), sourceJson[id]", StringComparison.Ordinal) &&
    !nativeSource.Contains("source._cellsArray = ", StringComparison.Ordinal) &&
    !nativeSource.Contains("original[id]._cellData._nextCellsArray = ", StringComparison.Ordinal) &&
    nativeSource.Split("map._cellsArray = prepared;", StringSplitOptions.None).Length - 1 == 1,
    "preparation checks the untouched source before the sole Apply commit");
Require(graphSource.Contains("chance != 3", StringComparison.Ordinal) &&
    graphSource.Contains("islandIds.Count != 2 || taxIds.Count != 2 || tournaments != 1 || travel != 1", StringComparison.Ordinal) &&
    graphSource.Contains("AreHalfRingApart(islandIds[0], islandIds[1]", StringComparison.Ordinal) &&
    graphSource.Contains("AreHalfRingApart(taxIds[0], taxIds[1]", StringComparison.Ordinal) &&
    graphSource.Contains("if (gap < 9)", StringComparison.Ordinal),
    "native adapter enforces exact service inventory, separated pairs and circular Chance gaps");

Console.WriteLine("PASS: exact 40-cell inventory (3 Chance, 2 Island, 2 Tax, 5 Resort, 23 City, 2 Trap, 1 Start/Tournament/Travel); all original ownables preserved; approved complete native effect templates and source contracts; paired services 20 steps apart; Chance gaps 10/9/21; independent suit90/artwork gate; forty two-lap walks and unsupported-source rejection.");

static string MethodSource(string sourceText, string signature)
{
    int start = sourceText.IndexOf(signature, StringComparison.Ordinal);
    Require(start >= 0, "native source contract method exists: " + signature);
    int open = sourceText.IndexOf('{', start);
    int depth = 0;
    for (int index = open; index < sourceText.Length; index++)
    {
        if (sourceText[index] == '{') depth++;
        else if (sourceText[index] == '}' && --depth == 0) return sourceText[start..(index + 1)];
    }
    throw new InvalidOperationException("Missing native source contract method boundary.");
}

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
