using System;
using System.Collections.Generic;
using BusinessTour;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace BusinessTourFiveRealms;

// The gameplay HUD factory uses the serialized view array directly. Expanding
// only the PlayersView getter cannot protect its fifth native array access.
internal static class FivePlayerHud
{
    private const string ColumnName = "FiveRealmsCompactPlayerColumn";
    private const string FifthPanelName = "FiveRealmsGameplayPlayer5";
    private static readonly Dictionary<IntPtr, LayoutState> Layouts = new();
    private static readonly Dictionary<IntPtr, InventoryState> Inventories = new();
    private static readonly Dictionary<IntPtr, BackgroundState> Backgrounds = new();
    private static readonly Dictionary<IntPtr, PauseState> Pauses = new();
    private static bool _seatMappingLogged;

    internal static void EnsureSeatMapping()
    {
        if (!ModState.IsSpecialMapActive)
        {
            return;
        }

        try
        {
            var places = BoardConfig.Places;
            if (places != null && !places.ContainsKey(ModState.FivePlayers))
            {
                var order = new Il2CppStructArray<int>(ModState.FivePlayers);
                for (int index = 0; index < order.Length; index++)
                {
                    order[index] = index;
                }
                places.Add(ModState.FivePlayers, order);
            }

            if (places != null && places.ContainsKey(ModState.FivePlayers) && !_seatMappingLogged)
            {
                _seatMappingLogged = true;
                Plugin.ModLog.LogInfo("Registered gameplay seating for five players; existing seating maps are unchanged.");
            }
        }
        catch (Exception ex)
        {
            Plugin.ModLog.LogWarning($"Could not register five-player gameplay seating: {ex.Message}");
        }
    }

    internal static void Ensure(UIGameHUD hud)
    {
        if (!ModState.IsSpecialMapActive || hud == null || hud._sortedPlayersView == null)
        {
            return;
        }

        EnsureSeatMapping();
        if (Layouts.ContainsKey(hud.Pointer))
        {
            Reflow(hud);
            return;
        }

        GameObject clone = null;
        GameObject column = null;
        LayoutState state = null;
        try
        {
            var current = hud._sortedPlayersView;
            if (current.Length < ModState.VanillaPlayers || current.Length > ModState.FivePlayers)
            {
                Plugin.ModLog.LogWarning($"Five-player HUD skipped an unexpected prefab with {current.Length} player views.");
                return;
            }

            for (int index = 0; index < ModState.VanillaPlayers; index++)
            {
                if (current[index] == null)
                {
                    Plugin.ModLog.LogWarning("Five-player HUD skipped a prefab with an unassigned player view.");
                    return;
                }
            }

            state = new LayoutState(current, hud._placesOfPlayers);
            var views = new Il2CppReferenceArray<PlayerView>(ModState.FivePlayers);
            var anchors = new Il2CppReferenceArray<Transform>(ModState.FivePlayers);
            for (int index = 0; index < current.Length; index++)
            {
                views[index] = current[index];
                anchors[index] = FindSinglePlayerAnchor(hud, current[index], index);
            }

            if (current.Length < ModState.FivePlayers)
            {
                int sourceIndex = FindLeftPanel(current);
                Transform source = anchors[sourceIndex];
                // Clone only the player-owned subtree. A corner container may
                // also own zoom, pause and basket controls even when it has
                // exactly one PlayerView; cloning/moving it duplicates those.
                clone = UnityEngine.Object.Instantiate(source.gameObject);
                state.Clone = clone;
                clone.name = FifthPanelName;
                clone.transform.SetParent(source.parent, false);
                PlayerView fifth = clone.GetComponentInChildren<PlayerView>(true);
                if (fifth == null)
                {
                    throw new InvalidOperationException("The cloned gameplay slot has no PlayerView component.");
                }
                fifth.PlayerId = string.Empty;
                views[ModState.FivePlayers - 1] = fifth;
                anchors[ModState.FivePlayers - 1] = clone.transform;
            }

            column = new GameObject(ColumnName);
            state.Column = column;
            RectTransform columnRect = column.AddComponent<RectTransform>();
            columnRect.SetParent(hud.transform, false);
            columnRect.anchorMin = Vector2.zero;
            columnRect.anchorMax = Vector2.one;
            columnRect.offsetMin = Vector2.zero;
            columnRect.offsetMax = Vector2.zero;
            columnRect.localScale = Vector3.one;

            float canvasHeight = columnRect.rect.height;
            if (canvasHeight < 100f)
            {
                canvasHeight = 1080f;
            }
            float rowHeight = canvasHeight * 0.14f;
            float topMargin = canvasHeight * 0.13f;
            float largestHeight = 0f;
            for (int index = 0; index < anchors.Length; index++)
            {
                RectTransform viewRect = views[index].transform.TryCast<RectTransform>();
                float height = viewRect == null ? 220f : viewRect.rect.height;
                largestHeight = Mathf.Max(largestHeight, height);
            }
            float scale = Mathf.Min(0.62f, (rowHeight - 12f) / Mathf.Max(220f, largestHeight));
            scale = Mathf.Max(0.25f, scale);

            for (int index = 0; index < anchors.Length; index++)
            {
                Transform anchor = anchors[index];
                state.Save(anchor);
                anchor.SetParent(columnRect, false);
                RectTransform rect = anchor.TryCast<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 1f);
                    rect.anchoredPosition = new Vector2(18f, -(topMargin + rowHeight * index));
                }
                else
                {
                    anchor.localPosition = new Vector3(-canvasHeight * 0.75f + 18f,
                        canvasHeight * 0.5f - topMargin - rowHeight * index, 0f);
                }
                Vector3 originalScale = anchor.localScale;
                anchor.localScale = new Vector3(originalScale.x * scale,
                    originalScale.y * scale, originalScale.z);
                views[index]._playerViewPositionType = PlayerViewPositionType.TopLeft;
            }

