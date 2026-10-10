using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace BusinessTourFiveRealms;

// Unwired, pure planning policy. This does not create IL2CPP objects, change
// live map resources, assign a new native suit value, or invent cell prices.
// An adapter must clone each complete native RawCellData identified below.
internal static class FivePlayerTopologyRules
{
    internal const int OriginalCellCount = 32;
    internal const int ExpandedCellCount = 40;
    internal const int CellsPerSide = 8;
    internal const int ExpandedSideCount = 5;
    internal const string ExpansionCityGroup = "five-realms.expansion-cities";

    // Inspected ClassicMap, not an assumed symmetric Monopoly board:
    // first side has six cities; the other sides have five/five/four.
    private static readonly TopologyCellKind[] ClassicKinds =
    {
        TopologyCellKind.Start, TopologyCellKind.City, TopologyCellKind.City, TopologyCellKind.City,
        TopologyCellKind.Resort, TopologyCellKind.City, TopologyCellKind.City, TopologyCellKind.City,
        TopologyCellKind.Island, TopologyCellKind.City, TopologyCellKind.City, TopologyCellKind.City,
        TopologyCellKind.Chance, TopologyCellKind.City, TopologyCellKind.Resort, TopologyCellKind.City,
        TopologyCellKind.Tournament, TopologyCellKind.City, TopologyCellKind.Resort, TopologyCellKind.City,
        TopologyCellKind.Chance, TopologyCellKind.City, TopologyCellKind.City, TopologyCellKind.City,
        TopologyCellKind.TravelNextTurn, TopologyCellKind.Resort, TopologyCellKind.City, TopologyCellKind.City,
        TopologyCellKind.Chance, TopologyCellKind.City, TopologyCellKind.Tax, TopologyCellKind.City
    };

    internal static TopologyExpansionPlan CreateClassicCandidate(IReadOnlyList<TopologySourceCell> source)
    {
        ValidateClassicSource(source);
        var cells = new TopologyCellPlan[ExpandedCellCount];
        for (int id = 0; id < OriginalCellCount; id++)
        {
            TopologySourceCell original = source[id];
            // The only necessary old-graph change is the former closing edge
            // 31->0 becoming 31->32. All old cell identity/economy stays native.
            cells[id] = new TopologyCellPlan(id, id, id / CellsPerSide, original.LineId,
                id + 1, original.Kind, original.NativeSuitId, original.DecorationId,
                false, null);
        }

        // Clone side two's complete native templates, but replace the new
        // corner with Chance and reserve two NEW cities as traps. This avoids
        // removing any original city or duplicating Start/lap rewards.
        int[] templates = { 12, 9, 10, 11, 12, 13, 14, 15 };
        TopologyCellKind[] kinds =
        {
            TopologyCellKind.Chance, TopologyCellKind.City, TopologyCellKind.City, TopologyCellKind.City,
            TopologyCellKind.Chance, TopologyCellKind.RogueTrap, TopologyCellKind.Resort, TopologyCellKind.RogueTrap
        };
        for (int offset = 0; offset < CellsPerSide; offset++)
        {
            int id = OriginalCellCount + offset;
            TopologySourceCell template = source[templates[offset]];
            TopologyCellKind kind = kinds[offset];
            bool city = kind == TopologyCellKind.City;
            bool trap = kind == TopologyCellKind.RogueTrap;
            // Null is deliberate: reusing Suit30 would enlarge an old city
            // monopoly, while an unreviewed numeric Suit90 could miss skins.
            int? nativeSuit = city ? null : trap ? 1 : template.NativeSuitId;
            int decoration = trap ? 1 : template.DecorationId;
            cells[id] = new TopologyCellPlan(id, templates[offset], ExpandedSideCount - 1,
                ExpandedSideCount, (id + 1) % ExpandedCellCount, kind, nativeSuit,
                decoration, true, city ? ExpansionCityGroup : null);
        }
        return new TopologyExpansionPlan(cells);
    }

    internal static int[] RegionStarts() => new[] { 0, 8, 16, 24, 32, 40 };

