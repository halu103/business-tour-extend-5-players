using BusinessTourFiveRealms;
using System.Reflection;

int checks = 0;
void Check(bool value, string name)
{
    checks++;
    if (!value) throw new InvalidOperationException(name);
}
void Equal(float actual, float expected, string name) => Check(Math.Abs(actual - expected) < .001f, name);

Equal(GameplayHudLayoutRules.MoneyFontSize(40), 50, "Native center money text grows from 40 to 50");
Equal(GameplayHudLayoutRules.MovingMoneyMultiplier(1f), 1.3f, "Native destination gets a 30 percent transfer enlargement");
Equal(GameplayHudLayoutRules.MovingMoneyMultiplier(GameplayHudLayoutRules.StationaryMoneyScale) *
    GameplayHudLayoutRules.StationaryMoneyScale, 1.3f,
    "A compact stationary pile does not shrink the animated transfer");
float compactDestination = GameplayHudLayoutRules.StationaryMoneyScale * GameplayHudLayoutRules.WorldRowScale;
Equal(GameplayHudLayoutRules.MovingMoneyMultiplier(compactDestination) * compactDestination, 1.3f,
    "Native transfer matching also compensates the compact parent row");
Equal(GameplayHudLayoutRules.MovingMoneyMultiplier(0), 1.3f, "Zero destination scale fails closed");
Equal(GameplayHudLayoutRules.MovingMoneyMultiplier(float.NaN), 1.3f, "Invalid destination scale fails closed");
Equal(GameplayHudLayoutRules.MoneyFontSize(0), 0, "Invalid font is unchanged");
Equal(GameplayHudLayoutRules.MoneyFontSize(-1), -1, "Negative font is unchanged");
Check(GameplayHudLayoutRules.MoneyFontSize(int.MaxValue) == int.MaxValue, "Overflow fails closed");
Equal(GameplayHudLayoutRules.ToolbarStep(1080, 5), 80, "Full-height toolbar uses 16px gaps");
Equal(GameplayHudLayoutRules.ToolbarStep(360, 5), 64, "Short toolbar uses non-overlapping 64px icons");
Equal(GameplayHudLayoutRules.ToolbarStep(float.NaN, 5), 80, "Invalid canvas fallback");

foreach (var viewport in new[] { (1920f,1080f), (1280f,720f), (1024f,768f), (2560f,1080f) })
foreach (float width in new[] { 100f, 320f, 600f })
foreach (float height in new[] { 100f, 350f, 500f })
for (int xi = -3; xi <= 10; xi++)
for (int yi = -3; yi <= 10; yi++)
{
    float left = viewport.Item1 * xi / 8f;
    float bottom = viewport.Item2 * yi / 8f;
    bool fit = GameplayHudLayoutRules.TryPopupTranslation(left, left + width, bottom, bottom + height,
        0, viewport.Item1, 0, viewport.Item2, out float x, out float y);
    Check(fit, "Native emote-sized panel can be translated into viewport");
    Check(left + x >= viewport.Item1 * .02f - .01f &&
        left + width + x <= viewport.Item1 - GameplayHudLayoutRules.ToolbarReservedWidth + .01f &&
        bottom + y >= viewport.Item2 * .02f - .01f &&
        bottom + height + y <= viewport.Item2 * .98f + .01f,
        "All popup edges fit and the right toolbar remains clear");
    Check(GameplayHudLayoutRules.TryPopupTranslation(left + x, left + width + x,
        bottom + y, bottom + height + y, 0, viewport.Item1, 0, viewport.Item2, out float x2, out float y2),
        "Fitted popup remains eligible");
    Equal(x2, 0, "No incremental horizontal drift");
    Equal(y2, 0, "No incremental vertical drift");
}
Check(!GameplayHudLayoutRules.TryPopupTranslation(0, 1920, 0, 1080,
    0,1920,0,1080,out _,out _), "Fullscreen click catcher is never moved");
Check(!GameplayHudLayoutRules.TryPopupTranslation(float.NaN, 200, 0, 100,
    0,1920,0,1080,out _,out _), "Invalid popup bounds fail closed");
Check(!GameplayHudLayoutRules.TryPopupTranslation(200, 100, 0, 100,
    0,1920,0,1080,out _,out _), "Inverted popup bounds fail closed");