            // Native InitializePlayers constructs real controllers, subscribes
            // money/turn/state events and sets native avatar providers for every
            // view here, including the fifth. No dummy avatar is inserted.
            hud._sortedPlayersView = views;
            hud._placesOfPlayers = anchors;
            state.ExpandedViews = views;
            state.Anchors = anchors;
            state.TopMargin = topMargin;
            state.RowHeight = rowHeight;
            Layouts.Add(hud.Pointer, state);
            Reflow(hud);
            Plugin.ModLog.LogInfo($"Gameplay HUD prepared {views.Length} native player views in a compact left column (scale={scale:F2}, row={rowHeight:F0}).");
        }
        catch (Exception ex)
        {
            if (state != null)
            {
                state.Restore(hud);
            }
            else
            {
                if (clone != null) UnityEngine.Object.Destroy(clone);
                if (column != null) UnityEngine.Object.Destroy(column);
            }
            Plugin.ModLog.LogWarning($"Five-player gameplay HUD initialization failed safely: {ex}");
        }
    }

    internal static void Reflow(UIGameHUD hud)
    {
        if (!ModState.IsSpecialMapActive || hud == null ||
            !Layouts.TryGetValue(hud.Pointer, out LayoutState state) || state.Column == null) return;
        try
        {
            RectTransform column = state.Column.transform.TryCast<RectTransform>();
            for (int index = 0; index < state.ExpandedViews.Length; index++)
            {
                PlayerView view = state.ExpandedViews[index];
                Transform anchor = state.Anchors[index];
                if (view == null || anchor == null) continue;
                // The native four prefabs have different corner-relative child
                // offsets. Position by actual visible frame bounds, not their
                // full-screen parent pivots, retaining native controllers.
                float left = float.PositiveInfinity;
                float top = float.NegativeInfinity;
                IncludeBounds(view.border?.transform, column, ref left, ref top);
                IncludeBounds(view.backgroundColor?.transform, column, ref left, ref top);
                IncludeBounds(view._avatarPlaceholder, column, ref left, ref top);
                IncludeBounds(view._playerName?.transform, column, ref left, ref top);
                IncludeBounds(view._moneyField?.transform, column, ref left, ref top);
                if (float.IsInfinity(left) || float.IsInfinity(top)) continue;
                Vector3 delta = new Vector3(column.rect.xMin + 18f - left,
                    column.rect.yMax - state.TopMargin - state.RowHeight * index - top, 0f);
                anchor.position += column.TransformVector(delta);
            }
        }
        catch (Exception ex)
        {
            Plugin.ModLog.LogWarning($"Could not align native five-player frames: {ex.Message}");
        }
    }

    private static void IncludeBounds(Transform transform, RectTransform column, ref float left, ref float top)
    {
        RectTransform rect = transform == null ? null : transform.TryCast<RectTransform>();
        if (rect == null) return;
        Rect bounds = rect.rect;
        for (int corner = 0; corner < 4; corner++)
        {
            Vector3 local = column.InverseTransformPoint(rect.TransformPoint(new Vector3(
                (corner & 1) == 0 ? bounds.xMin : bounds.xMax,
                (corner & 2) == 0 ? bounds.yMin : bounds.yMax, 0f)));
            left = Mathf.Min(left, local.x);
            top = Mathf.Max(top, local.y);
        }
    }

    internal static void Release(UIGameHUD hud)
    {
        if (hud != null && Layouts.TryGetValue(hud.Pointer, out LayoutState state))
        {
            Layouts.Remove(hud.Pointer);
            try
            {
                state.Restore(hud);
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogDebug($"Gameplay HUD had already released its objects: {ex.Message}");
            }
        }

        foreach (InventoryState inventory in Inventories.Values)
        {
            try { inventory.Restore(); }
            catch (Exception ex) { Plugin.ModLog.LogDebug($"Inventory objects were already released: {ex.Message}"); }
        }
        Inventories.Clear();
        foreach (BackgroundState background in Backgrounds.Values)
        {
            try { background.Restore(); }
            catch (Exception ex) { Plugin.ModLog.LogDebug($"Background inventory objects were already released: {ex.Message}"); }
        }
        Backgrounds.Clear();
        foreach (PauseState pause in Pauses.Values)
        {
            try { pause.Restore(); }
            catch (Exception ex) { Plugin.ModLog.LogDebug($"Pause objects were already released: {ex.Message}"); }
        }
        Pauses.Clear();
    }

    internal static void PrepareFactory(GameplayPlayerPanelFactory factory,
        Il2CppSystem.Collections.Generic.IList<IPlayer> players)
    {
        if (!ModState.IsSpecialMapActive || factory == null || players == null) return;
        try
        {
            var current = factory._playersViews;
            if (current == null || new Il2CppSystem.Collections.Generic.ICollection<PlayerView>(current.Pointer).Count == 0) return;

            // Pause creates its own serialized four-slot prefab and reads the
            // backing array without calling PlayersViews. Replace the factory
            // input before its deferred native iterator indexes player five.
            PausePlayerPanelsView pause = FindPauseView(current[0]);
            if (pause != null && new Il2CppSystem.Collections.Generic.ICollection<IPlayer>(players.Pointer).Count == ModState.FivePlayers)
            {
                EnsurePause(pause, factory);
            }

            // Neither HUD.InitializePlayers nor the native factory hides spare
            // slots. The native PlayerPanelController constructor shows only
            // each actual player's view, so an unoccupied fifth stays hidden.
            current = factory._playersViews;
            int viewCount = new Il2CppSystem.Collections.Generic.ICollection<PlayerView>(current.Pointer).Count;
            for (int index = 0; index < viewCount; index++)
            {
                if (current[index] != null) current[index].Hide();
            }
        }
        catch (Exception ex)
        {
            Plugin.ModLog.LogWarning($"Could not prepare native player panel factory: {ex}");
        }
    }

    private static PausePlayerPanelsView FindPauseView(PlayerView panel)
    {
        // Unity 6's generated generic GetComponentInParent<T>(bool) contains an
        // invalid managed generic constraint in this IL2CPP interop build. Use
        // the non-generic native API and wrap the returned native pointer.
        Transform current = panel == null ? null : panel.transform;
        while (current != null)
        {
            Component component = current.gameObject.GetComponent(Il2CppType.Of<PausePlayerPanelsView>());
            if (component != null) return new PausePlayerPanelsView(component.Pointer);
            current = current.parent;
        }
        return null;
    }

    private static void EnsurePause(PausePlayerPanelsView view, GameplayPlayerPanelFactory factory)
    {
        if (Pauses.TryGetValue(view.Pointer, out PauseState existing))
        {
            factory._playersViews = new Il2CppSystem.Collections.Generic.IList<PlayerView>(existing.Expanded.Pointer);
            return;
        }
        var current = view._sortedPlayersViews;
        if (current == null || current.Length < ModState.VanillaPlayers ||
            current.Length > ModState.FivePlayers) return;

        var state = new PauseState(view, current);
        try
        {
            var expanded = new Il2CppReferenceArray<PlayerView>(ModState.FivePlayers);
            PlayerView fifth = current.Length < ModState.FivePlayers ?
                state.Clone(current[0].gameObject, "FiveRealmsPausePlayer5").GetComponent<PlayerView>() : current[4];
            if (fifth == null) throw new InvalidOperationException("Pause slot clone has no PlayerView.");
            if (current.Length < ModState.FivePlayers) fifth.PlayerId = string.Empty;

            RectTransform parentRect = view.transform.TryCast<RectTransform>();
            float width = parentRect == null ? 1400f : parentRect.rect.width;
            if (width < 500f) width = 1400f;
            float spacing = width * 0.18f;
            for (int index = 0; index < expanded.Length; index++)
            {
                PlayerView panel = index < current.Length ? current[index] : fifth;
                if (panel == null) throw new InvalidOperationException("Pause has an unassigned player slot.");
                expanded[index] = panel;
                state.Save(panel.transform);
                panel.transform.SetParent(view.transform, false);
                RectTransform rect = panel.transform.TryCast<RectTransform>();
                float panelWidth = rect == null ? 300f : rect.rect.width;
                float scale = Mathf.Clamp((spacing - 12f) / Mathf.Max(300f, panelWidth), 0.25f, 0.70f);
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(0.5f, 0.5f);
                    rect.anchorMax = new Vector2(0.5f, 0.5f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = new Vector2((index - 2) * spacing, 0f);
                }
                else
                {
                    panel.transform.localPosition = new Vector3((index - 2) * spacing, 0f, 0f);
                }
                Vector3 originalScale = panel.transform.localScale;
                panel.transform.localScale = new Vector3(originalScale.x * scale, originalScale.y * scale, originalScale.z);
            }

            state.Expanded = expanded;
            view._sortedPlayersViews = expanded;
            factory._playersViews = new Il2CppSystem.Collections.Generic.IList<PlayerView>(expanded.Pointer);
            Pauses.Add(view.Pointer, state);
            Plugin.ModLog.LogInfo("Pause dialog prepared five native player views before native panel creation.");
        }
        catch (Exception ex)
        {
            try { state.Restore(); }
            catch (Exception restoreError) { Plugin.ModLog.LogDebug(restoreError.Message); }
            Plugin.ModLog.LogWarning($"Could not prepare five-player pause dialog: {ex}");
        }
    }

    internal static void ReleasePause(PausePlayerPanelsView view)
    {
        if (view == null || !Pauses.TryGetValue(view.Pointer, out PauseState state)) return;
        Pauses.Remove(view.Pointer);
        try { state.Restore(); }
        catch (Exception ex) { Plugin.ModLog.LogDebug($"Pause objects were already released: {ex.Message}"); }
    }

    internal static void EnsureInventory(UIGameInventory view)
    {
        if (!ModState.IsSpecialMapActive || view == null || Inventories.ContainsKey(view.Pointer)) return;

        var state = new InventoryState(view);
        try
        {
            state.Money = view._moneyPlaceholders;
            state.Cards = view._cards;
            state.Ownership = view._cellOwnshipIndicators;

            if (state.Money == null || state.Money.Length < ModState.VanillaPlayers ||
                state.Cards == null || state.Cards.Length < ModState.VanillaPlayers)
            {
                throw new InvalidOperationException("Inventory prefab does not contain four complete player slots.");
            }

            var money = new Il2CppReferenceArray<Transform>(ModState.FivePlayers);
            var cards = new Il2CppReferenceArray<CardInventory>(ModState.FivePlayers);
            Transform fifthMoney = state.Money.Length < ModState.FivePlayers ?
                state.Clone(state.Money[2].gameObject, "FiveRealmsMoneySlot5").transform : state.Money[4];
            CardInventory fifthCards = state.Cards.Length < ModState.FivePlayers ?
                state.Clone(state.Cards[2].gameObject, "FiveRealmsCardsSlot5").GetComponent<CardInventory>() : state.Cards[4];
            for (int index = 0; index < ModState.FivePlayers; index++)
            {
                money[index] = index < state.Money.Length ? state.Money[index] : fifthMoney;
                cards[index] = index < state.Cards.Length ? state.Cards[index] : fifthCards;
                if (money[index] == null || cards[index] == null)
                {
                    throw new InvalidOperationException("Inventory slot clone is incomplete.");
                }
                state.Save(money[index]);
                state.Save(cards[index].transform);
                PositionWorldRow(money[index], index, 0.16f);
                PositionWorldRow(cards[index].transform, index, 0.20f);
            }

            var groups = state.Ownership;
            var expandedGroups = groups == null ? null :
                new Il2CppReferenceArray<CellsOwnshipIndicator.InitParams>(groups.Length);
            if (groups != null)
            {
                for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
                {
                    CellsOwnshipIndicator.InitParams group = groups[groupIndex];
                    var original = group.Indicators;
                    if (original == null || original.Length < ModState.VanillaPlayers)
                    {
                        throw new InvalidOperationException($"Ownership indicator group {groupIndex} has incomplete player slots.");
                    }
                    var indicators = new Il2CppReferenceArray<CellsOwnshipIndicator>(ModState.FivePlayers);
                    CellsOwnshipIndicator fifthIndicator = original.Length < ModState.FivePlayers ?
                        state.Clone(original[2].gameObject, $"FiveRealmsOwnership5_{groupIndex}")
                            .GetComponent<CellsOwnshipIndicator>() : original[4];
                    for (int playerIndex = 0; playerIndex < indicators.Length; playerIndex++)
                    {
                        indicators[playerIndex] = playerIndex < original.Length ? original[playerIndex] : fifthIndicator;
                        if (indicators[playerIndex] == null)
                        {
                            throw new InvalidOperationException("Ownership indicator clone is incomplete.");
                        }
                        if (playerIndex >= original.Length)
                        {
                            indicators[playerIndex].Validate(group.CellIds.Length);
                        }
                        state.Save(indicators[playerIndex].transform);
                        PositionWorldRow(indicators[playerIndex].transform, playerIndex,
                            0.23f + groupIndex * 0.018f);
                    }
                    expandedGroups[groupIndex] = new CellsOwnshipIndicator.InitParams
                    {
                        CellIds = group.CellIds,
                        Indicators = indicators
                    };
                }
            }

            view._moneyPlaceholders = money;
            view._cards = cards;
            if (expandedGroups != null) view._cellOwnshipIndicators = expandedGroups;
            Inventories.Add(view.Pointer, state);
            Plugin.ModLog.LogInfo($"Gameplay inventory prepared five money/card slots and {groups?.Length ?? 0} five-player ownership indicator groups.");
        }
        catch (Exception ex)
        {
            try { state.Restore(); }
            catch (Exception restoreError) { Plugin.ModLog.LogDebug(restoreError.Message); }
            Plugin.ModLog.LogWarning($"Could not prepare five-player inventory: {ex}");
        }
    }

    internal static void AuditInventory(UIGameInventoryController controller)
    {
        if (!ModState.IsSpecialMapActive || controller == null || controller._view == null) return;
        try
        {
            var players = controller._players;
            int expected = players == null ? 0 :
                new Il2CppSystem.Collections.Generic.ICollection<IPlayer>(players.Pointer).Count;
            int initialized = controller._view._playerMoney?.Count ?? 0;
            if (expected != initialized)
            {
                Plugin.ModLog.LogWarning($"Gameplay money initialization is incomplete: {initialized}/{expected} actual players.");
            }
            else
            {
                Plugin.ModLog.LogInfo($"Gameplay inventory initialized native money for all {initialized} actual players.");
            }
        }
        catch (Exception ex)
        {
            Plugin.ModLog.LogDebug($"Gameplay inventory audit could not read released objects: {ex.Message}");
        }
    }

    internal static void EnsureBackground(LocationBackground background)
    {
        if (!ModState.IsSpecialMapActive || background == null || Backgrounds.ContainsKey(background.Pointer)) return;
        var current = background._inventoryContainers;
        if (current == null || current.Length < ModState.VanillaPlayers) return;
        var state = new BackgroundState(background, current);
        try
        {
            var containers = new Il2CppReferenceArray<Transform>(ModState.FivePlayers);
            Transform fifthContainer = current.Length < ModState.FivePlayers ?
                state.Clone(current[2].gameObject, "FiveRealmsBackgroundInventory5").transform : current[4];
            for (int index = 0; index < containers.Length; index++)
            {
                containers[index] = index < current.Length ? current[index] : fifthContainer;
                state.Save(containers[index]);
                PositionWorldRow(containers[index], index, 0.16f);
            }
            background._inventoryContainers = containers;
            Backgrounds.Add(background.Pointer, state);
        }
        catch (Exception ex)
        {
            try { state.Restore(); }
            catch (Exception restoreError) { Plugin.ModLog.LogDebug(restoreError.Message); }
            Plugin.ModLog.LogWarning($"Could not prepare five background inventory positions: {ex.Message}");
        }
    }

    private static void PositionWorldRow(Transform transform, int index, float x)
    {
        Camera camera = Camera.main;
        if (camera == null || !camera.orthographic) return;
        Vector3 original = transform.position;
        float depth = Vector3.Dot(original - camera.transform.position, camera.transform.forward);
        Vector3 position = camera.ViewportToWorldPoint(new Vector3(x, 0.82f - index * 0.14f, depth));
        position.z = original.z;
        transform.position = position;
        Vector3 scale = transform.localScale;
        transform.localScale = new Vector3(scale.x * 0.62f, scale.y * 0.62f, scale.z);
    }

    private static int FindLeftPanel(Il2CppReferenceArray<PlayerView> views)
    {
        for (int index = 0; index < views.Length; index++)
        {
            if (views[index].PlayerViewPositionType == PlayerViewPositionType.TopLeft)
            {
                return index;
            }
        }
        return Math.Min(2, views.Length - 1);
    }

    private static Transform FindSinglePlayerAnchor(UIGameHUD hud, PlayerView view, int index)
    {
        // _placesOfPlayers are full-screen corner anchors, not exclusive
        // player panels. Leave them (and their non-player controls) in place.
        // All visual PlayerView fields belong to this serialized subtree.
        Transform root = view.transform;
        bool frameOwned = IsOwned(root, view.border?.transform) &&
            IsOwned(root, view.backgroundColor?.transform) &&
            IsOwned(root, view._avatarPlaceholder) &&
            IsOwned(root, view._playerName?.transform) &&
            IsOwned(root, view._moneyField?.transform);
        if (!frameOwned)
            throw new InvalidOperationException($"Player panel {index} has external frame references; refusing to move shared HUD controls.");
        return view.transform;
    }

    private static bool IsOwned(Transform root, Transform child) =>
        child == null || child.Pointer == root.Pointer || child.IsChildOf(root);

    private sealed class LayoutState
    {
        internal readonly Il2CppReferenceArray<PlayerView> Views;
        internal readonly Il2CppReferenceArray<Transform> Places;
        private readonly PlayerViewPositionType[] _positionTypes;
        internal readonly List<TransformState> Transforms = new();
        internal GameObject Clone;
        internal GameObject Column;
        internal Il2CppReferenceArray<PlayerView> ExpandedViews;
        internal Il2CppReferenceArray<Transform> Anchors;
        internal float TopMargin;
        internal float RowHeight;

        internal LayoutState(Il2CppReferenceArray<PlayerView> views, Il2CppReferenceArray<Transform> places)
        {
            Views = views;
            Places = places;
            _positionTypes = new PlayerViewPositionType[views.Length];
            for (int index = 0; index < views.Length; index++)
            {
                _positionTypes[index] = views[index].PlayerViewPositionType;
            }
        }

        internal void Save(Transform transform) => Transforms.Add(new TransformState(transform));

        internal void Restore(UIGameHUD hud)
        {
            foreach (TransformState transform in Transforms)
            {
                try { transform.Restore(); }
                catch (Exception ex) { Plugin.ModLog.LogDebug($"HUD transform already released: {ex.Message}"); }
            }
            hud._sortedPlayersView = Views;
            hud._placesOfPlayers = Places;
            for (int index = 0; index < Views.Length; index++)
            {
                if (Views[index] != null)
                {
                    Views[index]._playerViewPositionType = _positionTypes[index];
                }
            }
            if (Clone != null) UnityEngine.Object.Destroy(Clone);
            if (Column != null) UnityEngine.Object.Destroy(Column);
        }
    }

    private sealed class TransformState
    {
        private readonly Transform _transform;
        private readonly Transform _parent;
        private readonly int _sibling;
        private readonly Vector3 _position;
        private readonly Vector3 _scale;
        private readonly Vector2 _anchorMin;
        private readonly Vector2 _anchorMax;
        private readonly Vector2 _pivot;
        private readonly Vector2 _anchoredPosition;

        internal TransformState(Transform transform)
        {
            _transform = transform;
            _parent = transform.parent;
            _sibling = transform.GetSiblingIndex();
            _position = transform.localPosition;
            _scale = transform.localScale;
            RectTransform rect = transform.TryCast<RectTransform>();
            if (rect != null)
            {
                _anchorMin = rect.anchorMin;
                _anchorMax = rect.anchorMax;
                _pivot = rect.pivot;
                _anchoredPosition = rect.anchoredPosition;
            }
        }

        internal void Restore()
        {
            if (_transform == null) return;
            _transform.SetParent(_parent, false);
            _transform.SetSiblingIndex(_sibling);
            _transform.localScale = _scale;
            RectTransform rect = _transform.TryCast<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = _anchorMin;
                rect.anchorMax = _anchorMax;
                rect.pivot = _pivot;
                rect.anchoredPosition = _anchoredPosition;
            }
            else
            {
                _transform.localPosition = _position;
            }
        }
    }

    private abstract class ClonedViewState
    {
        private readonly List<GameObject> _clones = new();
        private readonly Dictionary<IntPtr, TransformState> _transforms = new();

        internal GameObject Clone(GameObject source, string name)
        {
            GameObject clone = UnityEngine.Object.Instantiate(source);
            _clones.Add(clone);
            clone.name = name;
            clone.transform.SetParent(source.transform.parent, false);
            return clone;
        }

        internal void Save(Transform transform)
        {
            if (transform != null && !_transforms.ContainsKey(transform.Pointer))
            {
                _transforms.Add(transform.Pointer, new TransformState(transform));
            }
        }

        protected void RestoreObjects()
        {
            foreach (TransformState transform in _transforms.Values)
                try { transform.Restore(); }
                catch (Exception ex) { Plugin.ModLog.LogDebug($"UI transform already released: {ex.Message}"); }
            foreach (GameObject clone in _clones)
            {
                if (clone != null) UnityEngine.Object.Destroy(clone);
            }
        }
    }

    private sealed class InventoryState : ClonedViewState
    {
        private readonly UIGameInventory _view;
        internal Il2CppReferenceArray<Transform> Money;
        internal Il2CppReferenceArray<CardInventory> Cards;
        internal Il2CppReferenceArray<CellsOwnshipIndicator.InitParams> Ownership;

        internal InventoryState(UIGameInventory view) => _view = view;

        internal void Restore()
        {
            if (_view != null)
            {
                _view._moneyPlaceholders = Money;
                _view._cards = Cards;
                _view._cellOwnshipIndicators = Ownership;
            }
            RestoreObjects();
        }
    }

    private sealed class BackgroundState : ClonedViewState
    {
        private readonly LocationBackground _background;
        private readonly Il2CppReferenceArray<Transform> _containers;

        internal BackgroundState(LocationBackground background, Il2CppReferenceArray<Transform> containers)
        {
            _background = background;
            _containers = containers;
        }

        internal void Restore()
        {
            if (_background != null) _background._inventoryContainers = _containers;
            RestoreObjects();
        }
    }

    private sealed class PauseState : ClonedViewState
    {
        private readonly PausePlayerPanelsView _view;
        private readonly Il2CppReferenceArray<PlayerView> _original;
        internal Il2CppReferenceArray<PlayerView> Expanded;

        internal PauseState(PausePlayerPanelsView view, Il2CppReferenceArray<PlayerView> original)
        {
            _view = view;
            _original = original;
        }

        internal void Restore()
        {
            if (_view != null) _view._sortedPlayersViews = _original;
            RestoreObjects();
        }
    }
}

