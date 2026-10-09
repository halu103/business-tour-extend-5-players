using System;
using System.Collections.Generic;
using BusinessTour;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Navigation;
using UnityEngine;

namespace BusinessTourFiveRealms;

// Poll only the already-created window. Constructor and Initialize detours on
// this IL2CPP build have damaged unrelated UI; this component never creates or
// initializes another lobby controller.
public sealed class FifthLobbySlotUpdater : MonoBehaviour
{
    private float _nextCheck;
    private float _nextSpriteSync;
    private bool _reportedFirstUpdate;

    public FifthLobbySlotUpdater(IntPtr pointer) : base(pointer) { }

    public void Awake()
    {
        Plugin.ModLog.LogInfo("Five Realms runtime updater awakened.");
    }

    public void Update()
    {
        if (!_reportedFirstUpdate)
        {
            _reportedFirstUpdate = true;
            Plugin.ModLog.LogInfo("Five Realms runtime updater received its first Unity frame.");
        }
        if (Time.unscaledTime >= _nextSpriteSync)
        {
            _nextSpriteSync = Time.unscaledTime + 0.1f;
            PentagonalSpriteGeometry.SyncAll();
        }
        if (Time.unscaledTime < _nextCheck)
        {
            return;
        }
        _nextCheck = Time.unscaledTime + 0.5f;
        FifthLobbySlot.Tick();
    }
}

internal static class FifthLobbySlot
{
    private const int SlotIndex = ModState.FivePlayers - 1;
    private static LobbyState _current;
    private static string _lastError;
    private static string _lastPending;
    private static bool _auditNextTick = true;

    private static void ReportPending(string reason)
    {
        if (ModState.IsSpecialMapActive && !string.Equals(reason, _lastPending, StringComparison.Ordinal))
        {
            _lastPending = reason;
            Plugin.ModLog.LogInfo("Fifth private-lobby slot is waiting: " + reason + ".");
        }
    }

    internal static bool HasFifthOccupant()
    {
        try
        {
            IContext context = Context.Instance;
            if (context == null || !Photon.Pun.PhotonNetwork.InRoom)
            {
                return false;
            }
            var managerInterface = ContextExtensions.GetLobbyUIManager(context);
            var manager = managerInterface?.TryCast<UIManager<LogicAtomWindowLobby>>();
            if (manager == null || manager.OpenedWindows == null ||
                !manager.OpenedWindows.ContainsKey(LogicAtomWindowLobby.WndWithoutBet))
            {
                return false;
            }
            manager.Windows.TryGetValue(LogicAtomWindowLobby.WndWithoutBet, out var lobbyWindow);
            var controller = lobbyWindow?.TryCast<NoBetLobbyController>();
            var settings = controller?.RoomPlayersSettings?.TryCast<RoomPlayersSettings>();
            var slots = settings?._roomPlayersInfos;
            return slots != null && slots.Length > SlotIndex && slots[SlotIndex] != null;
        }
        catch (Exception ex)
        {
            Plugin.ModLog.LogWarning("Could not inspect fifth-slot occupancy: " + ex.Message);
            // If the mode is active and its existing card was created, err on
            // preserving the room until the next tick can inspect it again.
            return ModState.IsSpecialMapActive && _current != null;
        }
    }

    internal static void Tick()
    {
        try
        {
            bool audit = ModState.IsSpecialMapActive && _auditNextTick;
            if (audit) Plugin.ModLog.LogInfo("Fifth-slot audit: tick entered.");
            FifthPlayerColors.Apply(ModState.IsSpecialMapActive);
            if (!ModState.IsSpecialMapActive && _current == null)
            {
                return;
            }
            if (audit) Plugin.ModLog.LogInfo("Fifth-slot audit: resolving context.");
            EnsureFromContext(Context.Instance);
            if (audit)
            {
                _auditNextTick = false;
                Plugin.ModLog.LogInfo("Fifth-slot audit: tick completed.");
            }
        }
        catch (Exception ex)
        {
            string error = ex.GetType().Name + ": " + ex.Message;
            if (!string.Equals(error, _lastError, StringComparison.Ordinal))
            {
                _lastError = error;
                Plugin.ModLog.LogWarning("Fifth private-lobby slot is unavailable: " + ex);
            }
        }
    }

