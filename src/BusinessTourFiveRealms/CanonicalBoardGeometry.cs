using System;
using System.Collections.Generic;
using System.Numerics;

namespace BusinessTourFiveRealms;

// The logical board graph and the rendered tile footprints are independent.
// These shared polygon boundaries replace the four-side prefab outlines only
// for the special map. This class deliberately has no Unity/game dependencies.
internal sealed class CanonicalBoardGeometry
{
    internal const float DefaultInnerScale = 0.72f;
    internal const int MaximumCellCount = 65535;

    internal Vector2 Center { get; }
    internal float RadiusX { get; }
    internal float RadiusY { get; }
    internal float InnerScale { get; }
    internal int Winding { get; }
    internal int CellCount => Cells.Count;
    internal IReadOnlyList<int> RegionStarts { get; }
    internal IReadOnlyList<Vector2> OuterCorners { get; }
    internal IReadOnlyList<Vector2> InnerCorners { get; }
    internal IReadOnlyList<Vector2> OuterBoundary { get; }
    internal IReadOnlyList<Vector2> InnerBoundary { get; }
    internal IReadOnlyList<CanonicalBoardCell> Cells { get; }

    // The five-vertex outlines are suitable for a filled field/board backdrop.
    // The segmented extrusion outline matches every visible tile edge exactly.
    internal IReadOnlyList<Vector2> OuterOutline => OuterCorners;
    internal IReadOnlyList<Vector2> InnerOutline => InnerCorners;
    internal IReadOnlyList<Vector2> ExtrusionOutline => OuterBoundary;

    private CanonicalBoardGeometry(Vector2 center, float radiusX, float radiusY,
        float innerScale, int winding, int[] starts, Vector2[] outerCorners,
        Vector2[] innerCorners, Vector2[] outerBoundary, Vector2[] innerBoundary,
        CanonicalBoardCell[] cells)
    {
        Center = center;
        RadiusX = radiusX;
        RadiusY = radiusY;
        InnerScale = innerScale;
        Winding = winding;
        RegionStarts = Array.AsReadOnly(starts);
        OuterCorners = Array.AsReadOnly(outerCorners);
        InnerCorners = Array.AsReadOnly(innerCorners);
        OuterBoundary = Array.AsReadOnly(outerBoundary);
        InnerBoundary = Array.AsReadOnly(innerBoundary);
        Cells = Array.AsReadOnly(cells);
    }

    internal static CanonicalBoardGeometry Create(Vector2 center, float radiusX,
        float radiusY, int cellCount, int winding,
        float innerScale = DefaultInnerScale)
    {
        if (!IsFinite(center))
            throw new ArgumentException("The board center must be finite.", nameof(center));
        if (!float.IsFinite(radiusX) || radiusX <= 0f)
            throw new ArgumentOutOfRangeException(nameof(radiusX), "The horizontal radius must be finite and positive.");
        if (!float.IsFinite(radiusY) || radiusY <= 0f)
            throw new ArgumentOutOfRangeException(nameof(radiusY), "The vertical radius must be finite and positive.");
        if (cellCount < FiveRealmsLayoutRules.RegionCount || cellCount > MaximumCellCount)
            throw new ArgumentOutOfRangeException(nameof(cellCount),
                $"A five-sided board requires 5..{MaximumCellCount} cells.");
        if (winding != -1 && winding != 1)
            throw new ArgumentOutOfRangeException(nameof(winding), "The graph winding must be -1 or +1.");
        if (!float.IsFinite(innerScale) || innerScale <= 0f || innerScale >= 1f)
            throw new ArgumentOutOfRangeException(nameof(innerScale), "The inner scale must be strictly between zero and one.");

        int[] starts = FiveRealmsLayoutRules.RegionStarts(cellCount);
        int sides = FiveRealmsLayoutRules.RegionCount;
        var outerCorners = new Vector2[sides];
        var innerCorners = new Vector2[sides];
        for (int side = 0; side < sides; side++)
        {
            double angle = -Math.PI * 0.5d + winding * side * Math.PI * 2d / sides;
            // Pin the start boundary to the lower point without cosine noise.
            outerCorners[side] = side == 0
                ? new Vector2(center.X, center.Y - radiusY)
                : center + new Vector2((float)(Math.Cos(angle) * radiusX),
                    (float)(Math.Sin(angle) * radiusY));
            innerCorners[side] = center + (outerCorners[side] - center) * innerScale;
            if (!IsFinite(outerCorners[side]) || !IsFinite(innerCorners[side]))
                throw new InvalidOperationException($"Five-sided corner {side} overflowed the render coordinate range.");
        }

        var outerBoundary = new Vector2[cellCount];
        var innerBoundary = new Vector2[cellCount];
        var cells = new CanonicalBoardCell[cellCount];
        for (int side = 0; side < sides; side++)
        {
            int nextSide = (side + 1) % sides;
            int count = starts[side + 1] - starts[side];
            if (count <= 0)
                throw new InvalidOperationException($"Five-sided region {side} has no cells.");
            for (int step = 0; step < count; step++)
            {
                int index = starts[side] + step;
                float fraction = step / (float)count;
                // Generate each shared endpoint once. Cell meshes, backdrop
                // and hit areas all consume these exact same values, including
                // the final-to-first seam. Corner boundaries use exact corners.
                outerBoundary[index] = step == 0 ? outerCorners[side]
                    : Vector2.Lerp(outerCorners[side], outerCorners[nextSide], fraction);
                innerBoundary[index] = step == 0 ? innerCorners[side]
                    : center + (outerBoundary[index] - center) * innerScale;
            }
        }
        for (int side = 0; side < sides; side++)
        {
            int count = starts[side + 1] - starts[side];
            for (int step = 0; step < count; step++)
            {
                int index = starts[side] + step;
                int next = (index + 1) % cellCount;
                cells[index] = new CanonicalBoardCell(index, side, step, count,
                    innerBoundary[index], outerBoundary[index], outerBoundary[next],
                    innerBoundary[next], winding);
            }
        }

        var geometry = new CanonicalBoardGeometry(center, radiusX, radiusY,
            innerScale, winding, starts, outerCorners, innerCorners,
            outerBoundary, innerBoundary, cells);
        geometry.Validate();
        return geometry;
    }

