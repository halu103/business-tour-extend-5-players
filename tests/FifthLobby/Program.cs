using BusinessTourFiveRealms;
using System.Reflection;

int checks = 0;
void Require(bool condition, string message)
{
    checks++;
    if (!condition) throw new InvalidOperationException(message);
}

Require(FifthLobbyPresentationRules.SelectAdditionalColor(new[] { 3, 5, 2, 0 }) == 4,
    "The vanilla magenta/yellow/green/blue palette receives native coral, not another cyan.");
Require(FifthLobbyPresentationRules.SelectAdditionalColor(new[] { 4, 5, 2, 0 }) == 3,
    "A palette which already contains coral receives unused native magenta.");

void ExercisePalette(List<int> colors)
{
    if (colors.Count > 0)
    {
        int[] original = colors.ToArray();
        int fifth = FifthLobbyPresentationRules.SelectAdditionalColor(colors);
        Require(fifth >= 0 && fifth <= 5 && !colors.Contains(fifth), "An additional native color is unique.");
        Require(colors.SequenceEqual(original), "The first four player colors remain unchanged.");
        if (!colors.Contains(4)) Require(fifth == 4, "Coral is preferred whenever unused.");
    }
    if (colors.Count == 4) return;
    for (int color = 0; color <= 5; color++)
    {
        if (colors.Contains(color)) continue;
        colors.Add(color);
        ExercisePalette(colors);
        colors.RemoveAt(colors.Count - 1);
    }
}
ExercisePalette(new List<int>());

foreach (int[] invalid in new[] { Array.Empty<int>(), new[] { -1 }, new[] { 6 }, new[] { 0, 0 }, new[] { 0, 1, 2, 3, 4 } })
{
    bool rejected = false;
    try { FifthLobbyPresentationRules.SelectAdditionalColor(invalid); }
    catch (ArgumentException) { rejected = true; }
    Require(rejected, "Invalid native palettes fail closed.");
}

foreach (bool occupied in new[] { false, true })
foreach (bool ready in new[] { false, true })
    Require(FifthLobbyPresentationRules.ShowReady(occupied, ready) == (occupied && ready),
        "Only an occupied player whose native room state is ready can show a check.");
Require(FifthLobbyPresentationRules.ShowReady(true, true) &&
    !FifthLobbyPresentationRules.ShowReady(true, false) &&
    !FifthLobbyPresentationRules.ShowReady(false, true),
    "Ready, unready and slot removal clear the indicator without a permanent mark.");

using var fixture = Assembly.GetExecutingAssembly().GetManifestResourceStream("NativeFifthLobbySource.cs")
    ?? throw new InvalidOperationException("Missing fifth-lobby native source contract.");
using var reader = new StreamReader(fixture);
string source = reader.ReadToEnd();
string initialize = "Controller.InitializeSlotAttributes(SlotIndex, color, info);";
int initializedAt = source.IndexOf(initialize, StringComparison.Ordinal);
int syncAt = source.IndexOf("SyncNativeReady(info);", initializedAt, StringComparison.Ordinal);
Require(initializedAt >= 0 && syncAt > initializedAt,
    "Native color and avatar refresh precede the ready setter, so the native decorator has a real target.");
Require(source.Contains("_fifthAvatarConfig.IsReady = ready;", StringComparison.Ordinal) &&
    source.Contains("info?.IsReady ?? false", StringComparison.Ordinal),
    "The retained fifth config reflects actual native room readiness.");
Require(source.Contains("if (!changed)", StringComparison.Ordinal) &&
    source.Contains("TryCast<ReadyStateDecorator>()", StringComparison.Ordinal) &&
    source.Contains("OnReadyStateChanged", StringComparison.Ordinal),
    "A recreated native avatar reuses the native ready decorator even if readiness did not change.");
Require(!source.Contains("_fifthAvatarConfig.IsReady = true;", StringComparison.Ordinal) &&
    !source.Contains("new ReadyStateDecorator", StringComparison.Ordinal),
    "No forced-ready state or second ready listener is installed.");
Require(!source.Contains("PlayerColor.Blue2", StringComparison.Ordinal),
    "The fifth-seat policy no longer falls back to cyan.");

Console.WriteLine($"PASS: {checks} fifth-lobby palette/readiness policy and native-source contract checks. Live ready/unready visibility still requires an in-game test.");
