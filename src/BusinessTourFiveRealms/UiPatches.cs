using System;
using System.Collections.Generic;
using BusinessTour;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace BusinessTourFiveRealms;

internal static class SlotExpander
{
    private static readonly Dictionary<IntPtr, GroupState> Groups = new();
    private static readonly Dictionary<IntPtr, VersusState> VersusViews = new();

    internal static void Ensure(UIPlayerGroup group)
    {
        if (group == null || group._slotParents == null) return;
        if (!ModState.IsSpecialMapActive)
        {
            if (!FifthLobbySlot.HasFifthOccupant()) Restore(group);
            return;
        }
        if (Groups.TryGetValue(group.Pointer, out GroupState saved))
        {
            if (saved.Matches(group._slotParents))
            {
                group._slotParents = saved.Expanded;
                saved.Extra.gameObject.SetActive(true);
                return;
            }
            Groups.Remove(group.Pointer);
        }
        if (group._slotParents.Length != ModState.VanillaPlayers)
        {
            return;
        }

        var original = group._slotParents;
        var expanded = ExpandTransforms(original, "FiveRealmsLobbySlot5", false);
        if (expanded.Length != ModState.FivePlayers) return;
        Groups[group.Pointer] = new GroupState(group, original, expanded);
        group._slotParents = expanded;
        Plugin.ModLog.LogDebug("Expanded UIPlayerGroup to five slots.");
    }

    internal static void Ensure(UIVersus versus)
    {
        if (versus == null || versus._slotParents == null) return;
        if (!ModState.IsSpecialMapActive)
        {
            Restore(versus);
            return;
        }
        if (VersusViews.ContainsKey(versus.Pointer) || versus._slotParents.Length != ModState.VanillaPlayers)
        {
            return;
        }

        var state = new VersusState(versus);
        VersusViews.Add(versus.Pointer, state);
        try
        {
            versus._slotParents = ExpandTransforms(versus._slotParents, "FiveRealmsVersusSlot5", false);
            state.ExpandedSlots = versus._slotParents;
            if (versus._imageVs != null && versus._imageVs.Length > 0 && versus._imageVs.Length < ModState.FivePlayers - 1)
            {
                versus._imageVs = ExpandTransforms(versus._imageVs, "FiveRealmsVersusDivider4", false, ModState.FivePlayers - 1);
                state.ExpandedDividers = versus._imageVs;
            }
        }
        catch
        {
            Restore(versus);
            throw;
        }
        Plugin.ModLog.LogDebug("Expanded UIVersus to five slots.");
    }

    internal static void Reflow(UIVersus versus)
    {
        if (!ModState.IsSpecialMapActive || versus == null ||
            !VersusViews.TryGetValue(versus.Pointer, out VersusState state) ||
            versus._slotParents == null || versus._slotParents.Length != ModState.FivePlayers) return;
        try
        {
            RectTransform canvas = versus.transform.TryCast<RectTransform>();
            if (canvas == null || canvas.rect.width < 100f) return;
            float width = canvas.rect.width;
            float margin = width * 0.04f;
            float spacing = (width - margin * 2f) / ModState.FivePlayers;
            float maxPanelWidth = spacing * 0.80f;
            for (int index = 0; index < ModState.FivePlayers; index++)
            {
                Transform slot = versus._slotParents[index];
                MasterPlayerPanelView panel = versus.GetPlayerPanel(index);
                if (slot == null || panel == null) continue;
                state.FitName(panel);
                Rect frame = PanelBounds(panel, canvas);
                if (frame.width <= 0f) continue;
                float ratio = Mathf.Min(1f, maxPanelWidth / frame.width);
                if (ratio < 0.999f)
                {
                    Vector3 scale = slot.localScale;
                    slot.localScale = new Vector3(scale.x * ratio, scale.y * ratio, scale.z);
                    frame = PanelBounds(panel, canvas);
                }
                float center = canvas.rect.xMin + margin + spacing * (index + 0.5f);
                // Native slots use different screen anchors. Their local x
                // values are not one shared coordinate space, so assigning a
                // interpolated localPosition pushes the cloned fifth offscreen.
                // Translate by the rendered title/avatar bounds in canvas space
                // instead, after every native layout operation.
                slot.position += canvas.TransformVector(new Vector3(center - frame.center.x, 0f, 0f));
                if (!state.LayoutLogged)
                {
                    Rect fitted = PanelBounds(panel, canvas);
                    Plugin.ModLog.LogInfo($"Five-player loading card {index}: canvasWidth={width:F0}, x={fitted.xMin:F0}..{fitted.xMax:F0}, scale={slot.localScale.x:F2}.");
                }
            }
            var dividers = versus._imageVs;
            if (dividers != null)
            {
                for (int index = 0; index < Math.Min(dividers.Length, ModState.FivePlayers - 1); index++)
                {
                    Transform divider = dividers[index];
                    Rect bounds = RectBounds(divider, canvas);
                    if (divider == null || bounds.width <= 0f) continue;
                    float center = canvas.rect.xMin + margin + spacing * (index + 1f);
                    divider.position += canvas.TransformVector(new Vector3(center - bounds.center.x, 0f, 0f));
                }
            }
            state.LayoutLogged = true;
        }
        catch (Exception ex)
        {
            Plugin.ModLog.LogWarning($"Could not balance five-player loading cards: {ex.Message}");
        }
    }

