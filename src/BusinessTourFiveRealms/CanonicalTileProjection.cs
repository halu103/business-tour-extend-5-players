using System;
using System.Collections.Generic;
using System.Numerics;

namespace BusinessTourFiveRealms;

// Clip the represented sprite artwork to a logical tile face before projecting
// it. UVs only ever come from convex interpolation inside an original sprite
// triangle: missing artwork stays missing and reveals the native-colored bed.
internal static class CanonicalTileProjection
{
    // Upright native artwork cannot be deformed with the ground mesh. Move
    // only its owning anchor through the same bilinear face mapping instead.
    // Some native indicators are deliberately outside the baked face; use
    // the closest point on that face before applying an interior margin.
    internal static Vector2 ProjectAnchor(IReadOnlyList<Vector2> sourceQuad,
        IReadOnlyList<Vector2> targetQuad, Vector2 position, float inset = .1f,
        float minimumOuterFraction = 0f)
    {
        var source = new Quad(sourceQuad, nameof(sourceQuad));
        var target = new Quad(targetQuad, nameof(targetQuad));
        if (!Finite(position) || !float.IsFinite(inset) || inset < 0f || inset >= .5f ||
            !float.IsFinite(minimumOuterFraction) || minimumOuterFraction < 0f ||
            minimumOuterFraction > 1f-inset)
            throw new ArgumentException("Invalid native artwork anchor.");
        Point point = new(position);
        bool inside = true;
        for (int index = 0; index < 4; index++)
            if (Cross(source.Corners[(index+1)%4]-source.Corners[index],
                point-source.Corners[index])*source.Winding < 0d) inside = false;
        if (!inside)
        {
            double nearest = double.PositiveInfinity;
            Point closest = default;
            for (int index = 0; index < 4; index++)
            {
                Point a=source.Corners[index], edge=source.Corners[(index+1)%4]-a;
                double fraction=Math.Clamp(Dot(point-a,edge)/edge.LengthSquared,0d,1d);
                Point candidate=a+edge*fraction;
                double distance=(candidate-point).LengthSquared;
                if (distance<nearest) { nearest=distance; closest=candidate; }
            }
            point=closest;
        }
        Point coordinate=source.Coordinates(point);
        coordinate=new Point(Math.Clamp(coordinate.X,inset,1d-inset),
            Math.Clamp(coordinate.Y,Math.Max(inset,minimumOuterFraction),1d-inset));
        return target.Evaluate(coordinate).ToVector2();
    }

    internal static CanonicalTileMesh Project(IReadOnlyList<Vector2> sourceQuad,
        IReadOnlyList<Vector2> targetQuad, IReadOnlyList<Vector2> spritePositions,
        IReadOnlyList<Vector2> spriteUv, IReadOnlyList<int> triangleIndices)
    {
        var source = new Quad(sourceQuad, nameof(sourceQuad));
        var target = new Quad(targetQuad, nameof(targetQuad));
        if (spritePositions == null) throw new ArgumentNullException(nameof(spritePositions));
        if (spriteUv == null) throw new ArgumentNullException(nameof(spriteUv));
        if (triangleIndices == null) throw new ArgumentNullException(nameof(triangleIndices));
        if (spritePositions.Count != spriteUv.Count)
            throw new ArgumentException("Sprite positions and UVs must have the same length.", nameof(spriteUv));
        if (triangleIndices.Count % 3 != 0)
            throw new ArgumentException("Sprite triangle indices must be complete triples.", nameof(triangleIndices));
        for (int index = 0; index < spritePositions.Count; index++)
            if (!Finite(spritePositions[index]) || !Finite(spriteUv[index]))
                throw new ArgumentException($"Sprite vertex {index} is not finite.", nameof(spritePositions));
        foreach (int index in triangleIndices)
            if (index < 0 || index >= spritePositions.Count)
                throw new ArgumentOutOfRangeException(nameof(triangleIndices), "A sprite triangle index is outside the vertex array.");

        var positions = new List<Vector2>();
        var uv = new List<Vector2>();
        var sources = new List<Vector2>();
        var triangles = new List<int>();
        var precision = new PrecisionBudget(target);
        for (int face = 0; face < triangleIndices.Count; face += 3)
        {
            var polygon = new List<Vertex>(3);
            for (int corner = 0; corner < 3; corner++)
            {
                int index = triangleIndices[face + corner];
                polygon.Add(new Vertex(new Point(spritePositions[index]), new Point(spriteUv[index])));
            }
            double area = Cross(polygon[1].Position - polygon[0].Position,
                polygon[2].Position - polygon[0].Position);
            if (Math.Abs(area) <= source.ScaleSquared * 1e-14d) continue;
            // Sutherland-Hodgman keeps every new vertex on an original
            // triangle edge (and its UV on the matching interpolated edge).
            for (int edge = 0; edge < 4 && polygon.Count > 0; edge++)
                polygon = Clip(polygon, source.Corners[edge], source.Corners[(edge + 1) % 4], source.Winding);
            if (polygon.Count < 3) continue;
            for (int vertex = 1; vertex + 1 < polygon.Count; vertex++)
                Emit(polygon[0], polygon[vertex], polygon[vertex + 1], source,
                    target, 2, 0, precision, positions, uv, sources, triangles);
        }
        return new CanonicalTileMesh(positions.ToArray(), uv.ToArray(),
            sources.ToArray(), triangles.ToArray());
    }