    private static void ValidateClassicSource(IReadOnlyList<TopologySourceCell> source)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        if (source.Count != OriginalCellCount)
            throw new ArgumentException("The candidate requires the inspected, unmodified 32-cell ClassicMap.", nameof(source));
        for (int id = 0; id < source.Count; id++)
        {
            TopologySourceCell cell = source[id];
            if (cell == null || cell.CellId != id || cell.LineId != id / CellsPerSide + 1 ||
                cell.Kind != ClassicKinds[id] || cell.NextCellIds.Count != 1 ||
                cell.NextCellIds[0] != (id + 1) % OriginalCellCount)
                throw new ArgumentException("Unsupported native topology or modified ClassicMap cell at index " + id + ".", nameof(source));

            int expectedSuit;
            int expectedDecoration;
            if (cell.Kind == TopologyCellKind.City)
            {
                expectedSuit = (id / 4 + 1) * 10;
                expectedDecoration = id / 4 + 2;
            }
            else if (cell.Kind == TopologyCellKind.Resort)
            {
                expectedSuit = cell.LineId + 1;
                expectedDecoration = cell.LineId + 9;
            }
            else
            {
                expectedSuit = 1;
                expectedDecoration = 1;
            }
            if (cell.NativeSuitId != expectedSuit || cell.DecorationId != expectedDecoration)
                throw new ArgumentException("Unsupported native suit/decoration metadata at index " + id + ".", nameof(source));
        }
    }
}

internal enum TopologyCellKind
{
    City = 0,
    Start = 1,
    Resort = 2,
    Island = 3,
    Chance = 4,
    Tournament = 5,
    TravelNextTurn = 6,
    Tax = 7,
    RogueTrap = 8
}

// An inventory record, not a substitute for complete serialized native data.
internal sealed class TopologySourceCell
{
    internal TopologySourceCell(int cellId, TopologyCellKind kind, int lineId,
        int nativeSuitId, int decorationId, IReadOnlyList<int> nextCellIds)
    {
        if (nextCellIds == null) throw new ArgumentNullException(nameof(nextCellIds));
        CellId = cellId;
        Kind = kind;
        LineId = lineId;
        NativeSuitId = nativeSuitId;
        DecorationId = decorationId;
        var copiedIds = new int[nextCellIds.Count];
        for (int index = 0; index < copiedIds.Length; index++) copiedIds[index] = nextCellIds[index];
        NextCellIds = Array.AsReadOnly(copiedIds);
    }

    internal int CellId { get; }
    internal TopologyCellKind Kind { get; }
    internal int LineId { get; }
    internal int NativeSuitId { get; }
    internal int DecorationId { get; }
    internal IReadOnlyList<int> NextCellIds { get; }
}

internal sealed class TopologyCellPlan
{
    internal TopologyCellPlan(int cellId, int templateCellId, int sideIndex, int lineId,
        int nextCellId, TopologyCellKind kind, int? nativeSuitId, int decorationId,
        bool appended, string expansionCityGroup)
    {
        CellId = cellId;
        TemplateCellId = templateCellId;
        SideIndex = sideIndex;
        LineId = lineId;
        NextCellId = nextCellId;
        Kind = kind;
        NativeSuitId = nativeSuitId;
        DecorationId = decorationId;
        Appended = appended;
        ExpansionCityGroup = expansionCityGroup;
    }

    internal int CellId { get; }
    internal int TemplateCellId { get; }
    internal int SideIndex { get; }
    internal int LineId { get; }
    internal int NextCellId { get; }
    internal TopologyCellKind Kind { get; }
    internal int? NativeSuitId { get; }
    internal int DecorationId { get; }
    internal bool Appended { get; }
    internal string ExpansionCityGroup { get; }
}

internal sealed class TopologyExpansionPlan
{
    internal TopologyExpansionPlan(TopologyCellPlan[] cells)
    {
        Cells = Array.AsReadOnly((TopologyCellPlan[])cells.Clone());
        RequiredNativeWork = Array.AsReadOnly(new[]
        {
            "Clone complete loaded RawCellData and all economy/reward settings; inventory records are insufficient.",
            "Resolve the new independent three-city group without changing the eight original groups.",
            "Audit and add map-specific asset/skin lookup for the new city suit; do not change its gameplay grouping.",
            "Audit native 40-cell placement, fixed cell-count assumptions, travel/card references and synchronization.",
            "Verify five-resort rent tiers, purchase/info UI and all monopoly/line victory checks.",
            "Approve or redesign two traps on the added side; this is a candidate, not final random placement."
        });
    }

    internal ReadOnlyCollection<TopologyCellPlan> Cells { get; }
    internal ReadOnlyCollection<string> RequiredNativeWork { get; }
    internal bool IsRuntimeReady => false;

    internal void RequireRuntimeReady() => throw new InvalidOperationException(
        "This is an unwired topology candidate. Native city-suit/asset compatibility and complete native configuration cloning are unresolved.");
}
