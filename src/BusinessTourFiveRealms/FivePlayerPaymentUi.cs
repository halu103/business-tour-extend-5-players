using System;
using System.Collections.Generic;
using BusinessTour;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;

namespace BusinessTourFiveRealms;

// Observes an existing GameView rather than scanning Unity objects or detouring
// shared generic window-animation methods. All buttons and money remain native.
internal static class FivePlayerPaymentUi
{
    private static UIManager<LogicAtomWindowGameplay> _manager;
    private static readonly Dictionary<IntPtr, DialogState> Dialogs = new();
    private static readonly Dictionary<IntPtr, MoneyLabelState> MoneyLabels = new();
    private static readonly Dictionary<IntPtr, int> NativeFontOverrides = new();
    private static string _lastError;

    private static readonly LogicAtomWindowGameplay[] PurchaseWindows =
    {
        LogicAtomWindowGameplay.WndGamePlayBuyCity,
        LogicAtomWindowGameplay.WndGamePlayBuyAliceRoom,
        LogicAtomWindowGameplay.WndBuyHotel,
        LogicAtomWindowGameplay.WndBuyResort,
        LogicAtomWindowGameplay.WndBuyout,
        LogicAtomWindowGameplay.WndIslandEscape,
        LogicAtomWindowGameplay.WndRogueTrapEscape,
        LogicAtomWindowGameplay.WndWonderlandTravelEscape
    };

    // Wire into the existing GameView.OnLocationLoaded postfix. No additional
    // Harmony target or generic Context.Get service lookup is needed.
    internal static void Observe(GameView gameView)
    {
        if (!ModState.IsSpecialMapActive) { Reset(); return; }
        try
        {
            var manager = gameView?.UIManager?.TryCast<UIManager<LogicAtomWindowGameplay>>();
            if (manager == null) return;
            if (_manager != null && _manager.Pointer != manager.Pointer) Reset();
            _manager = manager;
        }
        catch (Exception ex) { Report(ex); }
    }

    // Call from the existing throttled updater (about twice per second).
    internal static void Tick()
    {
        if (!ModState.IsSpecialMapActive) { Reset(); return; }
        if (_manager == null) return;
        try
        {
            var opened = _manager.OpenedWindows;
            if (opened == null) return;
            foreach (LogicAtomWindowGameplay key in PurchaseWindows)
            {
                if (!opened.TryGetValue(key, out var window)) continue;
                var controller = window?.TryCast<Window<LogicAtomWindowGameplay>>();
                var view = controller?._baseView?.TryCast<UIWindowCommonView>();
                if (view == null || !view.gameObject.activeInHierarchy || !IsPurchaseView(view)) continue;
                RectTransform rect = view._windowRectTransform;
                if (rect == null) continue;
                bool tracked = Dialogs.TryGetValue(rect.Pointer, out DialogState state);
                if (tracked && !state.Matches(view, rect))
                {
                    // A pooled rect can acquire another native view owner. Do
                    // not re-use the previous owner's saved layout or wrapper.
                    if (!state.Restore()) continue;
                    Dialogs.Remove(rect.Pointer);
                    tracked = false;
                }
                if (!tracked)
                {
                    RectTransform canvas = FindCanvas(rect);
                    if (!IsDialogRect(rect, canvas)) continue;
                    state = new DialogState(view, rect, canvas);
                    try { state.Attach(); }
                    catch
                    {
                        // Failed reparenting must not delete a wrapper that
                        // still contains the native dialog. Retain it to retry.
                        if (!state.Restore()) Dialogs[rect.Pointer] = state;
                        throw;
                    }
                    Dialogs.Add(rect.Pointer, state);
                    Plugin.ModLog.LogInfo($"Five-player purchase dialog enlargement prepared for {view.GetIl2CppType().Name}; native actions unchanged.");
                }
                if (state.Matches(view, rect)) state.Reflow();
            }

            ObserveDebtLabels(opened);

            var stale = new List<IntPtr>();
            foreach (var pair in Dialogs)
                if (!pair.Value.IsAlive) stale.Add(pair.Key);
            foreach (IntPtr key in stale)
            {
                if (Dialogs[key].Restore()) Dialogs.Remove(key);
            }
            stale.Clear();
            foreach (var pair in MoneyLabels)
                if (!pair.Value.IsAlive) stale.Add(pair.Key);
            foreach (IntPtr key in stale)
                if (MoneyLabels[key].Restore()) MoneyLabels.Remove(key);
        }
        catch (Exception ex) { Report(ex); }
    }

