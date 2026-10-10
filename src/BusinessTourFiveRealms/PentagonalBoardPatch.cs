using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using BusinessTour;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace BusinessTourFiveRealms;

// Upright isometric art cannot be rotated like 3D tiles. A continuous ground
// warp preserves native tile seams and upright label/building rotations.
[HarmonyPatch]
internal static class PentagonalBoardPatch
{
    private static IntPtr _lastMap;
    private static IntPtr[] _lastCells;
    private static Vector3[] _lastPositions;
    private static List<CellSnapshot> _appliedSnapshots;

    internal static void RestoreApplied()
    {
        List<CellSnapshot> saved = _appliedSnapshots;
        _appliedSnapshots = null;
        _lastMap = IntPtr.Zero;
        _lastCells = null;
        _lastPositions = null;
        if (saved == null) return;
        foreach (CellSnapshot snapshot in saved)
            try { snapshot.Restore(); }
            catch (Exception ex) { Plugin.ModLog.LogDebug($"Board cell already released: {ex.Message}"); }
    }

    private static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(MapView), "BusinessTour_IMapView_Initialize");

    private static void Postfix(MapView __instance, ICameraService cameraService)
    {
        if (__instance == null) return;
        if (!ModState.IsSpecialMapActive) { PentagonalSpriteGeometry.Reset(); return; }
        CanonicalBoardPresentation.Apply(__instance, cameraService);
    }

    // Retained only as a diagnostic reference. This deformation is no longer
    // used: native four-corner footprints cannot form five clean board edges.
    private static void LegacyApply(MapView __instance, ICameraService cameraService)
    {
        if (__instance == null) return;
        if (!ModState.IsSpecialMapActive)
        {
            PentagonalSpriteGeometry.Reset();
            _lastCells = null;
            _lastPositions = null;
            return;
        }
        List<CellSnapshot> snapshots = null;
        try
        {
            var ordered = new SortedDictionary<int, ICellView>();
            var ids = __instance.CellsViewIds;
            if (ids != null)
            {
                for (int index = 0; index < ids.Length; index++)
                {
                    ICellView cell = __instance.GetCellViewById(ids[index]);
                    if (cell != null) ordered[ids[index]] = cell;
                }
            }
            var cells = new List<ICellView>(ordered.Values);
            if (cells.Count < 8 || cells.Count % 4 != 0)
                throw new InvalidOperationException($"Unsupported {cells.Count}-cell base map.");
            // Some reconnect paths initialize a view whose children have not
            // been recreated. Never apply a second warp to those same cells.
            if (IsAlreadyApplied(__instance, cells))
            {
                PentagonalSpriteGeometry.SyncAll();
                PentagonalSpriteGeometry.FitCamera(cameraService?.MainCamera ?? Camera.main);
                return;
            }
            PentagonalSpriteGeometry.Reset();
            _lastCells = null;
            _lastPositions = null;
            var positions = new Vector3[cells.Count];
            for (int index = 0; index < cells.Count; index++)
            {
                CellView cell = new CellView(cells[index].Pointer);
                SpriteRenderer ground = cell._cellRenderer?._renderer;
                // These centres are samples for a fitted native reference ring.
                // They are NOT individual fan boundaries: trimmed regional art
                // and the oversized native corner prefabs have different centres.
                positions[index] = ground != null ? ground.bounds.center : cells[index].Position;
                if (index % (cells.Count / 4) == 0)
                    Plugin.ModLog.LogInfo($"Native corner ground {index}: pivot={cells[index].Position}, rendererCenter={ground?.bounds.center}, sprite={ground?.sprite?.name}, size={ground?.bounds.size}.");
            }
            PentagonWarp warp = PentagonWarp.Create(positions);
            snapshots = new List<CellSnapshot>(cells.Count);
            foreach (ICellView view in cells) snapshots.Add(new CellSnapshot(new CellView(view.Pointer)));
            // Capture the native five-entry arrays first. Reset must restore
            // four-player cells, not retain the mod's sixth placement entry.
            foreach (CellSnapshot snapshot in snapshots)
            {
                snapshot.AddFifthPawnPlace();
                snapshot.Apply(warp);
            }
            PentagonalSpriteGeometry.ActiveWarp = warp;
            __instance.SortCellsByOrder();
            PentagonalSpriteGeometry.SyncAll();
            cameraService?.SetCameraToDefaultOnBoardSettings(__instance.MapSize);
            PentagonalSpriteGeometry.FitCamera(cameraService?.MainCamera ?? Camera.main);
            _lastMap = __instance.Pointer;
            _appliedSnapshots = snapshots;
            _lastCells = new IntPtr[cells.Count];
            _lastPositions = new Vector3[cells.Count];
            for (int index = 0; index < cells.Count; index++)
            {
                _lastCells[index] = cells[index].Pointer;
                _lastPositions[index] = cells[index].Position;
            }
            Plugin.ModLog.LogInfo($"Applied continuous five-sided board warp to {cells.Count} cells; upright artwork and hit areas preserved.");
        }
        catch (Exception ex)
        {
            // A missing sprite/shader must not leave half a map deformed or
            // its native ground renderers hidden.
            PentagonalSpriteGeometry.Reset();
            if (snapshots != null)
                foreach (CellSnapshot snapshot in snapshots)
                    try { snapshot.Restore(); } catch { }
            _lastCells = null;
            _lastPositions = null;
            Plugin.ModLog.LogError($"Five-sided board warp failed: {ex}");
        }
    }

    private static bool IsAlreadyApplied(MapView map, List<ICellView> cells)
    {
        if (PentagonalSpriteGeometry.ActiveWarp == null || _lastMap != map.Pointer ||
            _lastCells == null || _lastCells.Length != cells.Count) return false;
        for (int index = 0; index < cells.Count; index++)
            if (_lastCells[index] != cells[index].Pointer ||
                (cells[index].Position - _lastPositions[index]).sqrMagnitude > 0.000001f) return false;
        return true;
    }

    internal static Transform EnsureFifthPawnPlace(CellView cell)
    {
        // Native FreePlaces contains only indices 1..4; index 0 is reserved
        // for a lone centered pawn. Five serialized transforms are NOT five
        // simultaneous-player places. Add a fifth peripheral point at index 5.
        var slots = cell._slots;
        var points = cell._iconVisualPositions;
        var orders = cell.IconVisualOrders;
        if (slots == null || slots.Length < 5 || points == null || points.Length < 5 ||
            orders == null || orders.Length < 5 || cell.FreePlaces == null)
            throw new InvalidOperationException("Cell has incomplete native pawn placement data.");
        if (slots.Length >= 6) return null;
        var expandedSlots = new Il2CppReferenceArray<Transform>(6);
        var expandedPoints = new Il2CppStructArray<Vector3>(6);
        var expandedOrders = new Il2CppStructArray<int>(6);
        for (int index = 0; index < 5; index++)
        {
            expandedSlots[index] = slots[index];
            expandedPoints[index] = points[index];
            expandedOrders[index] = orders[index];
        }
        var peripheral = new List<Vector3>();
        for (int index = 1; index < 5; index++) peripheral.Add(points[index]);
        peripheral.Sort((first, second) => first.y.CompareTo(second.y));
        Vector3 fifthPoint = (peripheral[0] + peripheral[1]) * 0.5f;
        if ((fifthPoint - points[0]).sqrMagnitude < 0.0001f)
            fifthPoint = points[0] + new Vector3(0f, -0.22f, 0f);
        GameObject extra = new GameObject("FiveRealmsPeripheralPawn5");
        extra.transform.SetParent(slots[0].parent, false);
        extra.transform.position = fifthPoint;
        expandedSlots[5] = extra.transform;
        expandedPoints[5] = fifthPoint;
        expandedOrders[5] = orders[4] + Math.Max(1, CellView.OrdersPerCharacter);
        cell._slots = expandedSlots;
        cell._iconVisualPositions = expandedPoints;
        cell.IconVisualOrders = expandedOrders;
        if (!cell.FreePlaces.Contains(5)) cell.FreePlaces.Add(5);
        return extra.transform;
    }

    private sealed class CellSnapshot
    {
        private readonly CellView _cell;
        private readonly List<Anchor> _anchors = new();
        private readonly Vector3[] _corners;
        private readonly List<Vector3[]> _colliderPaths = new();
        private readonly SpriteRenderer _renderer;
        private readonly Matrix4x4 _rendererMatrix;
        private readonly PolygonCollider2D _collider;
        private readonly Il2CppStructArray<Vector3> _nativeCorners;
        private readonly Il2CppReferenceArray<Transform> _nativeSlots;
        private readonly Il2CppStructArray<Vector3> _nativeIconPositions;
        private readonly Il2CppStructArray<int> _nativeIconOrders;
        private readonly Il2CppSystem.Collections.Generic.List<int> _nativeFreePlaces;
        private readonly int[] _freePlaces;
        private readonly Vector3 _pressPosition;
        private readonly Vector3[] _iconPositions;
        private Transform _extraPawnPlace;

        internal CellSnapshot(CellView cell)
        {
            _cell = cell;
            _pressPosition = cell._pressCellStartPosition;
            _nativeSlots = cell._slots;
            _nativeIconPositions = cell._iconVisualPositions;
            _nativeIconOrders = cell.IconVisualOrders;
            _nativeCorners = cell._cornersArray;
            _nativeFreePlaces = cell.FreePlaces;
            if (_nativeFreePlaces != null)
            {
                _freePlaces = new int[_nativeFreePlaces.Count];
                for (int index = 0; index < _freePlaces.Length; index++)
                    _freePlaces[index] = _nativeFreePlaces[index];
            }
            if (_nativeIconPositions != null)
            {
                _iconPositions = new Vector3[_nativeIconPositions.Length];
                for (int index = 0; index < _iconPositions.Length; index++)
                    _iconPositions[index] = _nativeIconPositions[index];
            }
            CaptureAnchors(cell.transform);
            if (cell._cornersArray != null)
            {
                _corners = new Vector3[cell._cornersArray.Length];
                for (int index = 0; index < _corners.Length; index++)
                    _corners[index] = cell.transform.TransformPoint(cell._cornersArray[index]);
            }
            PolygonCollider2D collider = cell._collider2D;
            _collider = collider;
            if (collider != null)
            {
                for (int path = 0; path < collider.pathCount; path++)
                {
                    var points = ReadColliderPath(collider, path);
                    var world = new Vector3[points.Length];
                    for (int index = 0; index < points.Length; index++)
                    {
                        Vector2 point = points[index] + collider.offset;
                        world[index] = collider.transform.TransformPoint(new Vector3(point.x, point.y, 0f));
                    }
                    _colliderPaths.Add(world);
                }
            }
            _renderer = cell._cellRenderer?._renderer;
            if (_renderer != null) _rendererMatrix = _renderer.transform.localToWorldMatrix;
        }

        internal void AddFifthPawnPlace()
        {
            _extraPawnPlace = EnsureFifthPawnPlace(_cell);
            if (_extraPawnPlace != null)
                _anchors.Add(new Anchor(_extraPawnPlace, _extraPawnPlace.position));
        }

        private void CaptureAnchors(Transform transform)
        {
            if (transform == null) return;
            _anchors.Add(new Anchor(transform, transform.position));
            for (int child = 0; child < transform.childCount; child++) CaptureAnchors(transform.GetChild(child));
        }

        internal void Apply(PentagonWarp warp)
        {
            // Captured world coordinates prevent a parent and child from being
            // deformed twice. Their native rotations/scales remain untouched.
            foreach (Anchor anchor in _anchors) anchor.Transform.position = warp.Map(anchor.Position);
            if (_corners != null)
            {
                for (int index = 0; index < _corners.Length; index++)
                    _cell._cornersArray[index] = _cell.transform.InverseTransformPoint(warp.Map(_corners[index]));
            }
            PolygonCollider2D collider = _collider;
            if (collider != null)
            {
                for (int path = 0; path < _colliderPaths.Count; path++)
                {
                    List<Vector3> split = warp.SplitEdges(_colliderPaths[path]);
                    var points = new Il2CppStructArray<Vector2>(split.Count);
                    for (int index = 0; index < split.Count; index++)
                    {
                        Vector3 local = collider.transform.InverseTransformPoint(warp.Map(split[index]));
                        points[index] = new Vector2(local.x, local.y) - collider.offset;
                    }
                    collider.SetPath(path, points);
                }
            }
            _cell._pressCellStartPosition = _cell.transform.position;
            if (_cell._slots != null && _cell._iconVisualPositions != null)
            {
                int count = Math.Min(_cell._slots.Length, _cell._iconVisualPositions.Length);
                for (int index = 0; index < count; index++)
                    if (_cell._slots[index] != null) _cell._iconVisualPositions[index] = _cell._slots[index].position;
            }
            if (_renderer != null) PentagonalSpriteGeometry.Add(_renderer, _rendererMatrix, warp);
        }

        internal void Restore()
        {
            foreach (Anchor anchor in _anchors)
                try { if (anchor.Transform != null) anchor.Transform.position = anchor.Position; }
                catch (Exception ex) { Plugin.ModLog.LogDebug($"Board anchor already released: {ex.Message}"); }
            if (_cell == null) return;
            // Restore the exact objects captured, rather than writing into a
            // replacement collider/array installed by a reconnect lifecycle.
            try
            {
                if (_corners != null && _nativeCorners != null)
                    for (int index = 0; index < Math.Min(_corners.Length, _nativeCorners.Length); index++)
                        _nativeCorners[index] = _cell.transform.InverseTransformPoint(_corners[index]);
                PolygonCollider2D collider = _collider;
                if (collider != null)
                    for (int path = 0; path < Math.Min(_colliderPaths.Count, collider.pathCount); path++)
                    {
                        var points = new Il2CppStructArray<Vector2>(_colliderPaths[path].Length);
                        for (int index = 0; index < points.Length; index++)
                        {
                            Vector3 local = collider.transform.InverseTransformPoint(_colliderPaths[path][index]);
                            points[index] = new Vector2(local.x, local.y) - collider.offset;
                        }
                        collider.SetPath(path, points);
                    }
            }
            catch (Exception ex) { Plugin.ModLog.LogDebug($"Board hit area already released: {ex.Message}"); }
            _cell._pressCellStartPosition = _pressPosition;
            _cell._slots = _nativeSlots;
            _cell._iconVisualPositions = _nativeIconPositions;
            _cell.IconVisualOrders = _nativeIconOrders;
            if (_iconPositions != null && _nativeIconPositions != null)
                for (int index = 0; index < _iconPositions.Length; index++)
                    _nativeIconPositions[index] = _iconPositions[index];
            if (_nativeFreePlaces != null && _freePlaces != null)
            {
                _nativeFreePlaces.Clear();
                foreach (int place in _freePlaces) _nativeFreePlaces.Add(place);
            }
            if (_extraPawnPlace != null)
            {
                _extraPawnPlace.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(_extraPawnPlace.gameObject);
                _extraPawnPlace = null;
            }
        }

        private readonly struct Anchor
        {
            internal readonly Transform Transform;
            internal readonly Vector3 Position;
            internal Anchor(Transform transform, Vector3 position) { Transform = transform; Position = position; }
        }
    }

    // Unity 6's regenerated GetPath wrapper calls Unmarshal<T>, then tries to
    // instantiate abstract Il2CppArrayBase<T>. Invoke that same native
    // unmarshal step and wrap its returned array as the concrete struct array.
    // This also lets Unity release any temporary native-owned path memory.
    private static readonly Lazy<IntPtr> PathUnmarshalMethod = new(() =>
    {
        Type wrapperType = typeof(Sprite).Assembly.GetType("UnityEngine.Bindings.BlittableArrayWrapper", true);
        Type store = wrapperType.GetNestedType(
            "MethodInfoStoreGeneric_Unmarshal_Internal_Void_byref_Il2CppArrayBase_1_T_0`1",
            BindingFlags.NonPublic)?.MakeGenericType(typeof(Vector2));
        FieldInfo field = store?.GetField("Pointer", BindingFlags.Static | BindingFlags.NonPublic);
        IntPtr pointer = field != null ? (IntPtr)field.GetValue(null) : IntPtr.Zero;
        if (pointer == IntPtr.Zero) throw new MissingMethodException("Unity collider path unmarshal method is unavailable.");
        return pointer;
    });

    [StructLayout(LayoutKind.Sequential)]
    private struct ColliderPathWrapper
    {
        internal IntPtr Data;
        internal int Size;
        internal int UpdateFlags;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GetColliderPathDelegate(IntPtr collider, int path, IntPtr wrapper);

    private static readonly Lazy<GetColliderPathDelegate> GetColliderPathNative = new(() =>
        IL2CPP.ResolveICall<GetColliderPathDelegate>("UnityEngine.PolygonCollider2D::GetPath_Internal_Injected"));

    internal static unsafe Il2CppStructArray<Vector2> ReadColliderPath(PolygonCollider2D collider, int path)
    {
        IntPtr method = PathUnmarshalMethod.Value;
        GetColliderPathDelegate getPath = GetColliderPathNative.Value;
        ColliderPathWrapper wrapper = default;
        IntPtr array = IntPtr.Zero;
        void** arguments = stackalloc void*[1];
        arguments[0] = &array;
        try
        {
            getPath(UnityEngine.Object.MarshalledUnityObject.MarshalNotNull(collider), path, (IntPtr)(&wrapper));
        }
        finally
        {
            IntPtr exception = IntPtr.Zero;
            IL2CPP.il2cpp_runtime_invoke(method, (IntPtr)(&wrapper), arguments, ref exception);
            Il2CppException.RaiseExceptionIfNecessary(exception);
        }
        return array == IntPtr.Zero ? new Il2CppStructArray<Vector2>(0) : new Il2CppStructArray<Vector2>(array);
    }
}

