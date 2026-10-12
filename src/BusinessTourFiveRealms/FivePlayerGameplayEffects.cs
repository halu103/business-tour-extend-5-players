using System;
using System.Collections.Generic;
using BusinessTour;
using HarmonyLib;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace BusinessTourFiveRealms;

// Two concrete gameplay presentation boundaries. No Canvas-wide scaling,
// shared Show(bool) detours, replacement animation, or economy changes.
internal static class FivePlayerGameplayEffects
{
    private static readonly Dictionary<IntPtr, PopupState> Popups = new();
    private static readonly Dictionary<IntPtr, MovingScaleState> MovingMoney = new();
    private static string _lastPopupError;
    private static bool _movingMoneyLogged;

    internal static void EnlargeMovingMoney(UIGameInventory view, Transform destination, int playerId)
    {
        if (!ModState.IsSpecialMapActive || !FivePlayerHud.OwnsInventory(view)) return;
        try
        {
            if (view._playerMovingMoney == null ||
                !view._playerMovingMoney.TryGetValue(playerId, out IMoneyInventory money)) return;
            Transform root = money?.GameObject?.transform;
            if (root == null) return;
            // Native SetMovingMoneyScale first recomputes this effect's scale
            // from the stationary destination and the two parents' lossyScale.
            // Applying after that reset is non-accumulating even for pooled
            // objects, repeated transfers, or repeated visits to this method.
            // Its Transform argument is the destination, NOT the effect root.
            if (!MovingMoney.TryGetValue(root.Pointer, out MovingScaleState state) || !state.Owns(root))
            {
                state = new MovingScaleState(root);
                MovingMoney[root.Pointer] = state;
            }
            float destinationScale = FivePlayerHud.MoneyDestinationScale(view, destination);
            float multiplier = GameplayHudLayoutRules.MovingMoneyMultiplier(destinationScale);
            state.ApplyNativeBaseline(multiplier);
            if (!_movingMoneyLogged)
            {
                _movingMoneyLogged = true;
                Plugin.ModLog.LogInfo($"Five-player animated money uses native transfer scale with left-rail compensation (destination={destinationScale:F4}, multiplier={multiplier:F2}); moving templates are not compacted.");
            }
        }
        catch (Exception ex) { Plugin.ModLog.LogDebug($"Moving money presentation was already released: {ex.Message}"); }
    }

    internal static void FitSmiles(UIGameSmilesPanel view)
    {
        if (!ModState.IsSpecialMapActive || view == null) return;
        try
        {
            Transform anchor = view._buttonSmilesPositionTransform;
            GameObject group = view._smileGroup;
            if (anchor == null || group == null ||
                !(group.transform.Pointer == anchor.Pointer || group.transform.IsChildOf(anchor))) return;
            RectTransform canvas = FindCanvas(group.transform);
            RectTransform content = group.transform.TryCast<RectTransform>();
            if (canvas == null || content == null || content.Pointer == canvas.Pointer) return;

            if (!Popups.TryGetValue(view.Pointer, out PopupState state) || !state.Owns(view, anchor, content))
            {
                state = new PopupState(view, anchor, content, canvas);
                Popups[view.Pointer] = state;
            }
            // This boundary has just set the native anchor to the selected
            // Emotes button's world position. Keep that fresh native baseline
            // for restoration, not the prior mod-clamped position.
            state.CaptureNativePosition();
            state.Reflow();
        }
        catch (Exception ex)
        {
            if (_lastPopupError == ex.Message) return;
            _lastPopupError = ex.Message;
            Plugin.ModLog.LogWarning($"Could not fit native emote panel: {ex.Message}");
        }
    }

    internal static void ReflowPopups()
    {
        if (!ModState.IsSpecialMapActive || Popups.Count == 0) return;
        foreach (var entry in new List<KeyValuePair<IntPtr, PopupState>>(Popups))
        {
            try
            {
                if (!entry.Value.IsAlive)
                {
                    Popups.Remove(entry.Key);
                    continue;
                }
                if (entry.Value.IsVisible) entry.Value.Reflow();
            }
            catch (Exception ex)
            {
                Popups.Remove(entry.Key);
                Plugin.ModLog.LogDebug($"Emote panel was already released: {ex.Message}");
            }
        }
    }

    internal static void Reset()
    {
        foreach (PopupState popup in Popups.Values)
            try { popup.Restore(); }
            catch (Exception ex) { Plugin.ModLog.LogDebug($"Emote panel was already released: {ex.Message}"); }
        Popups.Clear();
        foreach (MovingScaleState effect in MovingMoney.Values)
            try { effect.Restore(); }
            catch (Exception ex) { Plugin.ModLog.LogDebug($"Money effect was already released: {ex.Message}"); }
        MovingMoney.Clear();
        _lastPopupError = null;
        _movingMoneyLogged = false;
    }

    private static RectTransform FindCanvas(Transform current)
    {
        while (current != null)
        {
            Component component = current.gameObject.GetComponent(Il2CppType.Of<Canvas>());
            if (component != null)
            {
                Canvas canvas = new Canvas(component.Pointer).rootCanvas;
                RectTransform rect = canvas?.transform.TryCast<RectTransform>();
                if (rect != null && rect.rect.width > 100f && rect.rect.height > 100f) return rect;
            }
            current = current.parent;
        }
        return null;
    }

    private sealed class MovingScaleState
    {
        private readonly Transform _root;
        private readonly int _instanceId;
        private Vector3 _nativeScale;
        private Vector3 _assignedScale;