    internal static void EnsureFromContext(IContext context)
    {
        if (context == null || context.Pointer == IntPtr.Zero)
        {
            ReportPending("game context is not ready");
            return;
        }

        if (_auditNextTick) Plugin.ModLog.LogInfo("Fifth-slot audit: resolving lobby manager.");
        var managerInterface = ContextExtensions.GetLobbyUIManager(context);
        var manager = managerInterface?.TryCast<UIManager<LogicAtomWindowLobby>>();
        if (manager == null)
        {
            ReportPending("private-lobby UI manager is not ready");
            return;
        }
        if (_auditNextTick) Plugin.ModLog.LogInfo("Fifth-slot audit: testing open lobby window.");
        if (manager.OpenedWindows == null ||
            !manager.OpenedWindows.ContainsKey(LogicAtomWindowLobby.WndWithoutBet))
        {
            // Opening the map picker temporarily hides the same lobby window.
            // Retain its callbacks and setup state until a different controller
            // or newly initialized vanilla panels identify a fresh lobby.
            ReportPending("private-lobby window is not open");
            return;
        }

        if (_auditNextTick) Plugin.ModLog.LogInfo("Fifth-slot audit: reading lobby window.");
        // Avoid a generated generic-method lookup here; the serialized native
        // dictionary exposes the same already-created controller.
        manager.Windows.TryGetValue(LogicAtomWindowLobby.WndWithoutBet, out var window);
        if (_auditNextTick) Plugin.ModLog.LogInfo("Fifth-slot audit: window dictionary resolved; checking native controller type.");
        NoBetLobbyController controller = window?.TryCast<NoBetLobbyController>();
        if (controller == null || controller.Pointer == IntPtr.Zero)
        {
            ReportPending(window == null ? "private-lobby window lookup returned no object" :
                "private-lobby window cannot be resolved as NoBetLobbyController");
            return;
        }
        if (_auditNextTick) Plugin.ModLog.LogInfo("Fifth-slot audit: controller resolved; checking initialized fields.");
        if (_auditNextTick)
        {
            Plugin.ModLog.LogInfo("Fifth-slot audit: reading LobbyView.");
            var auditView = controller.LobbyView;
            Plugin.ModLog.LogInfo("Fifth-slot audit: reading player group.");
            var auditGroup = controller._playerGroup;
            Plugin.ModLog.LogInfo("Fifth-slot audit: reading slot parents.");
            var auditParents = auditGroup?._slotParents;
            Plugin.ModLog.LogInfo("Fifth-slot audit: reading panels.");
            var auditPanels = controller._playerPanelViews;
            Plugin.ModLog.LogInfo("Fifth-slot audit: reading attributes.");
            var auditAttributes = controller._playerAttributes;
            Plugin.ModLog.LogInfo("Fifth-slot audit: reading slot controllers.");
            var auditControllers = controller._slotContolers;
            Plugin.ModLog.LogInfo("Fifth-slot audit: reading avatar configs.");
            var auditAvatars = controller._avatarsConfigs;
            Plugin.ModLog.LogInfo("Fifth-slot audit: reading avatar providers.");
            var auditProviders = controller._avatarProviders;
            Plugin.ModLog.LogInfo("Fifth-slot audit: reading room settings.");
            var auditSettings = controller.RoomPlayersSettings;
            Plugin.ModLog.LogInfo("Fifth-slot audit: reading lobby model.");
            var auditModel = controller.LobbyModel;
            Plugin.ModLog.LogInfo("Fifth-slot audit: reading player attribute provider.");
            var auditAttributeProvider = controller.PlayerAttributeProvider;
            Plugin.ModLog.LogInfo("Fifth-slot audit: reading avatar provider factory.");
            var auditAvatarFactory = controller.AvatarProviderFactory;
            Plugin.ModLog.LogInfo("Fifth-slot audit: initialized field reads finished.");
            Plugin.ModLog.LogInfo("Fifth-slot audit: comparing view to null.");
            Plugin.ModLog.LogInfo("Fifth-slot audit: view is null=" + (auditView == null));
            Plugin.ModLog.LogInfo("Fifth-slot audit: comparing group to null.");
            Plugin.ModLog.LogInfo("Fifth-slot audit: group is null=" + (auditGroup == null));
            Plugin.ModLog.LogInfo("Fifth-slot audit: counting slot parents.");
            Plugin.ModLog.LogInfo("Fifth-slot audit: parent count=" + (auditParents?.Length ?? -1));
            Plugin.ModLog.LogInfo("Fifth-slot audit: counting native panels.");
            Plugin.ModLog.LogInfo("Fifth-slot audit: panel count=" + (auditPanels?.Count ?? -1));
        }
        if (controller.LobbyView == null || controller._playerGroup == null ||
            controller._playerGroup._slotParents == null ||
            controller._playerGroup._slotParents.Length < ModState.VanillaPlayers ||
            controller._playerPanelViews == null || controller._playerPanelViews.Count < ModState.VanillaPlayers ||
            controller._playerAttributes == null || controller._slotContolers == null ||
            controller._avatarsConfigs == null || controller._avatarProviders == null ||
            controller.RoomPlayersSettings == null || controller.LobbyModel == null ||
            controller.PlayerAttributeProvider == null || controller.AvatarProviderFactory == null)
        {
            ReportPending($"native setup is incomplete (view={controller.LobbyView != null}, " +
                $"parents={controller._playerGroup?._slotParents?.Length ?? 0}, " +
                $"panels={controller._playerPanelViews?.Count ?? 0}, " +
                $"attributes={controller._playerAttributes?.Length ?? 0}, " +
                $"controllers={controller._slotContolers?.Length ?? 0}, " +
                $"avatars={controller._avatarsConfigs?.Count ?? 0}, " +
                $"providers={controller._avatarProviders?.Count ?? 0}, " +
                $"room={controller.RoomPlayersSettings != null}, " +
                $"model={controller.LobbyModel != null}, " +
                $"pawnProvider={controller.PlayerAttributeProvider != null}, " +
                $"avatarFactory={controller.AvatarProviderFactory != null})");
            return;
        }

        if (_auditNextTick) Plugin.ModLog.LogInfo("Fifth-slot audit: initialized-field condition passed.");
        if (_current == null || _current.Controller.Pointer != controller.Pointer ||
            _current.Group.Pointer != controller._playerGroup.Pointer || !_current.MatchesGeneration(controller))
        {
            if (!ModState.IsSpecialMapActive)
            {
                return;
            }
            if (_auditNextTick) Plugin.ModLog.LogInfo("Fifth-slot audit: reading native binder.");
            var nativeBinder = manager.Binder;
            if (_auditNextTick) Plugin.ModLog.LogInfo("Fifth-slot audit: wrapping native binder.");
            var binder = new IUIBinder<EventSource>(nativeBinder.Pointer);
            if (_auditNextTick) Plugin.ModLog.LogInfo("Fifth-slot audit: constructing lobby state.");
            _current = new LobbyState(controller, binder);
            if (_auditNextTick) Plugin.ModLog.LogInfo("Fifth-slot audit: lobby state constructed.");
        }

        if (ModState.IsSpecialMapActive)
        {
            _current.Ensure();
            if (!_current.IsCreated)
            {
                ReportPending("native fifth-seat setup was already attempted; reopen the private lobby for a clean retry");
                return;
            }
            _current.SetVisible(true);
            _current.Refresh(false);
            _lastPending = null;
        }
        else
        {
            // RoomCapacityRegistry preserves an occupied fifth slot. Keep its
            // card visible too, so a map change cannot conceal a real player.
            var slots = new RoomPlayersSettings(controller.RoomPlayersSettings.Pointer)._roomPlayersInfos;
            bool occupied = slots != null && slots.Length > SlotIndex && slots[SlotIndex] != null;
            _current.SetVisible(occupied);
            if (occupied)
            {
                _current.Refresh(false);
            }
        }
        _lastError = null;
    }

