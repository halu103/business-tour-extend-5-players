using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using BusinessTour;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace BusinessTourFiveRealms;

// Materializes a private copy only. The caller must first enable/audit the
// map-specific rendering alias for the independent expansion-city suit.
// It does not patch native placement, change global resources, or install
// a Harmony hook. Prepare never mutates its source; Apply commits once.
internal static class NativeFivePlayerMapTopology
{
    internal const int ExpansionCitySuitId = FivePlayerTopologyRules.ExpansionCitySuitId;

    internal static void Apply(MapConfig map, bool expansionCityAssetsReady,
        int trapDurationTurns, uint trapReleaseCost)
    {
        Il2CppReferenceArray<RawCellData> prepared = Prepare(map, expansionCityAssetsReady,
            trapDurationTurns, trapReleaseCost);
        // All cloning, graph validation and native economic checks occur
        // before this sole write. Call Prepare instead when a larger map
        // generation transaction needs to validate additional work first.
        map._cellsArray = prepared;
    }

    internal static Il2CppReferenceArray<RawCellData> Prepare(MapConfig source,
        bool expansionCityAssetsReady, int trapDurationTurns, uint trapReleaseCost)
    {
        if (!expansionCityAssetsReady)
            throw new InvalidOperationException("The map-specific expansion-city skin/asset alias must be ready before materializing suit 90.");
        if (source == null || source.Pointer == IntPtr.Zero || source._cellsArray == null)
            throw new ArgumentNullException(nameof(source));
        if (trapDurationTurns < 1) throw new ArgumentOutOfRangeException(nameof(trapDurationTurns));

        Il2CppReferenceArray<RawCellData> original = source._cellsArray;
        if (original.Length != FivePlayerTopologyRules.OriginalCellCount)
            throw new InvalidOperationException("Only the inspected, unmodified 32-cell ClassicMap can be expanded.");

        var inventory = new TopologySourceCell[original.Length];
        var sourceJson = new string[original.Length];
        for (int id = 0; id < original.Length; id++)
        {
            RawCellData raw = original[id];
            CellData data = RequireCellData(raw, "source " + id);
            RequireNativeName(data, "source " + id);
            var links = data._nextCellsArray;
            if (links == null) throw new InvalidOperationException("Missing source links at " + id + ".");
            int[] copiedLinks = new int[links.Length];
            for (int next = 0; next < copiedLinks.Length; next++) copiedLinks[next] = links[next];
            inventory[id] = new TopologySourceCell(data._cellId, (TopologyCellKind)data._cellType,
                data._lineId, (int)data._suitId, (int)data._decorationType, copiedLinks);
            sourceJson[id] = SerializeCell(data);
        }

        // Reject altered maps before producing any expanded data. This policy
        // deliberately does not accept an already trapped/composited source.
        TopologyExpansionPlan plan = FivePlayerTopologyRules.CreateClassicCandidate(inventory);
        ValidateResortEconomy(original, 4);

        var prepared = new Il2CppReferenceArray<RawCellData>(FivePlayerTopologyRules.ExpandedCellCount);
        var allocatedCellPointers = new HashSet<IntPtr>();
        var allocatedRawPointers = new HashSet<IntPtr>();
        for (int id = 0; id < prepared.Length; id++)
        {
            TopologyCellPlan entry = plan.Cells[id];
            RawCellData template = original[entry.TemplateCellId];
            RawCellData clone = CloneNativeRaw(template, sourceJson[entry.TemplateCellId]);
            CellData data = clone._cellData;
            if (!allocatedCellPointers.Add(data.Pointer) || !allocatedRawPointers.Add(clone.Pointer))
                throw new InvalidOperationException("Native serialization returned shared cell instances.");

            if (entry.Kind == TopologyCellKind.RogueTrap)
            {
                // Only Chance templates become the opposite traps12/32.
                // The native transition supplies Service/NoBuilding metadata;
                // full cloned settings otherwise remain unchanged.
                ConfigureTrap(data, trapDurationTurns, trapReleaseCost);
            }

            data._cellId = entry.CellId;
            data._lineId = entry.LineId;
            data._nextCellsArray = new Il2CppStructArray<int>(new[] { entry.NextCellId });
            if (entry.Appended && entry.Kind == TopologyCellKind.City)
            {
                // Never call SetCellType(City) after assigning an ID above31:
                // the native city-suit switch only initializes eight groups.
                data._suitId = (CellSuit)FivePlayerTopologyRules.ResolveNativeSuit(entry, expansionCityAssetsReady);
            }

            // Names are field-based, not reconstructed from the new CellId.
            // Keeping the full native template retains existing localization
            // and art keys; duplicated city names are an experimental limit.
            RequireNativeName(data, "prepared " + id);
            if (!string.Equals(data._cellNameLocalizationKey, template._cellData._cellNameLocalizationKey,
                StringComparison.Ordinal) || !string.Equals(data._overridenCellName,
                template._cellData._overridenCellName, StringComparison.Ordinal))
                throw new InvalidOperationException("Native clone changed a localization/name field at " + id + ".");
            prepared[id] = clone;
        }

        ValidatePreparedGraph(prepared, plan);
        ValidateOriginalPrefix(prepared, original, sourceJson, plan, trapDurationTurns, trapReleaseCost);
        for (int id = original.Length; id < prepared.Length; id++)
            ValidateCompleteTemplateSettings(prepared, original, sourceJson, plan.Cells[id],
                trapDurationTurns, trapReleaseCost);
        ValidateResortEconomy(prepared, 5);
        ValidateNativeResortLevels(prepared);

        // Defensive check against source aliasing or unexpected concurrent
        // mutation. A failed transaction must leave the complete source intact.
        if (source._cellsArray == null || source._cellsArray.Pointer != original.Pointer)
            throw new InvalidOperationException("The source map array changed during preparation.");
        for (int id = 0; id < original.Length; id++)
            if (!string.Equals(SerializeCell(original[id]._cellData), sourceJson[id], StringComparison.Ordinal))
                throw new InvalidOperationException("The source map changed during preparation at " + id + ".");

        return prepared;
    }