    internal static void Reset()
    {
        var restored = new List<IntPtr>();
        foreach (var pair in Dialogs)
            if (pair.Value.Restore()) restored.Add(pair.Key);
        foreach (IntPtr key in restored) Dialogs.Remove(key);
        restored.Clear();
        foreach (var pair in MoneyLabels)
            if (pair.Value.Restore()) restored.Add(pair.Key);
        foreach (IntPtr key in restored) MoneyLabels.Remove(key);
        NativeFontOverrides.Clear();
        _manager = null;
        if (Dialogs.Count == 0 && MoneyLabels.Count == 0) _lastError = null;
    }

    private static void ObserveDebtLabels(Il2CppSystem.Collections.Generic.Dictionary<LogicAtomWindowGameplay,
        IWindow<LogicAtomWindowGameplay, WindowResult, EventSource>> opened)
    {
        // The native rent/debt view is not a common animated dialog. Its
        // _indicatorsContainer also parents property-sale price markers, so
        // leave that container and every selection/button transform alone.
        if (!opened.TryGetValue(LogicAtomWindowGameplay.WndSellCell, out var window)) return;
        var controller = window?.TryCast<Window<LogicAtomWindowGameplay>>();
        var view = controller?._baseView?.TryCast<UISellCellView>();
        if (view == null || !view.gameObject.activeInHierarchy) return;
        TrackMoneyLine(view, view._neededMoney, true);
        TrackMoneyLine(view, view._currentMoney, false);
    }

    private static void TrackMoneyLine(UISellCellView view, UISellCellLine line, bool needed)
    {
        if (line == null) return;
        TrackMoneyLabel(view, line, line._text, needed, false);
        TrackMoneyLabel(view, line, line._amount, needed, true);
    }

    private static void TrackMoneyLabel(UISellCellView view, UISellCellLine line, TMP_Text label,
        bool needed, bool amount)
    {
        if (label == null || !label.gameObject.activeInHierarchy ||
            !label.transform.IsChildOf(line.transform)) return;
        RectTransform rect = label.rectTransform;
        if (rect == null || rect.rect.width <= 1f || rect.rect.height <= 1f || FindCanvas(rect) == null) return;
        int instance = label.GetInstanceID();
        if (NativeFontOverrides.TryGetValue(label.Pointer, out int overrideInstance) &&
            overrideInstance == instance) return;
        if (MoneyLabels.TryGetValue(label.Pointer, out MoneyLabelState state))
        {
            if (!state.Matches(view, line, label, needed, amount))
            {
                if (!state.Restore()) return;
                MoneyLabels.Remove(label.Pointer);
            }
            else
            {
                if (!state.HasOwnedFontSettings)
                {
                    // Another native component deliberately changed limits;
                    // stop owning this generation instead of fighting it.
                    if (state.Restore())
                    {
                        MoneyLabels.Remove(label.Pointer);
                        NativeFontOverrides[label.Pointer] = instance;
                    }
                }
                return;
            }
        }
        if (!PaymentUiLayoutRules.TryFontRange(label.fontSize, label.fontSizeMin,
            out float minimum, out float maximum)) return;
        state = new MoneyLabelState(view, line, label, needed, amount, minimum, maximum);
        try { state.Apply(); }
        catch
        {
            if (!state.Restore()) MoneyLabels[label.Pointer] = state;
            throw;
        }
        MoneyLabels.Add(label.Pointer, state);
        Plugin.ModLog.LogInfo($"Five-player rent/debt {(needed ? "needed" : "balance")} {(amount ? "amount" : "caption")} font maximum enlarged by 15%; native label bounds and actions unchanged.");
    }

    private static void Report(Exception ex)
    {
        string message = ex.GetType().Name + ": " + ex.Message;
        if (string.Equals(message, _lastError, StringComparison.Ordinal)) return;
        _lastError = message;
        Plugin.ModLog.LogWarning("Purchase dialog enlargement was skipped safely: " + message);
    }

    private static bool IsPurchaseView(UIWindowCommonView view) =>
        view.TryCast<UIBuyCityView>() != null || view.TryCast<UIWindowBuyHotel>() != null ||
        view.TryCast<UIWindowBuyResort>() != null || view.TryCast<UIWindowBuyout>() != null ||
        view.TryCast<UIWindowIslandEscape>() != null;

    private static RectTransform FindCanvas(Transform transform)
    {
        for (Transform node = transform; node != null; node = node.parent)
        {
            Canvas canvas = node.GetComponent<Canvas>();
            if (canvas != null)
                return (canvas.rootCanvas ?? canvas).transform.TryCast<RectTransform>();
        }
        return null;
    }

    private static bool IsDialogRect(RectTransform rect, RectTransform canvas)
    {
        if (canvas == null || rect.parent == null || rect.Pointer == canvas.Pointer ||
            rect.GetComponent<Canvas>() != null) return false;
        Rect dialog = rect.rect, screen = canvas.rect;
        // Full-screen selection backgrounds and the root Canvas are not dialogs.
        return dialog.width > 1f && dialog.height > 1f && screen.width > 1f && screen.height > 1f &&
            dialog.width < screen.width * .9f && dialog.height < screen.height * .9f;
    }

