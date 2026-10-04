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
    public const string Version = "0.1.0";

    internal static ManualLogSource ModLog { get; private set; } = null!;
    internal static ConfigEntry<bool> PreventAfkKick { get; private set; } = null!;
    internal static ConfigEntry<int> BackgroundKeepAliveSeconds { get; private set; } = null!;
    internal static ConfigEntry<int> MaxNetworkResends { get; private set; } = null!;
    internal static ConfigEntry<int> TrapDurationTurns { get; private set; } = null!;
    internal static ConfigEntry<int> TrapReleaseCost { get; private set; } = null!;

    public override void Load()
    {
        ModLog = Log;
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

        var harmony = new Harmony(Guid);
        PatchBootstrap.Apply(harmony, Log);
        Log.LogInfo($"{Name} {Version} loaded. Vanilla maps remain 4-player; {ModState.DisplayName} activates 5-player mode.");
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
        typeof(AfkHandlerPatch),
        typeof(ConnectionHandlerPatch),
        typeof(MapCollectionDefinitionsPatch),
        typeof(MapSelectionContextCapturePatch),
        typeof(SpecialMapDefinitionTypePatch),
        typeof(SpecialMapPreviewPatch),
        typeof(SpecialMapScreenshotPatch),
        typeof(SpecialMapSelectionStatusPatch),
        typeof(MapSelectionInitializationAuditPatch),
        typeof(SelectedMapTrySelectPatch),
        typeof(MapSettingsDefinitionUpdatePatch),
        typeof(MapSettingsDefinitionConstructorPatch),
        typeof(MapSettingsStringConstructorPatch),
        typeof(MapSettingsStringUpdatePatch),
        typeof(MapSettingsDataLoadPatch),
        typeof(RoomSettingsCapacityPatch),
        typeof(RoomPlayersSettingsCapacityPatch),
        typeof(StartupCapacityPatch),
        typeof(CreateRoomCommandCapacityPatch),
        typeof(RoomManagementCapacityPatch),
        typeof(GameConfigCapacityPatch),
        typeof(PlayersPanelCapacityPatch),
        typeof(UIPlayerGroupCountPatch),
        typeof(UIPlayerGroupAddChildPatch),
        typeof(UIVersusCountPatch),
        typeof(UIVersusUpdateCountPatch),
        typeof(UIVersusUpdateIndexesPatch),
        typeof(CompositeMapPatch),
        typeof(PentagonalBoardPatch)
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