    private static RawCellData CloneNativeRaw(RawCellData template, string serializedCell)
    {
        // Mirror MapConfig.CreateCopy (native token0x06006DCA): its full
        // CellData round-trip uses BusinessTour.JsonParser, not JsonUtility,
        // and then wraps the result with the original RawCellData metadata.
        Il2CppSystem.Object parsed = JsonParser.Deserialize(serializedCell, Il2CppType.Of<CellData>());
        if (parsed == null || parsed.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("Native CellData deserialization returned null.");
        var clone = new CellData(parsed.Pointer);
        if (clone.Pointer == template._cellData.Pointer ||
            !string.Equals(SerializeCell(clone), serializedCell, StringComparison.Ordinal))
            throw new InvalidOperationException("Native CellData clone is shared or does not preserve complete serialized settings.");
        var sourceLinks = template._cellData._nextCellsArray;
        if (sourceLinks != null && clone._nextCellsArray != null && sourceLinks.Pointer == clone._nextCellsArray.Pointer)
            throw new InvalidOperationException("Native CellData clone retained a shared link array.");
        return new RawCellData(template.MetaPosition, clone);
    }

    private static void ConfigureTrap(CellData data, int durationTurns, uint releaseCost)
    {
        data.SetCellType(CellType.RogueTrap);
        data._cellSkin = CellSkin.Fantasy;
        data._trapSettings = new TrapSettings(durationTurns, releaseCost);
    }

    private static void ValidatePreparedGraph(Il2CppReferenceArray<RawCellData> cells, TopologyExpansionPlan plan)
    {
        int resorts = 0;
        int traps = 0;
        int starts = 0;
        int cities = 0;
        int chance = 0;
        var chanceIds = new List<int>();
        var islandIds = new List<int>();
        var taxIds = new List<int>();
        int tournaments = 0;
        int travel = 0;
        int firstTrapId = -1;
        int lastTrapId = -1;
        for (int id = 0; id < cells.Length; id++)
        {
            CellData data = RequireCellData(cells[id], "candidate " + id);
            TopologyCellPlan expected = plan.Cells[id];
            if (data._cellId != id || data._lineId != expected.LineId ||
                data._cellType != (CellType)expected.Kind || data._nextCellsArray == null ||
                data._nextCellsArray.Length != 1 || data._nextCellsArray[0] != (id + 1) % cells.Length)
                throw new InvalidOperationException("Invalid expanded cell identity or graph edge at " + id + ".");
            int expectedSuit = FivePlayerTopologyRules.ResolveNativeSuit(expected, true);
            if ((int)data._suitId != expectedSuit || (int)data._decorationType != expected.DecorationId)
                throw new InvalidOperationException("Invalid expanded suit/decoration metadata at " + id + ".");
            if (data._cellType == CellType.Resort) resorts++;
            else if (data._cellType == CellType.RogueTrap)
            {
                if (traps == 0) firstTrapId = id;
                lastTrapId = id;
                traps++;
            }
            else if (data._cellType == CellType.Start) starts++;
            else if (data._cellType == CellType.City) cities++;
            else if (data._cellType == CellType.Chance)
            {
                chance++;
                chanceIds.Add(id);
            }
            else if (data._cellType == CellType.Island) islandIds.Add(id);
            else if (data._cellType == CellType.Tax) taxIds.Add(id);
            else if (data._cellType == CellType.Tournament) tournaments++;
            else if (data._cellType == CellType.TravelNextTurn) travel++;
        }
        if (cells.Length != 40 || resorts != 5 || traps != 2 || starts != 1 || cities != 23 || chance != 3 ||
            islandIds.Count != 2 || taxIds.Count != 2 || tournaments != 1 || travel != 1)
            throw new InvalidOperationException("The prepared 40-cell inventory differs from the approved candidate.");
        if (!FivePlayerTopologyRules.AreOppositeTrapCells(firstTrapId, lastTrapId, cells.Length))
            throw new InvalidOperationException("The approved traps must be twenty graph cells apart.");
        if (!FivePlayerTopologyRules.AreHalfRingApart(islandIds[0], islandIds[1], cells.Length) ||
            !FivePlayerTopologyRules.AreHalfRingApart(taxIds[0], taxIds[1], cells.Length))
            throw new InvalidOperationException("Native Island and Tax pairs must each be twenty graph cells apart.");
        for (int index = 0; index < chanceIds.Count; index++)
        {
            int next = chanceIds[(index + 1) % chanceIds.Count];
            int gap = (next - chanceIds[index] + cells.Length) % cells.Length;
            if (gap < 9) throw new InvalidOperationException("Native Chance cells must be at least nine graph cells apart.");
        }
    }

    private static void ValidateOriginalPrefix(Il2CppReferenceArray<RawCellData> prepared,
        Il2CppReferenceArray<RawCellData> original, string[] sourceJson, TopologyExpansionPlan plan,
        int trapDurationTurns, uint trapReleaseCost)
    {
        for (int id = 0; id < original.Length; id++)
        {
            TopologyCellPlan entry = plan.Cells[id];
            CellData clone = prepared[id]._cellData;
            if (prepared[id].Pointer == original[id].Pointer || clone.Pointer == original[id]._cellData.Pointer)
                throw new InvalidOperationException("Prepared cell aliases the original cell at " + id + ".");
            int approvedTemplateId = FivePlayerTopologyRules.OriginalTemplateCellId(id);
            CellType approvedKind = id == FivePlayerTopologyRules.FirstTrapCellId ?
                CellType.RogueTrap : original[approvedTemplateId]._cellData._cellType;
            if (entry.Appended || entry.TemplateCellId != approvedTemplateId ||
                (CellType)entry.Kind != approvedKind || entry.CellId != id ||
                entry.LineId != original[id]._cellData._lineId)
                throw new InvalidOperationException("Unapproved original service replacement at " + id + ".");

            // Ownable cells are pinned independently of service policy: every
            // original city/resort retains its position and complete template.
            CellData sourceCell = original[id]._cellData;
            if ((sourceCell._cellType == CellType.City || sourceCell._cellType == CellType.Resort) &&
                (entry.TemplateCellId != id || clone._cellType != sourceCell._cellType ||
                 clone._suitId != sourceCell._suitId || clone._decorationType != sourceCell._decorationType))
                throw new InvalidOperationException("An original ownable cell changed at " + id + ".");
            ValidateCompleteTemplateSettings(prepared, original, sourceJson, entry,
                trapDurationTurns, trapReleaseCost);
        }
    }

    private static void ValidateCompleteTemplateSettings(Il2CppReferenceArray<RawCellData> prepared,
        Il2CppReferenceArray<RawCellData> original, string[] sourceJson, TopologyCellPlan entry,
        int trapDurationTurns, uint trapReleaseCost)
    {
        int id = entry.CellId;
        RawCellData template = original[entry.TemplateCellId];
        // Build an independent expected full clone, then normalize only the
        // destination identity/line/edge, new-city suit and approved trap data.
        // Neither prepared nor source cells/arrays are modified by validation.
        CellData expected = CloneNativeRaw(template, sourceJson[entry.TemplateCellId])._cellData;
        if (entry.Kind == TopologyCellKind.RogueTrap)
        {
            if (template._cellData._cellType != CellType.Chance ||
                (id != FivePlayerTopologyRules.FirstTrapCellId && id != FivePlayerTopologyRules.SecondTrapCellId))
                throw new InvalidOperationException("Unapproved native trap conversion at " + id + ".");
            ConfigureTrap(expected, trapDurationTurns, trapReleaseCost);
        }
        expected._cellId = entry.CellId;
        expected._lineId = entry.LineId;
        expected._nextCellsArray = new Il2CppStructArray<int>(new[] { entry.NextCellId });
        if (entry.Appended && entry.Kind == TopologyCellKind.City)
            expected._suitId = (CellSuit)FivePlayerTopologyRules.ResolveNativeSuit(entry, true);
        if (!string.Equals(SerializeCell(prepared[id]._cellData), SerializeCell(expected), StringComparison.Ordinal))
            throw new InvalidOperationException("A cell's complete native template settings changed at " + id + ".");
        if (prepared[id].MetaPosition != template.MetaPosition)
            throw new InvalidOperationException("A cell's native template editor metadata changed at " + id + ".");
    }

    private static void ValidateResortEconomy(Il2CppReferenceArray<RawCellData> cells, int expectedCount)
    {
        CellData reference = null;
        int count = 0;
        for (int id = 0; id < cells.Length; id++)
        {
            CellData data = RequireCellData(cells[id], "economy " + id);
            if (data._cellType != CellType.Resort) continue;
            count++;
            var levels = data._levelSettingsArray;
            if (levels == null || levels.Length != 4 || levels[0].Price <= 0 ||
                !double.IsFinite(levels[0].Rent) || levels[0].Rent <= 0 ||
                !float.IsFinite(data._rentControlBonusMultiplier) || data._rentControlBonusMultiplier <= 0 ||
                !float.IsFinite(data._repurchaseMultiplier) || data._repurchaseMultiplier <= 0)
                throw new InvalidOperationException("Unsupported native resort economic settings at " + id + ".");
            reference ??= data;
            var referenceLevels = reference._levelSettingsArray;
            if (data._rentControlBonusMultiplier != reference._rentControlBonusMultiplier ||
                data._repurchaseMultiplier != reference._repurchaseMultiplier)
                throw new InvalidOperationException("Native resorts have inconsistent multipliers.");
            for (int level = 0; level < levels.Length; level++)
            {
                if (levels[level].Price != referenceLevels[level].Price ||
                    levels[level].Rent != referenceLevels[level].Rent ||
                    levels[level].Price != levels[0].Price ||
                    !AlmostEqual(levels[level].Rent, levels[0].Rent * Math.Pow(data._rentControlBonusMultiplier, level)))
                    throw new InvalidOperationException("Native resort price/rent tiers are inconsistent at " + id + ".");
            }
        }
        if (count != expectedCount) throw new InvalidOperationException("Unexpected native resort count.");
    }

    private static void ValidateNativeResortLevels(Il2CppReferenceArray<RawCellData> cells)
    {
        // Invoke the real native calculation against the complete candidate,
        // rather than appending an invented fifth serialized rent tier.
        Func<Il2CppSystem.Predicate<IRawCellData>, Il2CppReferenceArray<IRawCellData>> filter = predicate =>
        {
            var matching = new List<IRawCellData>();
            for (int id = 0; id < cells.Length; id++)
            {
                var raw = new IRawCellData(cells[id].Pointer);
                if (predicate.Invoke(raw)) matching.Add(raw);
            }
            return new Il2CppReferenceArray<IRawCellData>(matching.ToArray());
        };
        var nativeFilter = DelegateSupport.ConvertDelegate<
            Il2CppSystem.Func<Il2CppSystem.Predicate<IRawCellData>, Il2CppReferenceArray<IRawCellData>>>(filter);
        try
        {
            for (int id = 0; id < cells.Length; id++)
            {
                CellData data = cells[id]._cellData;
                if (data._cellType != CellType.Resort) continue;
                var calculated = data.BusinessTour_ICellData_GetControllBonusLevelSettings(nativeFilter);
                if (calculated == null || calculated.Length != 5)
                    throw new InvalidOperationException("Native resort calculation did not produce five ownership tiers.");
                LevelSetting first = data._levelSettingsArray[0];
                for (int level = 0; level < calculated.Length; level++)
                {
                    ILevelSetting actual = calculated[level];
                    double expectedRent = FivePlayerTopologyRules.ExpectedResortRent(first.Rent, data._rentControlBonusMultiplier, level + 1);
                    // The native method returns boxed LevelSetting values.
                    // Generated interface getters runtime-invoke a value-type
                    // method with the boxed object address; that is not a safe
                    // way to read this struct across the IL2CPP boundary. Read
                    // its verified, generated blittable layout after unboxing,
                    // without changing the native calculation or rent tiers.
                    LevelSetting value = ReadNativeLevelSetting(actual);
                    if (value._price != first._price || !AlmostEqual(value._rent, expectedRent))
                        throw new InvalidOperationException("Native five-resort ownership tier validation failed at cell " + id +
                            ", tier " + (level + 1) + ": price=" + value._price + ", rent=" + value._rent +
                            ", expected price=" + first._price + ", rent=" + expectedRent + ".");
                }
            }
        }
        finally
        {
            GC.KeepAlive(nativeFilter);
            GC.KeepAlive(filter);
        }
    }

    private static LevelSetting ReadNativeLevelSetting(ILevelSetting value)
    {
        if (value == null || value.Pointer == IntPtr.Zero ||
            IL2CPP.il2cpp_object_get_class(value.Pointer) != Il2CppClassPointerStore<LevelSetting>.NativeClassPtr)
            throw new InvalidOperationException("Native resort calculation returned an unsupported level-setting type.");
        IntPtr unboxed = IL2CPP.il2cpp_object_unbox(value.Pointer);
        if (unboxed == IntPtr.Zero)
            throw new InvalidOperationException("Native resort level-setting unboxing failed.");
        return Marshal.PtrToStructure<LevelSetting>(unboxed);
    }

    private static CellData RequireCellData(RawCellData raw, string location)
    {
        if (raw == null || raw.Pointer == IntPtr.Zero || raw._cellData == null || raw._cellData.Pointer == IntPtr.Zero)
            throw new InvalidOperationException("Missing native cell data at " + location + ".");
        return raw._cellData;
    }

    private static void RequireNativeName(CellData data, string location)
    {
        if (string.IsNullOrWhiteSpace(data._cellNameLocalizationKey))
            throw new InvalidOperationException("Missing native localization key at " + location + ".");
    }

    private static string SerializeCell(CellData data)
    {
        string json = JsonParser.Serialize(data, false);
        if (string.IsNullOrWhiteSpace(json) || string.Equals(json, "null", StringComparison.Ordinal))
            throw new InvalidOperationException("Native CellData serialization returned empty data.");
        return json;
    }

    private static bool AlmostEqual(double left, double right) =>
        double.IsFinite(left) && double.IsFinite(right) &&
        Math.Abs(left - right) <= Math.Max(1d, Math.Max(Math.Abs(left), Math.Abs(right))) * 1e-9d;
}
