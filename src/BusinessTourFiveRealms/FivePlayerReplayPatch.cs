using BusinessTour;
using HarmonyLib;

namespace BusinessTourFiveRealms;

// The vanilla seed format encodes PlayerCount and TurnOrder in 0..4.
// Five-player matches must not advertise a seed that the vanilla reader
// cannot decode. Keep real match data untouched; disable only seed display.
[HarmonyPatch(typeof(MatchReplayManager), nameof(MatchReplayManager.CanShowMatchSeed))]
internal static class FivePlayerReplaySeedPatch
{
    private static bool Prefix(ref bool __result)
    {
        if (!ModState.IsSpecialMapActive) return true;
        __result = false;
        return false;
    }
}
