using System;
using System.Reflection;
using System.Text;
using BusinessTour;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace BusinessTourFiveRealms;

[HarmonyPatch(typeof(MapCollection), nameof(MapCollection.Definitions), MethodType.Getter)]
internal static class MapCollectionDefinitionsPatch
{
    private static MapCollection _capturedCollection;
    private static bool _mapSelectionOpening;

    private static void Prefix(MapCollection __instance)
    {
        Capture(__instance);
        if (_mapSelectionOpening)
        {
            EnsureInjected(__instance);
        }
    }

    internal static void Capture(MapCollection collection)
    {
        if (collection == null || collection.Pointer == IntPtr.Zero)
        {
            return;
        }

        _capturedCollection = collection;
        ScanDefinitions(collection._definitions);
    }

    internal static void PrepareForMapSelection()
    {
        _mapSelectionOpening = true;
        EnsureInjected(_capturedCollection);
    }

    internal static void FinishMapSelectionInitialization()
    {
        _mapSelectionOpening = false;
    }

    internal static bool EnsureControllerHasSpecial(UIMapSelectionController controller)
    {
        Il2CppReferenceArray<MapDefinition> definitions = controller?._mapsDefinitions;
        if (definitions == null || definitions.Length == 0)
        {
            return false;
        }

        ScanDefinitions(definitions);
        if (ContainsSpecial(definitions))
        {
            return true;
        }

        MapDefinition special = GetOrCreateSpecial(ModState.Template);
        if (special == null)
        {
            return false;
        }

        controller._mapsDefinitions = Append(definitions, special);
        controller.ProcessDefinition(special);
        controller.RefreshViews();
        Plugin.ModLog.LogInfo($"Added {ModState.DisplayName} directly to the map picker.");
        return true;
    }

    private static void EnsureInjected(MapCollection collection)
    {
        if (collection == null || collection.Pointer == IntPtr.Zero)
        {
            return;
        }

        Il2CppReferenceArray<MapDefinition> definitions = collection._definitions;
        if (definitions == null || definitions.Length == 0)
        {
            return;
        }

        ScanDefinitions(definitions);
        if (ContainsSpecial(definitions))
        {
            return;
        }

        MapDefinition special = GetOrCreateSpecial(ModState.Template);
        if (special == null)
        {
            return;
        }

        collection._definitions = Append(definitions, special);
        Plugin.ModLog.LogInfo($"Injected selectable map on demand: {ModState.DisplayName} (base={ModState.Template.ItemId}).");
    }

    private static void ScanDefinitions(Il2CppReferenceArray<MapDefinition> definitions)
    {
        if (definitions == null)
        {
            return;
        }

        for (int index = 0; index < definitions.Length; index++)
        {
            MapDefinition definition = definitions[index];
            if (definition == null)
            {
                continue;
            }

            if (ModState.IsSpecialId(definition.ItemId))
            {
                ModState.SpecialDefinition = definition;
            }
            else if (ModState.Template == null && !string.IsNullOrWhiteSpace(definition.Map))
            {
                ModState.Template = definition;
            }
        }
    }

    private static bool ContainsSpecial(Il2CppReferenceArray<MapDefinition> definitions)
    {
        for (int index = 0; index < definitions.Length; index++)
        {
            if (definitions[index] != null && ModState.IsSpecialId(definitions[index].ItemId))
            {
                return true;
            }
        }

        return false;
    }

    private static MapDefinition GetOrCreateSpecial(BaseMapDefinition template)
    {
        MapDefinition special = ModState.SpecialDefinition;
        if (special != null && special.Pointer != IntPtr.Zero)
        {
            return special;
        }

        if (template == null || template.Pointer == IntPtr.Zero)
        {
            return null;
        }

        // IL2CPP's generated constructor unboxes Nullable<T> and therefore
        // cannot accept a managed null reference. Allocate a boxed nullable
        // whose native hasValue flag is false instead.
        var emptyDate = new Il2CppSystem.Nullable<Il2CppSystem.DateTime>(new Il2CppSystem.DateTime(1, 1, 1));
        emptyDate.hasValue = false;
        special = new CustomMapDefinition(ModState.MapId, ModState.DisplayName, emptyDate, emptyDate)
        {
            _Map_k__BackingField = ModState.AddMapTag(template.Map),
            _Cards_k__BackingField = template.Cards,
            _Rules_k__BackingField = template.Rules,
            _GameView_k__BackingField = template.GameView,
            _Skin_k__BackingField = template.Skin,
            _ItemNumber_k__BackingField = "5P",
            _ItemName_k__BackingField = "FiveRealms",
            _ItemType_k__BackingField = template._ItemType_k__BackingField,
            _ItemId_k__BackingField = ModState.MapId,
            _IsAlwaysAvailable_k__BackingField = true
        };
        ModState.SpecialDefinition = special;
        return special;
    }

