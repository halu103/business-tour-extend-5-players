using System.Numerics;
using BusinessTourFiveRealms;

int validated = 0;
var centers = new[] { Vector2.Zero, new Vector2(4.375f, -2.25f), new Vector2(-300f, 120f) };
var radii = new[]
{
    new Vector2(1f, 1f), new Vector2(9.5f, 5.25f),
    new Vector2(17f, 2f), new Vector2(2f, 17f)
};
var innerScales = new[] { 0.2f, 0.72f, 0.95f };
for (int cellCount = 5; cellCount <= 128; cellCount++)
    foreach (Vector2 center in centers)
        foreach (Vector2 radius in radii)
            foreach (float innerScale in innerScales)
                foreach (int winding in new[] { -1, 1 })
                {
                    try
                    {
                        var board = CanonicalBoardGeometry.Create(center, radius.X,
                            radius.Y, cellCount, winding, innerScale);
                        CheckBoard(board);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"count={cellCount}, center={center}, radius={radius}, innerScale={innerScale}, winding={winding}: {ex.Message}", ex);
                    }
                    validated++;
                }

var native = CanonicalBoardGeometry.Create(new Vector2(3f, 2f), 9f, 5f, 32, 1);
Require(native.RegionStarts.SequenceEqual(new[] { 0, 7, 13, 20, 26, 32 }), "native 32-cell region boundaries");
Require(native.Cells.GroupBy(c => c.RegionIndex).Select(g => g.Count())
    .SequenceEqual(new[] { 7, 6, 7, 6, 6 }), "balanced 7/6/7/6/6 distribution");
Require(native.OuterCorners[0] == new Vector2(3f, -3f), "exact lower start point");
Require(native.InnerScale == 0.72f, "default inner polygon scale");
Require(ReferenceEquals(native.OuterOutline, native.OuterCorners), "outer outline uses canonical corners");
Require(ReferenceEquals(native.InnerOutline, native.InnerCorners), "inner field uses canonical corners");
Require(ReferenceEquals(native.ExtrusionOutline, native.OuterBoundary), "extrusion shares tile boundaries");

var mirror = CanonicalBoardGeometry.Create(native.Center, native.RadiusX, native.RadiusY, 32, -1);
for (int index = 0; index < native.CellCount; index++)
{
    Vector2 first = native.Cells[index].Center, second = mirror.Cells[index].Center;
    Require(Close(first.Y, second.Y) && Close(first.X - native.Center.X,
        -(second.X - native.Center.X)), "changing graph winding mirrors traversal without moving the start");
}

var again = CanonicalBoardGeometry.Create(native.Center, native.RadiusX, native.RadiusY, 32, 1);
Require(native.OuterBoundary.SequenceEqual(again.OuterBoundary) &&
    native.InnerBoundary.SequenceEqual(again.InnerBoundary), "bitwise deterministic boundaries");
Require(native.Cells.Zip(again.Cells).All(pair => pair.First.Quad.SequenceEqual(pair.Second.Quad)),
    "bitwise deterministic tile quads");

foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
{
    Reject(() => CanonicalBoardGeometry.Create(Vector2.Zero, invalid, 5f, 32, 1), "invalid horizontal radius");
    Reject(() => CanonicalBoardGeometry.Create(Vector2.Zero, 5f, invalid, 32, 1), "invalid vertical radius");
}
foreach (float invalid in new[] { 0f, -1f, 1f, 2f, float.NaN, float.PositiveInfinity })
    Reject(() => CanonicalBoardGeometry.Create(Vector2.Zero, 9f, 5f, 32, 1, invalid), "invalid inner scale");
foreach (int invalid in new[] { -1, 0, 4, int.MaxValue })
    Reject(() => CanonicalBoardGeometry.Create(Vector2.Zero, 9f, 5f, invalid, 1), "invalid cell count");
foreach (int invalid in new[] { -2, 0, 2 })
    Reject(() => CanonicalBoardGeometry.Create(Vector2.Zero, 9f, 5f, 32, invalid), "invalid graph winding");
foreach (Vector2 invalid in new[] { new Vector2(float.NaN, 0f), new Vector2(0f, float.PositiveInfinity) })
    Reject(() => CanonicalBoardGeometry.Create(invalid, 9f, 5f, 32, 1), "invalid center");
Reject(() => CanonicalBoardGeometry.Create(new Vector2(1e20f, 1e20f), 1f, 1f, 32, 1),
    "coordinates whose float precision collapses tiles fail closed");
Reject(() => CanonicalBoardGeometry.Create(Vector2.Zero, float.MaxValue, float.MaxValue, 32, 1),
    "overflowing interpolation fails closed");
Reject(() => CanonicalBoardGeometry.Create(Vector2.Zero, float.Epsilon, float.Epsilon, 32, 1),
    "subnormal geometry whose edges collapse fails closed");

// Read-only wrappers must prevent a caller from breaking shared seam values.
RejectMutation(() => ((IList<Vector2>)native.OuterBoundary)[0] = Vector2.One, "boundary mutation");
RejectMutation(() => ((IList<Vector2>)native.Cells[0].Quad)[0] = Vector2.One, "quad mutation");

Console.WriteLine($"PASS: {validated:N0} canonical annuli, both graph windings, all tile quads CCW, exact seams, area coverage, deterministic geometry and invalid-input guards.");

