using System;

namespace BusinessTourFiveRealms;

// Pure calculations shared by the gameplay presentation and standalone tests.
internal static class GameplayHudLayoutRules
{
    internal const float MovingMoneyScale = 1.3f;
    internal const float StationaryMoneyScale = .32f;
    internal const float WorldRowScale = .62f;
    internal const float MoneyFontScale = 1.25f;
    internal const float ToolbarReservedWidth = 112f;

    // Native transfers match the stationary pile's visual scale first. The
    // left rail compacts that pile and its row; compensate a tracked target so
    // the transfer is 130% of native size, not 130% of an already tiny pile.
    internal static float MovingMoneyMultiplier(float destinationScale) =>
        Finite(destinationScale) && destinationScale > 0f && destinationScale <= 1f
            ? MovingMoneyScale / destinationScale : MovingMoneyScale;

    internal static int MoneyFontSize(int nativeSize) =>
        nativeSize > 0 && nativeSize <= int.MaxValue / MoneyFontScale
            ? (int)Math.Round(nativeSize * MoneyFontScale, MidpointRounding.AwayFromZero) : nativeSize;

    internal static float ToolbarStep(float canvasHeight, int count)
    {
        if (!Finite(canvasHeight) || canvasHeight <= 0 || count < 1) return 80f;
        return Math.Max(64f, Math.Min(80f, (canvasHeight - 56f) / count));
    }

    internal static bool TryPopupTranslation(float left, float right, float bottom, float top,
        float viewportLeft, float viewportRight, float viewportBottom, float viewportTop,
        out float x, out float y)
    {
        x = y = 0f;
        if (!Finite(left) || !Finite(right) || !Finite(bottom) || !Finite(top) ||
            !Finite(viewportLeft) || !Finite(viewportRight) || !Finite(viewportBottom) ||
            !Finite(viewportTop) || right <= left || top <= bottom ||
            viewportRight <= viewportLeft || viewportTop <= viewportBottom) return false;
        float paddingX = (viewportRight - viewportLeft) * .02f;
        float paddingY = (viewportTop - viewportBottom) * .02f;
        float minX = viewportLeft + paddingX;
        float maxX = viewportRight - ToolbarReservedWidth;
        float minY = viewportBottom + paddingY;
        float maxY = viewportTop - paddingY;
        // Translation can fit this panel without distorting its native tab /
        // scroll layout. An oversized/fullscreen root must not be moved.
        if (right - left > maxX - minX || top - bottom > maxY - minY) return false;
        if (right > maxX) x = maxX - right;
        else if (left < minX) x = minX - left;
        if (top > maxY) y = maxY - top;
        else if (bottom < minY) y = minY - bottom;
        return Finite(x) && Finite(y);
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
