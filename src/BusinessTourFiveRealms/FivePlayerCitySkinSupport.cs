using System;
using System.Collections.Generic;
using BusinessTour;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace BusinessTourFiveRealms;

// Suit 90 is the added side's independent GAMEPLAY city group. Only an art
// lookup may borrow Suit3: changing CellData._suitId would merge monopolies.
// These fallbacks leave the native resource dictionaries/arrays untouched.
internal static class FivePlayerCitySkinSupport
{
    internal const CellSuit ExpansionCitySuit = (CellSuit)FivePlayerTopologyRules.ExpansionCitySuitId;
    internal const CellSuit NativeCityArtSuit = (CellSuit)FivePlayerTopologyRules.NativeCityArtSuitId;

    private static readonly HashSet<IntPtr> WarnedConfigs = new();
    private static bool _skinNamePatchInstalled;
    private static bool _catalogPatchInstalled;

    internal static bool IsReady => _skinNamePatchInstalled && _catalogPatchInstalled;

    // The bootstrap calls this only after Harmony successfully installs the
    // complete class processor. A failed/missing detour must not permit the
    // topology adapter to commit cells whose art cannot be resolved.
    internal static void MarkPatchInstalled(Type patchType)
    {
        if (patchType == typeof(FivePlayerCitySkinNamePatch)) _skinNamePatchInstalled = true;
        if (patchType == typeof(FivePlayerCityCatalogSkinNamePatch)) _catalogPatchInstalled = true;
    }

    internal static void ValidateAliases(Il2CppReferenceArray<RawCellData> cells)
    {
        if (!ModState.IsSpecialMapActive)
            throw new InvalidOperationException("Added-city aliases are only supported on Five Realms.");
        if (!IsReady)
            throw new InvalidOperationException("Both added-city artwork detours must be installed.");
        if (cells == null || cells.Length == 0)
            throw new InvalidOperationException("The candidate has no cells to validate.");

        var skins = new HashSet<CellSkin>();
        bool hasExpansionCity = false;
        for (int index = 0; index < cells.Length; index++)
        {
            CellData data = cells[index]?._cellData;
            if (data == null)
                throw new InvalidOperationException("The candidate contains a missing cell definition.");
            skins.Add(data._cellSkin);
            if (data._cellType == CellType.City && data._suitId == ExpansionCitySuit)
                hasExpansionCity = true;
        }
        if (!hasExpansionCity)
            throw new InvalidOperationException("The candidate has no independent added-city group.");

        // Entity.CellViewProvider (2.21 token0x060025D6/RVA0xBC1F20)
        // resolves this exact registered context service; CellViewFactory
        // calls that getter before its native skin lookup. The context may
        // lazily load the catalog, so a global Unity object scan performed
        // earlier during map preparation can see no catalog indefinitely.
        // Resolve the same game-owned provider, not a guessed resource path,
        // replacement ScriptableObject, or an arbitrary loaded catalog.
        ICellViewCompositeProvider provider = Context.Instance?.Get<ICellViewCompositeProvider>();
        CellsConfigsCatalog catalog = provider?.TryCast<CellsConfigsCatalog>();
        if (catalog == null || catalog.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("The active native cell-art provider is not a supported CellsConfigsCatalog.");

        foreach (CellSkin skin in skins)
        {
            if (skin != CellSkin.Classic && skin != CellSkin.Fantasy && skin != CellSkin.Wonderland)
                throw new InvalidOperationException("The added city uses an unsupported skin: " + skin + ".");
            CellSkinConfig config = catalog.GetCompositeConfig(skin)?._cellSkinConfig;
            if (config == null || config.Pointer == IntPtr.Zero ||
                !config.TryGetSkinByCellSuit(NativeCityArtSuit, out string nativeAlias) ||
                string.IsNullOrEmpty(nativeAlias))
                throw new InvalidOperationException("The native " + skin + " Suit3 artwork alias is missing.");

            // Exercise BOTH actual patched native entry points and compare to
            // the native alias. No serialized array or dictionary is edited;
            // the only lazy initialization is the game's own normal lookup.
            string skinAlias = config.GetSkinName(CellType.City, ExpansionCitySuit);
            string catalogAlias = catalog.GetBasicCellSkinName(CellType.City, ExpansionCitySuit, skin);
            if (!string.Equals(skinAlias, nativeAlias, StringComparison.Ordinal) ||
                !string.Equals(catalogAlias, nativeAlias, StringComparison.Ordinal))
                throw new InvalidOperationException("The " + skin + " added-city artwork detours did not resolve the exact native alias.");
        }
    }

    internal static bool NeedsAlias(CellType type, CellSuit suit, string result) =>
        ModState.IsSpecialMapActive && type == CellType.City &&
        suit == ExpansionCitySuit && string.IsNullOrEmpty(result);

    internal static void FillMissingAlias(CellSkinConfig config, ref string result)
    {
        if (config == null || config.Pointer == IntPtr.Zero) return;
        try
        {
            // Resolve the exact serialized alias from this selected native
            // Classic/Fantasy/Wonderland config. Do not guess asset paths or
            // call the patched GetSkinName recursively.
            if (config.TryGetSkinByCellSuit(NativeCityArtSuit, out string alias) &&
                !string.IsNullOrEmpty(alias))
            {
                result = alias;
                return;
            }
            WarnOnce(config, "the native Suit3 art alias is missing");
        }
        catch (Exception ex)
        {
            WarnOnce(config, ex.Message);
        }
    }

    private static void WarnOnce(CellSkinConfig config, string reason)
    {
        if (WarnedConfigs.Add(config.Pointer))
            Plugin.ModLog.LogWarning("Five Realms could not resolve added-city artwork: " + reason + ".");
    }
}

// Current 2.21 interop token 0x06006B19, native RVA 0x7F82E0; unique body.
[HarmonyPatch(typeof(CellSkinConfig), nameof(CellSkinConfig.GetSkinName),
    new[] { typeof(CellType), typeof(CellSuit) })]
internal static class FivePlayerCitySkinNamePatch
{
    private static void Postfix(CellSkinConfig __instance, CellType __0, CellSuit __1,
        ref string __result)
    {
        if (FivePlayerCitySkinSupport.NeedsAlias(__0, __1, __result))
            FivePlayerCitySkinSupport.FillMissingAlias(__instance, ref __result);
    }
}

// Current 2.21 interop token 0x06006B07, native RVA 0x7F7FE0; unique body.
// Native catalog code INLINES GetSkinName, so the preceding patch alone does
// not cover its lookup. Keep the same scope and use its actual skin config.
[HarmonyPatch(typeof(CellsConfigsCatalog), nameof(CellsConfigsCatalog.GetBasicCellSkinName),
    new[] { typeof(CellType), typeof(CellSuit), typeof(CellSkin) })]
internal static class FivePlayerCityCatalogSkinNamePatch
{
    private static void Postfix(CellsConfigsCatalog __instance, CellType __0, CellSuit __1,
        CellSkin __2, ref string __result)
    {
        if (!FivePlayerCitySkinSupport.NeedsAlias(__0, __1, __result)) return;
        if (__instance == null || __instance.Pointer == IntPtr.Zero) return;
        try
        {
            CellSkinConfig config = __instance.GetCompositeConfig(__2)?._cellSkinConfig;
            FivePlayerCitySkinSupport.FillMissingAlias(config, ref __result);
        }
        catch (Exception ex)
        {
            Plugin.ModLog.LogWarning("Five Realms could not select added-city artwork for " +
                __2 + ": " + ex.Message);
        }
    }
}