    private static List<Vertex> Clip(List<Vertex> polygon, Point a, Point b, int winding)
    {
        var result = new List<Vertex>(polygon.Count + 1);
        Point edge = b - a;
        Vertex previous = polygon[polygon.Count - 1];
        double previousDistance = Cross(edge, previous.Position - a) * winding;
        foreach (Vertex current in polygon)
        {
            double distance = Cross(edge, current.Position - a) * winding;
            bool previousInside = previousDistance >= 0d, inside = distance >= 0d;
            if (previousInside != inside)
            {
                // This is a convex interpolation, never extrapolation.
                double fraction = previousDistance / (previousDistance - distance);
                if (!double.IsFinite(fraction) || fraction < 0d || fraction > 1d)
                    throw new InvalidOperationException("Tile clipping produced an invalid edge intersection.");
                AppendUnique(result, Vertex.Lerp(previous, current, fraction));
            }
            if (inside) AppendUnique(result, current);
            previous = current;
            previousDistance = distance;
        }
        if (result.Count > 1 && result[0].Position.Equals(result[result.Count - 1].Position))
            result.RemoveAt(result.Count - 1);
        return result;
    }

    private static void AppendUnique(List<Vertex> polygon, Vertex vertex)
    {
        if (polygon.Count == 0 || !polygon[polygon.Count - 1].Position.Equals(vertex.Position))
            polygon.Add(vertex);
    }

    private static void Emit(Vertex a, Vertex b, Vertex c, Quad source, Quad target,
        int refinement, int adaptiveDepth, PrecisionBudget precision, List<Vector2> positions, List<Vector2> uv,
        List<Vector2> sources, List<int> triangles)
    {
        double sourceArea = Cross(b.Position - a.Position, c.Position - a.Position);
        if (Math.Abs(sourceArea) <= source.ScaleSquared * 1e-14d) return;
        // Bilinear projection is generally curved inside a quadrilateral.
        // Matching midpoint subdivision approximates those curves on both
        // sides of every sprite triangle seam without changing UV provenance.
        if (refinement > 0)
        {
            Subdivide(a, b, c, source, target, refinement - 1, adaptiveDepth,
                precision, positions, uv, sources, triangles);
            return;
        }
        Point pa = target.Evaluate(source.Coordinates(a.Position));
        Point pb = target.Evaluate(source.Coordinates(b.Position));
        Point pc = target.Evaluate(source.Coordinates(c.Position));
        double mappedArea = Cross(pb - pa, pc - pa);
        int expected = Math.Sign(sourceArea) * source.Winding * target.Winding;
        if (mappedArea * expected <= target.ScaleSquared * 1e-14d)
        {
            if (adaptiveDepth >= 6)
                throw new InvalidOperationException("A clipped sprite triangle would fold or collapse on the canonical tile.");
            Subdivide(a, b, c, source, target, 0, adaptiveDepth + 1,
                precision, positions, uv, sources, triangles);
            return;
        }
        // All emitted triangles have CCW render winding regardless of the
        // native graph's traversal direction or the source sprite triangles.
        if (mappedArea < 0d)
        {
            (b, c) = (c, b);
            (pb, pc) = (pc, pb);
        }
        // Corner clipping can produce arbitrarily thin slivers which are
        // valid in double precision but cannot render after float conversion.
        // Drop only those slivers, with a strict per-face lost-area budget;
        // coarse-coordinate collapse or meaningful folds still fail closed.
        Point qa = new(pa.ToVector2()), qb = new(pb.ToVector2()), qc = new(pc.ToVector2());
        if (Cross(qb - qa, qc - qa) <= 0d)
        {
            if (precision.Discard(Math.Abs(mappedArea))) return;
            throw new InvalidOperationException("Projected sprite geometry exceeds float coordinate precision.");
        }
        int start = positions.Count;
        Add(a, qa, positions, uv, sources);
        Add(b, qb, positions, uv, sources);
        Add(c, qc, positions, uv, sources);
        triangles.Add(start);
        triangles.Add(start + 1);
        triangles.Add(start + 2);
    }