internal sealed class PentagonWarp
{
    internal readonly Vector2 Center;
    internal readonly Vector2[] Source;
    internal readonly Vector2[] Target;
    internal readonly bool PlaneXY;
    internal int SectorCount => Source.Length;
    private readonly float _winding;

    private PentagonWarp(Vector2 center, Vector2[] source, Vector2[] target, bool planeXY)
    {
        Center = center; Source = source; Target = target; PlaneXY = planeXY;
        _winding = Math.Sign(Cross(source[0] - center, source[1] - center));
        if (_winding == 0f) throw new InvalidOperationException("Degenerate board sector.");
        for (int index = 0; index < source.Length; index++)
        {
            int next = (index + 1) % source.Length;
            if (Cross(source[index] - center, source[next] - center) * _winding < 0.00001f ||
                Cross(target[index] - center, target[next] - center) * _winding < 0.00001f)
                throw new InvalidOperationException($"Board sector {index} would overlap or fold.");
        }
    }

    internal static PentagonWarp Create(Vector3[] positions)
    {
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
        float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
        foreach (Vector3 point in positions)
        {
            minX = Mathf.Min(minX, point.x); maxX = Mathf.Max(maxX, point.x);
            minY = Mathf.Min(minY, point.y); maxY = Mathf.Max(maxY, point.y);
            minZ = Mathf.Min(minZ, point.z); maxZ = Mathf.Max(maxZ, point.z);
        }
        bool planeXY = maxY - minY >= maxZ - minZ;
        if (maxX - minX < 0.01f || (planeXY ? maxY - minY : maxZ - minZ) < 0.01f)
            throw new InvalidOperationException("Degenerate board bounds.");
        var native = new Vector2[positions.Length];
        for (int index = 0; index < positions.Length; index++)
            native[index] = new Vector2(positions[index].x, planeXY ? positions[index].y : positions[index].z);
        Vector2[] source = FitNativeRing(native, out Vector2 center);
        var corners = new Vector2[4];
        minX = minY = float.PositiveInfinity; maxX = maxY = float.NegativeInfinity;
        for (int index = 0; index < 4; index++)
        {
            corners[index] = source[index * source.Length / 4];
            minX = Mathf.Min(minX, corners[index].x); maxX = Mathf.Max(maxX, corners[index].x);
            minY = Mathf.Min(minY, corners[index].y); maxY = Mathf.Max(maxY, corners[index].y);
        }
        float width = maxX - minX, height = maxY - minY;
        // The former 8/8/4/4/8 layout merely pushed one square-side midpoint
        // outwards. It stretched a few tiles while retaining four long sides.
        // Instead distribute all cells over five equal sides (32 => 7/6/7/6/6).
        // Fan knots follow one canonical four-sided ring, not individually
        // trimmed sprite bounds. A raw-centre fan introduced slope kinks at
        // oversized old corner cells, making the continuous inner/outer square
        // boundaries look like notches and spikes. All homothetic reference
        // rings now map to the same straight-edged pentagon, at every radius.
        // Sprite meshes and colliders are still split on identical boundaries.
        var target = new Vector2[positions.Length];
        var pentagon = new Vector2[5];
        var sideCounts = new int[5];
        int[] regionStarts = FiveRealmsLayoutRules.RegionStarts(positions.Length);
        float winding = Math.Sign(Cross(corners[0] - center, corners[1] - center));
        if (winding == 0f) throw new InvalidOperationException("Degenerate native board winding.");
        // Native boards are isometric artwork in XY, not a tilted 3D plane.
        // Remove small mixed-prefab pivot skews while retaining that projection.
        // The first corner remains the lower start tile, in the same direction
        // as the original cell order; this does not change any board graph IDs.
        float radiusX = width * 0.5f * 1.04f;
        float radiusY = height * 0.5f * 1.04f;
        float initialAngle = Mathf.Atan2((corners[0].y - center.y) / radiusY,
            (corners[0].x - center.x) / radiusX);
        float cardinalAngle = Mathf.Round(initialAngle / (Mathf.PI * 0.5f)) * Mathf.PI * 0.5f;
        if (Mathf.Abs(initialAngle - cardinalAngle) < Mathf.PI / 12f)
            initialAngle = cardinalAngle;
        for (int side = 0; side < 5; side++)
        {
            float angle = initialAngle + winding * side * Mathf.PI * 2f / 5f;
            pentagon[side] = center + new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
            sideCounts[side] = regionStarts[side + 1] - regionStarts[side];
        }
        if (!IsConvex(pentagon)) throw new InvalidOperationException("The five-sided target is not convex.");
        int offset = 0;
        for (int side = 0; side < 5; side++)
        {
            for (int step = 0; step < sideCounts[side]; step++)
                target[offset + step] = Vector2.LerpUnclamped(pentagon[side], pentagon[(side + 1) % 5],
                    step / (float)sideCounts[side]);
            offset += sideCounts[side];
        }
        Plugin.ModLog.LogInfo($"Balanced regular isometric pentagon: {positions.Length} cells, sides {string.Join("/", sideCounts)}, plane={(planeXY ? "XY" : "XZ")}; continuous {source.Length}-sector ground/collider warp.");
        return new PentagonWarp(center, source, target, planeXY);
    }