    private sealed class LobbyState
    {
        internal readonly NoBetLobbyController Controller;
        internal readonly UIPlayerGroup Group;
        private readonly IUIBinder<EventSource> _binder;
        // Keep converted managed callbacks alive while the native objects hold
        // them. No callback is installed on any of the four vanilla seats.
        private readonly List<Il2CppSystem.Delegate> _callbacks = new List<Il2CppSystem.Delegate>();
        private readonly Vector3[] _originalPositions = new Vector3[ModState.VanillaPlayers];
        private readonly Vector3[] _originalScales = new Vector3[ModState.VanillaPlayers];
        private readonly IntPtr[] _vanillaPanelPointers = new IntPtr[ModState.VanillaPlayers];
        private readonly bool _layoutWasEnabled;
        private OtherPlayerPanelView _panel;
        private PlayerAttributes _attributes;
        private PlayerAttributeProvider _attributeProvider;
        private bool _created;
        private bool _attempted;
        private bool? _visible;
        private string _snapshot;

        internal bool IsCreated => _created;

        internal bool MatchesGeneration(NoBetLobbyController controller)
        {
            // The game may reuse a controller object but rebuild all its seats
            // when reopening a room. Its pointer alone is not a generation ID.
            for (int index = 0; index < ModState.VanillaPlayers; index++)
            {
                if (controller._playerPanelViews[index]?.Pointer != _vanillaPanelPointers[index])
                {
                    return false;
                }
            }
            return true;
        }

        internal LobbyState(NoBetLobbyController controller, IUIBinder<EventSource> binder)
        {
            Plugin.ModLog.LogInfo("Fifth-slot setup: reading player-group layout.");
            Controller = controller;
            Group = controller._playerGroup;
            _binder = binder;
            _layoutWasEnabled = Group._group != null && Group._group.enabled;
            Plugin.ModLog.LogInfo("Fifth-slot setup: reading existing slot transforms.");
            for (int index = 0; index < ModState.VanillaPlayers; index++)
            {
                Transform slot = Group._slotParents[index];
                _originalPositions[index] = slot.localPosition;
                _originalScales[index] = slot.localScale;
                Plugin.ModLog.LogInfo("Fifth-slot setup: reading existing panel " + index + ".");
                _vanillaPanelPointers[index] = controller._playerPanelViews[index]?.Pointer ?? IntPtr.Zero;
            }
            Plugin.ModLog.LogInfo("Fifth-slot setup: existing transforms and panels read.");
        }

