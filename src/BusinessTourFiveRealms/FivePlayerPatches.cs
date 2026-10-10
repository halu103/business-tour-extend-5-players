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
    private static readonly List<CapacityState> Instances = new();

    internal static void Register(RoomPlayersSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        CapacityState state = null;
        for (int index = Instances.Count - 1; index >= 0; index--)
        {
            RoomPlayersSettings existing = Instances[index].Settings;
            if (existing == null || existing.Pointer == IntPtr.Zero)
            {
                Instances.RemoveAt(index);
                continue;
            }

            if (existing.Pointer == settings.Pointer)
            {
                state = Instances[index];
            }
        }

        if (state == null)
        {
            if (settings._roomPlayersInfos == null) return;
            state = new CapacityState(settings, ModState.IsSpecialMapActive);
            Instances.Add(state);
        }
        state.Apply(ModState.IsSpecialMapActive);
    }

    internal static void Apply(bool fivePlayerMode)
    {
        for (int index = Instances.Count - 1; index >= 0; index--)
        {
            CapacityState state = Instances[index];
            RoomPlayersSettings settings = state.Settings;
            try
            {
                if (settings == null || settings.Pointer == IntPtr.Zero)
                {
                    Instances.RemoveAt(index);
                    continue;
                }

                state.Apply(fivePlayerMode);
            }
            catch (Exception ex)
            {
                // A released IL2CPP object may leave a managed wrapper behind.
                Instances.RemoveAt(index);
                Plugin.ModLog.LogDebug($"Dropped a stale room-capacity handle: {ex.Message}");
            }
        }
    }

    private sealed class CapacityState
    {
        internal readonly RoomPlayersSettings Settings;
        private Il2CppReferenceArray<IRoomPlayerInfo> _original;
        private Il2CppReferenceArray<IRoomPlayerInfo> _expanded;

        internal CapacityState(RoomPlayersSettings settings, bool active)
        {
            Settings = settings;
            CaptureOriginal(settings._roomPlayersInfos, active);
        }

        private void CaptureOriginal(Il2CppReferenceArray<IRoomPlayerInfo> current, bool active)
        {
            // Startup can legitimately construct a new special-map room with
            // five slots before this first access. Its ordinary-map baseline
            // is four. Existing 1v1/two-slot objects retain their exact two-slot
            // array instead of being restored to a blanket global four.
            int originalSize = active && current.Length == ModState.FivePlayers
                ? ModState.VanillaPlayers : current.Length;
            _original = current.Length == originalSize ? current :
                new Il2CppReferenceArray<IRoomPlayerInfo>(originalSize);
            if (_original.Pointer != current.Pointer) Copy(current, _original);
            _expanded = active && current.Length == ModState.FivePlayers ? current : null;
        }

        internal void Apply(bool active)
        {
            var current = Settings._roomPlayersInfos;
            if (current == null) return;
            if (current.Pointer != _original.Pointer && current.Pointer != _expanded?.Pointer)
            {
                // A native reinitialization may replace the backing array on a
                // reused settings object. Capture that new generation, never
                // copy an older room's array back into it.
                CaptureOriginal(current, active);
            }
            int targetSize = active && _original.Length <= ModState.VanillaPlayers
                ? ModState.FivePlayers : _original.Length;
            if (current.Length == targetSize &&
                (active || current.Pointer == _original.Pointer)) return;
            if (targetSize < current.Length)
            {
                for (int index = targetSize; index < current.Length; index++)
                {
                    if (current[index] == null) continue;
                    Plugin.ModLog.LogWarning($"Kept {current.Length} internal player slots because slot {index + 1} is occupied.");
                    return;
                }
            }

            var resized = active ? new Il2CppReferenceArray<IRoomPlayerInfo>(targetSize) : _original;
            Copy(current, resized);
            Settings._roomPlayersInfos = resized;
            if (active) _expanded = resized;
            Plugin.ModLog.LogDebug($"Internal room slots resized {current.Length} -> {targetSize} (original={_original.Length}).");
        }

        private static void Copy(Il2CppReferenceArray<IRoomPlayerInfo> source,
            Il2CppReferenceArray<IRoomPlayerInfo> target)
        {
            int copyCount = Math.Min(source.Length, target.Length);
            for (int index = 0; index < copyCount; index++) target[index] = source[index];
        }
    }
}
