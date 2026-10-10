using BusinessTourFiveRealms;

int checks = 0;
void Equal(float actual, float expected, string name)
{
    checks++;
    if (Math.Abs(actual - expected) > .00001f)
        throw new InvalidOperationException($"{name}: expected {expected}, got {actual}.");
}

Equal(PaymentUiLayoutRules.FitScale(-200, 200, -150, 150, 0, 0, -960, 960, -540, 540), 1.15f,
    "Small centered dialog is enlarged by fifteen percent");
Equal(PaymentUiLayoutRules.FitScale(500, 900, -150, 150, 700, 0, -960, 960, -540, 540), 1.108f,
    "Right-edge enlargement is capped with a two-percent margin");
Equal(PaymentUiLayoutRules.FitScale(-200, 200, 170, 470, 0, 320, -960, 960, -540, 540), 1.15f,
    "Asymmetric pivot retains preferred enlargement when it fits");
Equal(PaymentUiLayoutRules.FitScale(-950, 950, -530, 530, 0, 0, -960, 960, -540, 540), 1f,
    "Already oversized native dialog is not shrunk");
Equal(PaymentUiLayoutRules.FitScale(850, 1250, -150, 150, 1050, 0, -960, 960, -540, 540), 1f,
    "Offscreen native dialog is not rewritten");
Equal(PaymentUiLayoutRules.FitScale(-200, 200, -150, 150, -200, -150, -960, 960, -540, 540), 1.15f,
    "Corner pivot does not divide by zero");
Equal(PaymentUiLayoutRules.FitScale(float.NaN, 200, -150, 150, 0, 0, -960, 960, -540, 540), 1f,
    "Nonfinite window fails closed");
Equal(PaymentUiLayoutRules.FitScale(-200, 200, -150, 150, 0, 0, -960, 960, -540, 540, -.1f), 1f,
    "Negative padding fails closed");
Equal(PaymentUiLayoutRules.FitScale(200, -200, -150, 150, 0, 0, -960, 960, -540, 540), 1f,
    "Inverted window fails closed");

foreach (float nativeFontSize in new[] { 8f, 16f, 24f, 36f, 72f })
foreach (float nativeMinimum in new[] { 0f, 8f, 18f, 36f })
{
    if (!PaymentUiLayoutRules.TryFontRange(nativeFontSize, nativeMinimum, out float minimum, out float maximum))
        throw new InvalidOperationException("Valid font range was rejected.");
    Equal(maximum, nativeFontSize * 1.15f, "Payment font maximum grows by at most fifteen percent");
    Equal(minimum, Math.Min(nativeMinimum, nativeFontSize), "Payment font minimum is not forced above native size");
}
foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.MaxValue })
{
    checks++;
    if (PaymentUiLayoutRules.TryFontRange(invalid, 0f, out _, out _))
        throw new InvalidOperationException("Invalid or overflowing font size was accepted.");
}
foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity })
{
    checks++;
    if (PaymentUiLayoutRules.TryFontRange(24f, invalid, out _, out _))
        throw new InvalidOperationException("Invalid native font minimum was accepted.");
}

// Exercise portrait/landscape viewports, edge-pivoted dialogs, and arbitrary
// positions. Whenever enlargement is applied, all padded bounds must fit.
foreach (var viewport in new[] { (1920f, 1080f), (1280f, 720f), (1024f, 768f), (720f, 1280f) })
for (int x = 1; x <= 8; x++)
for (int y = 1; y <= 8; y++)
foreach (float pivotFraction in new[] { 0f, .25f, .5f, .75f, 1f })
{
    float w = viewport.Item1 * .24f, h = viewport.Item2 * .2f;
    float px = viewport.Item1 * x / 10f, py = viewport.Item2 * y / 10f;
    float left = px - w * pivotFraction, right = left + w;
    float bottom = py - h * pivotFraction, top = bottom + h;
    float scale = PaymentUiLayoutRules.FitScale(left, right, bottom, top, px, py,
        0, viewport.Item1, 0, viewport.Item2);
    checks++;
    if (scale < 1f || scale > 1.15001f || !float.IsFinite(scale))
        throw new InvalidOperationException("Invalid enlargement range.");
    if (scale <= 1f) continue;
    float padX = viewport.Item1 * .02f, padY = viewport.Item2 * .02f;
    if (px + (left - px) * scale < padX - .001f ||
        px + (right - px) * scale > viewport.Item1 - padX + .001f ||
        py + (bottom - py) * scale < padY - .001f ||
        py + (top - py) * scale > viewport.Item2 - padY + .001f)
        throw new InvalidOperationException("Enlarged dialog exceeded its padded viewport.");
}

Console.WriteLine($"PASS: {checks} payment-dialog and font sizing checks; native oversized layouts remain unchanged.");
