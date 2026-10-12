using System.Numerics;
using BusinessTourFiveRealms;

var square = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
var packedUv = new[] { new Vector2(.2f, .3f), new Vector2(.2f, .4f), new Vector2(.35f, .4f), new Vector2(.35f, .3f) };
var indices = new[] { 0, 1, 2, 0, 2, 3 };
int checkedMeshes = 0;

var full = CanonicalTileProjection.Project(square, square, square, packedUv, indices);
Check(full, square, square, packedUv, indices);
Require(Close(Area(full), 1d), "full native artwork covers exactly one logical face");
Require(full.Positions.Zip(full.SourcePositions).All(pair => Vector2.Distance(pair.First, pair.Second) < 1e-6f),
    "identity projection retains source positions");

// Real trap/corner artwork can be tight-trimmed and not cover a logical face.
// No corner sampling is attempted: the half-coverage triangle remains half.
var trimmed = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f) };
var trimmedUv = new[] { new Vector2(.62f, .11f), new Vector2(.78f, .11f), new Vector2(.62f, .26f) };
var one = new[] { 0, 1, 2 };
var half = CanonicalTileProjection.Project(square, square, trimmed, trimmedUv, one);
Check(half, square, trimmed, trimmedUv, one);
Require(Close(Area(half), .5d), "trimmed artwork preserves its transparent missing half");

var outside = new[] { new Vector2(-2f, -2f), new Vector2(5f, -2f), new Vector2(-2f, 5f) };
var outsideUv = new[] { new Vector2(.4f, .6f), new Vector2(.8f, .6f), new Vector2(.4f, .9f) };
var clipped = CanonicalTileProjection.Project(square, square, outside, outsideUv, one);
Check(clipped, square, outside, outsideUv, one);
Require(Close(Area(clipped), 1d), "sprite triangle beyond all four edges clips exactly to the face");
Require(clipped.SourcePositions.All(p => p.X >= -1e-6f && p.X <= 1f + 1e-6f && p.Y >= -1e-6f && p.Y <= 1f + 1e-6f),
    "clipped positions never retain old baked rims outside the logical face");

var disjoint = new[] { new Vector2(3f, 3f), new Vector2(4f, 3f), new Vector2(3f, 4f) };
var empty = CanonicalTileProjection.Project(square, square, disjoint, trimmedUv, one);
Require(empty.IsEmpty && empty.Positions.Count == 0 && empty.UV.Count == 0,
    "no coverage returns an empty mesh for the solid-bed fallback");
Require(CanonicalTileProjection.Project(square, square, Array.Empty<Vector2>(),
    Array.Empty<Vector2>(), Array.Empty<int>()).IsEmpty, "empty native mesh remains empty");
var degenerate = new[] { new Vector2(0f, 0f), new Vector2(.5f, .5f), new Vector2(1f, 1f) };
Require(CanonicalTileProjection.Project(square, square, degenerate, trimmedUv, one).IsEmpty,
    "degenerate native sprite triangle has no coverage");