    private static Il2CppReferenceArray<MapDefinition> Append(
        Il2CppReferenceArray<MapDefinition> definitions,
        MapDefinition special)
    {
        var expanded = new Il2CppReferenceArray<MapDefinition>(definitions.Length + 1);
        for (int index = 0; index < definitions.Length; index++)
        {
            expanded[index] = definitions[index];
        }
        expanded[definitions.Length] = special;
        return expanded;
    }
}

[HarmonyPatch(typeof(UIMapSelectionController), MethodType.Constructor, new[] { typeof(IContext) })]
internal static class MapSelectionContextCapturePatch
{
    internal static NoBetLobbyController ActiveLobbyController { get; private set; }

    private static void Postfix(IContext context)
    {
        try
        {
            MapCollectionDefinitionsPatch.Capture(context?.Get<MapCollection>());

            NoBetLobbyController controller = context?.Get<NoBetLobbyController>();
            if (controller != null && controller.Pointer != IntPtr.Zero)
            {
                ActiveLobbyController = controller;
                Plugin.ModLog.LogInfo(Describe(controller));
            }
        }
        catch (Exception ex)
        {
            Plugin.ModLog.LogWarning($"Could not capture the map picker context: {ex.Message}");
        }
    }

    private static string Describe(NoBetLobbyController controller)
    {
        var result = new StringBuilder("Captured initialized private lobby:");
        result.Append($" panels={controller._playerPanelViews?.Count ?? -1}");
        result.Append($", attributes={controller._playerAttributes?.Length ?? -1}");
        result.Append($", controllers={controller._slotContolers?.Length ?? -1}");
        result.Append($", avatarConfigs={controller._avatarsConfigs?.Count ?? -1}");
        result.Append($", avatarProviders={controller._avatarProviders?.Count ?? -1}");
        result.Append($", groupSlots={controller._playerGroup?._slotParents?.Length ?? -1}.");

        if (controller._playerGroup?._slotParents != null)
        {
            for (int index = 0; index < controller._playerGroup._slotParents.Length; index++)
            {
                UnityEngine.Transform slot = controller._playerGroup._slotParents[index];
                result.Append($" slot[{index}]={Describe(slot)} children={slot?.childCount ?? -1};");
            }
        }

        if (controller._playerPanelViews != null)
        {
            for (int index = 0; index < controller._playerPanelViews.Count; index++)
            {
                result.Append($" panel[{index}]={Describe(controller._playerPanelViews[index]?.transform)};");
            }
        }

        return result.ToString();
    }

    private static string Describe(UnityEngine.Transform transform)
    {
        if (transform == null)
        {
            return "null";
        }

        UnityEngine.Vector3 position = transform.localPosition;
        string parent = transform.parent == null ? "<root>" : transform.parent.name;
        return $"{transform.name}@{parent} pos=({position.x:F1},{position.y:F1},{position.z:F1}) active={transform.gameObject.activeSelf}";
    }
}

[HarmonyPatch(typeof(CustomMapDefinition), nameof(CustomMapDefinition.DefinitionType), MethodType.Getter)]
internal static class SpecialMapDefinitionTypePatch
{
    private static void Postfix(CustomMapDefinition __instance, ref MapDefinitionType __result)
    {
        if (ModState.IsSpecialId(__instance.ItemId))
        {
            __result = MapDefinitionType.OfficialMap;
        }
    }
}

[HarmonyPatch(typeof(SkinItemDefinitionBase), nameof(SkinItemDefinitionBase.PreviewPath), MethodType.Getter)]
internal static class SpecialMapPreviewPatch
{
    private static void Postfix(SkinItemDefinitionBase __instance, ref string __result)
    {
        if (ModState.IsSpecialId(__instance.ItemId) && ModState.Template != null)
        {
            __result = ModState.Template.PreviewPath;
        }
    }
}

[HarmonyPatch(typeof(BaseMapDefinition), nameof(BaseMapDefinition.ScreenshotPath), MethodType.Getter)]
internal static class SpecialMapScreenshotPatch
{
    private static void Postfix(BaseMapDefinition __instance, ref string __result)
    {
        if (ModState.IsSpecialDefinition(__instance) && ModState.Template != null)
        {
            __result = ModState.Template.ScreenshotPath;
        }
    }
}

[HarmonyPatch]
internal static class SpecialMapSelectionStatusPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(UIMapSelectionController), "GetStatus", new[] { typeof(string) });

    private static void Postfix(string itemId, ref ItemStatus __result)
    {
        if (ModState.IsSpecialId(itemId) && __result != ItemStatus.Selected)
        {
            __result = ItemStatus.Available;
        }
    }
}

[HarmonyPatch]
internal static class MapSelectionInitializationAuditPatch
{
    private static MethodBase TargetMethod() => AccessTools.Method(
        typeof(UIMapSelectionController),
        nameof(UIMapSelectionController.Initialize),
        new[]
        {
            typeof(IUIBinder<EventSource>),
            typeof(Il2CppReferenceArray<Il2CppSystem.Object>)
        });

