using System;
using System.Collections.Generic;
using BusinessTour;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Photon.Pun;

namespace BusinessTourFiveRealms;

[HarmonyPatch(typeof(MapConfig), nameof(MapConfig.CreateCopy))]
internal static class CompositeMapPatch
{
    private static readonly HashSet<IntPtr> ProcessedMaps = new HashSet<IntPtr>();
    private static readonly Dictionary<IntPtr, MapConfig> PreparedCopies = new Dictionary<IntPtr, MapConfig>();

    internal static void Reset()
    {
        ProcessedMaps.Clear();
        PreparedCopies.Clear();
    }

    private static void Postfix(IMapConfig __0, ref MapConfig __result)
    {
        // A deep copy of a generated map already contains its traps. Applying
        // generation again would add traps beyond the hard two-trap budget.
        if (ModState.IsSpecialMapActive && __0 != null && __result != null &&
            ProcessedMaps.Contains(__0.Pointer))
        {
            ProcessedMaps.Add(__result.Pointer);
            return;
        }
        TryApply(__result, "MapConfig.CreateCopy");
    }

    internal static IMapConfig PrepareMapData(IMapConfig source, string caller)
    {
        if (!ModState.IsSpecialMapActive || source == null || source.Pointer == IntPtr.Zero ||
            ProcessedMaps.Contains(source.Pointer))
        {
            return source;
        }

        if (PreparedCopies.TryGetValue(source.Pointer, out MapConfig prepared) && prepared != null)
        {
            return new IMapConfig(prepared.Pointer);
        }

        try
        {
            // Native CreateCopy deep-copies each CellData through serialization;
            // never change the cached vanilla resource or its shared cells.
            MapConfig copy = MapConfig.CreateCopy(source);
            TryApply(copy, caller);
            if (copy != null && ProcessedMaps.Contains(copy.Pointer))
            {
                PreparedCopies[source.Pointer] = copy;
                return new IMapConfig(copy.Pointer);
            }
        }
        catch (Exception ex)
        {
            Plugin.ModLog.LogError($"Could not prepare Five Realms data from {caller}; using the safe base map: {ex}");
        }
        return source;
    }

    internal static void TryApply(MapConfig map, string source)
    {
        if (!ModState.IsSpecialMapActive || map == null || map.Pointer == IntPtr.Zero || map._cellsArray == null)
        {
            return;
        }

        if (ProcessedMaps.Contains(map.Pointer))
        {
            return;
        }

        try
        {
            // Prepare the complete 40-cell graph away from the resource being
            // consumed. Native cloning, economy, artwork and skin selection
            // must all pass before the only array write below.
            uint seed = BuildSeed();
            var prepared = NativeFivePlayerMapTopology.Prepare(map,
                FivePlayerCitySkinSupport.IsReady,
                Math.Max(1, Plugin.TrapDurationTurns.Value),
                (uint)Math.Max(0, Plugin.TrapReleaseCost.Value));
            ApplyFiveRegions(prepared, seed);
            FivePlayerCitySkinSupport.ValidateAliases(prepared);
            map._cellsArray = prepared;
            ProcessedMaps.Add(map.Pointer);
            Plugin.ModLog.LogInfo($"Five Realms V3 40-cell map data applied from {source}, seed=0x{seed:X8}, resorts=5, chance=3, islands=2, tax=2, traps=2.");
        }
        catch (Exception ex)
        {
            Plugin.ModLog.LogError($"Five Realms generation failed; the safe base map remains active: {ex}");
        }
    }