static void CheckBoard(CanonicalBoardGeometry board)
{
    Require(board.OuterCorners.Count == 5 && board.InnerCorners.Count == 5, "exactly five visible corners");
    Require(board.OuterBoundary.Count == board.CellCount && board.InnerBoundary.Count == board.CellCount,
        "every cell has one canonical boundary pair");
    Require(board.RegionStarts[0] == 0 && board.RegionStarts[^1] == board.CellCount, "the graph is fully partitioned");
    Require(board.OuterCorners[0] == new Vector2(board.Center.X, board.Center.Y - board.RadiusY),
        "native cell order begins at lower point");
    double tileArea = 0d;
    foreach (CanonicalBoardCell cell in board.Cells)
    {
        int nextIndex = (cell.Index + 1) % board.CellCount;
        CanonicalBoardCell next = board.Cells[nextIndex];
        Require(cell.Index >= board.RegionStarts[cell.RegionIndex] &&
            cell.Index < board.RegionStarts[cell.RegionIndex + 1], "region/index mapping does not reorder the graph");
        Require(cell.StepInRegion == cell.Index - board.RegionStarts[cell.RegionIndex], "stable regional step");
        Require(cell.OuterStart == board.OuterBoundary[cell.Index] &&
            cell.InnerStart == board.InnerBoundary[cell.Index], "tile start uses shared canonical values");
        Require(cell.OuterEnd == next.OuterStart && cell.InnerEnd == next.InnerStart,
            "bitwise exact neighboring seam, including final-to-first seam");
        Require(cell.Quad.Count == 4 && cell.Area > 0d, "positive collider/render quad area for either graph winding");
        Require(cell.Quad.Contains(cell.InnerStart) && cell.Quad.Contains(cell.OuterStart) &&
            cell.Quad.Contains(cell.OuterEnd) && cell.Quad.Contains(cell.InnerEnd), "quad keeps all four logical endpoints");
        for (int vertex = 0; vertex < 4; vertex++)
        {
            Vector2 a = cell.Quad[vertex], b = cell.Quad[(vertex + 1) % 4], c = cell.Quad[(vertex + 2) % 4];
            Require(Cross(b - a, c - b) > 0d, "all convex quad turns are strictly positive");
            Require(Cross(b - a, cell.Center - a) > 0d, "cell center is strictly inside its footprint");
        }
        tileArea += cell.Area;
    }
    for (int side = 0; side < 5; side++)
    {
        int start = board.RegionStarts[side], end = board.RegionStarts[side + 1];
        Require(board.OuterBoundary[start] == board.OuterCorners[side] &&
            board.InnerBoundary[start] == board.InnerCorners[side], "region boundaries coincide with visible corners");
        Vector2 a = board.OuterCorners[side], b = board.OuterCorners[(side + 1) % 5];
        double expected = Vector2.Distance(a, b) / (end - start);
        for (int index = start; index < end; index++)
        {
            Vector2 first = board.OuterBoundary[index], second = board.OuterBoundary[(index + 1) % board.CellCount];
            Require(Math.Abs(Vector2.Distance(first, second) - expected) <= Math.Max(expected * 0.0001d, 0.0001d),
                "each region has uniform tile widths, with no random corner growth");
            // Validate in normalized isometric space: the inner polygon is a
            // uniform .72 (or configured) inset of a regular pentagon.
            Vector2 outer = first - board.Center;
            Vector2 inner = board.InnerBoundary[index] - board.Center;
            Require(Vector2.Distance(inner, outer * board.InnerScale) <= Math.Max(board.RadiusX, board.RadiusY) * 0.00003f,
                "the entire inner contour is homothetic, not per-cell shifted");
        }
    }
    double expectedArea = Math.Abs(CanonicalBoardGeometry.SignedArea(board.OuterBoundary)) -
        Math.Abs(CanonicalBoardGeometry.SignedArea(board.InnerBoundary));
    Require(Math.Abs(tileArea - expectedArea) <= expectedArea * 0.00005d, "tiles exactly cover the annulus");
    // Inverse ellipse projection produces five equal sides and equal radii.
    var normalized = board.OuterCorners.Select(p => new Vector2(
        (p.X - board.Center.X) / board.RadiusX, (p.Y - board.Center.Y) / board.RadiusY)).ToArray();
    double firstSide = Vector2.Distance(normalized[0], normalized[1]);
    for (int side = 0; side < 5; side++)
    {
        Require(Math.Abs(normalized[side].Length() - 1d) < 0.00005d, "equal normalized corner radii");
        Require(Math.Abs(Vector2.Distance(normalized[side], normalized[(side + 1) % 5]) - firstSide) < 0.0001d,
            "equal normalized pentagon sides");
    }
}

static bool Close(float a, float b) => Math.Abs(a - b) < 0.00001f;
static double Cross(Vector2 a, Vector2 b) => (double)a.X * b.Y - (double)a.Y * b.X;
static void Require(bool value, string description)
{
    if (!value) throw new InvalidOperationException("FAIL: " + description);
}
static void Reject(Action action, string description)
{
    bool rejected = false;
    try { action(); }
    catch (ArgumentException) { rejected = true; }
    catch (InvalidOperationException) { rejected = true; }
    Require(rejected, description);
}
static void RejectMutation(Action action, string description)
{
    bool rejected = false;
    try { action(); }
    catch (NotSupportedException) { rejected = true; }
    Require(rejected, description);
}