    private static void Prefix()
    {
        // Do not mutate the game's global definition list during startup.  The
        // native UI/resource bootstrap assumes that list only contains bundled
        // definitions and could unload sprites referenced by a synthetic item.
        MapCollectionDefinitionsPatch.PrepareForMapSelection();
    }

    private static void Postfix(UIMapSelectionController __instance)
    {
        MapCollectionDefinitionsPatch.FinishMapSelectionInitialization();
        Il2CppReferenceArray<MapDefinition> definitions = __instance?._mapsDefinitions;
        int count = definitions?.Length ?? 0;
        bool found = false;
        for (int index = 0; index < count; index++)
        {
            if (definitions[index] != null && ModState.IsSpecialId(definitions[index].ItemId))
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            try
            {
                found = MapCollectionDefinitionsPatch.EnsureControllerHasSpecial(__instance);
                definitions = __instance?._mapsDefinitions;
                count = definitions?.Length ?? 0;
            }
            catch (Exception ex)
            {
                Plugin.ModLog.LogError($"Could not add {ModState.DisplayName} to the map picker: {ex}");
            }
        }

        if (found)
        {
            Plugin.ModLog.LogInfo($"Map selection UI contains {ModState.DisplayName} ({count} definitions).");
        }
        else
        {
            Plugin.ModLog.LogWarning($"Map selection UI opened without {ModState.DisplayName} ({count} definitions).");
        }
    }
}

[HarmonyPatch]
internal static class SelectedMapTrySelectPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(SelectedMapProvider), "TrySelect", new[] { typeof(int), typeof(string) });

    private static void Postfix(string defId, bool __result)
    {
        if (__result)
        {
            ModState.SetSpecialMap(ModState.IsSpecialId(defId), "map selection");
        }
    }
}

[HarmonyPatch(typeof(MapSettings), nameof(MapSettings.UpdateMapSettings), new[] { typeof(BaseMapDefinition) })]
internal static class MapSettingsDefinitionUpdatePatch
{
    private static void Prefix(BaseMapDefinition definition)
    {
        ModState.SetSpecialMap(ModState.IsSpecialDefinition(definition), "map settings update");
    }
}

[HarmonyPatch(typeof(MapSettings), MethodType.Constructor, new[] { typeof(BaseMapDefinition) })]
internal static class MapSettingsDefinitionConstructorPatch
{
    private static void Prefix(BaseMapDefinition initialDefinition)
    {
        ModState.SetSpecialMap(
            ModState.IsSpecialDefinition(initialDefinition),
            "map settings constructor");
    }
}

[HarmonyPatch(typeof(MapSettings), MethodType.Constructor, new[] { typeof(string), typeof(string), typeof(string), typeof(string) })]
internal static class MapSettingsStringConstructorPatch
{
    private static void Prefix(string tableName)
    {
        if (MapSettingsDataLoadPatch.IsRemovingInternalTag)
        {
            return;
        }

        ModState.SetSpecialMap(
            ModState.IsTaggedMapPath(tableName),
            "synchronized room map");
    }
}

[HarmonyPatch(typeof(MapSettings), nameof(MapSettings.UpdateMapSettings), new[] { typeof(string), typeof(string), typeof(string), typeof(string) })]
internal static class MapSettingsStringUpdatePatch
{
    private static void Postfix(MapSettings __instance)
    {
        if (MapSettingsDataLoadPatch.IsRemovingInternalTag)
        {
            return;
        }

        // UpdateMapSettings(string, ...) is a partial-update API: an empty
        // table name means "leave the current table unchanged".  Looking at
        // the argument in a prefix therefore turns ordinary deck/rules/view
        // synchronization into a false vanilla-map selection.  Read the
        // effective value after the native method has applied the update so a
        // real untagged room/map still disables Five Realms.
        ModState.SetSpecialMap(
            ModState.IsTaggedMapPath(__instance?.TableName),
            "synchronized room map update");
    }
}

[HarmonyPatch(typeof(MapSettingsUtils), nameof(MapSettingsUtils.GetMapDataFromSettings))]
internal static class MapSettingsDataLoadPatch
{
    internal static bool IsRemovingInternalTag { get; private set; }

    private static void Prefix(ref IMapSettings mapSettings)
    {
        if (mapSettings == null || !ModState.IsTaggedMapPath(mapSettings.TableName))
        {
            return;
        }

        ModState.SetSpecialMap(true, "map data load");
        MapSettings replacement;
        IsRemovingInternalTag = true;
        try
        {
            replacement = new MapSettings(
                ModState.RemoveMapTag(mapSettings.TableName),
                mapSettings.DeckName,
                mapSettings.RulesName,
                mapSettings.GameViewName);
        }
        finally
        {
            IsRemovingInternalTag = false;
        }

        mapSettings = new IMapSettings(replacement.Pointer);
    }
}