    internal static void ReflowActiveVersus()
    {
        if (!ModState.IsSpecialMapActive || VersusViews.Count == 0) return;
        foreach (var entry in new List<KeyValuePair<IntPtr, VersusState>>(VersusViews))
        {
            VersusState state = entry.Value;
            try
            {
                if (state.View != null && state.View.gameObject.activeInHierarchy) Reflow(state.View);
                else if (state.View == null) VersusViews.Remove(entry.Key);
            }
            catch { VersusViews.Remove(entry.Key); }
        }
    }

    private static Rect PanelBounds(MasterPlayerPanelView panel, RectTransform canvas)
    {
        float left = float.PositiveInfinity, right = float.NegativeInfinity;
        float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
        IncludeRect(panel._titleBack?.transform, canvas, ref left, ref right, ref bottom, ref top);
        IncludeRect(panel._playerName?.transform, canvas, ref left, ref right, ref bottom, ref top);
        IncludeRect(panel._playerAvatarPlaceHolder, canvas, ref left, ref right, ref bottom, ref top);
        IncludeRect(panel._playerDicePlaceHolder, canvas, ref left, ref right, ref bottom, ref top);
        IncludeRect(panel._playerIconPlaceHolder, canvas, ref left, ref right, ref bottom, ref top);
        return float.IsInfinity(left) ? default : new Rect(left, bottom, right - left, top - bottom);
    }

    private static Rect RectBounds(Transform transform, RectTransform canvas)
    {
        float left = float.PositiveInfinity, right = float.NegativeInfinity;
        float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
        IncludeRect(transform, canvas, ref left, ref right, ref bottom, ref top);
        return float.IsInfinity(left) ? default : new Rect(left, bottom, right - left, top - bottom);
    }

    private static void IncludeRect(Transform transform, RectTransform canvas,
        ref float left, ref float right, ref float bottom, ref float top)
    {
        RectTransform rect = transform == null ? null : transform.TryCast<RectTransform>();
        if (rect == null || rect.rect.width <= 0f || rect.rect.height <= 0f) return;
        Rect bounds = rect.rect;
        for (int corner = 0; corner < 4; corner++)
        {
            Vector3 point = canvas.InverseTransformPoint(rect.TransformPoint(new Vector3(
                (corner & 1) == 0 ? bounds.xMin : bounds.xMax,
                (corner & 2) == 0 ? bounds.yMin : bounds.yMax, 0f)));
            left = Mathf.Min(left, point.x);
            right = Mathf.Max(right, point.x);
            bottom = Mathf.Min(bottom, point.y);
            top = Mathf.Max(top, point.y);
        }
    }

    internal static void Restore(UIPlayerGroup group)
    {
        if (group == null || !Groups.TryGetValue(group.Pointer, out GroupState state)) return;
        if (!state.Matches(group._slotParents))
        {
            Groups.Remove(group.Pointer);
            return;
        }
        group._slotParents = state.Original;
        if (state.Extra != null) state.Extra.gameObject.SetActive(false);
    }

    internal static void Restore(UIVersus versus)
    {
        if (versus == null || !VersusViews.TryGetValue(versus.Pointer, out VersusState state)) return;
        VersusViews.Remove(versus.Pointer);
        state.Restore();
    }

    internal static void ApplyMode(bool active)
    {
        if (active) return;
        // The native arrays must be four again, not merely a hidden fifth
        // GameObject: CountSlots and native loops read their array lengths.
        if (!FifthLobbySlot.HasFifthOccupant())
            foreach (GroupState state in new List<GroupState>(Groups.Values))
                try { Restore(state.Group); }
                catch (Exception ex) { Groups.Remove(state.Group.Pointer); Plugin.ModLog.LogDebug(ex.Message); }
        foreach (VersusState state in new List<VersusState>(VersusViews.Values))
            try { Restore(state.View); }
            catch (Exception ex) { VersusViews.Remove(state.View.Pointer); Plugin.ModLog.LogDebug(ex.Message); }
    }