    // Fail closed before the caller hides any native renderers. A folded or
    // collapsed tile must never be installed as a supposedly successful map.
    internal void Validate()
    {
        double scaleArea = (double)RadiusX * RadiusY;
        double minimumCellArea = scaleArea * 1e-8d / CellCount;
        double outerArea = SignedArea(OuterCorners);
        double innerArea = SignedArea(InnerCorners);
        if (!double.IsFinite(outerArea) || outerArea * Winding <= 0d ||
            !double.IsFinite(innerArea) || innerArea * Winding <= 0d ||
            Math.Abs(innerArea) >= Math.Abs(outerArea))
            throw new InvalidOperationException("The five-sided board outlines are degenerate or have the wrong winding.");

        double tilesArea = 0d;
        for (int index = 0; index < CellCount; index++)
        {
            CanonicalBoardCell cell = Cells[index];
            CanonicalBoardCell next = Cells[(index + 1) % CellCount];
            if (cell.Index != index || cell.OuterEnd != next.OuterStart ||
                cell.InnerEnd != next.InnerStart)
                throw new InvalidOperationException($"Five-sided tile {index} has a broken boundary or graph index.");
            if (!IsFinite(cell.Center) || !double.IsFinite(cell.Area) || cell.Area <= minimumCellArea)
                throw new InvalidOperationException($"Five-sided tile {index} is non-finite or collapsed.");
            for (int vertex = 0; vertex < cell.Quad.Count; vertex++)
            {
                Vector2 a = cell.Quad[vertex];
                Vector2 b = cell.Quad[(vertex + 1) % cell.Quad.Count];
                Vector2 c = cell.Quad[(vertex + 2) % cell.Quad.Count];
                if (!IsFinite(a) || Cross(b - a, c - b) <= minimumCellArea)
                    throw new InvalidOperationException($"Five-sided tile {index} is folded or is not strictly convex.");
            }
            tilesArea += cell.Area;
        }
        // Validate against the exact shared boundary values consumed by the
        // meshes, not ideal infinite-precision side lines. Float interpolation
        // can move a midpoint by one coordinate ULP on translated small maps.
        double annulusArea = Math.Abs(SignedArea(OuterBoundary)) - Math.Abs(SignedArea(InnerBoundary));
        if (Math.Abs(tilesArea - annulusArea) > Math.Max(annulusArea * 0.00005d, scaleArea * 1e-8d))
            throw new InvalidOperationException("The five-sided tiles do not cover exactly one board annulus.");
    }

    internal static double SignedArea(IReadOnlyList<Vector2> polygon)
    {
        if (polygon == null) throw new ArgumentNullException(nameof(polygon));
        if (polygon.Count < 3) return 0d;
        // Relative coordinates avoid catastrophic cancellation when a map is
        // translated far from world origin.
        Vector2 origin = polygon[0];
        double twiceArea = 0d;
        for (int index = 1; index + 1 < polygon.Count; index++)
            twiceArea += Cross(polygon[index] - origin, polygon[index + 1] - origin);
        return twiceArea * 0.5d;
    }

    private static bool IsFinite(Vector2 point) =>
        float.IsFinite(point.X) && float.IsFinite(point.Y);

    private static double Cross(Vector2 first, Vector2 second) =>
        (double)first.X * second.Y - (double)first.Y * second.X;
}

internal sealed class CanonicalBoardCell
{
    internal int Index { get; }
    internal int RegionIndex { get; }
    internal int StepInRegion { get; }
    internal int RegionCellCount { get; }
    internal Vector2 InnerStart { get; }
    internal Vector2 OuterStart { get; }
    internal Vector2 OuterEnd { get; }
    internal Vector2 InnerEnd { get; }
    internal Vector2 Center { get; }
    internal IReadOnlyList<Vector2> Quad { get; }
    internal double Area { get; }

    internal CanonicalBoardCell(int index, int regionIndex, int stepInRegion,
        int regionCellCount, Vector2 innerStart, Vector2 outerStart,
        Vector2 outerEnd, Vector2 innerEnd, int winding)
    {
        Index = index;
        RegionIndex = regionIndex;
        StepInRegion = stepInRegion;
        RegionCellCount = regionCellCount;
        InnerStart = innerStart;
        OuterStart = outerStart;
        OuterEnd = outerEnd;
        InnerEnd = innerEnd;
        // Named endpoints always follow native graph traversal. Rendering and
        // collider vertices always have positive/CCW winding independently.
        var quad = winding > 0
            ? new[] { innerStart, outerStart, outerEnd, innerEnd }
            : new[] { innerStart, innerEnd, outerEnd, outerStart };
        Quad = Array.AsReadOnly(quad);
        Area = CanonicalBoardGeometry.SignedArea(Quad);
        Center = new Vector2(
            (float)(((double)innerStart.X + outerStart.X + outerEnd.X + innerEnd.X) * 0.25d),
            (float)(((double)innerStart.Y + outerStart.Y + outerEnd.Y + innerEnd.Y) * 0.25d));
    }
}