        internal MovingScaleState(Transform root)
        {
            _root = root;
            _instanceId = root.GetInstanceID();
        }

        internal bool Owns(Transform root) => _root != null && root != null &&
            root.Pointer == _root.Pointer && root.GetInstanceID() == _instanceId;

        internal void ApplyNativeBaseline(float multiplier)
        {
            _nativeScale = _root.localScale;
            if (!Finite(_nativeScale.x) || !Finite(_nativeScale.y) || !Finite(_nativeScale.z)) return;
            _assignedScale = new Vector3(_nativeScale.x * multiplier,
                _nativeScale.y * multiplier, _nativeScale.z);
            _root.localScale = _assignedScale;
        }

        internal void Restore()
        {
            if (Owns(_root) && Near(_root.localScale, _assignedScale)) _root.localScale = _nativeScale;
        }
    }

    private sealed class PopupState
    {
        private readonly UIGameSmilesPanel _view;
        private readonly Transform _anchor;
        private readonly RectTransform _content;
        private readonly RectTransform _canvas;
        private readonly int _viewId;
        private readonly int _anchorId;
        private readonly int _contentId;
        private readonly int _canvasId;
        private readonly Transform _parent;
        private Vector3 _nativePosition;
        private Vector3 _assignedPosition;
        private bool _assigned;
        private bool _logged;
        private readonly Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3> _corners =
            new(4);

        internal PopupState(UIGameSmilesPanel view, Transform anchor, RectTransform content, RectTransform canvas)
        {
            _view = view;
            _anchor = anchor;
            _content = content;
            _canvas = canvas;
            _viewId = view.GetInstanceID();
            _anchorId = anchor.GetInstanceID();
            _contentId = content.GetInstanceID();
            _canvasId = canvas.GetInstanceID();
            _parent = anchor.parent;
        }

        internal bool Owns(UIGameSmilesPanel view, Transform anchor, RectTransform content) =>
            view != null && view.Pointer == _view.Pointer && view.GetInstanceID() == _viewId &&
            anchor != null && anchor.Pointer == _anchor.Pointer && anchor.GetInstanceID() == _anchorId &&
            content != null && content.Pointer == _content.Pointer && content.GetInstanceID() == _contentId;

        internal bool IsAlive => _view != null && _anchor != null && _content != null && _canvas != null &&
            _view.GetInstanceID() == _viewId && _anchor.GetInstanceID() == _anchorId &&
            _content.GetInstanceID() == _contentId && _canvas.GetInstanceID() == _canvasId &&
            _view._smileGroup?.transform.Pointer == _content.Pointer &&
            _view._buttonSmilesPositionTransform?.Pointer == _anchor.Pointer &&
            _anchor.parent?.Pointer == _parent?.Pointer;
        internal bool IsVisible => IsAlive && _content.gameObject.activeInHierarchy;

        internal void CaptureNativePosition()
        {
            _nativePosition = _anchor.localPosition;
            _assigned = false;
        }

        internal void Reflow()
        {
            if (!IsAlive) return;
            // If another native transition moved this anchor, do not restore
            // an old mod position or fight its animation. A new explicit native
            // SetSmilesButtonPosition call can rebind this pooled window.
            if (_assigned && !Near(_anchor.localPosition, _assignedPosition)) return;
            _content.GetWorldCorners(_corners);
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            for (int index = 0; index < 4; index++)
            {
                Vector3 point = _canvas.InverseTransformPoint(_corners[index]);
                left = Mathf.Min(left, point.x); right = Mathf.Max(right, point.x);
                bottom = Mathf.Min(bottom, point.y); top = Mathf.Max(top, point.y);
            }
            Rect viewport = _canvas.rect;
            if (!GameplayHudLayoutRules.TryPopupTranslation(left, right, bottom, top,
                viewport.xMin, viewport.xMax, viewport.yMin, viewport.yMax, out float x, out float y)) return;
            if (Mathf.Abs(x) > .001f || Mathf.Abs(y) > .001f)
            {
                _anchor.position += _canvas.TransformVector(new Vector3(x, y, 0f));
                _assignedPosition = _anchor.localPosition;
                _assigned = true;
            }
            if (!_logged)
            {
                _logged = true;
                Plugin.ModLog.LogInfo($"Five-player emote panel fitted inside its canvas, clear of vertical toolbar (shift={x:F0},{y:F0}, size={right-left:F0}x{top-bottom:F0}).");
            }
        }

        internal void Restore()
        {
            if (IsAlive && _assigned && Near(_anchor.localPosition, _assignedPosition))
                _anchor.localPosition = _nativePosition;
        }
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static bool Near(Vector3 left, Vector3 right) => (left - right).sqrMagnitude < .0001f;
}

[HarmonyPatch(typeof(UIGameInventory), nameof(UIGameInventory.SetMovingMoneyScale))]
internal static class FivePlayerMovingMoneyScalePatch
{
    private static void Postfix(UIGameInventory __instance, Transform __0, int __1) =>
        FivePlayerGameplayEffects.EnlargeMovingMoney(__instance, __0, __1);
}

[HarmonyPatch(typeof(UIGameSmilesPanel), nameof(UIGameSmilesPanel.SetSmilesButtonPosition))]
internal static class FivePlayerSmilesPositionPatch
{
    private static void Postfix(UIGameSmilesPanel __instance) => FivePlayerGameplayEffects.FitSmiles(__instance);
}