[HarmonyPatch(typeof(PlayersHolder), nameof(PlayersHolder.BusinessTour_IPlayersHolder_CreateAndAddPlayer))]
internal static class FivePlayerHudSeatsPatch
{
    private static void Prefix() => FivePlayerHud.EnsureSeatMapping();
}

[HarmonyPatch(typeof(UIGameHUDController), nameof(UIGameHUDController.InitializePlayers))]
internal static class FivePlayerHudLayoutPatch
{
    private static void Prefix(UIGameHUDController __instance) => FivePlayerHud.Ensure(__instance?._gameHUD);
    private static void Postfix(UIGameHUDController __instance) => FivePlayerHud.Reflow(__instance?._gameHUD);
}

[HarmonyPatch(typeof(UIGameHUD), nameof(UIGameHUD.RegisterManagedUIElements))]
internal static class FivePlayerHudRegistrationPatch
{
    private static void Prefix(UIGameHUD __instance) => FivePlayerHud.Ensure(__instance);
}

[HarmonyPatch(typeof(UIGameHUDController), nameof(UIGameHUDController.OnClose))]
internal static class FivePlayerHudReleasePatch
{
    private static void Postfix(UIGameHUDController __instance) => FivePlayerHud.Release(__instance?._gameHUD);
}

[HarmonyPatch(typeof(UIGameInventoryController), nameof(UIGameInventoryController.InitializePlayers))]
internal static class FivePlayerInventoryLayoutPatch
{
    private static void Prefix(UIGameInventoryController __instance) => FivePlayerHud.EnsureInventory(__instance?._view);