    private sealed class DialogState
    {
        private readonly UIWindowCommonView _view;
        private readonly RectTransform _rect, _canvas;
        private readonly Transform _parent;
        private readonly int _sibling;
        private readonly int _viewInstance, _rectInstance, _canvasInstance, _parentInstance;
        private readonly Vector2 _minimum, _maximum, _pivot, _size;
        private readonly Vector3 _position;
        private readonly Quaternion _rotation;
        private GameObject _holder;
        private RectTransform _wrapper;
        private int _wrapperInstance;

        internal DialogState(UIWindowCommonView view, RectTransform rect, RectTransform canvas)
        {
            _view = view; _rect = rect; _canvas = canvas;
            _parent = rect.parent; _sibling = rect.GetSiblingIndex();
            _viewInstance = view.GetInstanceID(); _rectInstance = rect.GetInstanceID();
            _canvasInstance = canvas.GetInstanceID(); _parentInstance = _parent.GetInstanceID();
            _minimum = rect.anchorMin; _maximum = rect.anchorMax;
            _pivot = rect.pivot; _size = rect.sizeDelta;
            _position = rect.anchoredPosition3D; _rotation = rect.localRotation;
        }

        private bool OwnsRectGeneration => _rect != null && _rect.GetInstanceID() == _rectInstance;
        private bool OwnsWrapperGeneration => _wrapper != null && _wrapper.GetInstanceID() == _wrapperInstance;

        internal bool IsAlive => OwnsRectGeneration && OwnsWrapperGeneration &&
            _canvas != null && _canvas.GetInstanceID() == _canvasInstance &&
            _view != null && _view.GetInstanceID() == _viewInstance &&
            _view._windowRectTransform?.Pointer == _rect.Pointer &&
            _rect.parent?.Pointer == _wrapper.Pointer;

        internal bool Matches(UIWindowCommonView view, RectTransform rect) =>
            IsAlive && view.Pointer == _view.Pointer && view.GetInstanceID() == _viewInstance &&
            rect.Pointer == _rect.Pointer && rect.GetInstanceID() == _rectInstance;

        internal void Attach()
        {
            _holder = new GameObject("FiveRealmsPurchaseDialogScale");
            _holder.layer = _rect.gameObject.layer;
            _wrapper = _holder.AddComponent<RectTransform>();
            _wrapperInstance = _wrapper.GetInstanceID();
            _wrapper.SetParent(_parent, false);
            _wrapper.SetSiblingIndex(_sibling);
            _wrapper.anchorMin = _minimum; _wrapper.anchorMax = _maximum;
            _wrapper.pivot = _pivot; _wrapper.sizeDelta = _size;
            _wrapper.anchoredPosition3D = _position;
            _wrapper.localRotation = _rotation; _wrapper.localScale = Vector3.one;

            _rect.SetParent(_wrapper, false);
            _rect.anchorMin = Vector2.zero; _rect.anchorMax = Vector2.one;
            _rect.pivot = _pivot; _rect.sizeDelta = Vector2.zero;
            _rect.anchoredPosition3D = Vector3.zero; _rect.localRotation = Quaternion.identity;
            // Do not change the child's localScale: native Show/Close tweens it.
            Reflow();
        }

        internal void Reflow()
        {
            if (!IsAlive) return;
            var corners = new Il2CppStructArray<Vector3>(4);
            _wrapper.GetWorldCorners(corners);
            Vector3 origin = _canvas.InverseTransformPoint(_wrapper.position);
            float current = _wrapper.localScale.x;
            if (current <= 0f || !float.IsFinite(current)) return;
            float left = float.PositiveInfinity, right = float.NegativeInfinity;
            float bottom = float.PositiveInfinity, top = float.NegativeInfinity;
            foreach (Vector3 corner in corners)
            {
                Vector3 point = _canvas.InverseTransformPoint(corner);
                // Obtain nominal unscaled bounds without writing transforms
                // while the native open/close animation is running.
                point = origin + (point - origin) / current;
                left = Mathf.Min(left, point.x); right = Mathf.Max(right, point.x);
                bottom = Mathf.Min(bottom, point.y); top = Mathf.Max(top, point.y);
            }
            Rect viewport = _canvas.rect;
            float scale = PaymentUiLayoutRules.FitScale(left, right, bottom, top, origin.x, origin.y,
                viewport.xMin, viewport.xMax, viewport.yMin, viewport.yMax);
            _wrapper.localScale = new Vector3(scale, scale, 1f);
        }