    private static Vector2[] FitNativeRing(Vector2[] samples, out Vector2 center)
    {
        // The regular native side cells form an affine square. Least-squares
        // fitting C + X*qx + Y*qy removes regional crop/pivot jitter. Exclude
        // the four special corner sprites: their bounds have larger footprints
        // and are not representative of the ring's centre line.
        int sideLength = samples.Length / 4, count = 0;
        Vector2 centerSum = Vector2.zero, xSum = Vector2.zero, ySum = Vector2.zero;
        float xWeight = 0f, yWeight = 0f;
        for (int index = 0; index < samples.Length; index++)
        {
            if (index % sideLength == 0) continue;
            Vector2 coordinate = SquareCoordinate(index, sideLength);
            centerSum += samples[index]; count++;
            xSum += samples[index] * coordinate.x;
            ySum += samples[index] * coordinate.y;
            xWeight += coordinate.x * coordinate.x;
            yWeight += coordinate.y * coordinate.y;
        }
        if (count < 4 || xWeight < 0.0001f || yWeight < 0.0001f)
            throw new InvalidOperationException("Insufficient native cells for the square reference grid.");
        // Symmetric samples on all four sides have sum(q)=sum(qx*qy)=0,
        // so the three least-squares normal equations are diagonal.
        center = centerSum / count;
        Vector2 xAxis = xSum / xWeight, yAxis = ySum / yWeight;
        if (Mathf.Abs(Cross(xAxis, yAxis)) < 0.0001f)
            throw new InvalidOperationException("Degenerate native square reference grid.");
        var fitted = new Vector2[samples.Length];
        float maxResidual = 0f;
        for (int index = 0; index < samples.Length; index++)
        {
            Vector2 coordinate = SquareCoordinate(index, sideLength);
            fitted[index] = center + xAxis * coordinate.x + yAxis * coordinate.y;
            if (index % sideLength != 0)
                maxResidual = Mathf.Max(maxResidual, (samples[index] - fitted[index]).magnitude);
        }
        Plugin.ModLog.LogInfo($"Native square reference fitted from {count} non-corner ground sprites: X=({xAxis.x:F3},{xAxis.y:F3}), Y=({yAxis.x:F3},{yAxis.y:F3}), max crop/pivot residual={maxResidual:F3}.");
        return fitted;
    }