    private static void Subdivide(Vertex a, Vertex b, Vertex c, Quad source, Quad target,
        int refinement, int adaptiveDepth, PrecisionBudget precision, List<Vector2> positions, List<Vector2> uv,
        List<Vector2> sources, List<int> triangles)
    {
        Vertex ab = Vertex.Lerp(a, b, 0.5d), bc = Vertex.Lerp(b, c, 0.5d), ca = Vertex.Lerp(c, a, 0.5d);
        Emit(a, ab, ca, source, target, refinement, adaptiveDepth, precision, positions, uv, sources, triangles);
        Emit(ab, b, bc, source, target, refinement, adaptiveDepth, precision, positions, uv, sources, triangles);
        Emit(ca, bc, c, source, target, refinement, adaptiveDepth, precision, positions, uv, sources, triangles);
        Emit(ab, bc, ca, source, target, refinement, adaptiveDepth, precision, positions, uv, sources, triangles);
    }

    private static void Add(Vertex vertex, Point position, List<Vector2> positions,
        List<Vector2> uv, List<Vector2> sources)
    {
        positions.Add(position.ToVector2());
        uv.Add(vertex.Uv.ToVector2());
        sources.Add(vertex.Position.ToVector2());
    }

    private sealed class PrecisionBudget
    {
        private readonly double _singleLimit, _totalLimit;
        private readonly bool _fineCoordinates;
        private double _discarded;

        internal PrecisionBudget(Quad target)
        {
            double faceArea = Math.Abs(Cross(target.Corners[1] - target.Corners[0], target.Corners[2] - target.Corners[0])
                + Cross(target.Corners[2] - target.Corners[0], target.Corners[3] - target.Corners[0]));
            // Areas here are doubled. At most one part per million of the
            // logical face per sliver and ten parts per million in total.
            _singleLimit = faceArea * 1e-6d;
            _totalLimit = faceArea * 1e-5d;
            double spacing = 0d;
            foreach (Point corner in target.Corners)
            {
                float x = Math.Abs((float)corner.X), y = Math.Abs((float)corner.Y);
                spacing = Math.Max(spacing, Math.Max((double)MathF.BitIncrement(x) - x,
                    (double)MathF.BitIncrement(y) - y));
            }
            _fineCoordinates = spacing <= Math.Sqrt(target.ScaleSquared) * 1e-5d;
        }

        internal bool Discard(double area)
        {
            if (!_fineCoordinates || area > _singleLimit || _discarded + area > _totalLimit) return false;
            _discarded += area;
            return true;
        }
    }

    private sealed class Quad
    {
        internal readonly Point[] Corners;
        internal readonly double ScaleSquared;
        internal readonly int Winding;
        private readonly Point _a, _b, _c, _d;

        internal Quad(IReadOnlyList<Vector2> corners, string name)
        {
            if (corners == null) throw new ArgumentNullException(name);
            if (corners.Count != 4) throw new ArgumentException("A logical tile face must have four named corners.", name);
            Corners = new Point[4];
            for (int index = 0; index < 4; index++)
            {
                if (!Finite(corners[index])) throw new ArgumentException("Logical tile corners must be finite.", name);
                Corners[index] = new Point(corners[index]);
            }
            for (int index = 0; index < 4; index++)
                ScaleSquared = Math.Max(ScaleSquared, (Corners[(index + 1) % 4] - Corners[index]).LengthSquared);
            if (ScaleSquared <= 0d) throw new ArgumentException("Logical tile face is collapsed.", name);
            Winding = Math.Sign(Cross(Corners[1] - Corners[0], Corners[2] - Corners[1]));
            if (Winding == 0) throw new ArgumentException("Logical tile face has a collapsed edge.", name);
            for (int index = 0; index < 4; index++)
                if (Cross(Corners[(index + 1) % 4] - Corners[index],
                    Corners[(index + 2) % 4] - Corners[(index + 1) % 4]) * Winding <= ScaleSquared * 1e-12d)
                    throw new ArgumentException("Logical tile face must be strictly convex and nondegenerate.", name);
            // Input order: inner-start(0,0), outer-start(0,1),
            // outer-end(1,1), inner-end(1,0).
            _a = Corners[0];
            _b = Corners[3] - _a;
            _c = Corners[1] - _a;
            _d = Corners[2] - Corners[3] - Corners[1] + _a;
        }

        internal Point Evaluate(Point coordinate)
        {
            double u = coordinate.X, v = coordinate.Y;
            // Preserve exact canonical endpoints when a clip hits a corner.
            if (u == 0d && v == 0d) return Corners[0];
            if (u == 0d && v == 1d) return Corners[1];
            if (u == 1d && v == 1d) return Corners[2];
            if (u == 1d && v == 0d) return Corners[3];
            return _a + _b * u + _c * v + _d * (u * v);
        }