        internal bool Restore()
        {
            try
            {
                // A different native owner may have reparented a pooled view;
                // never move it back using stale ownership information.
                if (OwnsRectGeneration && OwnsWrapperGeneration && _rect.parent?.Pointer == _wrapper.Pointer)
                {
                    if (_parent == null || _parent.GetInstanceID() != _parentInstance)
                    {
                        Report(new InvalidOperationException("The original dialog parent is unavailable; its native child is retained for safe cleanup."));
                        return false;
                    }
                    _rect.SetParent(_parent, false); _rect.SetSiblingIndex(_sibling);
                    _rect.anchorMin = _minimum; _rect.anchorMax = _maximum;
                    _rect.pivot = _pivot; _rect.sizeDelta = _size;
                    _rect.anchoredPosition3D = _position; _rect.localRotation = _rotation;
                    // localScale stays at its current native animation value,
                    // exactly the property that this helper never modified.
                }
                // Destroying a Transform also destroys its children. Never
                // hide/delete this wrapper while any native child remains,
                // even when restoration failed or its generation has changed.
                Transform holderTransform = _holder == null ? null : _holder.transform;
                if (holderTransform != null && holderTransform.childCount != 0)
                {
                    Report(new InvalidOperationException("A purchase wrapper still owns native children; cleanup will be retried without hiding them."));
                    return false;
                }
                if (_holder != null)
                {
                    _holder.SetActive(false);
                    UnityEngine.Object.Destroy(_holder);
                }
                _holder = null; _wrapper = null;
                return true;
            }
            catch (Exception ex) { Report(ex); return false; }
        }
    }

    private sealed class MoneyLabelState
    {
        private readonly UISellCellView _view;
        private readonly UISellCellLine _line;
        private readonly TMP_Text _label;
        private readonly bool _needed, _amount;
        private readonly int _viewInstance, _lineInstance, _labelInstance;
        private readonly float _fontSize, _nativeMinimum, _nativeMaximum;
        private readonly bool _autoSizing;
        private readonly float _minimum, _maximum;
        private bool _touched, _initialized;

        internal MoneyLabelState(UISellCellView view, UISellCellLine line, TMP_Text label,
            bool needed, bool amount, float minimum, float maximum)
        {
            _view = view; _line = line; _label = label;
            _needed = needed; _amount = amount;
            _viewInstance = view.GetInstanceID(); _lineInstance = line.GetInstanceID();
            _labelInstance = label.GetInstanceID();
            _fontSize = label.fontSize; _nativeMinimum = label.fontSizeMin;
            _nativeMaximum = label.fontSizeMax; _autoSizing = label.enableAutoSizing;
            _minimum = minimum; _maximum = maximum;
        }

        private bool OwnsLabelGeneration => _label != null && _label.GetInstanceID() == _labelInstance;

        internal bool IsAlive => OwnsLabelGeneration && _view != null && _line != null &&
            _view.GetInstanceID() == _viewInstance && _line.GetInstanceID() == _lineInstance &&
            (_needed ? _view._neededMoney : _view._currentMoney)?.Pointer == _line.Pointer &&
            (_amount ? _line._amount : _line._text)?.Pointer == _label.Pointer &&
            _label.transform.IsChildOf(_line.transform);

        internal bool Matches(UISellCellView view, UISellCellLine line, TMP_Text label,
            bool needed, bool amount) => IsAlive && view.Pointer == _view.Pointer &&
            line.Pointer == _line.Pointer && label.Pointer == _label.Pointer &&
            needed == _needed && amount == _amount;

        internal bool HasOwnedFontSettings => OwnsLabelGeneration && _label.enableAutoSizing &&
            Near(_label.fontSizeMin, _minimum) && Near(_label.fontSizeMax, _maximum);

        internal void Apply()
        {
            _touched = true;
            _label.fontSizeMin = _minimum;
            _label.fontSizeMax = _maximum;
            _label.fontSize = _maximum;
            _label.enableAutoSizing = true;
            _initialized = true;
        }

        internal bool Restore()
        {
            try
            {
                if (OwnsLabelGeneration && _touched && (!_initialized || HasOwnedFontSettings))
                {
                    _label.enableAutoSizing = false;
                    _label.fontSizeMin = _nativeMinimum; _label.fontSizeMax = _nativeMaximum;
                    _label.fontSize = _fontSize; _label.enableAutoSizing = _autoSizing;
                }
                _touched = false;
                return true;
            }
            catch (Exception ex) { Report(ex); return false; }
        }

        private static bool Near(float a, float b) =>
            Mathf.Abs(a - b) <= .00001f * Mathf.Max(1f, Mathf.Abs(a), Mathf.Abs(b));
    }
}
