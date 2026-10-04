using System;
using System.Collections.Generic;
using System.Reflection;
using BusinessTour;
using HarmonyLib;
using UnityEngine;

namespace BusinessTourFiveRealms;

/// <summary>
/// The vanilla renderer lays all cells out as a four-sided loop.  Five Realms
/// keeps the native cell graph and movement rules, then moves the instantiated
/// views onto a closed five-sided perimeter.  That keeps networking based on
/// cell ids while making the fifth region a real side/corner on every client.
/// </summary>
[HarmonyPatch]
internal static class PentagonalBoardPatch
{
    private static MethodBase TargetMethod() =>
        AccessTools.Method(typeof(MapView), "BusinessTour_IMapView_Initialize");

    private static void Postfix(MapView __instance, ICameraService cameraService)
    {
        if (!ModState.IsSpecialMapActive || __instance == null)
        {
            return;
        }

        try
        {
            List<ICellView> cells = GetCellsInIdOrder(__instance);
            if (cells.Count < 5)
            {
                Plugin.ModLog.LogWarning("Five-sided layout skipped because fewer than five cell views were created.");
                return;
            }

            ApplyFiveSidedLayout(cells);
            __instance.SortCellsByOrder();
            cameraService?.SetCameraToDefaultOnBoardSettings(__instance.MapSize);
            Plugin.ModLog.LogInfo($"Applied five-sided board geometry to {cells.Count} cells.");
        }
        catch (Exception ex)
        {
            // The native map remains playable if a future build changes its
            // view internals; geometry must never take down room creation.
            Plugin.ModLog.LogError($"Five-sided board layout failed; keeping the native layout: {ex}");
        }
    }

    private static List<ICellView> GetCellsInIdOrder(MapView mapView)
    {
        var result = new List<KeyValuePair<int, ICellView>>();
        var ids = mapView.CellsViewIds;
        if (ids == null)
        {
            return new List<ICellView>();
        }

        for (int index = 0; index < ids.Length; index++)
        {
            int id = ids[index];
            ICellView view = mapView.GetCellViewById(id);
            if (view != null)
            {
                result.Add(new KeyValuePair<int, ICellView>(id, view));
            }
        }

        result.Sort((left, right) => left.Key.CompareTo(right.Key));
        var ordered = new List<ICellView>(result.Count);
        for (int index = 0; index < result.Count; index++)
        {
            ordered.Add(result[index].Value);
        }
        return ordered;
    }

    private static void ApplyFiveSidedLayout(IReadOnlyList<ICellView> cells)
    {
        int count = cells.Count;
        var oldPositions = new Vector3[count];
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;

        for (int index = 0; index < count; index++)
        {
            Vector3 position = cells[index].Position;
            oldPositions[index] = position;
            minX = Mathf.Min(minX, position.x);
            maxX = Mathf.Max(maxX, position.x);
            minY = Mathf.Min(minY, position.y);
            maxY = Mathf.Max(maxY, position.y);
        }

        float width = maxX - minX;
        float height = maxY - minY;
        if (width < 0.01f || height < 0.01f)
        {
            throw new InvalidOperationException("Native board bounds are degenerate.");
        }

        float centerX = (minX + maxX) * 0.5f;
        float centerY = (minY + maxY) * 0.5f;
        Vector2[] unitVertices = BuildPointedPentagon();
        Bounds2D unitBounds = Measure(unitVertices);
        float radiusX = width / unitBounds.Width;
        float radiusY = height / unitBounds.Height;

        var vertices = new Vector2[5];
        for (int index = 0; index < vertices.Length; index++)
        {
            vertices[index] = new Vector2(
                centerX + (unitVertices[index].x - unitBounds.CenterX) * radiusX,
                centerY + (unitVertices[index].y - unitBounds.CenterY) * radiusY);
        }

        int[] cornerCells = BuildCornerCells(count);
        var newPositions = new Vector3[count];
        for (int side = 0; side < 5; side++)
        {
            int start = cornerCells[side];
            int end = cornerCells[side + 1];
            int intervals = end - start;
            for (int rawIndex = start; rawIndex < end; rawIndex++)
            {
                int cellIndex = rawIndex % count;
                float progress = (rawIndex - start) / (float)intervals;
                Vector2 point = Vector2.Lerp(vertices[side], vertices[(side + 1) % 5], progress);
                newPositions[cellIndex] = new Vector3(point.x, point.y, oldPositions[cellIndex].z);
            }
        }

        for (int index = 0; index < count; index++)
        {
            ICellView cell = cells[index];
            Transform transform = cell.Transform;
            Vector3 oldTangent = oldPositions[(index + 1) % count] - oldPositions[index];
            Vector3 newTangent = newPositions[(index + 1) % count] - newPositions[index];

            cell.Position = newPositions[index];
            if (transform != null && oldTangent.sqrMagnitude > 0.0001f && newTangent.sqrMagnitude > 0.0001f)
            {
                float oldAngle = Mathf.Atan2(oldTangent.y, oldTangent.x) * Mathf.Rad2Deg;
                float newAngle = Mathf.Atan2(newTangent.y, newTangent.x) * Mathf.Rad2Deg;
                float delta = Mathf.DeltaAngle(oldAngle, newAngle);
                transform.rotation = Quaternion.AngleAxis(delta, Vector3.forward) * transform.rotation;
            }
        }

        // CellView.Initialize cached the child player-slot positions before the
        // board was reshaped. Refresh only that cache; invoking Initialize a
        // second time would duplicate native subscriptions/setup.
        for (int index = 0; index < count; index++)
        {
            CellView concrete = new CellView(cells[index].Pointer);
            if (concrete._slots == null || concrete._iconVisualPositions == null)
            {
                continue;
            }
            int slotCount = Math.Min(concrete._slots.Length, concrete._iconVisualPositions.Length);
            for (int slot = 0; slot < slotCount; slot++)
            {
                if (concrete._slots[slot] != null)
                {
                    concrete._iconVisualPositions[slot] = concrete._slots[slot].position;
                }
            }
        }
    }

    private static Vector2[] BuildPointedPentagon()
    {
        // One vertex points upward, creating the requested diamond-like
        // silhouette while remaining an actual five-corner polygon.
        var vertices = new Vector2[5];
        for (int index = 0; index < vertices.Length; index++)
        {
            float radians = (90f - index * 72f) * Mathf.Deg2Rad;
            vertices[index] = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }
        return vertices;
    }

    private static int[] BuildCornerCells(int count)
    {
        var corners = new int[6];
        for (int corner = 0; corner <= 5; corner++)
        {
            corners[corner] = Mathf.RoundToInt(corner * count / 5f);
        }
        corners[0] = 0;
        corners[5] = count;
        return corners;
    }

    private static Bounds2D Measure(IReadOnlyList<Vector2> points)
    {
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        for (int index = 0; index < points.Count; index++)
        {
            minX = Mathf.Min(minX, points[index].x);
            maxX = Mathf.Max(maxX, points[index].x);
            minY = Mathf.Min(minY, points[index].y);
            maxY = Mathf.Max(maxY, points[index].y);
        }
        return new Bounds2D(minX, maxX, minY, maxY);
    }

    private readonly struct Bounds2D
    {
        internal readonly float Width;
        internal readonly float Height;
        internal readonly float CenterX;
        internal readonly float CenterY;

        internal Bounds2D(float minX, float maxX, float minY, float maxY)
        {
            Width = maxX - minX;
            Height = maxY - minY;
            CenterX = (minX + maxX) * 0.5f;
            CenterY = (minY + maxY) * 0.5f;
        }
    }
}
