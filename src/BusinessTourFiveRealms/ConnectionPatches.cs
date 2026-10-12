using System;
using System.Collections.Generic;
using BusinessTour;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Photon.Realtime;

namespace BusinessTourFiveRealms;

[HarmonyPatch(typeof(AfkHandler), nameof(AfkHandler.Handle))]
internal static class AfkHandlerPatch
{
    private static void Prefix(AfkHandler __instance, bool forced)
    {
        if (forced && Plugin.PreventAfkKick.Value)
        {
            // Vanilla kicks at three forced skipped turns. Reset before the native
            // increment so its other timeout side effects still run normally.
            __instance._turnSkipCount = 0;
        }
    }
}

// OnDecisionMade contains a native inlined copy of AfkHandler.Handle in
// 2.21. Resetting only Handle's entry point cannot protect that path. Guard
// this handler's own AFK callback, not the room's general exit/disconnect.
internal static class AfkCallbackProtection
{
    private static readonly Dictionary<IntPtr, Guard> Guards = new();

    internal static void Attach(AfkHandler handler)
    {
        if (handler == null || handler.Pointer == IntPtr.Zero || Guards.ContainsKey(handler.Pointer)) return;
        Il2CppSystem.Action original = handler.OnKick;
        if (original == null || original.Pointer == IntPtr.Zero) return;
        try
        {
            var guard = new Guard(handler, original);
            handler.OnKick = guard.NativeCallback;
            Guards.Add(handler.Pointer, guard);
        }
        catch (Exception ex)
        {
            Plugin.ModLog.LogWarning("Could not guard the local AFK callback: " + ex.Message);
        }
    }

    internal static void Release(AfkHandler handler)
    {
        if (handler == null || !Guards.TryGetValue(handler.Pointer, out Guard guard)) return;
        try
        {
            // Restore only the callback we still own. A native replacement is
            // neither called nor overwritten during cleanup.
            if (handler.OnKick?.Pointer == guard.NativeCallback.Pointer)
                handler.OnKick = guard.Original;
            Guards.Remove(handler.Pointer);
        }
        catch (Exception ex)
        {
            Plugin.ModLog.LogWarning("Could not release the local AFK callback: " + ex.Message);
        }
    }

    private sealed class Guard
    {
        internal readonly AfkHandler Handler;
        internal readonly Il2CppSystem.Action Original;
        internal readonly Action ManagedCallback;
        internal readonly Il2CppSystem.Action NativeCallback;
        private bool _reported;

        internal Guard(AfkHandler handler, Il2CppSystem.Action original)
        {
            Handler = handler;
            Original = original;
            ManagedCallback = Invoke;
            NativeCallback = DelegateSupport.ConvertDelegate<Il2CppSystem.Action>(ManagedCallback);
        }

        private void Invoke()
        {
            if (Plugin.PreventAfkKick.Value && Handler._turnSkipCount >= 3)
            {
                Handler._turnSkipCount = 0;
                if (!_reported)
                {
                    Plugin.ModLog.LogInfo("Suppressed the local three-missed-roll AFK callback; native forced-turn decisions remain unchanged.");
                    _reported = true;
                }
                return;
            }
            Original.Invoke();
        }
    }
}

// Current token 0x06002D84 / RVA 0x549BF0: a unique virtual factory body.
// PlayerBehaviourFactory invokes it through the IFactory interface, so this
// also covers the handler later used by OnDecisionMade's inlined logic.
[HarmonyPatch(typeof(AfkHandlerFactory),
    nameof(AfkHandlerFactory.Method_Private_Virtual_Final_New_AfkHandler_IPlayer_0),
    new[] { typeof(IPlayer) })]
internal static class AfkCallbackFactoryPatch
{
    private static void Postfix(AfkHandler __result) => AfkCallbackProtection.Attach(__result);
}

[HarmonyPatch(typeof(PlayerBehaviour), nameof(PlayerBehaviour.Release))]
internal static class AfkCallbackReleasePatch
{
    private static void Prefix(PlayerBehaviour __instance) => AfkCallbackProtection.Release(__instance?._afkHandler);
}

[HarmonyPatch(typeof(ConnectionHandler), "Awake")]
internal static class ConnectionHandlerPatch
{
    private static void Postfix(ConnectionHandler __instance)
    {
        try
        {
            __instance.DisconnectAfterKeepAlive = false;
            __instance.KeepAliveInBackground = Math.Max(60, Plugin.BackgroundKeepAliveSeconds.Value);
        }
        catch (Exception ex)
        {
            Plugin.ModLog.LogWarning($"Could not extend the active Photon connection handler: {ex.Message}");
        }
    }
}

