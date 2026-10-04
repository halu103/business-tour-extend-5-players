using System;
using System.Collections.Generic;
using BusinessTour;
using HarmonyLib;
using Photon.Pun;

namespace BusinessTourFiveRealms;

[HarmonyPatch(typeof(MapConfig), nameof(MapConfig.CreateCopy))]
internal static class CompositeMapPatch
{
    private static void Postfix(ref MapConfig __result)
    {
        if (!ModState.IsSpecialMapActive || __result == null || __result._cellsArray == null)
        {
            return;
        }

        try
        {
            ApplyFiveRegions(__result, BuildSeed());
        }
        catch (Exception ex)
        {
            Plugin.ModLog.LogError($"Five Realms generation failed; the safe base map remains active: {ex}");
        }
    }

    private static void ApplyFiveRegions(MapConfig map, uint seed)
    {
        int cellCount = map._cellsArray.Length;
        if (cellCount < 5)
        {
            return;
        }

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

        var eligibleByRegion = new List<int>[5];
        for (int region = 0; region < eligibleByRegion.Length; region++)
        {
            eligibleByRegion[region] = new List<int>();
        }

        for (int index = 0; index < cellCount; index++)
        {
            RawCellData raw = map._cellsArray[index];
            CellData data = raw?._cellData;
            if (data == null)
            {
                continue;
            }

            int region = Math.Min(4, index * 5 / cellCount);
            data._cellSkin = regionSkins[region];
            if (index != 0 && data._cellType == CellType.City)
            {
                eligibleByRegion[region].Add(index);
            }
        }

        int trapsPlaced = 0;
        for (int region = 0; region < eligibleByRegion.Length; region++)
        {
            List<int> candidates = eligibleByRegion[region];
            if (candidates.Count == 0)
            {
                continue;
            }

            int cellIndex = candidates[random.Next(candidates.Count)];
            CellData data = map._cellsArray[cellIndex]._cellData;
            data._cellType = CellType.RogueTrap;
            data._trapSettings = new TrapSettings(
                Math.Max(1, Plugin.TrapDurationTurns.Value),
                (uint)Math.Max(0, Plugin.TrapReleaseCost.Value));
            trapsPlaced++;
        }

        Plugin.ModLog.LogInfo(
            $"Generated Five Realms seed=0x{seed:X8}, cells={cellCount}, traps={trapsPlaced}, " +
            $"skins={string.Join("/", regionSkins)}.");
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
