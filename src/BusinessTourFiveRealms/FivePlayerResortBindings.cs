using System;
using System.Collections.Generic;
using BusinessTour;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace BusinessTourFiveRealms;

// The current Fantasy prefab has native Text children, but its legacy caption
// fields are null. FillResortView translates _buyButtonText with a dictionary
// keyed by Text; SetRent later dereferences _rentText. Bind only those missing
// references, never replace controls, prices, localization or window geometry.
internal static class FivePlayerResortBindings
{
    private static readonly Dictionary<IntPtr, BindingState> States = new();
    private static string _lastError;

    internal static void Prepare(UIWindowBuyResortController controller)
    {
        if (!ModState.IsSpecialMapActive) { Reset(); return; }
        try
        {
            UIWindowBuyResort view = controller?._buyresort;
            if (view == null) return;
            if (States.TryGetValue(view.Pointer, out BindingState previous))
            {
                if (previous.Matches(view)) return;
                if (!previous.Restore()) return;
                States.Remove(view.Pointer);
            }
            bool needsBuy = view._buyButtonText == null;
            bool needsRent = view._rentText == null;
            if (!needsBuy && !needsRent) return; // Classic already has both.
            Transform buyParent = view._buyButton?.transform;
            Transform rentParent = view._rentTextPlaceHolder;
            Text buy = needsBuy ? FindNativeText(view, buyParent, "Text") : null;
            Text rent = needsRent ? FindNativeText(view, rentParent, "Value") : null;
            if ((needsBuy && buy == null) || (needsRent && rent == null))
                throw new InvalidOperationException("Expected native resort caption children are unavailable.");
            var state = new BindingState(view, buy, rent);
            States.Add(view.Pointer, state); // Track before any assignment.
            state.Apply();
            Plugin.ModLog.LogInfo("Five-player resort: bound missing buy/rent Text references to existing native children; native actions, localization and prices unchanged.");
        }
        catch (Exception ex)
        {
            string message = ex.GetType().Name + ": " + ex.Message;
            if (message != _lastError)
            {
                _lastError = message;
                Plugin.ModLog.LogWarning("Native resort caption binding was skipped: " + message);
            }
        }
    }

    private static Text FindNativeText(UIWindowBuyResort view, Transform parent, string childName)
    {
        if (parent == null || !parent.IsChildOf(view.transform)) return null;
        Transform child = parent.Find(childName);
        if (child == null || child.parent?.Pointer != parent.Pointer) return null;
        return child.GetComponent<Text>();
    }

    internal static void Reset()
    {
        var restored = new List<IntPtr>();
        foreach (var pair in States)
            if (pair.Value.Restore()) restored.Add(pair.Key);
        foreach (IntPtr key in restored) States.Remove(key);
        if (States.Count == 0) _lastError = null;
    }

    private sealed class BindingState
    {
        private readonly UIWindowBuyResort _view;
        private readonly int _viewId;
        private readonly Text _buy, _rent;
        private readonly int _buyId, _rentId;
        private bool _buyOwned, _rentOwned;

        internal BindingState(UIWindowBuyResort view, Text buy, Text rent)
        {
            _view = view; _viewId = view.GetInstanceID(); _buy = buy; _rent = rent;
            _buyId = buy == null ? 0 : buy.GetInstanceID();
            _rentId = rent == null ? 0 : rent.GetInstanceID();
        }

        private bool OwnsView => _view != null && _view.GetInstanceID() == _viewId;

        internal bool Matches(UIWindowBuyResort view) => OwnsView && view.Pointer == _view.Pointer &&
            view._buyButtonText != null && view._rentText != null &&
            (_buy == null || (_buyOwned && _buy != null && _buy.GetInstanceID() == _buyId &&
                _view._buyButtonText?.Pointer == _buy.Pointer)) &&
            (_rent == null || (_rentOwned && _rent != null && _rent.GetInstanceID() == _rentId &&
                _view._rentText?.Pointer == _rent.Pointer));

        internal void Apply()
        {
            if (_buy != null && _view._buyButtonText == null)
            {
                _buyOwned = true;
                _view._buyButtonText = _buy;
            }
            if (_rent != null && _view._rentText == null)
            {
                _rentOwned = true;
                _view._rentText = _rent;
            }
        }

        internal bool Restore()
        {
            try
            {
                if (OwnsView)
                {
                    // A native rebind is not ours to undo. Instance IDs also
                    // prevent pointer reuse from modifying another generation.
                    if (_buyOwned && _buy != null && _buy.GetInstanceID() == _buyId &&
                        _view._buyButtonText?.Pointer == _buy.Pointer) _view._buyButtonText = null;
                    if (_rentOwned && _rent != null && _rent.GetInstanceID() == _rentId &&
                        _view._rentText?.Pointer == _rent.Pointer) _view._rentText = null;
                }
                _buyOwned = _rentOwned = false;
                return true;
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogDebug("Resort caption restore deferred: " + ex.Message);
                return false;
            }
        }
    }
}

// Unique native body on the pinned game build (generated token 0x0600A429,
// RVA 0x9DF030). Do not patch shared generic Show/Initialize or Translate.
[HarmonyPatch(typeof(UIWindowBuyResortController), nameof(UIWindowBuyResortController.FillResortView))]
internal static class FivePlayerResortCaptionPatch
{
    private static void Prefix(UIWindowBuyResortController __instance) => FivePlayerResortBindings.Prepare(__instance);
}
