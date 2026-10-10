using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace BusinessTourFiveRealms;

[BepInPlugin(Guid, Name, Version)]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "vn.businesstour.fiverealms";
    public const string Name = "Business Tour Five Realms";
    public const string Version = "0.2.0";

    internal static ManualLogSource ModLog { get; private set; } = null!;
    internal static ConfigEntry<bool> PreventAfkKick { get; private set; } = null!;
    internal static ConfigEntry<int> BackgroundKeepAliveSeconds { get; private set; } = null!;
    internal static ConfigEntry<int> MaxNetworkResends { get; private set; } = null!;
    internal static ConfigEntry<int> TrapDurationTurns { get; private set; } = null!;
    internal static ConfigEntry<int> TrapReleaseCost { get; private set; } = null!;

    public override void Load()
    {
        ModLog = Log;
#if BT5_DIAGNOSTIC_NOOP
        Log.LogInfo($"{Name} {Version} diagnostic no-op loaded; no game APIs or Harmony patches were touched.");
        return;
#else
        PreventAfkKick = Config.Bind("Connection", "PreventAfkTurnKick", true,
            "Prevents the vanilla kick after three forced skipped turns.");
        BackgroundKeepAliveSeconds = Config.Bind("Connection", "BackgroundKeepAliveSeconds", 86400,
            "How long Photon keeps acknowledgements alive while the game is in the background.");
        MaxNetworkResends = Config.Bind("Connection", "MaxNetworkResends", 10,
            "Photon resend limit before treating the connection as lost.");
        TrapDurationTurns = Config.Bind("Five Realms", "TrapDurationTurns", 2,
            "Turns spent in each generated Rogue Trap.");
        TrapReleaseCost = Config.Bind("Five Realms", "TrapReleaseCost", 100000,
            "Release price for each generated Rogue Trap.");

        ApplyConnectionSettings();

#if BT5_DIAGNOSTIC_CONNECTION_ONLY
        Log.LogInfo($"{Name} {Version} connection-only diagnostic loaded; Harmony patches were not installed.");
        return;
#else

        var harmony = new Harmony(Guid);
        PatchBootstrap.Apply(harmony, Log);
#if !BT5_DIAGNOSTIC_PATCH_GROUP_A
        var updater = AddComponent<FifthLobbySlotUpdater>();
        UnityEngine.Object.DontDestroyOnLoad(updater.gameObject);
#endif
#if BT5_DIAGNOSTIC_FORCE_SPECIAL_MAP
        // Diagnostic builds can exercise the five-player/map-rendering path
        // through Map Editor -> Bot Test without owning Business Tour Club.
        ModState.SetSpecialMap(true, "diagnostic forced mode");
#endif
        Log.LogInfo($"{Name} {Version} loaded. Vanilla maps remain 4-player; {ModState.DisplayName} activates 5-player mode.");
#endif
#endif
    }

    private static void ApplyConnectionSettings()
    {
        try
        {
            Application.runInBackground = true;
            PhotonNetwork.KeepAliveInBackground = Math.Max(60, BackgroundKeepAliveSeconds.Value);
            PhotonNetwork.MaxResendsBeforeDisconnect = Math.Max(5, MaxNetworkResends.Value);
            ModLog.LogInfo($"Background keep-alive={PhotonNetwork.KeepAliveInBackground}s, max resends={PhotonNetwork.MaxResendsBeforeDisconnect}.");
        }
        catch (Exception ex)
        {
            ModLog.LogWarning($"Could not apply Photon defaults yet: {ex.Message}");
        }
    }
}