    private sealed class GroupState
    {
        internal readonly UIPlayerGroup Group;
        internal readonly Il2CppReferenceArray<Transform> Original;
        internal readonly Il2CppReferenceArray<Transform> Expanded;
        internal Transform Extra => Expanded[ModState.FivePlayers - 1];

        internal GroupState(UIPlayerGroup group, Il2CppReferenceArray<Transform> original,
            Il2CppReferenceArray<Transform> expanded)
        {
            Group = group;
            Original = original;
            Expanded = expanded;
        }

        internal bool Matches(Il2CppReferenceArray<Transform> current)
        {
            if (current == null || current.Length < Original.Length || Extra == null) return false;
            for (int index = 0; index < Original.Length; index++)
                if (Original[index] == null || current[index]?.Pointer != Original[index].Pointer) return false;
            return true;
        }
    }

    private sealed class VersusState
    {
        internal readonly UIVersus View;
        private readonly Il2CppReferenceArray<Transform> _slots;
        private readonly Il2CppReferenceArray<Transform> _dividers;
        private readonly List<TransformState> _transforms = new();
        private readonly Dictionary<IntPtr, LoadingNameLayout> _names = new();
        internal Il2CppReferenceArray<Transform> ExpandedSlots;
        internal Il2CppReferenceArray<Transform> ExpandedDividers;
        internal bool LayoutLogged;

        internal VersusState(UIVersus view)
        {
            View = view;
            _slots = view._slotParents;
            _dividers = view._imageVs;
            Save(_slots);
            Save(_dividers);
        }

        private void Save(Il2CppReferenceArray<Transform> transforms)
        {
            if (transforms == null) return;
            foreach (Transform transform in transforms)
                if (transform != null) _transforms.Add(new TransformState(transform));
        }

        internal void FitName(MasterPlayerPanelView panel)
        {
            var label = panel._playerName;
            RectTransform title = panel._titleBack?.transform.TryCast<RectTransform>();
            if (label == null || title == null || title.rect.width <= 0f) return;
            if (!_names.TryGetValue(label.Pointer, out LoadingNameLayout layout))
            {
                layout = new LoadingNameLayout(label);
                _names.Add(label.Pointer, layout);
                Plugin.ModLog.LogInfo($"Five-player loading name {label.text}: parent={label.transform.parent?.name}, title={title.name}; fitting inside its own title, native text unchanged.");
            }
            layout.Fit(title, panel._playerAvatarPlaceHolder);
        }

        internal void Restore()
        {
            // A controller may reuse a view object but replace its serialized
            // children. Never write an old generation's arrays into new ones.
            bool sameGeneration = View != null && Matches(View._slotParents, _slots);
            if (sameGeneration)
            {
                View._slotParents = _slots;
                View._imageVs = _dividers;
            }
            if (sameGeneration)
            {
                foreach (LoadingNameLayout layout in _names.Values)
                    try { layout.Restore(); } catch (Exception ex) { Plugin.ModLog.LogDebug(ex.Message); }
                foreach (TransformState state in _transforms)
                    try { state.Restore(); } catch (Exception ex) { Plugin.ModLog.LogDebug(ex.Message); }
            }
            RemoveExtras(ExpandedSlots, _slots);
            RemoveExtras(ExpandedDividers, _dividers);
        }

        private static bool Matches(Il2CppReferenceArray<Transform> current,
            Il2CppReferenceArray<Transform> original)
        {
            if (current == null || original == null || current.Length < original.Length) return false;
            for (int index = 0; index < original.Length; index++)
                if (original[index] == null || current[index]?.Pointer != original[index].Pointer) return false;
            return true;
        }

        private static void RemoveExtras(Il2CppReferenceArray<Transform> expanded,
            Il2CppReferenceArray<Transform> original)
        {
            if (expanded == null) return;
            for (int index = original?.Length ?? 0; index < expanded.Length; index++)
                try
                {
                    if (expanded[index] == null) continue;
                    expanded[index].gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(expanded[index].gameObject);
                }
                catch (Exception ex) { Plugin.ModLog.LogDebug(ex.Message); }
        }
    }

    private sealed class LoadingNameLayout
    {
        private readonly UnityEngine.UI.Text _label;
        private readonly RectTransform _rect;
        private readonly Transform _parent;
        private readonly int _sibling;
        private readonly Vector2 _min, _max, _pivot, _size, _position;
        private readonly Vector3 _scale;
        private readonly Quaternion _rotation;
        private readonly TextAnchor _alignment;
        private readonly bool _bestFit;
        private readonly int _minimum, _maximum;
        private readonly HorizontalWrapMode _horizontal;
        private readonly VerticalWrapMode _vertical;

