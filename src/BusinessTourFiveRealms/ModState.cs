using System;
using BusinessTour;
using ExitGames.Client.Photon;
using Photon.Pun;

namespace BusinessTourFiveRealms;

internal static class ModState
{
    internal const int VanillaPlayers = 4;
    internal const int FivePlayers = 5;
    internal const string MapId = "BT5_FIVE_REALMS_V1";
    internal const string DisplayName = "Five Realms — 5 Players";
    internal const string MapPathTag = "::BT5P1";
    internal const string RoomModeProperty = "BT5_MODE";

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

    internal static void PublishRoomMode(bool active)
    {
        try
        {
            Photon.Realtime.Room room = PhotonNetwork.CurrentRoom;
            if (room == null)
            {
                return;
            }

            // The private room is created while the vanilla map is still
            // selected, so the CreateRoom hook legitimately sees four slots.
            // Selecting Five Realms happens afterwards; update the live Photon
            // room as well as our internal arrays so a real fifth peer may join.
            if (PhotonNetwork.IsMasterClient)
            {
                room.MaxPlayers = active ? FivePlayers : VanillaPlayers;
            }

            var properties = new Hashtable();
            properties[(Il2CppSystem.String)RoomModeProperty] =
                (Il2CppSystem.String)(active ? MapId : string.Empty);
            if (!room.SetCustomProperties(properties))
            {
                Plugin.ModLog.LogWarning("Photon rejected the Five Realms room marker update.");
            }
        }
        catch (Exception ex)
        {
            Plugin.ModLog.LogWarning($"Could not synchronize the Five Realms room marker: {ex.Message}");
        }
    }

    internal static bool ApplyCurrentRoomMode(string source, bool clearWhenMissing)
    {
        try
        {
            Photon.Realtime.Room room = PhotonNetwork.CurrentRoom;
            if (room == null)
            {
                if (clearWhenMissing)
                {
                    SetSpecialMap(false, source + " (no room)");
                }
                return false;
            }

            return ApplyRoomMode(room.CustomProperties, source, clearWhenMissing);
        }
        catch (Exception ex)
        {
            Plugin.ModLog.LogWarning($"Could not read the Five Realms room marker: {ex.Message}");
            return false;
        }
    }

    internal static bool ApplyRoomMode(Hashtable properties, string source, bool clearWhenMissing = false)
    {
        if (properties != null)
        {
            Il2CppSystem.Object value = properties[(Il2CppSystem.String)RoomModeProperty];
            if (value != null)
            {
                SetSpecialMap(string.Equals(value.ToString(), MapId, StringComparison.Ordinal), source);
                return true;
            }
        }

        if (clearWhenMissing)
        {
            SetSpecialMap(false, source + " (marker absent)");
        }
        return false;
    }

    internal static void SetSpecialMap(bool active, string source)
    {
#if BT5_DIAGNOSTIC_FORCE_SPECIAL_MAP
        active = true;
#endif
        if (_specialMapActive == active)
        {
            return;
        }

        _specialMapActive = active;
        FifthPlayerColors.Apply(active);
        RoomCapacityRegistry.Apply(active);
        if (active)
        {
            FivePlayerHud.EnsureSeatMapping();
        }
        else
        {
            PentagonalSpriteGeometry.Reset();
            CompositeMapPatch.Reset();
        }
        Plugin.ModLog.LogInfo($"Five-player map mode {(active ? "enabled" : "disabled")} ({source}).");
    }

    internal static int Capacity(int vanillaValue) =>
        IsSpecialMapActive && vanillaValue <= VanillaPlayers ? FivePlayers : vanillaValue;

    internal static byte Capacity(byte vanillaValue) =>
        IsSpecialMapActive && vanillaValue <= VanillaPlayers ? (byte)FivePlayers : vanillaValue;
}
