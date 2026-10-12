using System;
using System.Collections.Generic;
using System.Numerics;

namespace BusinessTourFiveRealms;

// Fit a camera-aligned label rectangle inside its convex tile, rather than
// reusing the old square-side rotation or its axis-aligned bounding box.
internal static class BoardLabelLayoutRules
{
    // A fit that merely stops at the edge still makes adjacent names appear
    // joined. Keep an explicit visual gutter on each label's own inner band.
    internal const float EdgePaddingFraction = .07f;
    // Keep a full-height native name strip: the previous .28..58 name band
    // fixed overlap but made captions unnecessarily tiny. Rent is separate
    // and foreground-rendered, so its outer strip cannot be hidden by houses.
    internal const float NameInnerFraction = 0f;
    internal const float NameOuterFraction = .55f;
    internal const float ServiceNameOuterFraction = .98f;
    internal const float RentInnerFraction = .62f;
    internal const float RentOuterFraction = .98f;
    internal const float FivePlayerInnerScale = .64f;
    internal const float NameTargetPixels = 20f;
    internal const float RentTargetPixels = 22f;
    internal const float MaximumLabelEnlargement = 2.5f;
    // Verified native TagManager ordering: GameFieldBack < GameField <
    // GameFieldOver < Characters < CardEffect < UI. Rent is board information,
    // not a modal or card effect. Resolve this existing layer by name at runtime.
    internal const string BoardInformationLayerName = "GameFieldOver";
    internal const string CharacterLayerName = "Characters";
    internal const int BoardTransparentRenderQueue = 3000;

    // Only switch between matching native TMP families. A different shader
    // must keep its authored material rather than lose its atlas/properties.
    internal static string RentOverlayShaderFor(string nativeShader) => nativeShader switch
    {
        "TextMeshPro/Distance Field" => "TextMeshPro/Distance Field Overlay",
        "TextMeshPro/Distance Field Overlay" => "TextMeshPro/Distance Field Overlay",
        "TextMeshPro/Mobile/Distance Field" => "TextMeshPro/Mobile/Distance Field Overlay",
        "TextMeshPro/Mobile/Distance Field Overlay" => "TextMeshPro/Mobile/Distance Field Overlay",
        _ => null
    };

    internal static float ReadabilityScale(float worldHeight,float cameraSize,int pixelHeight,bool rent,int lineCount=1)
    {
        if (!Finite(worldHeight) || !Finite(cameraSize) || worldHeight<=0f || cameraSize<=0f || pixelHeight<=0 || lineCount<1)
            throw new ArgumentException("Invalid label readability projection.");
        // Multiline native service captions must not share one single-line
        // pixel budget. Measure average line height without changing their
        // translated text or native wrapping; tile fitting still caps width.
        double scale=(rent?RentTargetPixels:NameTargetPixels)*2d*cameraSize*lineCount/(worldHeight*pixelHeight);
        if (!double.IsFinite(scale) || scale<=0d)
            throw new ArgumentException("Label readability projection overflowed.");
        return (float)Math.Min(MaximumLabelEnlargement,scale);
    }

    internal static Vector3 CameraDepthOffset(Vector3 original,Vector3 forward)
    {
        if (!Finite(original.X) || !Finite(original.Y) || !Finite(original.Z) ||
            !Finite(forward.X) || !Finite(forward.Y) || !Finite(forward.Z))
            throw new ArgumentException("Invalid label depth offset.");
        float lengthSquared=forward.LengthSquared();
        if (!Finite(lengthSquared) || lengthSquared<1e-8f)
            throw new ArgumentException("Invalid label camera direction.");
        // Preserve the native distance along the view axis, not a world-plane
        // displacement that would shift an otherwise fitted label off-screen.
        Vector3 result=forward*(Vector3.Dot(original,forward)/lengthSquared);
        if (!Finite(result.X) || !Finite(result.Y) || !Finite(result.Z))
            throw new ArgumentException("Label depth projection overflowed.");
        return result;
    }

    internal static float FitScale(IReadOnlyList<Vector2> quad, Vector2 center,
        float width, float height, float padding = .03f,float maximumScale=1f)
    {
        if (quad == null || quad.Count != 4 || !Finite(center.X) || !Finite(center.Y) ||
            !Finite(width) || !Finite(height) || !Finite(padding) || !Finite(maximumScale) ||
            width <= 0f || height <= 0f || padding < 0f || maximumScale<=0f)
            throw new ArgumentException("Invalid label footprint.");
        double area = 0;
        for (int index = 0; index < 4; index++)
        {
            Vector2 a=quad[index], b=quad[(index+1)%4];
            if (!Finite(a.X) || !Finite(a.Y)) throw new ArgumentException("Non-finite tile.");
            area+=(double)a.X*b.Y-(double)a.Y*b.X;
        }
        if (Math.Abs(area)<1e-8) throw new ArgumentException("Degenerate tile.");
        float winding=Math.Sign(area), scale=maximumScale;
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
        return Math.Max(0f,Math.Min(maximumScale,scale));
    }

    private static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
}
