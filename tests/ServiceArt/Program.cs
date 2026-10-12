using BusinessTourFiveRealms;

// Exact service overrides read from the game's bundled map catalogs. Horses
// travel9 and Wonderland travel14 cannot satisfy native TravelNextTurn6.
var nativePrefabs = new Dictionary<(int Skin, TopologyCellKind Kind), string>
{
    [(0, TopologyCellKind.Start)] = "CellStart",
    [(0, TopologyCellKind.Island)] = "CellIsland",
    [(0, TopologyCellKind.Tournament)] = "CellTournament",
    [(0, TopologyCellKind.TravelNextTurn)] = "CellTravel",
    [(1, TopologyCellKind.Start)] = "CellStart_Fantasy",
    [(1, TopologyCellKind.Tournament)] = "CellTournament_Knight",
    [(1, TopologyCellKind.RogueTrap)] = "CellRogueTrap",
    [(2, TopologyCellKind.Start)] = "CellStart_Wonderland"
};

for (int regionSkin = 0; regionSkin < 3; regionSkin++)
{
    foreach (var service in new[]
    {
        (TopologyCellKind.Island, "CellIsland"),
        (TopologyCellKind.Tournament, "CellTournament"),
        (TopologyCellKind.TravelNextTurn, "CellTravel")
    })
    {
        int selected = FiveRealmsServiceArtRules.ResolveSkin(service.Item1, regionSkin);
        Require(nativePrefabs.TryGetValue((selected, service.Item1), out string actual) &&
            actual == service.Item2, "original Classic service art resolves for every region theme");
    }

    foreach (TopologyCellKind kind in new[]
    {
        TopologyCellKind.Start, TopologyCellKind.City,
        TopologyCellKind.Chance, TopologyCellKind.Tax
    })
        Require(FiveRealmsServiceArtRules.ResolveSkin(kind, regionSkin) == regionSkin,
            "supported regional art remains themed: " + kind);
    int startSkin = FiveRealmsServiceArtRules.ResolveSkin(TopologyCellKind.Start, regionSkin);
    Require(nativePrefabs.ContainsKey((startSkin, TopologyCellKind.Start)),
        "regional Start override is available; no unnecessary Classic fallback");
    int trapSkin = FiveRealmsServiceArtRules.ResolveSkin(TopologyCellKind.RogueTrap, regionSkin);
    Require(nativePrefabs[(trapSkin, TopologyCellKind.RogueTrap)] == "CellRogueTrap",
        "both trap cells retain their native Fantasy artwork family");
    Require(FiveRealmsServiceArtRules.ResolveSkin(TopologyCellKind.Resort, regionSkin) == 0,
        "all five real Resort2 cells retain Classic beach art, not AliceRoom15/obelisk art");
}

Require(!nativePrefabs.ContainsKey((2, TopologyCellKind.Tournament)),
    "Wonderland cannot render Classic World Championships without a fallback");
Require(!nativePrefabs.ContainsKey((1, TopologyCellKind.TravelNextTurn)) &&
    !nativePrefabs.ContainsKey((2, TopologyCellKind.TravelNextTurn)),
    "the two non-Classic travel variants are not exact type6 artwork overrides");
// Decoration10..13 read from the current BuildingsData catalogs: the ground
// keys alone are insufficient evidence that a native Resort2 has matching art.
foreach (int decoration in new[] { 10, 11, 12, 13 })
{
    var catalogFamilies = new Dictionary<int, (int CellType, string Ground)>
    {
        [0] = (2, "Cell_Resort"),
        [1] = (2, "cell_obelisk"),
        [2] = (15, new[] { "cell_alice_living_room", "cell_alice_kitchen", "cell_alice_bedroom", "cell_alice_bathroom" }[decoration - 10])
    };
    foreach (int regionSkin in new[] { 0, 1, 2 })
    {
        int resolved = FiveRealmsServiceArtRules.ResolveSkin(TopologyCellKind.Resort, regionSkin);
        Require(catalogFamilies[resolved] == (2, "Cell_Resort"),
            "native Resort2 resolves real beach ground/decoration for every supported resort suit");
    }
    Require(catalogFamilies[2].CellType != (int)TopologyCellKind.Resort,
        "AliceRoom15 is never treated as equivalent native Resort2 artwork");
}
RequireThrows(() => FiveRealmsServiceArtRules.ResolveSkin(TopologyCellKind.Start, -1),
    "negative region skin rejected");
RequireThrows(() => FiveRealmsServiceArtRules.ResolveSkin(TopologyCellKind.Island, 3),
    "unknown region skin rejected before fallback");
RequireThrows(() => FiveRealmsServiceArtRules.ResolveSkin((TopologyCellKind)9, 1),
    "different native travel family rejected rather than substituted");

Console.WriteLine("PASS: World Championships/World Tour/palm island and native Resort2 beach art resolve to Classic in all regional skins; regional Start/cities/Chance/Tax and Fantasy traps preserved; incompatible travel/AliceRoom families excluded.");

static void Require(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException("FAIL: " + description);
}

static void RequireThrows(Action action, string description)
{
    try { action(); }
    catch (ArgumentOutOfRangeException) { return; }
    throw new InvalidOperationException("FAIL: " + description);
}