        internal void Ensure()
        {
            if (!_attempted) Plugin.ModLog.LogInfo("Fifth-slot setup: ensuring room mode.");
            // Native team mode pairs exactly two teams and has slot-index
            // branches only for 0..3. Five Realms is a five-player FFA map.
            if (Controller.LobbyModel.TeamMode)
            {
                if (Controller.RoomSettings.IsMaster)
                {
                    Controller.RoomSettings.SetTeamMode(false);
                }
                else
                {
                    throw new InvalidOperationException("Five-player lobby requires free-for-all mode on the host.");
                }
            }
            if (_created)
            {
                return;
            }
            if (_attempted)
            {
                // A failed native setup can have installed some listeners.
                // Repeating it would duplicate those listeners. A fresh lobby
                // provides fresh native objects for a clean retry.
                return;
            }
            if (_binder == null)
            {
                throw new InvalidOperationException("The private-lobby button binder is not initialized.");
            }

            var roomPlayers = new RoomPlayersSettings(Controller.RoomPlayersSettings.Pointer);
            RoomCapacityRegistry.Register(roomPlayers);
            // RoomPlayerInfos filters out nulls and returns only occupants;
            // its length is player count, not available slot capacity.
            if (roomPlayers._roomPlayersInfos == null || roomPlayers._roomPlayersInfos.Length < ModState.FivePlayers)
            {
                throw new InvalidOperationException("The current room did not accept five internal slots.");
            }

            _attempted = true;

            Plugin.ModLog.LogInfo("Fifth-slot setup: expanding slot transforms.");
            SlotExpander.Ensure(Group);
            if (Group._slotParents == null || Group._slotParents.Length < ModState.FivePlayers)
            {
                throw new InvalidOperationException("The private-lobby slot group has no fifth parent.");
            }

            if (Controller._playerPanelViews.Count > SlotIndex)
            {
                _panel = Controller._playerPanelViews[SlotIndex].TryCast<OtherPlayerPanelView>();
            }
            else
            {
                // SlotExpander clones its source's children as well as its
                // layout. Remove those unregistered copies before adding a
                // clean native prefab with fresh listeners and placeholders.
                Transform parent = Group._slotParents[SlotIndex];
                for (int index = parent.childCount - 1; index >= 0; index--)
                {
                    GameObject staleCopy = parent.GetChild(index).gameObject;
                    staleCopy.SetActive(false);
                    UnityEngine.Object.Destroy(staleCopy);
                }
                Plugin.ModLog.LogInfo("Fifth-slot setup: creating guest panel.");
                _panel = CreateOtherPlayerPanel(Controller.PrefabNameOtherPlayerPanel);
                if (_panel == null)
                {
                    throw new InvalidOperationException("The native guest panel prefab could not be created.");
                }
                _panel.name = "FiveRealmsPrivatePlayer5";
                Plugin.ModLog.LogInfo("Fifth-slot setup: attaching clean panel to slot parent.");
                Group.AddChild(SlotIndex, _panel.transform, Controller.LobbyParams.IsSlave);
                Controller._playerPanelViews.Add(new MasterPlayerPanelView(_panel.Pointer));
            }
            if (_panel == null)
            {
                throw new InvalidOperationException("The fifth native panel is not a guest panel.");
            }

            var panelSettings = new PlayerPanelsSettings
            {
                ShowChooseControllerButtons = true,
                ShowControlPanel = true
            };
            Plugin.ModLog.LogInfo("Fifth-slot setup: preparing panel palette and controls.");
            FifthColorSelectionButtonsPatch.PreparePanel(_panel._colorSelectionPanel,
                RoomSettingsExtensions.FfaPlayerColors);
            _panel.SetViewSettings(panelSettings);
            if (Controller.LobbyParams.IsSlave)
            {
                _panel.ShowEmptyPanel();
            }
            _panel.BindInteractiveUIElements(_binder, SlotIndex);

            Plugin.ModLog.LogInfo("Fifth-slot setup: extending controller and pawn-attribute arrays.");
            Controller._slotContolers = Expand(Controller._slotContolers, ModState.FivePlayers);
            Controller._slotContolers[SlotIndex] = new Il2CppSystem.ValueTuple<InputType, int>(InputType.Standart, 0);
            Controller._playerAttributes = Expand(Controller._playerAttributes, ModState.FivePlayers);
            _attributes = Controller._playerAttributes[SlotIndex] == null
                ? new PlayerAttributes()
                : new PlayerAttributes(Controller._playerAttributes[SlotIndex].Pointer);
            Controller._playerAttributes[SlotIndex] = new IPlayerAttributes(_attributes.Pointer);
            Plugin.ModLog.LogInfo("Fifth-slot setup: attaching pawn-attribute events.");
            AttachFifthAttributes();

            Plugin.ModLog.LogInfo("Fifth-slot setup: creating fifth avatar provider.");
            while (Controller._avatarsConfigs.Count <= SlotIndex)
            {
                var emptyColor = new Il2CppSystem.Nullable<int>(0) { hasValue = false };
                var config = new AvatarOrPortraitConfig(string.Empty, string.Empty, string.Empty,
                    emptyColor, _panel.AvatarPlaceholder, false, false);
                var factory = Controller.AvatarProviderFactory.TryCast<AvatarProviderFactory>();
                if (factory == null)
                {
                    throw new InvalidOperationException("The native avatar factory has an unsupported concrete type.");
                }
                IAvatarController provider = factory.Create(AvatarBehaviours.LobbyAvatar,
                    new IAvatarConfig(config.Pointer));
                if (provider == null)
                {
                    throw new InvalidOperationException("The native fifth avatar provider could not be created.");
                }
                Controller._avatarsConfigs.Add(config);
                Controller._avatarProviders.Add(provider);
            }

            Plugin.ModLog.LogInfo("Fifth-slot setup: binding fifth-seat actions.");
            BindFifthActions();
            Plugin.ModLog.LogInfo("Fifth-slot setup: updating fifth input selector.");
            Controller.UpdateSlotPanel(SlotIndex);
            _created = true;
            Plugin.ModLog.LogInfo($"Created functional private-lobby slot 5: panels={Controller._playerPanelViews.Count}, " +
                $"attributes={Controller._playerAttributes.Length}, avatars={Controller._avatarsConfigs.Count}, " +
                $"controllers={Controller._slotContolers.Length}, internalSlots={roomPlayers._roomPlayersInfos.Length}.");
        }

