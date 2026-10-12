using System;
using System.Collections.Generic;
using System.Numerics;

namespace BusinessTourFiveRealms;

// Pure containment for native planar art. Upright animated art consumes only
// BaseCenter; it must keep its own three-dimensional shape and internal motion.
internal static class BoardArtworkLayoutRules
{
    internal const float OuterFraction=.65f;
    internal const float EdgePaddingFraction=.04f;
    internal const float ChanceGroundInsetScale=.86f;

    internal static Vector2[] InsetChanceGround(IReadOnlyList<Vector2> quad)
    {
        ValidateQuad(quad);
        Vector2 center=(quad[0]+quad[1]+quad[2]+quad[3])*.25f;
        var result=new Vector2[4];
        for(int index=0;index<4;index++)
        {
            result[index]=Vector2.Lerp(center,quad[index],ChanceGroundInsetScale);
            if(!Finite(result[index]))throw new ArgumentException("Chance ground inset overflowed.");
        }
        return result;
    }

    internal static Vector2 BaseCenter(IReadOnlyList<Vector2> quad)
    {
        ValidateQuad(quad);
        Vector2 inner=(quad[0]+quad[3])*.5f,outer=(quad[1]+quad[2])*.5f;
        Vector2 center=Vector2.Lerp(inner,outer,OuterFraction);
        if(!Finite(center))throw new ArgumentException("Artwork center overflowed.");
        return center;
    }

    internal static float FitPlanarScale(IReadOnlyList<Vector2> quad,Vector2 center,
        IReadOnlyList<Vector2> visibleOffsets,float paddingFraction=EdgePaddingFraction)
    {
        int winding=ValidateQuad(quad);
        if(!Finite(center) || visibleOffsets==null || visibleOffsets.Count<3 ||
            !float.IsFinite(paddingFraction) || paddingFraction<0f || paddingFraction>=.5f)
            throw new ArgumentException("Invalid native artwork footprint.");
        double shortest=double.PositiveInfinity,largest=0d;
        foreach(Vector2 offset in visibleOffsets)
        {
            if(!Finite(offset))throw new ArgumentException("Non-finite native artwork bounds.");
            largest=Math.Max(largest,(double)offset.X*offset.X+(double)offset.Y*offset.Y);
        }
        if(largest<1e-12d)throw new ArgumentException("Degenerate native artwork bounds.");
        for(int index=0;index<4;index++)
            shortest=Math.Min(shortest,Length(quad[(index+1)%4]-quad[index]));
        double padding=shortest*paddingFraction,scale=1d;
        for(int index=0;index<4;index++)
        {
            Vector2 a=quad[index],edge=quad[(index+1)%4]-a;
            double length=Length(edge),nx=-edge.Y*winding/length,ny=edge.X*winding/length;
            double available=nx*((double)center.X-a.X)+ny*((double)center.Y-a.Y)-padding;
            if(available<=0d)return 0f;
            double extent=0d;
            foreach(Vector2 offset in visibleOffsets)
                extent=Math.Max(extent,-(nx*offset.X+ny*offset.Y));
            if(extent>0d)scale=Math.Min(scale,available/extent);
        }
        if(!double.IsFinite(scale))throw new ArgumentException("Native artwork fit overflowed.");
        // A native icon that already fits stays at its authored size.
        return (float)Math.Max(0d,Math.Min(1d,scale));
    }

    private static int ValidateQuad(IReadOnlyList<Vector2> quad)
    {
        if(quad==null || quad.Count!=4)throw new ArgumentException("Artwork requires a four-corner tile.");
        double area=0d;
        for(int index=0;index<4;index++)
        {
            Vector2 a=quad[index],b=quad[(index+1)%4];
            if(!Finite(a) || !Finite(b))throw new ArgumentException("Non-finite artwork tile.");
            area+=(double)a.X*b.Y-(double)a.Y*b.X;
        }
        if(!double.IsFinite(area) || Math.Abs(area)<1e-10d)
            throw new ArgumentException("Degenerate artwork tile.");
        int winding=Math.Sign(area);
        for(int index=0;index<4;index++)
        {
            Vector2 a=quad[index],edge=quad[(index+1)%4]-a;
            double length=Length(edge);
            if(!Finite(edge) || length<1e-6d)throw new ArgumentException("Degenerate artwork tile edge.");
            for(int other=0;other<4;other++)
            {
                Vector2 offset=quad[other]-a;
                if(((double)edge.X*offset.Y-(double)edge.Y*offset.X)*winding/length< -1e-6d)
                    throw new ArgumentException("Non-convex artwork tile.");
            }
        }
        return winding;
    }

    private static double Length(Vector2 point)=>Math.Sqrt((double)point.X*point.X+(double)point.Y*point.Y);
    private static bool Finite(Vector2 point)=>float.IsFinite(point.X)&&float.IsFinite(point.Y);
}
