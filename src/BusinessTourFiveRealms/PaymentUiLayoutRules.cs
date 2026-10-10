using System;

namespace BusinessTourFiveRealms;

// Pure viewport-fit calculation; shared with the standalone layout tests.
internal static class PaymentUiLayoutRules
{
    internal const float PreferredScale = 1.15f;

    internal static bool TryFontRange(float nativeFontSize, float nativeMinimum,
        out float minimum, out float maximum)
    {
        minimum = nativeMinimum;
        maximum = nativeFontSize;
        if (!Finite(nativeFontSize) || !Finite(nativeMinimum) ||
            nativeFontSize <= 0f || nativeMinimum < 0f) return false;
        maximum = nativeFontSize * PreferredScale;
        if (!Finite(maximum)) return false;
        // Never force the minimum above the existing native size. A narrow
        // label may stay at its old size, rather than overflowing at +15%.
        minimum = Math.Min(nativeMinimum, nativeFontSize);
        return true;
    }

    internal static float FitScale(float left, float right, float bottom, float top,
        float pivotX, float pivotY, float viewportLeft, float viewportRight,
        float viewportBottom, float viewportTop, float paddingFraction = .02f)
    {
        if (!Finite(left) || !Finite(right) || !Finite(bottom) || !Finite(top) ||
            !Finite(pivotX) || !Finite(pivotY) || !Finite(viewportLeft) ||
            !Finite(viewportRight) || !Finite(viewportBottom) || !Finite(viewportTop) ||
            !Finite(paddingFraction) || right <= left || top <= bottom ||
            viewportRight <= viewportLeft || viewportTop <= viewportBottom ||
            paddingFraction < 0f || paddingFraction >= .5f)
            return 1f;

        float paddingX = (viewportRight - viewportLeft) * paddingFraction;
        float paddingY = (viewportTop - viewportBottom) * paddingFraction;
        float limit = PreferredScale;
        Limit(ref limit, pivotX - left, pivotX - viewportLeft - paddingX);
        Limit(ref limit, right - pivotX, viewportRight - paddingX - pivotX);
        Limit(ref limit, pivotY - bottom, pivotY - viewportBottom - paddingY);
        Limit(ref limit, top - pivotY, viewportTop - paddingY - pivotY);

        // This feature enlarges small dialogs; it does not rewrite the native
        // layout of an already oversized/offscreen window by shrinking it.
        return Math.Max(1f, Math.Min(PreferredScale, limit));
    }

    private static void Limit(ref float limit, float extent, float available)
    {
        if (extent > .00001f) limit = Math.Min(limit, available / extent);
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