        private OtherPlayerPanelView CreateOtherPlayerPanel(string prefabName)
        {
            // Native LobbyController.GetInstance<T> loads a GameObject from
            // ResourceManager, instantiates it, then reads its component. Its
            // prefab is not a Context service factory. Use the concrete loader
            // specialization already used by the vanilla lobby, avoiding both
            // that missing factory lookup and the generic controller wrapper.
            Plugin.ModLog.LogInfo("Fifth-slot factory: resolving native resource manager.");
            var managerInterface = Controller.ResourceManager;
            var manager = managerInterface?.TryCast<ResourceManagement.ResourceManager>();
            if (manager == null)
            {
                throw new InvalidOperationException("The native resource manager is unavailable.");
            }
            Plugin.ModLog.LogInfo("Fifth-slot factory: loading native guest prefab " + prefabName + ".");
            GameObject prefab = manager.Load<GameObject>(prefabName);
            if (prefab == null)
            {
                throw new InvalidOperationException("The native guest-panel prefab could not be loaded: " + prefabName);
            }
            Plugin.ModLog.LogInfo("Fifth-slot factory: instantiating clean guest prefab.");
            GameObject created = UnityEngine.Object.Instantiate(prefab);
            Plugin.ModLog.LogInfo("Fifth-slot factory: resolving created panel.");
            OtherPlayerPanelView panel = created?.GetComponent<OtherPlayerPanelView>();
            if (panel == null && created != null)
            {
                created.SetActive(false);
                UnityEngine.Object.Destroy(created);
            }
            return panel;
        }

        private void AttachFifthAttributes()
        {
            _attributeProvider = new PlayerAttributeProvider(Controller.PlayerAttributeProvider.Pointer);
            var configs = _attributeProvider._configs;
            if (configs == null || configs.Length < ModState.VanillaPlayers)
            {
                throw new InvalidOperationException("The native pawn provider is not initialized.");
            }
            var config = new PlayerAttributesConfig(SlotIndex,
                new IPlayerAttributesEvents(_attributes.Pointer), _panel.PlayerIconPlaceholder,
                _panel.PlayerDicePlaceholder, null);
            var expanded = Expand(configs, ModState.FivePlayers);
            expanded[SlotIndex] = new IPlayerAttributesConfig(config.Pointer);
            _attributeProvider._configs = expanded;

            var released = Convert<Il2CppSystem.EventHandler>(
                new Action<Il2CppSystem.Object, Il2CppSystem.EventArgs>((_, __) => _attributeProvider.Release(SlotIndex)));
            var dice = Convert<Il2CppSystem.EventHandler<string>>(
                new Action<Il2CppSystem.Object, string>((_, id) => _attributeProvider.SetDice(id, SlotIndex)));
            var smile = Convert<Il2CppSystem.EventHandler<string>>(
                new Action<Il2CppSystem.Object, string>((_, id) => _attributeProvider.SetSmile3D(id, SlotIndex)));
            // These two native event argument types contain references inside
            // value types. Il2CppInterop cannot marshal them into a managed
            // callback. Bind the game's existing native methods to a fresh
            // native closure containing slot 5 instead, preserving the exact
            // IL2CPP ABI and all four existing seats and their listeners.
            var nativeTarget = new PlayerAttributeProvider.__c__DisplayClass4_0
            {
                config = new IPlayerAttributesConfig(config.Pointer),
                __4__this = _attributeProvider
            };
            var character = CreateNativeAttributeCallback<Il2CppSystem.EventHandler<CharacterChangedEventArgs>>(
                nativeTarget, attributes => attributes.CharacterChanged);
            var color = CreateNativeAttributeCallback<Il2CppSystem.EventHandler<Il2CppSystem.ValueTuple<string, PlayerColor>>>(
                nativeTarget, attributes => attributes.CharacterColorChanged);
            _attributes.add_Released(released);
            _attributes.add_DiceChanged(dice);
            _attributes.add_Smile3DChanged(smile);
            _attributes.add_CharacterChanged(character);
            _attributes.add_CharacterColorChanged(color);
        }

        private T CreateNativeAttributeCallback<T>(PlayerAttributeProvider.__c__DisplayClass4_0 target,
            Func<PlayerAttributes, T> getCallback) where T : Il2CppSystem.Delegate
        {
            for (int index = 0; index < ModState.VanillaPlayers; index++)
            {
                var sourceInterface = Controller._playerAttributes[index];
                if (sourceInterface == null) continue;
                T source = getCallback(new PlayerAttributes(sourceInterface.Pointer));
                var callbacks = source?.GetInvocationList();
                if (callbacks == null) continue;
                for (int callbackIndex = 0; callbackIndex < callbacks.Length; callbackIndex++)
                {
                    var callback = callbacks[callbackIndex];
                    var sourceTarget = callback?.Target?.TryCast<PlayerAttributeProvider.__c__DisplayClass4_0>();
                    if (sourceTarget == null || sourceTarget.__4__this?.Pointer != _attributeProvider.Pointer)
                    {
                        continue;
                    }
                    var bound = Il2CppSystem.Delegate.CreateDelegate(Il2CppType.Of<T>(), target, callback.Method);
                    T result = bound?.TryCast<T>();
                    if (result == null)
                    {
                        throw new InvalidOperationException("The native slot-5 attribute callback could not be bound.");
                    }
                    _callbacks.Add(result);
                    return result;
                }
            }
            throw new InvalidOperationException("The native pawn-attribute callback template was not found for " + typeof(T).Name + ".");
        }