    private static Vector2 SquareCoordinate(int index, int sideLength)
    {
        int side = index / sideLength;
        float fraction = (index % sideLength) / (float)sideLength;
        return side switch
        {
            0 => new Vector2(-fraction, -1f + fraction),
            1 => new Vector2(-1f + fraction, fraction),
            2 => new Vector2(fraction, 1f - fraction),
            _ => new Vector2(1f - fraction, -fraction)
        };
    }

    private static bool IsConvex(Vector2[] polygon)
    {
        float sign = 0f;
        for (int index = 0; index < polygon.Length; index++)
        {
            Vector2 a = polygon[index], b = polygon[(index + 1) % polygon.Length], c = polygon[(index + 2) % polygon.Length];
            float turn = Cross(b - a, c - b);
            if (Math.Abs(turn) < 0.00001f) return false;
            if (sign == 0f) sign = Math.Sign(turn);
            else if (turn * sign <= 0f) return false;
        }
        return true;
    }

    internal Vector2 Planar(Vector3 point) => new(point.x, PlaneXY ? point.y : point.z);

    internal Vector3 Map(Vector3 point)
    {
        Vector2 relative = Planar(point) - Center;
        int sector = 0;
        for (int index = 0; index < Source.Length; index++)
        {
            Vector2 a = Source[index] - Center, b = Source[(index + 1) % Source.Length] - Center;
            if (Cross(a, relative) * _winding >= -0.0001f && Cross(relative, b) * _winding >= -0.0001f)
            { sector = index; break; }
        }
        int next = (sector + 1) % Source.Length;
        Vector2 first = Source[sector] - Center, second = Source[next] - Center;
        float denominator = Cross(first, second);
        float u = Cross(relative, second) / denominator, v = Cross(first, relative) / denominator;
        Vector2 mapped = Center + (Target[sector] - Center) * u + (Target[next] - Center) * v;
        return PlaneXY ? new Vector3(mapped.x, mapped.y, point.z) : new Vector3(mapped.x, point.y, mapped.y);
    }