        internal LoadingNameLayout(UnityEngine.UI.Text label)
        {
            _label=label; _rect=label.rectTransform; _parent=_rect.parent; _sibling=_rect.GetSiblingIndex();
            _min=_rect.anchorMin; _max=_rect.anchorMax; _pivot=_rect.pivot;
            _size=_rect.sizeDelta; _position=_rect.anchoredPosition;
            _scale=_rect.localScale; _rotation=_rect.localRotation;
            _alignment=label.alignment; _bestFit=label.resizeTextForBestFit;
            _minimum=label.resizeTextMinSize; _maximum=label.resizeTextMaxSize;
            _horizontal=label.horizontalOverflow; _vertical=label.verticalOverflow;
        }

        internal void Fit(RectTransform title, Transform avatar)
        {
            if (_rect.parent?.Pointer != title.Pointer) _rect.SetParent(title, false);
            _rect.anchorMin=Vector2.zero; _rect.anchorMax=Vector2.one; _rect.pivot=new Vector2(.5f,.5f);
            // Native title backgrounds extend behind the portrait. Fit the name
            // into the visible remainder, not into the entire coloured strip.
            float leftInset=8f;
            RectTransform portrait=avatar?.TryCast<RectTransform>();
            if (portrait != null)
            {
                var corners=new Il2CppStructArray<Vector3>(4);
                portrait.GetWorldCorners(corners);
                float right=float.NegativeInfinity;
                float top=float.NegativeInfinity, bottom=float.PositiveInfinity;
                foreach (Vector3 corner in corners)
                {
                    Vector3 local=title.InverseTransformPoint(corner);
                    right=Mathf.Max(right,local.x);
                    top=Mathf.Max(top,local.y); bottom=Mathf.Min(bottom,local.y);
                }
                if (top > title.rect.yMin && bottom < title.rect.yMax)
                    leftInset=Mathf.Clamp(right-title.rect.xMin+8f,8f,title.rect.width*.6f);
            }
            _rect.offsetMin=new Vector2(leftInset,2f); _rect.offsetMax=new Vector2(-8f,-2f);
            _rect.localScale=Vector3.one; _rect.localRotation=Quaternion.identity;
            _label.alignment=TextAnchor.MiddleCenter;
            _label.resizeTextForBestFit=true; _label.resizeTextMinSize=10;
            _label.resizeTextMaxSize=Math.Max(10,_label.fontSize);
            _label.horizontalOverflow=HorizontalWrapMode.Wrap; _label.verticalOverflow=VerticalWrapMode.Truncate;
        }

        internal void Restore()
        {
            if (_rect == null || _label == null) return;
            _rect.SetParent(_parent,false); _rect.SetSiblingIndex(_sibling);
            _rect.anchorMin=_min; _rect.anchorMax=_max; _rect.pivot=_pivot;
            _rect.sizeDelta=_size; _rect.anchoredPosition=_position;
            _rect.localScale=_scale; _rect.localRotation=_rotation;
            _label.alignment=_alignment; _label.resizeTextForBestFit=_bestFit;
            _label.resizeTextMinSize=_minimum; _label.resizeTextMaxSize=_maximum;
            _label.horizontalOverflow=_horizontal; _label.verticalOverflow=_vertical;
        }
    }

    private readonly struct TransformState
    {
        private readonly Transform _transform;
        private readonly Vector3 _position;
        private readonly Vector3 _scale;
        private readonly bool _active;
        internal TransformState(Transform transform)
        {
            _transform = transform;
            _position = transform.localPosition;
            _scale = transform.localScale;
            _active = transform.gameObject.activeSelf;
        }
        internal void Restore()
        {
            if (_transform == null) return;
            _transform.localPosition = _position;
            _transform.localScale = _scale;
            _transform.gameObject.SetActive(_active);
        }
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
    private static void Postfix(UIVersus __instance) => SlotExpander.Reflow(__instance);
}

[HarmonyPatch(typeof(UIVersus), nameof(UIVersus.UpdateByPlayerIndexes))]
internal static class UIVersusUpdateIndexesPatch
{
    private static void Prefix(UIVersus __instance) => SlotExpander.Ensure(__instance);
    private static void Postfix(UIVersus __instance) => SlotExpander.Reflow(__instance);
}

// Initialize calls ShowPlayers immediately after creating the view, before
// UpdateByPlayerIndexes. Expanding only at the later method clones an already
// populated fourth panel and never creates the fifth native avatar provider.
// Expand here so ShowPlayers' native array-length loop initializes all five.
[HarmonyPatch(typeof(UIVersusController), nameof(UIVersusController.ShowPlayers))]
internal static class UIVersusShowPlayersPatch
{
    private static void Postfix(UIVersusController __instance) => SlotExpander.Reflow(__instance?._uiVersus);

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