internal static class PatchBootstrap
{
    private static readonly Type[] PatchTypes =
    {
#if BT5_DIAGNOSTIC_PATCH_GROUP_A
        typeof(AfkHandlerPatch),
        typeof(ConnectionHandlerPatch),
        typeof(MapCollectionDefinitionsPatch),
        typeof(SpecialMapPreviewPatch),
        typeof(SpecialMapScreenshotPatch),
        typeof(SpecialMapSelectionStatusPatch),
        typeof(MapSelectionInitializationAuditPatch),
        typeof(SelectedMapTrySelectPatch),
        typeof(MapSettingsDefinitionUpdatePatch),
        typeof(MapSettingsStringUpdatePatch),
        typeof(MapSettingsDataLoadPatch),
        typeof(JoinedRoomModePatch),
        typeof(RoomPropertiesModePatch),
        typeof(LeftRoomModePatch)
#elif BT5_DIAGNOSTIC_PATCH_GROUP_B1A
        typeof(RoomPlayersSettingsFreeSlotPatch),
        typeof(RoomPlayersSettingsInfosPatch)
#elif BT5_DIAGNOSTIC_PATCH_GROUP_B1B
        typeof(StartupCapacityPatch),
        typeof(RoomManagementCapacityPatch),
        typeof(GameConfigCapacityPatch)
#elif BT5_DIAGNOSTIC_PATCH_STARTUP_CAPACITY
        typeof(StartupCapacityPatch)
#elif BT5_DIAGNOSTIC_PATCH_ROOM_CAPACITY
        typeof(RoomManagementCapacityPatch)
#elif BT5_DIAGNOSTIC_PATCH_GAME_CONFIG_CAPACITY
        typeof(GameConfigCapacityPatch)
#elif BT5_DIAGNOSTIC_PATCH_PLAYERS_PANEL_CAPACITY
        typeof(PlayersPanelCapacityPatch)
#elif BT5_DIAGNOSTIC_PATCH_PLAYERS_PANEL_RUNTIME
        typeof(PlayersPanelRuntimeAuditPatch)
#elif BT5_DIAGNOSTIC_PATCH_NO_BET_LOBBY_CAPACITY
        typeof(NoBetLobbyCapacityPatch)
#elif BT5_DIAGNOSTIC_PATCH_GROUP_B1
        typeof(RoomPlayersSettingsFreeSlotPatch),
        typeof(RoomPlayersSettingsInfosPatch),
        typeof(StartupCapacityPatch),
        typeof(RoomManagementCapacityPatch),
        typeof(GameConfigCapacityPatch)
#elif BT5_DIAGNOSTIC_PATCH_GROUP_B2
        typeof(UIPlayerGroupAddChildPatch),
        typeof(UIVersusUpdateCountPatch),
        typeof(UIVersusUpdateIndexesPatch),
        typeof(CompositeMapPatch),
        typeof(CompositeMapInitializationPatch),
        typeof(PentagonalBoardPatch)
#elif BT5_DIAGNOSTIC_PATCH_GROUP_B
        typeof(RoomPlayersSettingsFreeSlotPatch),
        typeof(RoomPlayersSettingsInfosPatch),
        typeof(StartupCapacityPatch),
        typeof(RoomManagementCapacityPatch),
        typeof(GameConfigCapacityPatch),
        typeof(UIPlayerGroupAddChildPatch),
        typeof(UIVersusUpdateCountPatch),
        typeof(UIVersusUpdateIndexesPatch),
        typeof(CompositeMapPatch),
        typeof(CompositeMapInitializationPatch),
        typeof(PentagonalBoardPatch)
#else
        typeof(AfkHandlerPatch),
        typeof(ConnectionHandlerPatch),
        typeof(MapCollectionDefinitionsPatch),
        typeof(SpecialMapPreviewPatch),
        typeof(SpecialMapScreenshotPatch),
        typeof(SpecialMapSelectionStatusPatch),
        typeof(MapSelectionInitializationAuditPatch),
        typeof(SelectedMapTrySelectPatch),
        typeof(MapSettingsDefinitionUpdatePatch),
        typeof(MapSettingsStringUpdatePatch),
        typeof(MapSettingsDataLoadPatch),
        typeof(JoinedRoomModePatch),
        typeof(RoomPropertiesModePatch),
        typeof(LeftRoomModePatch),
        typeof(RoomPlayersSettingsFreeSlotPatch),
        typeof(FifthLobbyReleasePatch),
        typeof(RoomPlayersSettingsInfosPatch),
        typeof(StartupCapacityPatch),
        typeof(RoomManagementCapacityPatch),
        typeof(UIPlayerGroupAddChildPatch),
        typeof(UIVersusUpdateCountPatch),
        typeof(UIVersusUpdateIndexesPatch),
        typeof(UIVersusShowPlayersPatch),
        typeof(FivePlayerHudSeatsPatch),
        typeof(FivePlayerHudRegistrationPatch),
        typeof(FivePlayerHudLayoutPatch),
        typeof(FivePlayerHudReleasePatch),
        typeof(FivePlayerInventoryLayoutPatch),
        typeof(FivePlayerInventoryBackgroundPatch),
        typeof(FivePlayerPanelFactoryPatch),
        typeof(FivePlayerPauseReleasePatch),
        typeof(FivePlayerReplaySeedPatch),
        typeof(CompositeMapPatch),
        typeof(CompositeMapInitializationPatch),
        typeof(CompositeMapResourceLoadPatch),
        typeof(CompositeMapLocationLoadPatch),
        typeof(CompositeMapVisualConfigPatch),
        typeof(PentagonalBoardPatch),
        typeof(PentagonalBackgroundPatch),
        typeof(PentagonalSpriteSyncPatch)
#endif
    };

    internal static void Apply(Harmony harmony, ManualLogSource log)
    {
        var failures = new List<string>();
        foreach (Type patchType in PatchTypes)
        {
            try
            {
                harmony.CreateClassProcessor(patchType).Patch();
            }
            catch (Exception ex)
            {
                failures.Add(patchType.Name);
                log.LogError($"Patch {patchType.Name} failed: {ex}");
            }
        }

        if (failures.Count > 0)
        {
            log.LogWarning("Some safeguards are unavailable on this game build: " + string.Join(", ", failures));
        }
    }
}