        private void BindFifthActions()
        {
            Bind(EventSource.WndDummyNoBetDropdownBtn, SlotIndex,
                index => Controller.Method_Private_Void_Int32_PDM_5(index));
            Bind(EventSource.WndDummyNoBetSelPlayerCancelBtn, SlotIndex,
                index => Controller.Method_Private_Void_Int32_PDM_6(index));
            Bind(EventSource.WndDummyPlayerPanelKickBtn, SlotIndex,
                index => { Controller.LobbyModel.KickPlayer(index); Refresh(true); });
            Bind(EventSource.WndDummyNoBetToMyTeamBtn, SlotIndex,
                index => { Controller.LobbyModel.ToMyTeam(index); Refresh(true); });
            for (int variant = 0; variant < 3; variant++)
            {
                int buttonIndex = SlotIndex * 3 + variant;
                Bind(EventSource.WndDummyNoBetHotSeatBtn, buttonIndex,
                    index => { Controller.Method_Private_Void_Int32_PDM_2(index); Refresh(true); });
                Bind(EventSource.WndDummyNoBetAiLevelBtn, buttonIndex,
                    index => { Controller.Method_Private_Void_Int32_PDM_1(index); Refresh(true); });
            }
            Bind(EventSource.PlayerPanelPrevPawnBtn, SlotIndex,
                index => { Controller.Method_Private_Void_Int32_PDM_13(index); Refresh(true); });
            Bind(EventSource.PlayerPanelPrevPawnSwipe, SlotIndex,
                index => { Controller.Method_Private_Void_Int32_PDM_13(index); Refresh(true); });
            Bind(EventSource.PlayerPanelNextPawnBtn, SlotIndex,
                index => { Controller.Method_Private_Void_Int32_PDM_14(index); Refresh(true); });
            Bind(EventSource.PlayerPanelNextPawnSwipe, SlotIndex,
                index => { Controller.Method_Private_Void_Int32_PDM_14(index); Refresh(true); });
            Bind(EventSource.PlayerControlChoicePanelNextBtn, SlotIndex,
                index => { Controller.OnGuestControllerNext(index); Refresh(true); });
            Bind(EventSource.PlayerControlChoicePanelPreviousBtn, SlotIndex,
                index => { Controller.OnGuestControllerPrevious(index); Refresh(true); });
        }

        private void Bind(EventSource source, int index, Action<int> action)
        {
            var callback = Convert<Il2CppSystem.Action<int>>(new Action<int>(value =>
            {
                try
                {
                    if (_created && ModState.IsSpecialMapActive && Controller.IsActiveView)
                    {
                        action(value);
                    }
                }
                catch (Exception ex)
                {
                    Plugin.ModLog.LogWarning($"Fifth-slot action {source} failed: {ex.Message}");
                }
            }));
            _binder.BindAction(source, callback, index);
        }

        private T Convert<T>(Delegate callback) where T : Il2CppSystem.Delegate
        {
            T converted = DelegateSupport.ConvertDelegate<T>(callback);
            _callbacks.Add(converted);
            return converted;
        }

        internal void Refresh(bool force)
        {
            if (!_created || _panel == null)
            {
                return;
            }
            var slots = new RoomPlayersSettings(Controller.RoomPlayersSettings.Pointer)._roomPlayersInfos;
            IRoomPlayerInfo info = slots != null && slots.Length > SlotIndex ? slots[SlotIndex] : null;
            bool master = !Controller.LobbyParams.IsSlave;
            string snapshot = info == null ? "empty:" + master :
                $"{info.Id}|{info.IconId}|{info.DiceId}|{info.PlayerColor}|{info.IsReady}|{info.IsBot}|{info.DisplayedName}|{master}";
            if (!force && string.Equals(snapshot, _snapshot, StringComparison.Ordinal))
            {
                return;
            }

            if (info == null)
            {
                _attributes.Release();
                _panel.ShowEmptyPanel();
                if (!master)
                {
                    _panel.Disable();
                }
            }
            else
            {
                PlayerColor color = info.PlayerColor;
                if (color == PlayerColor.Undefined)
                {
                    color = RoomSettingsExtensions.FfaPlayerColors.Length > SlotIndex
                        ? RoomSettingsExtensions.FfaPlayerColors[SlotIndex]
                        : PlayerColor.Blue2;
                }
                _panel.ShowPawnPanel();
                _panel.ShowKickButton(master);
                _panel.ShowToMyTeamButton(false);
                Controller.InitializeSlotAttributes(SlotIndex, color, info);
                bool changePawn = Controller.CheckNeedToShowPawnButtons(info, master);
                Controller.UpdateChangeInputControlPanel(SlotIndex, changePawn);
                _panel.ShowSelectPawnButtons(changePawn);
                _panel.SetPlayerName(info);
                _panel.SetTitleBackgroundColor(color);
                if (!info.IsBot)
                {
                    _panel.SetPlatformIcon(info.PlayerPlatform);
                }
                Controller.SetupColorPanel(new MasterPlayerPanelView(_panel.Pointer), SlotIndex);
            }
            _snapshot = snapshot;
            Plugin.ModLog.LogInfo(info == null ? "Fifth private-lobby slot is empty." :
                $"Fifth private-lobby slot contains {info.DisplayedName} (bot={info.IsBot}, ready={info.IsReady}).");
        }