// Native gameplay_bundle Buttons children. Save/remove changes each following
// sibling index, so restoration must undo moves rather than replay them.
string[] nativeButtons = { "ClaimVictory", "GoldShop", "ButtonSmiles", "ButtonPause", "Basket", "ZoomButton" };
string[] extractedButtons = { "ButtonPause", "ZoomButton", "ButtonSmiles", "GoldShop", "Basket" };
List<string> ModelRestoration(string[] extraction, bool reverse)
{
    var children = nativeButtons.ToList();
    var saved = new List<(string Name, int Sibling)>();
    foreach (string name in extraction)
    {
        saved.Add((name, children.IndexOf(name)));
        children.Remove(name);
    }
    foreach (var entry in reverse ? saved.AsEnumerable().Reverse() : saved)
        children.Insert(Math.Clamp(entry.Sibling, 0, children.Count), entry.Name);
    return children;
}
Check(!ModelRestoration(extractedButtons, false).SequenceEqual(nativeButtons),
    "Forward restoration reproduces the former native toolbar order regression");
Check(ModelRestoration(extractedButtons, true).SequenceEqual(nativeButtons),
    "Reverse restoration recovers the actual shipped toolbar order");

IEnumerable<string[]> Permutations(string[] remaining)
{
    if (remaining.Length == 0) { yield return Array.Empty<string>(); yield break; }
    for (int index = 0; index < remaining.Length; index++)
        foreach (string[] tail in Permutations(remaining.Where((_, i) => i != index).ToArray()))
            yield return new[] { remaining[index] }.Concat(tail).ToArray();
}
foreach (string[] extraction in Permutations(extractedButtons))
    Check(ModelRestoration(extraction, true).SequenceEqual(nativeButtons),
        "Reverse undo preserves native siblings for every extraction order");

// Tie the pure model to the actual Unity restoration boundary without loading
// game assemblies or creating native UI in this standalone test suite.
using var sourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("NativeGameplayHudSource.cs")
    ?? throw new InvalidOperationException("Native HUD source contract resource is missing.");
using var sourceReader = new StreamReader(sourceStream);
string hudSource = sourceReader.ReadToEnd();
int inventoryAuditStart = hudSource.IndexOf("internal static void AuditInventory", StringComparison.Ordinal);
int inventoryAuditEnd = hudSource.IndexOf("internal static void EnsureBackground", inventoryAuditStart, StringComparison.Ordinal);
string inventoryAudit = hudSource[inventoryAuditStart..inventoryAuditEnd];
Check(inventoryAudit.Contains("controller._view._playerMoney.Values", StringComparison.Ordinal) &&
    !inventoryAudit.Contains("_playerMovingMoney", StringComparison.Ordinal) &&
    !inventoryAudit.Contains("_movingMoneyCollection", StringComparison.Ordinal),
    "Only stationary piles are compacted, never native moving templates");
Check(hudSource.Contains("if (PositionWorldRow(money[index], index, 0.168f))", StringComparison.Ordinal) &&
    hudSource.Contains("state.MarkMoneyRowScaled(money[index]);", StringComparison.Ordinal),
    "A money row is marked compact only after the camera-dependent placement succeeds");
Check(hudSource.Contains("_scaledMoneyRows.Contains(destination.parent.Pointer)", StringComparison.Ordinal) &&
    hudSource.Contains("(scaledParent ? GameplayHudLayoutRules.WorldRowScale : 1f)", StringComparison.Ordinal),
    "Animation compensation includes only a parent row actually compacted by this layout");
int layoutStart = hudSource.IndexOf("private sealed class LayoutState", StringComparison.Ordinal);
int layoutEnd = hudSource.IndexOf("private sealed class ToolbarControl", layoutStart, StringComparison.Ordinal);
string layoutSource = hudSource[layoutStart..layoutEnd];
Check(layoutSource.Contains("for (int index = Transforms.Count - 1; index >= 0; index--)", StringComparison.Ordinal) &&
    layoutSource.Contains("Transforms[index].Restore();", StringComparison.Ordinal),
    "Native HUD restoration uses the tested reverse undo order");
Check(hudSource.Contains("Layouts.Clear();", StringComparison.Ordinal) &&
    hudSource.Contains("existing.Owns(hud)", StringComparison.Ordinal) &&
    layoutSource.Contains("hud.GetInstanceID() == _instanceId", StringComparison.Ordinal),
    "Destroyed or recycled HUD generations cannot block a later five-player expansion");

Console.WriteLine($"Gameplay HUD rules passed {checks} checks: vertical rail, non-drifting popup containment, money typography, native toolbar restoration.");
