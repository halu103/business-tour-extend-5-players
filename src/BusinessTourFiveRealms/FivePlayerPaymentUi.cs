using System;
using System.Collections.Generic;
using BusinessTour;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using TMPro;
using UnityEngine;

namespace BusinessTourFiveRealms;

// Observes an existing GameView rather than scanning Unity objects or detouring
// shared generic window-animation methods. Purchase windows retain their exact
// native hierarchy and geometry; only bounded rent/debt typography is changed.
internal static class FivePlayerPaymentUi
{
    private static UIManager<LogicAtomWindowGameplay> _manager;
    private static readonly Dictionary<IntPtr, MoneyLabelState> MoneyLabels = new();
    private static readonly Dictionary<IntPtr, int> NativeFontOverrides = new();
    private static string _lastError;

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
            // Native Show/Close owns _windowRectTransform and its scale tween.
            // Inserting an extra RectTransform and rewriting the native anchors
            // is not a safe presentation-only resize: it changes the parent
            // space used by the pooled view and its descendants. Do not touch
            // purchase/repurchase windows, including their close/confirm buttons.
            ObserveDebtLabels(opened);

            var stale = new List<IntPtr>();
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
        foreach (var pair in MoneyLabels)
            if (pair.Value.Restore()) restored.Add(pair.Key);
        foreach (IntPtr key in restored) MoneyLabels.Remove(key);
        NativeFontOverrides.Clear();
        _manager = null;
        if (MoneyLabels.Count == 0) _lastError = null;
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
        Plugin.ModLog.LogWarning("Rent/debt font enlargement was skipped safely: " + message);
    }

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