var quadrilaterals = new[]
{
    square,
    new[] { new Vector2(-3f, -2f), new Vector2(-4f, 1f), new Vector2(2f, 2f), new Vector2(4f, -2f) },
    new[] { new Vector2(-1f, -3f), new Vector2(-3f, 3f), new Vector2(4f, 4f), new Vector2(3f, -1f) },
    new[] { new Vector2(17f, 11f), new Vector2(16f, 16f), new Vector2(21f, 16.5f), new Vector2(21f, 12f) }
};
int anchorChecks=0;
foreach(var source in quadrilaterals)
foreach(var target in quadrilaterals)
foreach(bool reverseSource in new[]{false,true})
foreach(bool reverseTarget in new[]{false,true})
{
    var s=reverseSource?ReverseNamed(source):source;
    var t=reverseTarget?ReverseNamed(target):target;
    foreach(float u in new[]{0f,.1f,.5f,.9f,1f})
    foreach(float v in new[]{0f,.1f,.5f,.9f,1f})
    foreach(float outer in new[]{0f,.6f})
    {
        Vector2 native=Evaluate(s,u,v);
        Vector2 actual=CanonicalTileProjection.ProjectAnchor(s,t,native,.1f,outer);
        Vector2 expected=Evaluate(t,Math.Clamp(u,.1f,.9f),Math.Clamp(v,Math.Max(.1f,outer),.9f));
        Require(Vector2.Distance(actual,expected)<.00004f,
            "upright artwork base maps through its own face while reserving the inner label band");
        Require(Vector2.Distance(actual,CanonicalTileProjection.ProjectAnchor(s,t,native,.1f,outer))<1e-6f,
            "anchor projection is baseline-based, deterministic and does not drift");
        anchorChecks++;
    }
    foreach(Vector2 outsideAnchor in new[]{new Vector2(-100f,-100f),new Vector2(100f,100f),
        new Vector2(-100f,100f),new Vector2(100f,-100f)})
    {
        Vector2 actual=CanonicalTileProjection.ProjectAnchor(s,t,outsideAnchor);
        Vector2 uv=Invert(t,actual);
        Require(uv.X>=.0999f && uv.X<=.9001f && uv.Y>=.0999f && uv.Y<=.9001f,
            "external native effect anchors clamp inside their own face, not a neighboring tile");
        anchorChecks++;
    }
}
Reject(()=>CanonicalTileProjection.ProjectAnchor(square,square,new Vector2(float.NaN,0f)),"nonfinite anchor");
Reject(()=>CanonicalTileProjection.ProjectAnchor(square,square,Vector2.Zero,.5f),"collapsed anchor margin");
Reject(()=>CanonicalTileProjection.ProjectAnchor(square,square,Vector2.Zero,-.1f),"negative anchor margin");
Reject(()=>CanonicalTileProjection.ProjectAnchor(square,square,Vector2.Zero,.1f,.95f),"building band beyond face");
foreach (var source in quadrilaterals)
    foreach (var target in quadrilaterals)
        foreach (bool reverseSource in new[] { false, true })
            foreach (bool reverseTarget in new[] { false, true })
            {
                var s = reverseSource ? ReverseNamed(source) : source;
                var t = reverseTarget ? ReverseNamed(target) : target;
                // Native quad triangles cover the source completely. Change
                // both graph and triangle orientation independently.
                foreach (var triangleWinding in new[] { indices, new[] { 0, 2, 1, 0, 3, 2 } })
                {
                    var mesh = CanonicalTileProjection.Project(s, t, s, packedUv, triangleWinding);
                    Check(mesh, t, s, packedUv, triangleWinding);
                    Require(Close(Area(mesh), Math.Abs(PolygonArea(t)), 0.0001d),
                        "varying convex quadrilaterals preserve full target coverage");
                    for (int vertex = 0; vertex < mesh.Positions.Count; vertex++)
                    {
                        // Independent Newton inversion verifies the analytic
                        // bilinear inverse; atlas UVs remain native per-triangle
                        // interpolation, not fabricated logical-face UVs.
                        Vector2 coordinate = Invert(s, mesh.SourcePositions[vertex]);
                        Vector2 expected = Evaluate(t, coordinate.X, coordinate.Y);
                        Require(Vector2.Distance(mesh.Positions[vertex], expected) < 0.00004f,
                            "bilinear inverse maps source parameters into target, not affine extrapolation");
                    }
                    checkedMeshes++;
                }
            }

// Seeded non-parallelogram faces stress the bilinear inverse and clipping.
var random = new Random(5719);
for (int sample = 0; sample < 200; sample++)
{
    var s = JitteredQuad(random);
    var t = JitteredQuad(random);
    var mesh = CanonicalTileProjection.Project(s, t, outside, outsideUv, one);
    Check(mesh, t, outside, outsideUv, one);
    checkedMeshes++;
}