        internal Point Coordinates(Point position)
        {
            Point r = position - _a;
            // cross(r - b*u, c + d*u) = 0 gives a quadratic in u.
            double a = -Cross(_b, _d);
            double b = Cross(r, _d) - Cross(_b, _c);
            double c = Cross(r, _c);
            double coefficientScale = Math.Max(Math.Abs(a), Math.Max(Math.Abs(b), Math.Abs(c)));
            bool found = false;
            Point best = default;
            double error = double.PositiveInfinity;
            if (Math.Abs(a) <= coefficientScale * 1e-13d)
            {
                if (Math.Abs(b) > coefficientScale * 1e-14d)
                    Consider(-c / b, r, position, ref found, ref best, ref error);
            }
            else
            {
                double discriminant = b * b - 4d * a * c;
                double tolerance = (b * b + Math.Abs(4d * a * c)) * 1e-13d;
                if (discriminant >= -tolerance)
                {
                    double root = Math.Sqrt(Math.Max(0d, discriminant));
                    double q = -0.5d * (b + Math.CopySign(root, b));
                    if (q != 0d)
                    {
                        Consider(q / a, r, position, ref found, ref best, ref error);
                        Consider(c / q, r, position, ref found, ref best, ref error);
                    }
                    else Consider(-b / (2d * a), r, position, ref found, ref best, ref error);
                }
            }
            if (!found || error > ScaleSquared * 1e-16d)
                throw new InvalidOperationException("A clipped vertex could not be inverted inside its logical tile face.");
            return best;
        }

        private void Consider(double u, Point r, Point position, ref bool found,
            ref Point best, ref double error)
        {
            if (!double.IsFinite(u) || u < -1e-8d || u > 1d + 1e-8d) return;
            Point vAxis = _c + _d * u;
            double denominator = vAxis.LengthSquared;
            if (denominator <= ScaleSquared * 1e-24d) return;
            double v = Dot(r - _b * u, vAxis) / denominator;
            if (!double.IsFinite(v) || v < -1e-8d || v > 1d + 1e-8d) return;
            // This snaps face parameters at floating-point clip boundaries;
            // it does not clamp or modify the original interpolated sprite UV.
            u = SnapUnit(u);
            v = SnapUnit(v);
            Point coordinate = new(u, v);
            double candidateError = (Evaluate(coordinate) - position).LengthSquared;
            if (candidateError < error)
            {
                found = true;
                best = coordinate;
                error = candidateError;
            }
        }

        private static double SnapUnit(double value) => value <= 1e-10d ? 0d
            : value >= 1d - 1e-10d ? 1d : value;
    }

    private readonly struct Vertex
    {
        internal readonly Point Position;
        internal readonly Point Uv;
        internal Vertex(Point position, Point uv) { Position = position; Uv = uv; }
        internal static Vertex Lerp(Vertex a, Vertex b, double fraction) =>
            new(a.Position + (b.Position - a.Position) * fraction,
                a.Uv + (b.Uv - a.Uv) * fraction);
    }

    private readonly struct Point : IEquatable<Point>
    {
        internal readonly double X;
        internal readonly double Y;
        internal double LengthSquared => X * X + Y * Y;
        internal Point(double x, double y) { X = x; Y = y; }
        internal Point(Vector2 point) { X = point.X; Y = point.Y; }
        internal Vector2 ToVector2()
        {
            var result = new Vector2((float)X, (float)Y);
            if (!Finite(result)) throw new InvalidOperationException("Projected sprite data exceeds finite float coordinates.");
            return result;
        }
        public bool Equals(Point other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object obj) => obj is Point other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y);
        public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);
        public static Point operator *(Point point, double value) => new(point.X * value, point.Y * value);
    }

    private static bool Finite(Vector2 value) => float.IsFinite(value.X) && float.IsFinite(value.Y);
    private static double Cross(Point a, Point b) => a.X * b.Y - a.Y * b.X;
    private static double Dot(Point a, Point b) => a.X * b.X + a.Y * b.Y;
}

internal sealed class CanonicalTileMesh
{
    internal IReadOnlyList<Vector2> Positions { get; }
    internal IReadOnlyList<Vector2> UV { get; }
    // Diagnostic/test provenance: source positions before canonical mapping.
    internal IReadOnlyList<Vector2> SourcePositions { get; }
    internal IReadOnlyList<int> Triangles { get; }
    internal bool IsEmpty => Triangles.Count == 0;

    internal CanonicalTileMesh(Vector2[] positions, Vector2[] uv,
        Vector2[] sourcePositions, int[] triangles)
    {
        Positions = Array.AsReadOnly(positions);
        UV = Array.AsReadOnly(uv);
        SourcePositions = Array.AsReadOnly(sourcePositions);
        Triangles = Array.AsReadOnly(triangles);
    }
}
