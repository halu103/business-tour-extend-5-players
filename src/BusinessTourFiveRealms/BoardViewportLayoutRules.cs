using System;
using System.Numerics;

namespace BusinessTourFiveRealms;

// This is camera-plane framing, not another world-radius multiplier. The
// latter is cancelled by fitting the larger board back into the same viewport.
internal static class BoardViewportLayoutRules
{
    // Leave the compact five-player rail at the left and the vertical toolbar
    // at the right. The top gap also accommodates native buildings above the
    // top face; the bottom gap keeps the connection/version text unobstructed.
    internal const float Left = .17f;
    internal const float Right = .965f;
    internal const float Bottom = .035f;
    internal const float Top = .92f;
    internal const float HorizontalMargin = 1.0f;
    internal const float VerticalMargin = 1.45f;

    internal static OrthographicBoardFrame Fit(float left, float right,
        float bottom, float top, float aspect)
    {
        if (!float.IsFinite(left) || !float.IsFinite(right) ||
            !float.IsFinite(bottom) || !float.IsFinite(top) ||
            !float.IsFinite(aspect) || right <= left || top <= bottom || aspect <= 0f)
            throw new ArgumentException("Invalid orthographic board bounds.");

        float size = Math.Max((right-left+HorizontalMargin)/(2f*aspect*(Right-Left)),
            (top-bottom+VerticalMargin)/(2f*(Top-Bottom)));
        if (!float.IsFinite(size) || size <= 0f)
            throw new ArgumentException("Board framing overflowed the camera range.");
        // World offset of the camera from the board's camera-plane midpoint.
        // Both axes must use the same size to preserve all tile proportions.
        var offset = new Vector2((left+right)*.5f-(Left+Right-1f)*size*aspect,
            (bottom+top)*.5f-(Bottom+Top-1f)*size);
        if (!float.IsFinite(offset.X) || !float.IsFinite(offset.Y))
            throw new ArgumentException("Board framing offset is not finite.");
        return new OrthographicBoardFrame(size,offset);
    }
}

internal readonly struct OrthographicBoardFrame
{
    internal float Size { get; }
    internal Vector2 Offset { get; }
    internal OrthographicBoardFrame(float size,Vector2 offset) { Size=size;Offset=offset; }
}