        internal void SetVisible(bool visible)
        {
            if (!_created || _visible == visible)
            {
                return;
            }
            if (Group._group != null)
            {
                Group._group.enabled = visible ? false : _layoutWasEnabled;
            }
            Group._slotParents[SlotIndex].gameObject.SetActive(visible);
            for (int index = 0; index < ModState.VanillaPlayers; index++)
            {
                Transform parent = Group._slotParents[index];
                parent.localPosition = _originalPositions[index];
                parent.localScale = _originalScales[index];
            }
            if (visible)
            {
                float left = _originalPositions[0].x;
                float right = _originalPositions[ModState.VanillaPlayers - 1].x;
                for (int index = 0; index < ModState.FivePlayers; index++)
                {
                    Transform parent = Group._slotParents[index];
                    Vector3 position = _originalPositions[Math.Min(index, ModState.VanillaPlayers - 1)];
                    position.x = Mathf.Lerp(left, right, index / (float)SlotIndex);
                    parent.localPosition = position;
                    parent.localScale = _originalScales[Math.Min(index, ModState.VanillaPlayers - 1)] * 0.78f;
                }
            }
            _visible = visible;
            _snapshot = null;
        }
    }

    private static Il2CppReferenceArray<T> Expand<T>(Il2CppReferenceArray<T> current, int count)
        where T : Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase
    {
        if (current != null && current.Length >= count)
        {
            return current;
        }
        var expanded = new Il2CppReferenceArray<T>(count);
        if (current != null)
        {
            for (int index = 0; index < current.Length; index++)
            {
                expanded[index] = current[index];
            }
        }
        return expanded;
    }
}

// Every vanilla lobby panel also receives the expanded palette. Protect the
// native SetupButtons loop itself, before it indexes its serialized four-item
// list, rather than waiting for the fifth-seat updater after a room event.
[HarmonyPatch(typeof(ColorSelectionPanel), nameof(ColorSelectionPanel.SetupButtons))]
internal static class FifthColorSelectionButtonsPatch
{
    private sealed class ExtraButton
    {
        internal ColorSelectionPanel Panel;
        internal ColorSelectionButton Button;
    }

    private static readonly Dictionary<int, ExtraButton> Extras = new Dictionary<int, ExtraButton>();
    private static string _lastError;

    internal static void PrepareLoadedPanels(Il2CppStructArray<PlayerColor> colors)
    {
        // Prepare the existing native panels before publishing an expanded
        // palette triggers their normal RoomPropertiesChanged callbacks. This
        // path does not require a detour on SetupButtons.
        var objects = Resources.FindObjectsOfTypeAll(Il2CppType.Of<ColorSelectionPanel>());
        if (objects == null)
        {
            return;
        }
        for (int index = 0; index < objects.Length; index++)
        {
            var panel = objects[index]?.TryCast<ColorSelectionPanel>();
            if (panel != null)
            {
                PreparePanel(panel, colors);
            }
        }
    }

    internal static void PreparePanel(ColorSelectionPanel panel, Il2CppStructArray<PlayerColor> colors)
    {
        if (panel == null || colors == null)
        {
            return;
        }
        var nativeColors = new Il2CppSystem.Collections.Generic.List<PlayerColor>();
        for (int index = 0; index < colors.Length; index++)
        {
            nativeColors.Add(colors[index]);
        }
        Ensure(panel, nativeColors);
    }

    private static bool Prefix(ColorSelectionPanel __instance,
        Il2CppSystem.Collections.Generic.List<PlayerColor> __1)
    {
        if (!ModState.IsSpecialMapActive && (__instance == null || !Extras.ContainsKey(__instance.GetInstanceID())))
        {
            return true;
        }
        try
        {
            Ensure(__instance, __1);
            _lastError = null;
            return true;
        }
        catch (Exception ex)
        {
            string error = ex.ToString();
            if (!string.Equals(error, _lastError, StringComparison.Ordinal))
            {
                _lastError = error;
                Plugin.ModLog.LogWarning("Could not prepare the five-color selector; retained its previous state: " + error);
            }
            // Do not let the native loop index past its serialized button list
            // if preparation failed. This leaves the rest of the lobby usable.
            return false;
        }
    }