    internal float HalfPlane(Vector3 point, int sector, bool first)
    {
        Vector2 relative = Planar(point) - Center;
        return first
            ? Cross(Source[sector] - Center, relative) * _winding
            : Cross(relative, Source[(sector + 1) % Source.Length] - Center) * _winding;
    }

    internal List<Vector3> SplitEdges(Vector3[] polygon)
    {
        var result = new List<Vector3>();
        for (int index = 0; index < polygon.Length; index++)
        {
            Vector3 a = polygon[index], b = polygon[(index + 1) % polygon.Length];
            var cuts = new List<float> { 0f };
            Vector2 start = Planar(a) - Center, edge = Planar(b) - Planar(a);
            foreach (Vector2 boundary in Source)
            {
                Vector2 ray = boundary - Center;
                float divisor = Cross(edge, ray);
                if (Math.Abs(divisor) < 0.000001f) continue;
                float t = -Cross(start, ray) / divisor;
                if (t > 0.0001f && t < 0.9999f && Vector2.Dot(start + edge * t, ray) >= 0f) cuts.Add(t);
            }
            cuts.Sort();
            float previous = -1f;
            foreach (float cut in cuts)
            {
                if (cut - previous <= 0.0001f) continue;
                result.Add(Vector3.LerpUnclamped(a, b, cut));
                previous = cut;
            }
        }
        return result;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
}

internal static class PentagonalSpriteGeometry
{
    private static readonly Dictionary<IntPtr, SpriteProxy> Proxies = new();
    private static Camera _fittedCamera;
    private static Vector3 _originalCameraPosition;
    private static float _originalCameraSize;
    internal static PentagonWarp ActiveWarp { get; set; }

