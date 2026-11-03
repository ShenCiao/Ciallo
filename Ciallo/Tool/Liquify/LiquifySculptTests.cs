using System;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace Ciallo.Tool;

[TestSuite, RequireGodotRuntime]
public class LiquifySculptTests
{
    [TestCase]
    public void ZeroPressureCanRiseAndSplitTravelRetainsTheReversiblePressureProfile()
    {
        var dab = new LiquifyDab(Vector2.Zero, new(2, 0), 64, 0.5f, 0.7f);
        var half = dab with { Delta = dab.Delta / 2 };
        foreach (float original in new[] { 0f, 0.00001f, 0.2f, 0.8f })
        {
            float raised = LiquifySculpt.ApplyPressure(Vector2.Zero, original, dab);
            AssertThat(raised > original).IsTrue();
            float split = LiquifySculpt.ApplyPressure(Vector2.Zero,
                LiquifySculpt.ApplyPressure(Vector2.Zero, original, half), half);
            Near(split, raised);
            Near(LiquifySculpt.ApplyPressure(Vector2.Zero, raised, dab with { Strength = -dab.Strength }), original);
        }

        float low = LiquifySculpt.ApplyPressure(Vector2.Zero, 0.2f, dab);
        float high = LiquifySculpt.ApplyPressure(Vector2.Zero, 0.8f, dab);
        float liftedZero = LiquifySculpt.ApplyPressure(Vector2.Zero, 0f, dab);
        Near((low - liftedZero) / (high - liftedZero), 0.25f);
    }

    [TestCase]
    public void ClippedPressureCanLeaveEitherBoundaryWhileZeroStrengthIsExactlyNeutral()
    {
        var strong = new LiquifyDab(Vector2.Zero, new(1000, 0), 64, 1, 1);
        float maximum = LiquifySculpt.ApplyPressure(Vector2.Zero, 0.8f, strong);
        float minimum = LiquifySculpt.ApplyPressure(Vector2.Zero, 0.2f, strong with { Strength = -1 });
        AssertThat(maximum).IsEqual(1f);
        AssertThat(minimum).IsEqual(0f);

        var click = strong with { Delta = Vector2.Inf, Strength = 0.5f };
        AssertThat(LiquifySculpt.ApplyPressure(Vector2.Zero, minimum, click) > 0).IsTrue();
        AssertThat(LiquifySculpt.ApplyPressure(Vector2.Zero, maximum, click with { Strength = -0.5f }) < 1).IsTrue();
        foreach (float original in new[] { 0f, 0.00001f, 0.2f, 1f })
        {
            AssertThat(LiquifySculpt.ApplyPressure(Vector2.Zero, original, strong with { Strength = 0 })).IsEqual(original);
            AssertThat(LiquifySculpt.ApplyPressure(Vector2.Zero, original, strong with { Delta = Vector2.Zero })).IsEqual(original);
            AssertThat(LiquifySculpt.ApplyPressure(new(64, 0), original, strong)).IsEqual(original);
        }
    }

    private static void Near(float actual, float expected) =>
        AssertThat(Math.Abs(actual - expected) < 0.000001f).IsTrue();
}