    private static void Ensure(ColorSelectionPanel panel,
        Il2CppSystem.Collections.Generic.List<PlayerColor> colors)
    {
        if (panel == null || colors == null || panel._colorButtonList == null)
        {
            return;
        }
        var buttons = panel._colorButtonList;
        int count = colors.Count;
        if (count > ModState.FivePlayers || count < 1)
        {
            throw new InvalidOperationException("Unexpected native color palette size: " + count);
        }
        int id = panel.GetInstanceID();
        if (Extras.TryGetValue(id, out ExtraButton extra) && extra.Panel.Pointer != panel.Pointer)
        {
            Extras.Remove(id);
            extra = null;
        }
        if (count == ModState.FivePlayers && buttons.Count < count)
        {
            if (buttons.Count != ModState.VanillaPlayers)
            {
                throw new InvalidOperationException("The native color selector has " + buttons.Count + " buttons.");
            }
            if (extra == null || extra.Button == null)
            {
                var source = buttons[buttons.Count - 1];
                if (source == null)
                {
                    throw new InvalidOperationException("The native color-selector template is missing.");
                }
                GameObject clone = UnityEngine.Object.Instantiate(source.gameObject);
                clone.name = "FiveRealmsColor5";
                clone.transform.SetParent(source.transform.parent, false);
                clone.transform.localScale = source.transform.localScale;
                // Layout groups arrange this automatically; the delta also
                // covers prefabs whose buttons use explicit local positions.
                Vector3 delta = source.transform.localPosition - buttons[buttons.Count - 2].transform.localPosition;
                clone.transform.localPosition = source.transform.localPosition + delta;
                var button = clone.GetComponent<ColorSelectionButton>();
                if (button == null)
                {
                    clone.SetActive(false);
                    UnityEngine.Object.Destroy(clone);
                    throw new InvalidOperationException("The cloned color-selector button has no native component.");
                }
                // SetupColorButton clears and rebinds its Unity button listeners
                // for this component and the current native color callback.
                extra = new ExtraButton { Panel = panel, Button = button };
                Extras[id] = extra;
                Plugin.ModLog.LogInfo("Expanded a native lobby color selector to five buttons.");
            }
            buttons.Add(extra.Button);
            extra.Button.gameObject.SetActive(true);
        }
        else if (count == ModState.VanillaPlayers && extra != null)
        {
            if (buttons.Count > ModState.VanillaPlayers && buttons[buttons.Count - 1].Pointer == extra.Button.Pointer)
            {
                buttons.RemoveAt(buttons.Count - 1);
            }
            extra.Button.gameObject.SetActive(false);
        }
        if (buttons.Count < count)
        {
            throw new InvalidOperationException("The color selector still has fewer buttons than colors.");
        }
        var textures = panel._colorTextures?.m_dict;
        if (textures == null)
        {
            throw new InvalidOperationException("The native color-selector sprites are missing.");
        }
        if (count == ModState.FivePlayers && extra != null)
        {
            Plugin.ModLog.LogInfo("Prepared native five-color selector " + id + " (buttons=" + buttons.Count + ").");
        }
        for (int index = 0; index < count; index++)
        {
            PlayerColor color = colors[index];
            if (!textures.ContainsKey(color))
            {
                PlayerColor fallback = color == PlayerColor.Red2 ? PlayerColor.Red : PlayerColor.Blue;
                if (!textures.TryGetValue(fallback, out Sprite sprite) || sprite == null)
                {
                    throw new InvalidOperationException("The native color-selector sprite is missing for " + color);
                }
                textures.Add(color, sprite);
                Plugin.ModLog.LogWarning("The native color selector lacks a " + color + " sprite; using " + fallback + " artwork.");
            }
        }
    }
}

internal static class FifthPlayerColors
{
    private static Il2CppStructArray<PlayerColor> _vanillaColors;
    private static bool _expanded;

    internal static void Apply(bool active)
    {
        if (active == _expanded)
        {
            return;
        }
        if (!active)
        {
            if (FifthLobbySlot.HasFifthOccupant())
            {
                return;
            }
            if (_vanillaColors != null)
            {
                FifthColorSelectionButtonsPatch.PrepareLoadedPanels(_vanillaColors);
                RoomSettingsExtensions.FfaPlayerColors = _vanillaColors;
            }
            _expanded = false;
            return;
        }

        var colors = RoomSettingsExtensions.FfaPlayerColors;
        if (colors == null || colors.Length == 0)
        {
            return;
        }
        _vanillaColors = colors;
        if (colors.Length < ModState.FivePlayers)
        {
            var expanded = new Il2CppStructArray<PlayerColor>(ModState.FivePlayers);
            for (int index = 0; index < colors.Length; index++)
            {
                expanded[index] = colors[index];
            }
            // Every value already has native pawn, title and HUD assets. Pick
            // a native color absent from the four-player palette.
            PlayerColor fifth = PlayerColor.Blue2;
            foreach (PlayerColor candidate in new[]
                { PlayerColor.Blue2, PlayerColor.Red2, PlayerColor.Green, PlayerColor.Yellow, PlayerColor.Blue, PlayerColor.Red })
            {
                bool present = false;
                for (int index = 0; index < colors.Length; index++)
                {
                    present |= colors[index] == candidate;
                }
                if (!present)
                {
                    fifth = candidate;
                    break;
                }
            }
            for (int index = colors.Length; index < expanded.Length; index++)
            {
                expanded[index] = fifth;
            }
            FifthColorSelectionButtonsPatch.PrepareLoadedPanels(expanded);
            RoomSettingsExtensions.FfaPlayerColors = expanded;
            Plugin.ModLog.LogInfo($"Expanded the free-for-all palette to five native colors (fifth={fifth}).");
        }
        _expanded = true;
    }
}