    internal static void Add(SpriteRenderer renderer, Matrix4x4 sourceMatrix, PentagonWarp warp, bool isBackground = false)
    {
        if (renderer == null || renderer.sprite == null) return;
        if (Proxies.TryGetValue(renderer.Pointer, out SpriteProxy existing))
        {
            if (existing.IsAlive) return;
            existing.Dispose();
            Proxies.Remove(renderer.Pointer);
        }
        var proxy = new SpriteProxy(renderer, sourceMatrix, warp, isBackground);
        Proxies.Add(renderer.Pointer, proxy);
    }

    internal static void Reset()
    {
        CanonicalBoardPresentation.Restore();
        PentagonalBoardPatch.RestoreApplied();
        ActiveWarp = null;
        foreach (SpriteProxy proxy in Proxies.Values)
            try { proxy.Dispose(true); }
            catch (Exception ex) { Plugin.ModLog.LogDebug($"Board sprite already released: {ex.Message}"); }
        Proxies.Clear();
        try
        {
            if (_fittedCamera != null)
            {
                _fittedCamera.transform.position = _originalCameraPosition;
                _fittedCamera.orthographicSize = _originalCameraSize;
            }
        }
        catch (Exception ex) { Plugin.ModLog.LogDebug($"Board camera already released: {ex.Message}"); }
        _fittedCamera = null;
    }

    internal static void FitCamera(Camera camera)
    {
        if (camera == null || !camera.orthographic) return;
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity, maxY = float.NegativeInfinity;
        float aspect = Mathf.Max(0.1f, camera.aspect);
        foreach (SpriteProxy proxy in Proxies.Values)
        {
            if (!proxy.IsAlive || proxy.IsBackground) continue;
            Bounds bounds = proxy.Bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = bounds.center + new Vector3(
                    (corner & 1) == 0 ? -bounds.extents.x : bounds.extents.x,
                    (corner & 2) == 0 ? -bounds.extents.y : bounds.extents.y,
                    (corner & 4) == 0 ? -bounds.extents.z : bounds.extents.z);
                Vector3 local = camera.transform.InverseTransformPoint(point);
                minX = Mathf.Min(minX, local.x); maxX = Mathf.Max(maxX, local.x);
                minY = Mathf.Min(minY, local.y); maxY = Mathf.Max(maxY, local.y);
            }
        }
        if (float.IsInfinity(minX)) return;
        if (_fittedCamera == null || _fittedCamera.Pointer != camera.Pointer)
        {
            _fittedCamera = camera;
            _originalCameraPosition = camera.transform.position;
            _originalCameraSize = camera.orthographicSize;
        }
        // Keep the board out of the five-seat rail and the top-right native
        // toolbar. The extra world-space margin accommodates upright buildings
        // whose sprite heights are intentionally not flattened by the warp.
        const float left = 0.20f, right = 0.03f, top = 0.12f, bottom = 0.08f;
        const float artMargin = 0.65f;
        float boardHalfX = (maxX - minX) * 0.5f + artMargin;
        float boardHalfY = (maxY - minY) * 0.5f + artMargin;
        float required = Mathf.Max(boardHalfX / (aspect * (1f - left - right)),
            boardHalfY / (1f - top - bottom));
        float viewportCenterX = (left + 1f - right) * 0.5f;
        float viewportCenterY = (bottom + 1f - top) * 0.5f;
        float shiftX = (minX + maxX) * 0.5f - (viewportCenterX - 0.5f) * 2f * required * aspect;
        float shiftY = (minY + maxY) * 0.5f - (viewportCenterY - 0.5f) * 2f * required;
        camera.transform.position += camera.transform.right * shiftX + camera.transform.up * shiftY;
        camera.orthographicSize = required;
        Plugin.ModLog.LogDebug($"Five-sided camera fit size={required:F2}, HUD rail={left:P0}, toolbar margin={top:P0}.");
    }

