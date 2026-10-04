using System;
using BusinessTour;

namespace BusinessTourFiveRealms;

internal static class ModState
{
    internal const int VanillaPlayers = 4;
    internal const int FivePlayers = 5;
    internal const string MapId = "BT5_FIVE_REALMS_V1";
    internal const string DisplayName = "Five Realms — 5 Players";
    internal const string MapPathTag = "::BT5P1";

    private static bool _specialMapActive;

    internal static BaseMapDefinition Template { get; set; }
    internal static MapDefinition SpecialDefinition { get; set; }
    internal static bool IsSpecialMapActive => _specialMapActive;

    internal static bool IsSpecialId(string value) =>
        string.Equals(value, MapId, StringComparison.Ordinal);

    internal static bool IsTaggedMapPath(string value) =>
        !string.IsNullOrEmpty(value) && value.EndsWith(MapPathTag, StringComparison.Ordinal);

    internal static string AddMapTag(string path) =>
        IsTaggedMapPath(path) ? path : path + MapPathTag;

    internal static string RemoveMapTag(string path) =>
        IsTaggedMapPath(path) ? path.Substring(0, path.Length - MapPathTag.Length) : path;

    internal static bool IsSpecialDefinition(BaseMapDefinition definition) =>
        definition != null && (IsSpecialId(definition.ItemId) || IsTaggedMapPath(definition.Map));

    internal static void SetSpecialMap(bool active, string source)
    {
        if (_specialMapActive == active)
        {
            return;
        }

        _specialMapActive = active;
        RoomCapacityRegistry.Apply(active);
        Plugin.ModLog.LogInfo($"Five-player map mode {(active ? "enabled" : "disabled")} ({source}).");
    }

    internal static int Capacity(int vanillaValue) =>
        IsSpecialMapActive && vanillaValue <= VanillaPlayers ? FivePlayers : vanillaValue;

    internal static byte Capacity(byte vanillaValue) =>
        IsSpecialMapActive && vanillaValue <= VanillaPlayers ? (byte)FivePlayers : vanillaValue;
}