    private static void ApplyFiveRegions(Il2CppReferenceArray<RawCellData> cells, uint seed)
    {
        int cellCount = cells.Length;
        if (cellCount != FivePlayerTopologyRules.ExpandedCellCount)
            throw new InvalidOperationException("Five Realms V3 requires exactly forty cells.");

        int existingTraps = 0;
        int firstTrapId = -1;
        int lastTrapId = -1;
        for (int index = 0; index < cellCount; index++)
            if (cells[index]?._cellData?._cellType == CellType.RogueTrap)
            {
                if (existingTraps == 0) firstTrapId = index;
                lastTrapId = index;
                existingTraps++;
            }
        if (existingTraps != FiveRealmsLayoutRules.MaximumTraps ||
            !FivePlayerTopologyRules.AreOppositeTrapCells(firstTrapId, lastTrapId, cellCount))
            throw new InvalidOperationException("The expanded board must contain exactly two traps twenty graph cells apart.");

        var random = new StableRandom(seed);
        var regionSkins = new CellSkin[5];
        for (int region = 0; region < regionSkins.Length; region++)
        {
            CellSkin candidate;
            do
            {
                candidate = (CellSkin)random.Next(3);
            }
            while (region > 0 && candidate == regionSkins[region - 1]);
            regionSkins[region] = candidate;
        }

        int[] regionStarts = FivePlayerTopologyRules.RegionStarts();

        for (int index = 0; index < cellCount; index++)
        {
            RawCellData raw = cells[index];
            CellData data = raw?._cellData;
            if (data == null)
            {
                throw new InvalidOperationException("Missing expanded cell data at " + index + ".");
            }

            int region = 0;
            while (region < FiveRealmsLayoutRules.RegionCount - 1 && index >= regionStarts[region + 1]) region++;
            // Regional themes lack exact overrides for some Classic services.
            // Resolve only their artwork; retain the complete native service
            // data instead of substituting another family of travel/tournament.
            data._cellSkin = (CellSkin)FiveRealmsServiceArtRules.ResolveSkin(
                (TopologyCellKind)data._cellType, (int)regionSkins[region]);
        }

        Plugin.ModLog.LogInfo(
            $"Prepared Five Realms seed=0x{seed:X8}, cells={cellCount}, traps={existingTraps}/2, " +
            $"trapCells={firstTrapId}/{lastTrapId}, separation={cellCount / 2}, skins={string.Join("/", regionSkins)}.");
    }

    private static uint BuildSeed()
    {
        string roomName = string.Empty;
        try
        {
            roomName = PhotonNetwork.CurrentRoom?.Name ?? string.Empty;
        }
        catch
        {
            // Menu previews have no Photon room. The fixed fallback still makes
            // the generated preview deterministic.
        }

        return Fnv1A(ModState.MapId + "|" + roomName);
    }

    private static uint Fnv1A(string value)
    {
        uint hash = 2166136261;
        for (int index = 0; index < value.Length; index++)
        {
            hash ^= value[index];
            hash *= 16777619;
        }
        return hash == 0 ? 0x5F3759DFu : hash;
    }

    private sealed class StableRandom
    {
        private uint _state;

        internal StableRandom(uint seed)
        {
            _state = seed == 0 ? 0xA341316Cu : seed;
        }

        internal int Next(int exclusiveMax)
        {
            if (exclusiveMax <= 1)
            {
                return 0;
            }

            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return (int)(x % (uint)exclusiveMax);
        }
    }
}

[HarmonyPatch(typeof(Map), nameof(Map.Initialize), new[]
{
    typeof(IMapConfig),
    typeof(BusinessTour.BoardDecorationsConfig.BuildingsDecorationsConfig)
})]
internal static class CompositeMapInitializationPatch
{
    private static void Prefix(ref IMapConfig __0)
    {
        __0 = CompositeMapPatch.PrepareMapData(__0, "Map.Initialize");
    }
}

// Load(string) inlines the other overload in native 2.21, so patching only
// Load(IMapConfig) misses that path. Intercept its resource-service result
// before both Map.Initialize and the LocationLoaded event consume it.
[HarmonyPatch(typeof(MapResourcesProcessor), nameof(MapResourcesProcessor.LoadMapConfig))]
internal static class CompositeMapResourceLoadPatch
{
    private static void Postfix(ref IMapConfig __result) =>
        __result = CompositeMapPatch.PrepareMapData(__result, "MapResourcesProcessor.LoadMapConfig");
}

[HarmonyPatch(typeof(LocationManager), nameof(LocationManager.Load), new[] { typeof(IMapConfig) })]
internal static class CompositeMapLocationLoadPatch
{
    private static void Prefix(ref IMapConfig __0) =>
        __0 = CompositeMapPatch.PrepareMapData(__0, "LocationManager.Load");
}

[HarmonyPatch(typeof(GameView), nameof(GameView.OnLocationLoaded))]
internal static class CompositeMapVisualConfigPatch
{
    private static void Prefix(ref IMapConfig __1) =>
        __1 = CompositeMapPatch.PrepareMapData(__1, "GameView.OnLocationLoaded");
}