    internal static void Sync(CellRenderer renderer)
    {
        CanonicalBoardPresentation.Sync(renderer);
        SpriteRenderer native = renderer?._renderer;
        if (native != null && Proxies.TryGetValue(native.Pointer, out SpriteProxy proxy)) proxy.Sync();
    }

    internal static void SyncAll()
    {
        CanonicalBoardPresentation.SyncAll();
        var stale = new List<IntPtr>();
        foreach (var entry in Proxies)
        {
            if (entry.Value.IsAlive) entry.Value.Sync();
            else stale.Add(entry.Key);
        }
        foreach (IntPtr key in stale)
        {
            Proxies[key].Dispose();
            Proxies.Remove(key);
        }
    }

    internal static void WarpBackground(GameView view)
    {
        if (!ModState.IsSpecialMapActive || ActiveWarp == null) return;
        SpriteRenderer renderer = view?._background?._backgroundSprite;
        if (renderer != null && !Proxies.ContainsKey(renderer.Pointer))
        {
            LogBackgroundRenderers(view._background.transform);
            Add(renderer, renderer.transform.localToWorldMatrix, ActiveWarp, true);
            Plugin.ModLog.LogInfo($"Applied matching five-sided warp to background sprite {renderer.name}.");
        }
    }

    private static void LogBackgroundRenderers(Transform node)
    {
        if (node == null) return;
        SpriteRenderer sprite = node.GetComponent<SpriteRenderer>();
        if (sprite != null)
            Plugin.ModLog.LogInfo($"Native background sprite {node.name}: sprite={sprite.sprite?.name}, position={node.position}, bounds={sprite.bounds.center}/{sprite.bounds.size}, active={node.gameObject.activeInHierarchy}.");
        for (int child = 0; child < node.childCount; child++) LogBackgroundRenderers(node.GetChild(child));
    }

    private sealed class SpriteProxy
    {
        private readonly SpriteRenderer _native;
        private readonly MeshRenderer _renderer;
        private readonly MeshFilter _filter;
        private readonly Material _material;
        private readonly MaterialPropertyBlock _properties;
        private readonly Matrix4x4 _sourceMatrix;
        private readonly Matrix4x4 _warpedInverse;
        private readonly PentagonWarp _warp;
        private readonly GameObject _holder;
        private readonly bool _originalForceRenderingOff;
        private IntPtr _spritePointer;
        private IntPtr _materialPointer;
        private bool _flipX;
        private bool _flipY;
        private Mesh _mesh;
        internal bool IsAlive => _native != null && _renderer != null;
        internal bool IsBackground { get; }
        internal Bounds Bounds => _renderer.bounds;

        internal void Dispose(bool restoreNative = false)
        {
            // Scene teardown can destroy Unity objects between callbacks.
            // Cleanup remains idempotent and must never interrupt leaving a room.
            try { if (restoreNative && _native != null) _native.forceRenderingOff = _originalForceRenderingOff; } catch { }
            try { if (_holder != null) { _holder.SetActive(false); UnityEngine.Object.Destroy(_holder); } } catch { }
            try { if (_mesh != null) UnityEngine.Object.Destroy(_mesh); } catch { }
            try { if (_material != null) UnityEngine.Object.Destroy(_material); } catch { }
        }

        internal SpriteProxy(SpriteRenderer native, Matrix4x4 sourceMatrix, PentagonWarp warp, bool isBackground)
        {
            _native = native; _sourceMatrix = sourceMatrix; _warp = warp;
            _originalForceRenderingOff = native.forceRenderingOff;
            IsBackground = isBackground;
            _warpedInverse = native.transform.worldToLocalMatrix;
            if (native.drawMode != SpriteDrawMode.Simple)
                throw new InvalidOperationException($"Cannot warp non-simple sprite {native.name} safely.");
            var holder = new GameObject("FiveRealmsWarpedGround");
            _holder = holder;
            holder.layer = native.gameObject.layer;
            holder.transform.SetParent(native.transform, false);
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.identity;
            holder.transform.localScale = Vector3.one;
            _filter = holder.AddComponent<MeshFilter>();
            _renderer = holder.AddComponent<MeshRenderer>();
            _properties = new MaterialPropertyBlock();
            try
            {
                Material source = native.sharedMaterial;
                Shader shader = source != null ? source.shader : Shader.Find("Sprites/Default");
                if (shader == null) throw new InvalidOperationException("No sprite-compatible ground shader was found.");
                _material = source != null ? new Material(source) : new Material(shader);
                _materialPointer = source?.Pointer ?? IntPtr.Zero;
                _renderer.sharedMaterial = _material;
                _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _renderer.receiveShadows = false;
                _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                _renderer.renderingLayerMask = native.renderingLayerMask;
                Sync();
            }
            catch
            {
                Dispose(true);
                throw;
            }
        }

