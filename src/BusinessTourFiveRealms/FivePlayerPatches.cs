using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BusinessTour;
using CodeStage.AntiCheat.ObscuredTypes;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace BusinessTourFiveRealms;

[HarmonyPatch(typeof(RoomSettings), MethodType.Constructor, new[] { typeof(int), typeof(IContext), typeof(bool) })]
internal static class RoomSettingsCapacityPatch
{
    private static void Prefix(ref int __0) => __0 = ModState.Capacity(__0);
}

[HarmonyPatch(typeof(RoomPlayersSettings), MethodType.Constructor, new[] { typeof(int) })]
internal static class RoomPlayersSettingsCapacityPatch
{
    private static void Prefix(ref int maximumPlayersCount) => maximumPlayersCount = ModState.Capacity(maximumPlayersCount);

    private static void Postfix(RoomPlayersSettings __instance) => RoomCapacityRegistry.Register(__instance);
}

// Constructor detours are deliberately not installed on this Unity 6 IL2CPP
// build. Il2CppInterop cannot use its constructor backend here and falls back
// to a trampoline that has already caused unrelated UI corruption. Register
// and resize the same object at its first normal method/property access.
[HarmonyPatch(typeof(RoomPlayersSettings), nameof(RoomPlayersSettings.GetFreeSlotIndex))]
internal static class RoomPlayersSettingsFreeSlotPatch
{
    private static void Prefix(RoomPlayersSettings __instance) =>
        RoomCapacityRegistry.Register(__instance);
}

[HarmonyPatch(typeof(RoomPlayersSettings), nameof(RoomPlayersSettings.RoomPlayerInfos), MethodType.Getter)]
internal static class RoomPlayersSettingsInfosPatch
{
    private static void Prefix(RoomPlayersSettings __instance) =>
        RoomCapacityRegistry.Register(__instance);
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
    new[] { typeof(IContext), typeof(IRoomSettings), typeof(byte), typeof(string) })]
internal static class CreateRoomCommandCapacityPatch
{
    private static void Prefix(ref byte __2) => __2 = ModState.Capacity(__2);
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
    private static void Prefix(ref int maxPLayerPanelsCount)
    {
        int original = maxPLayerPanelsCount;
        maxPLayerPanelsCount = ModState.Capacity(maxPLayerPanelsCount);
        if (maxPLayerPanelsCount != original)
        {
            Plugin.ModLog.LogInfo($"Creating private-lobby UI with {maxPLayerPanelsCount} player panels.");
        }
    }
}

[HarmonyPatch(typeof(PlayersPanelController), nameof(PlayersPanelController.CreatePlayerPanels))]
internal static class PlayersPanelRuntimeAuditPatch
{
    private static void Prefix(PlayersPanelController __instance) =>
        Plugin.ModLog.LogInfo($"CreatePlayerPanels prefix: {Describe(__instance)}");

    private static void Postfix(PlayersPanelController __instance) =>
        Plugin.ModLog.LogInfo($"CreatePlayerPanels postfix: {Describe(__instance)}");

    private static string Describe(PlayersPanelController controller)
    {
        try
        {
            int panels = controller?._playerInfoPanels?.Length ?? -1;
            int configs = controller?._avatarsConfigs?.Count ?? -1;
            int providers = controller?._avatarProviders?.Count ?? -1;
            int viewPanels = controller?._view?._panels?.Count ?? -1;
            return $"panels={panels}, configs={configs}, providers={providers}, viewPanels={viewPanels}";
        }
        catch (Exception ex)
        {
            return $"unavailable ({ex.GetType().Name}: {ex.Message})";
        }
    }
}

[HarmonyPatch(typeof(NoBetLobbyController), nameof(NoBetLobbyController.UpdatePlayers))]
internal static class NoBetLobbyCapacityPatch
{
    private static string _lastSnapshot;

    private static void Postfix(NoBetLobbyController __instance)
    {
        if (!ModState.IsSpecialMapActive)
        {
            return;
        }

        try
        {
            int panels = __instance?._playerPanelViews?.Count ?? -1;
            int attributes = __instance?._playerAttributes?.Length ?? -1;
            int controllers = __instance?._slotContolers?.Length ?? -1;
            int avatars = __instance?._avatarsConfigs?.Count ?? -1;
            int groupSlots = __instance?._playerGroup?._slotParents?.Length ?? -1;
            string snapshot = $"panels={panels}, attributes={attributes}, controllers={controllers}, avatars={avatars}, groupSlots={groupSlots}";
            if (!string.Equals(snapshot, _lastSnapshot, StringComparison.Ordinal))
            {
                _lastSnapshot = snapshot;
                Plugin.ModLog.LogInfo($"NoBet lobby audit after UpdatePlayers: slots={NoBetLobbyController.SLOTS_COUNT}, maxGuests={NoBetLobbyController.MAX_GUESTS_COUNT}, {snapshot}.");
                Plugin.ModLog.LogInfo(DescribeHierarchy(__instance));
            }
        }
        catch (Exception ex)
        {
            Plugin.ModLog.LogWarning($"Could not audit the five-slot private lobby: {ex.Message}");
        }
    }

    private static string DescribeHierarchy(NoBetLobbyController controller)
    {
        var result = new StringBuilder("NoBet lobby hierarchy:");
        if (controller?._playerGroup?._slotParents != null)
        {
            for (int index = 0; index < controller._playerGroup._slotParents.Length; index++)
            {
                Transform slot = controller._playerGroup._slotParents[index];
                result.Append($" slot[{index}]={Describe(slot)} children={slot?.childCount ?? -1};");
            }
        }

        if (controller?._playerPanelViews != null)
        {
            for (int index = 0; index < controller._playerPanelViews.Count; index++)
            {
                MasterPlayerPanelView panel = controller._playerPanelViews[index];
                result.Append($" panel[{index}]={Describe(panel?.transform)};");
            }
        }

        return result.ToString();
    }

    private static string Describe(Transform transform)
    {
        if (transform == null)
        {
            return "null";
        }

        string parent = transform.parent == null ? "<root>" : transform.parent.name;
        Vector3 position = transform.localPosition;
        return $"{transform.name}@{parent} pos=({position.x:F1},{position.y:F1},{position.z:F1}) active={transform.gameObject.activeSelf}";
    }
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

        bool known = false;
        for (int index = Instances.Count - 1; index >= 0; index--)
        {
            RoomPlayersSettings existing = Instances[index];
            if (existing == null || existing.Pointer == IntPtr.Zero)
            {
                Instances.RemoveAt(index);
                continue;
            }

            if (existing.Pointer == settings.Pointer)
            {
                known = true;
            }
        }

        if (!known)
        {
            Instances.Add(settings);
        }
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
