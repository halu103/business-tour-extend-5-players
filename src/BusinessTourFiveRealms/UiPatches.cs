using System;
using BusinessTour;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace BusinessTourFiveRealms;

internal static class SlotExpander
{
    internal static void Ensure(UIPlayerGroup group)
    {
        if (!ModState.IsSpecialMapActive || group == null || group._slotParents == null || group._slotParents.Length >= ModState.FivePlayers)
        {
            return;
        }

        group._slotParents = ExpandTransforms(group._slotParents, "FiveRealmsLobbySlot5", false);
        Plugin.ModLog.LogDebug("Expanded UIPlayerGroup to five slots.");
    }

    internal static void Ensure(UIVersus versus)
    {
        if (!ModState.IsSpecialMapActive || versus == null || versus._slotParents == null || versus._slotParents.Length >= ModState.FivePlayers)
        {
            return;
        }

        versus._slotParents = ExpandTransforms(versus._slotParents, "FiveRealmsVersusSlot5", true);
        if (versus._imageVs != null && versus._imageVs.Length > 0 && versus._imageVs.Length < ModState.FivePlayers - 1)
        {
            versus._imageVs = ExpandTransforms(versus._imageVs, "FiveRealmsVersusDivider4", true, ModState.FivePlayers - 1);
        }
        Plugin.ModLog.LogDebug("Expanded UIVersus to five slots.");
    }

    private static Il2CppReferenceArray<Transform> ExpandTransforms(
        Il2CppReferenceArray<Transform> current,
        string cloneName,
        bool rebalance,
        int targetCount = ModState.FivePlayers)
    {
        if (current.Length == 0 || current[current.Length - 1] == null)
        {
            return current;
        }

        var expanded = new Il2CppReferenceArray<Transform>(targetCount);
        int copyCount = Math.Min(current.Length, targetCount);
        for (int index = 0; index < copyCount; index++)
        {
            expanded[index] = current[index];
        }

        Transform source = current[current.Length - 1];
        for (int index = copyCount; index < targetCount; index++)
        {
            GameObject clone = UnityEngine.Object.Instantiate(source.gameObject);
            clone.name = cloneName + "_" + index;
            clone.transform.SetParent(source.parent, false);
            expanded[index] = clone.transform;
        }

        if (rebalance && targetCount > 1)
        {
            float left = current[0].localPosition.x;
            float right = current[current.Length - 1].localPosition.x;
            for (int index = 0; index < targetCount; index++)
            {
                Transform slot = expanded[index];
                Vector3 position = slot.localPosition;
                position.x = Mathf.Lerp(left, right, index / (float)(targetCount - 1));
                slot.localPosition = position;
                slot.localScale = slot.localScale * 0.84f;
            }
        }

        return expanded;
    }
}

// Do not patch CountSlots: Unity IL2CPP shares its native body with Spine
// MeshGenerator.VertexCount and U2D VertexBuffer.bufferCount on build 25392206.
// Expanding here would reinterpret those unrelated objects as lobby groups.
[HarmonyPatch(typeof(UIPlayerGroup), nameof(UIPlayerGroup.AddChild))]
internal static class UIPlayerGroupAddChildPatch
{
    private static void Prefix(UIPlayerGroup __instance) => SlotExpander.Ensure(__instance);
}

// Its CountSlots getter likewise shares a body with GoldPass.PlayerProgress
// and UIWindowSelectOtherPlayer.HelpersCount. Expand at lifecycle methods.
[HarmonyPatch(typeof(UIVersus), nameof(UIVersus.UpdateByCountPlayers))]
internal static class UIVersusUpdateCountPatch
{
    private static void Prefix(UIVersus __instance) => SlotExpander.Ensure(__instance);
}

[HarmonyPatch(typeof(UIVersus), nameof(UIVersus.UpdateByPlayerIndexes))]
internal static class UIVersusUpdateIndexesPatch
{
    private static void Prefix(UIVersus __instance) => SlotExpander.Ensure(__instance);
}

// Initialize calls ShowPlayers immediately after creating the view, before
// UpdateByPlayerIndexes. Expanding only at the later method clones an already
// populated fourth panel and never creates the fifth native avatar provider.
// Expand here so ShowPlayers' native array-length loop initializes all five.
[HarmonyPatch(typeof(UIVersusController), nameof(UIVersusController.ShowPlayers))]
internal static class UIVersusShowPlayersPatch
{
    private static void Prefix(UIVersusController __instance)
    {
        try
        {
            if (__instance != null) SlotExpander.Ensure(__instance._uiVersus);
        }
        catch (Exception ex)
        {
            Plugin.ModLog.LogWarning($"Could not prepare five-player loading panels: {ex.Message}");
        }
    }
}
