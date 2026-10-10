using System;
using System.Collections.Generic;
using System.Numerics;

namespace BusinessTourFiveRealms;

// Fit a camera-aligned label rectangle inside its convex tile, rather than
// reusing the old square-side rotation or its axis-aligned bounding box.
internal static class BoardLabelLayoutRules
{
    internal static float FitScale(IReadOnlyList<Vector2> quad, Vector2 center,
        float width, float height, float padding = .03f)
    {
        if (quad == null || quad.Count != 4 || !Finite(center.X) || !Finite(center.Y) ||
            !Finite(width) || !Finite(height) || !Finite(padding) ||
            width <= 0f || height <= 0f || padding < 0f)
            throw new ArgumentException("Invalid label footprint.");
        double area = 0;
        for (int index = 0; index < 4; index++)
        {
            Vector2 a=quad[index], b=quad[(index+1)%4];
            if (!Finite(a.X) || !Finite(a.Y)) throw new ArgumentException("Non-finite tile.");
            area+=(double)a.X*b.Y-(double)a.Y*b.X;
        }
        if (Math.Abs(area)<1e-8) throw new ArgumentException("Degenerate tile.");
        float winding=Math.Sign(area), scale=1f;
        for (int index=0; index<4; index++)
        {
            Vector2 a=quad[index], edge=quad[(index+1)%4]-a;
            float length=edge.Length();
            if (length<1e-6f) throw new ArgumentException("Degenerate tile edge.");
            Vector2 normal=new Vector2(-edge.Y,edge.X)*(winding/length);
            if (Vector2.Dot(normal,quad[(index+2)%4]-a)<-1e-5f)
                throw new ArgumentException("Non-convex tile.");
            float available=Vector2.Dot(normal,center-a)-padding;
            float extent=(Math.Abs(normal.X)*width+Math.Abs(normal.Y)*height)*.5f;
            if (available<=0f) return 0f;
            scale=Math.Min(scale,available/extent);
        }
        return Math.Max(0f,Math.Min(1f,scale));
    }

    private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
}
