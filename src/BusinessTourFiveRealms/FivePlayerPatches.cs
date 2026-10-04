using System;
using System.Collections.Generic;
using System.Reflection;
using BusinessTour;
using CodeStage.AntiCheat.ObscuredTypes;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace BusinessTourFiveRealms;

[HarmonyPatch(typeof(RoomSettings), MethodType.Constructor, new[] { typeof(int), typeof(IContext) })]
internal static class RoomSettingsCapacityPatch
{
    private static void Prefix(ref int maximumPlayersCount) => maximumPlayersCount = ModState.Capacity(maximumPlayersCount);
}

[HarmonyPatch(typeof(RoomPlayersSettings), MethodType.Constructor, new[] { typeof(int) })]
internal static class RoomPlayersSettingsCapacityPatch
{
    private static void Prefix(ref int maximumPlayersCount) => maximumPlayersCount = ModState.Capacity(maximumPlayersCount);

    private static void Postfix(RoomPlayersSettings __instance) => RoomCapacityRegistry.Register(__instance);
}

[HarmonyPatch]
internal static class StartupCapacityPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(StartupManager), "Initialize", new[] { typeof(int) });

    private static void Prefix(ref int roomMaximumPlayersCount) =>
        roomMaximumPlayersCount = ModState.Capacity(roomMaximumPlayersCount);
}

[HarmonyPatch(typeof(CreateRoomCommand), MethodType.Constructor,
    new[] { typeof(IContext), typeof(IRoomSettings), typeof(IPlayersColorsDistribution), typeof(ITeamDistribution), typeof(byte), typeof(string) })]
internal static class CreateRoomCommandCapacityPatch
{
    private static void Prefix(ref byte maxPlayersInRoom) => maxPlayersInRoom = ModState.Capacity(maxPlayersInRoom);
}

[HarmonyPatch]
internal static class RoomManagementCapacityPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(typeof(RoomManagement), "CreateRoom");

    private static void Prefix(ref byte maxPlayers) => maxPlayers = ModState.Capacity(maxPlayers);
}

[HarmonyPatch(typeof(GameConfig), nameof(GameConfig.MaxPlayers), MethodType.Getter)]
internal static class GameConfigCapacityPatch
{
    private static void Postfix(ref ObscuredInt __result)
    {
        if (ModState.IsSpecialMapActive && (int)__result <= ModState.VanillaPlayers)
        {
            __result = (ObscuredInt)ModState.FivePlayers;
        }
    }
}

[HarmonyPatch(typeof(PlayersPanelController), MethodType.Constructor,
    new[] { typeof(IUIBinder<EventSource>), typeof(Transform), typeof(IContext), typeof(IInRoomPlayersLobbyEvent), typeof(IRoomSettings), typeof(int) })]
internal static class PlayersPanelCapacityPatch
{
    private static void Prefix(ref int maxPLayerPanelsCount) =>
        maxPLayerPanelsCount = ModState.Capacity(maxPLayerPanelsCount);
}

internal static class RoomCapacityRegistry
{
    private static readonly List<RoomPlayersSettings> Instances = new List<RoomPlayersSettings>();

    internal static void Register(RoomPlayersSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        Instances.Add(settings);
        if (ModState.IsSpecialMapActive)
        {
            Resize(settings, ModState.FivePlayers);
        }
    }

    internal static void Apply(bool fivePlayerMode)
    {
        int targetSize = fivePlayerMode ? ModState.FivePlayers : ModState.VanillaPlayers;
        for (int index = Instances.Count - 1; index >= 0; index--)
        {
            RoomPlayersSettings settings = Instances[index];
            try
            {
                if (settings == null || settings.Pointer == IntPtr.Zero)
                {
                    Instances.RemoveAt(index);
                    continue;
                }

                Resize(settings, targetSize);
            }
            catch (Exception ex)
            {
                // A released IL2CPP object may leave a managed wrapper behind.
                Instances.RemoveAt(index);
                Plugin.ModLog.LogDebug($"Dropped a stale room-capacity handle: {ex.Message}");
            }
        }
    }

    private static void Resize(RoomPlayersSettings settings, int targetSize)
    {
        Il2CppReferenceArray<IRoomPlayerInfo> current = settings._roomPlayersInfos;
        if (current == null || current.Length == targetSize)
        {
            return;
        }

        if (targetSize < current.Length)
        {
            for (int index = targetSize; index < current.Length; index++)
            {
                if (current[index] != null)
                {
                    Plugin.ModLog.LogWarning("Kept five internal player slots because slot 5 is occupied.");
                    return;
                }
            }
        }

        var resized = new Il2CppReferenceArray<IRoomPlayerInfo>(targetSize);
        int copyCount = Math.Min(current.Length, targetSize);
        for (int index = 0; index < copyCount; index++)
        {
            resized[index] = current[index];
        }
        settings._roomPlayersInfos = resized;
        Plugin.ModLog.LogDebug($"Internal room slots resized {current.Length} -> {targetSize}.");
    }
}