var repeat = CanonicalTileProjection.Project(square, quadrilaterals[1], outside, outsideUv, one);
var repeatAgain = CanonicalTileProjection.Project(square, quadrilaterals[1], outside, outsideUv, one);
Require(repeat.Positions.SequenceEqual(repeatAgain.Positions) && repeat.UV.SequenceEqual(repeatAgain.UV) &&
    repeat.Triangles.SequenceEqual(repeatAgain.Triangles), "projection is deterministic");

// A tight sprite edge can clip almost exactly through a logical corner. The
// resulting fan has valid double-precision slivers which disappear when its
// subdivided vertices are rounded to float at ordinary board coordinates.
var boardSource = square.Select(p => p + new Vector2(5f, 7f)).ToArray();
var boardTarget = square.Select(p => p + new Vector2(8f, 8f)).ToArray();
var cornerClip = new[] { new Vector2(4f, 6f), new Vector2(7f, 6f), new Vector2(4f, MathF.BitIncrement(9f)) };
double cornerEpsilon = (double)MathF.BitIncrement(9f) - 9d;
double cornerCoverage = 1d - .5d * (1d - cornerEpsilon / 3d) * (1d - 2d * cornerEpsilon / (3d + cornerEpsilon));
var cornerMesh = CanonicalTileProjection.Project(boardSource, boardTarget, cornerClip, outsideUv, one);
Check(cornerMesh, boardTarget, cornerClip, outsideUv, one);
Require(Close(Area(cornerMesh), cornerCoverage, 0.00001d), "near-corner float slivers preserve analytically clipped face coverage");
var cornerAgain = CanonicalTileProjection.Project(boardSource, boardTarget, cornerClip, outsideUv, one);
Require(cornerMesh.Positions.SequenceEqual(cornerAgain.Positions) && cornerMesh.UV.SequenceEqual(cornerAgain.UV) &&
    cornerMesh.Triangles.SequenceEqual(cornerAgain.Triangles), "discarding unrenderable boundary slivers is deterministic");
foreach (Vector2 offset in new[] { new Vector2(-10f, 5f), new Vector2(5f, -10f), new Vector2(10f, -10f) })
    foreach (bool reverseSource in new[] { false, true })
        foreach (bool reverseTarget in new[] { false, true })
        {
            var s = reverseSource ? ReverseNamed(boardSource) : boardSource;
            var t = square.Select(p => p + offset).ToArray();
            if (reverseTarget) t = ReverseNamed(t);
            var mesh = CanonicalTileProjection.Project(s, t, cornerClip, outsideUv, one);
            Check(mesh, t, cornerClip, outsideUv, one);
            Require(Close(Area(mesh), cornerCoverage, 0.00001d), "boundary slivers stay bounded at all signed board positions and windings");
            checkedMeshes++;
        }

var microscopic = new[] { new Vector2(.1f, .1f), new Vector2(.1000001f, .1f), new Vector2(.1f, .2f) };
var microscopicMesh = CanonicalTileProjection.Project(square, boardTarget, microscopic, trimmedUv, one);
Require(microscopicMesh.IsEmpty && microscopicMesh.Positions.Count == 0,
    "an entirely float-collapsed microscopic triangle leaves no orphaned vertices");
var excessiveMicroscopic = Enumerable.Range(0, 8192).SelectMany(_ => one).ToArray();
Reject(() => CanonicalTileProjection.Project(square, boardTarget, microscopic, trimmedUv, excessiveMicroscopic),
    "cumulative discarded area cannot exceed the per-face precision budget");

// At genuinely distant coordinates the precision loss is not microscopic
// relative to the face. Do not silently drop large pieces of the artwork.
var coarseTarget = square.Select(p => p * 8f + new Vector2(67_108_864f, 67_108_864f)).ToArray();
Reject(() => CanonicalTileProjection.Project(square, coarseTarget, square, packedUv, indices),
    "distant float precision collapse still fails closed");