        internal void Sync()
        {
            if (!IsAlive) return;
            Sprite sprite = _native.sprite;
            if (sprite == null) { _renderer.enabled = false; return; }
            if (_spritePointer != sprite.Pointer || _flipX != _native.flipX || _flipY != _native.flipY)
            {
                Rebuild(sprite);
                _spritePointer = sprite.Pointer;
                _flipX = _native.flipX;
                _flipY = _native.flipY;
            }
            Material source = _native.sharedMaterial;
            if (source != null && source.Pointer != _materialPointer)
            {
                _material.shader = source.shader;
                _material.CopyPropertiesFromMaterial(source);
                _materialPointer = source.Pointer;
                _material.mainTexture = sprite.texture;
            }
            // SpriteRenderer supplies these values implicitly; MeshRenderer
            // does not. Preserve custom game properties, then explicitly set
            // sprite texture/tint and avoid applying flip a second time.
            _properties.Clear();
            _native.GetPropertyBlock(_properties);
            _properties.SetTexture("_MainTex", sprite.texture);
            _properties.SetColor("_RendererColor", _native.color);
            _properties.SetVector("_Flip", Vector4.one);
            _renderer.SetPropertyBlock(_properties);
            _renderer.sortingLayerID = _native.sortingLayerID;
            _renderer.sortingOrder = _native.sortingOrder;
            _renderer.enabled = _native.enabled && !_originalForceRenderingOff;
            _native.forceRenderingOff = true;
        }

        private void Rebuild(Sprite sprite)
        {
            var nativeVertices = sprite.vertices;
            var nativeUv = sprite.uv;
            var nativeTriangles = sprite.triangles;
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            var source = new Vertex[nativeVertices.Length];
            for (int index = 0; index < source.Length; index++)
            {
                Vector2 point = nativeVertices[index];
                if (_native.flipX) point.x = -point.x;
                if (_native.flipY) point.y = -point.y;
                source[index] = new Vertex(_sourceMatrix.MultiplyPoint3x4(new Vector3(point.x, point.y, 0f)), nativeUv[index]);
            }
            for (int index = 0; index + 2 < nativeTriangles.Length; index += 3)
            {
                for (int sector = 0; sector < _warp.SectorCount; sector++)
                {
                    var polygon = new List<Vertex>
                    {
                        source[nativeTriangles[index]], source[nativeTriangles[index + 1]], source[nativeTriangles[index + 2]]
                    };
                    polygon = Clip(Clip(polygon, sector, true), sector, false);
                    if (polygon.Count < 3) continue;
                    int start = vertices.Count;
                    foreach (Vertex vertex in polygon)
                    {
                        vertices.Add(_warpedInverse.MultiplyPoint3x4(_warp.Map(vertex.Position)));
                        uv.Add(vertex.Uv);
                    }
                    for (int face = 1; face + 1 < polygon.Count; face++)
                    { triangles.Add(start); triangles.Add(start + face); triangles.Add(start + face + 1); }
                }
            }
            var mesh = new Mesh { name = "FiveRealmsGroundMesh" };
            mesh.vertices = new Il2CppStructArray<Vector3>(vertices.ToArray());
            mesh.uv = new Il2CppStructArray<Vector2>(uv.ToArray());
            var colors = new Il2CppStructArray<Color>(vertices.Count);
            for (int index = 0; index < colors.Length; index++) colors[index] = Color.white;
            mesh.colors = colors;
            mesh.triangles = new Il2CppStructArray<int>(triangles.ToArray());
            mesh.RecalculateBounds();
            _filter.sharedMesh = mesh;
            if (_mesh != null) UnityEngine.Object.Destroy(_mesh);
            _mesh = mesh;
            _material.mainTexture = sprite.texture;
        }

        private List<Vertex> Clip(List<Vertex> polygon, int sector, bool first)
        {
            var result = new List<Vertex>();
            if (polygon.Count == 0) return result;
            Vertex previous = polygon[polygon.Count - 1];
            float previousDistance = _warp.HalfPlane(previous.Position, sector, first);
            foreach (Vertex current in polygon)
            {
                float distance = _warp.HalfPlane(current.Position, sector, first);
                bool previousInside = previousDistance >= -0.000001f, inside = distance >= -0.000001f;
                if (previousInside != inside)
                {
                    float t = previousDistance / (previousDistance - distance);
                    result.Add(new Vertex(Vector3.LerpUnclamped(previous.Position, current.Position, t), Vector2.LerpUnclamped(previous.Uv, current.Uv, t)));
                }
                if (inside) result.Add(current);
                previous = current; previousDistance = distance;
            }
            return result;
        }

        private readonly struct Vertex
        {
            internal readonly Vector3 Position;
            internal readonly Vector2 Uv;
            internal Vertex(Vector3 position, Vector2 uv) { Position = position; Uv = uv; }
        }
    }
}

[HarmonyPatch(typeof(GameView), nameof(GameView.OnLocationLoaded))]
internal static class PentagonalBackgroundPatch
{
    private static void Postfix(GameView __instance)
    {
        try
        {
            if (CanonicalBoardPresentation.IsActive) CanonicalBoardPresentation.ReplaceBackground(__instance);
            else PentagonalSpriteGeometry.WarpBackground(__instance);
        }
        catch (Exception ex) { Plugin.ModLog.LogWarning($"Five-sided background warp failed: {ex.Message}"); }
    }
}

[HarmonyPatch]
internal static class PentagonalSpriteSyncPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(CellRenderer), "set_Sprite");
        yield return AccessTools.Method(typeof(CellRenderer), "BusinessTour_IBlackAndWhiteView_SetBlackAndWhite");
        yield return AccessTools.Method(typeof(CellRenderer), "BusinessTour_IOrderableView_SetOrder");
    }

    private static void Postfix(CellRenderer __instance)
    {
        if (!ModState.IsSpecialMapActive) return;
        try { PentagonalSpriteGeometry.Sync(__instance); }
        catch (Exception ex) { Plugin.ModLog.LogWarning($"Could not synchronize five-sided ground art: {ex.Message}"); }
    }
}
