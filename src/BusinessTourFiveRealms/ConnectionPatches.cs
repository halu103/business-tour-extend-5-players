using System;
using BusinessTour;
using HarmonyLib;
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