Reject(() => CanonicalTileProjection.Project(null!, square, square, packedUv, indices), "null source");
Reject(() => CanonicalTileProjection.Project(square, null!, square, packedUv, indices), "null target");
Reject(() => CanonicalTileProjection.Project(square.Take(3).ToArray(), square, square, packedUv, indices), "non-quad face");
var folded = new[] { square[0], square[2], square[1], square[3] };
Reject(() => CanonicalTileProjection.Project(folded, square, square, packedUv, indices), "folded source");
Reject(() => CanonicalTileProjection.Project(square, folded, square, packedUv, indices), "folded target");
var collapsed = new[] { Vector2.Zero, Vector2.Zero, Vector2.Zero, Vector2.Zero };
Reject(() => CanonicalTileProjection.Project(collapsed, square, square, packedUv, indices), "collapsed face");
var concave = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(.2f, .2f), new Vector2(1f, 0f) };
Reject(() => CanonicalTileProjection.Project(concave, square, square, packedUv, indices), "concave face");
var nonfinite = (Vector2[])square.Clone(); nonfinite[2] = new Vector2(float.NaN, 0f);
Reject(() => CanonicalTileProjection.Project(nonfinite, square, square, packedUv, indices), "nonfinite face");
Reject(() => CanonicalTileProjection.Project(square, square, nonfinite, packedUv, indices), "nonfinite sprite position");
Reject(() => CanonicalTileProjection.Project(square, square, square, nonfinite, indices), "nonfinite UV");
Reject(() => CanonicalTileProjection.Project(square, square, square, packedUv.Take(3).ToArray(), indices), "UV count mismatch");
Reject(() => CanonicalTileProjection.Project(square, square, square, packedUv, new[] { 0, 1 }), "partial triangle");
Reject(() => CanonicalTileProjection.Project(square, square, square, packedUv, new[] { -1, 1, 2 }), "negative index");
Reject(() => CanonicalTileProjection.Project(square, square, square, packedUv, new[] { 0, 1, 4 }), "out-of-range index");
RejectMutation(() => ((IList<Vector2>)full.Positions)[0] = Vector2.One, "read-only positions");
RejectMutation(() => ((IList<Vector2>)full.UV)[0] = Vector2.One, "read-only UVs");

Console.WriteLine($"PASS: {checkedMeshes} varying-face projections, {anchorChecks} upright native artwork anchors within their own face/outer building band, full/trimmed/clipped/empty coverage, near-corner float slivers and aggregate loss budget, distant precision rejection, both windings, CCW triangles, interpolated original UV provenance, deterministic and invalid-input guards.");

static void Check(CanonicalTileMesh mesh, IReadOnlyList<Vector2> target,
    IReadOnlyList<Vector2> originalPositions, IReadOnlyList<Vector2> originalUv, IReadOnlyList<int> originalTriangles)
{
    Require(mesh.Positions.Count == mesh.UV.Count && mesh.Positions.Count == mesh.SourcePositions.Count,
        "position/UV/provenance counts agree");
    Require(mesh.Triangles.Count % 3 == 0, "complete output triangle indices");
    int targetWinding = Math.Sign(PolygonArea(target));
    for (int vertex = 0; vertex < mesh.Positions.Count; vertex++)
    {
        Vector2 position = mesh.Positions[vertex], uv = mesh.UV[vertex], source = mesh.SourcePositions[vertex];
        Require(float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(uv.X) && float.IsFinite(uv.Y),
            "all output data finite");
        for (int edge = 0; edge < 4; edge++)
            Require(Cross(target[(edge + 1) % 4] - target[edge], position - target[edge]) * targetWinding >= -0.00005d,
                "projected vertices are inside canonical face");
        bool belongs = false;
        for (int face = 0; face < originalTriangles.Count; face += 3)
        {
            int ia = originalTriangles[face], ib = originalTriangles[face + 1], ic = originalTriangles[face + 2];
            var weights = Barycentric(source, originalPositions[ia], originalPositions[ib], originalPositions[ic]);
            if (Math.Min(weights.X, Math.Min(weights.Y, weights.Z)) < -0.00003f) continue;
            Vector2 expectedUv = originalUv[ia] * weights.X + originalUv[ib] * weights.Y + originalUv[ic] * weights.Z;
            if (Vector2.Distance(expectedUv, uv) < 0.000004f) { belongs = true; break; }
        }
        Require(belongs, "every emitted UV is a convex interpolation inside its original sprite triangle");
    }
    for (int triangle = 0; triangle < mesh.Triangles.Count; triangle += 3)
    {
        int a = mesh.Triangles[triangle], b = mesh.Triangles[triangle + 1], c = mesh.Triangles[triangle + 2];
        Require(a >= 0 && b >= 0 && c >= 0 && a < mesh.Positions.Count && b < mesh.Positions.Count && c < mesh.Positions.Count,
            "all output indices in range");
        Require(Cross(mesh.Positions[b] - mesh.Positions[a], mesh.Positions[c] - mesh.Positions[a]) > 0d,
            "all output triangles have positive winding");
    }
}