    private static void Postfix(UIGameInventoryController __instance) => FivePlayerHud.AuditInventory(__instance);
}

[HarmonyPatch(typeof(GameView), nameof(GameView.ShowGameUIAndInventory), new[] { typeof(IBoardManagerEvents) })]
internal static class FivePlayerInventoryBackgroundPatch
{
    // GetPlayerMoneyPosition returns Nullable<Vector3> as a native value type.
    // Detouring that getter breaks its hidden return-buffer ABI in this loader,
    // even when a patch only has a Prefix. Expand containers on this earlier
    // void initialization boundary instead and leave the getter entirely native.
    private static void Prefix(GameView __instance) => FivePlayerHud.EnsureBackground(__instance?._background);
}

[HarmonyPatch(typeof(GameplayPlayerPanelFactory), nameof(GameplayPlayerPanelFactory.Create))]
internal static class FivePlayerPanelFactoryPatch
{
    private static void Prefix(GameplayPlayerPanelFactory __instance,
        Il2CppSystem.Collections.Generic.IList<IPlayer> __0) => FivePlayerHud.PrepareFactory(__instance, __0);
}

[HarmonyPatch(typeof(UIGamePauseController), nameof(UIGamePauseController.Close))]
internal static class FivePlayerPauseReleasePatch
{
    private static void Prefix(UIGamePauseController __instance, out PausePlayerPanelsView __state) =>
        __state = __instance?._playerPanelsView;

    private static void Postfix(PausePlayerPanelsView __state) => FivePlayerHud.ReleasePause(__state);
}