static Vector2[] JitteredQuad(Random random)
{
    float Next() => (float)random.NextDouble() * .5f - .25f;
    return new[] { new Vector2(Next(), Next()), new Vector2(Next(), 1f + Next()),
        new Vector2(1f + Next(), 1f + Next()), new Vector2(1f + Next(), Next()) };
}
static Vector2[] ReverseNamed(Vector2[] quad) => new[] { quad[0], quad[3], quad[2], quad[1] };
static Vector2 Evaluate(IReadOnlyList<Vector2> quad, float u, float v) =>
    Vector2.Lerp(Vector2.Lerp(quad[0], quad[3], u), Vector2.Lerp(quad[1], quad[2], u), v);
static Vector2 Invert(IReadOnlyList<Vector2> quad, Vector2 position)
{
    Vector2 b = quad[3] - quad[0], c = quad[1] - quad[0], d = quad[2] - quad[3] - quad[1] + quad[0];
    Vector2 result = new(.5f, .5f);
    for (int iteration = 0; iteration < 30; iteration++)
    {
        Vector2 delta = Evaluate(quad, result.X, result.Y) - position;
        Vector2 du = b + d * result.Y, dv = c + d * result.X;
        double determinant = Cross(du, dv);
        result -= new Vector2((float)(Cross(delta, dv) / determinant), (float)(Cross(du, delta) / determinant));
    }
    return result;
}
static Vector3 Barycentric(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
{
    Vector2 x = b - a, y = c - a, q = p - a;
    double determinant = Cross(x, y);
    float v = (float)(Cross(q, y) / determinant), w = (float)(Cross(x, q) / determinant);
    return new Vector3(1f - v - w, v, w);
}
static double Area(CanonicalTileMesh mesh)
{
    double area = 0d;
    for (int index = 0; index < mesh.Triangles.Count; index += 3)
    {
        Vector2 a = mesh.Positions[mesh.Triangles[index]], b = mesh.Positions[mesh.Triangles[index + 1]], c = mesh.Positions[mesh.Triangles[index + 2]];
        area += Cross(b - a, c - a) * .5d;
    }
    return area;
}
static double PolygonArea(IReadOnlyList<Vector2> polygon)
{
    double area = 0d;
    for (int index = 1; index + 1 < polygon.Count; index++)
        area += Cross(polygon[index] - polygon[0], polygon[index + 1] - polygon[0]) * .5d;
    return area;
}
static double Cross(Vector2 a, Vector2 b) => (double)a.X * b.Y - (double)a.Y * b.X;
static bool Close(double a, double b, double tolerance = 0.00001d) => Math.Abs(a - b) <= tolerance * Math.Max(1d, Math.Max(Math.Abs(a), Math.Abs(b)));
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
