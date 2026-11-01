using System.Collections.Generic;
using Ciallo.Geometry;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace Ciallo.Tests;

[TestSuite, RequireGodotRuntime]
public class PolylineSimplificationTests
{
    [TestCase]
    public void StraightStrokeKeepsRadiusPeakWithUnevenSampleSpacing()
    {
        List<Vector2> positions = [new(0, 0), new(2, 0), new(6, 0), new(10, 0), new(11, 0)];
        float[] radii = [0, 1, 3, 1, 0];

        positions.SimplifyRdp(0.1f, out var indices, radii: radii);

        // The centerline alone is straight; only the width profile preserves the taper.
        AssertThat(indices).ContainsExactly(0, 2, 3, 4);
    }

    [TestCase]
    public void UnevenSamplesKeepRadiusAndPressureFeaturesWithIndependentTolerances()
    {
        List<Vector2> positions = [new(0, 0), new(2, 0), new(6, 0), new(7, 0), new(10, 0), new(11, 0)];
        float[] radii = [0, 1, 3, 2.5f, 1, 0];
        float[] pressures = [0, 0.8f, 6f / 11f, 7f / 11f, 10f / 11f, 1];

        positions.SimplifyRdp(0.1f, out var indices, radii: radii, pressures: pressures);
        AssertThat(indices).ContainsExactly(0, 1, 2, 4, 5);

        // A loose spatial tolerance must not relax the pressure constraint.
        positions.SimplifyRdp(10f, out indices, radii: radii, pressures: pressures);
        AssertThat(indices).ContainsExactly(0, 1, 2, 5);
    }

    [TestCase]
    public void CollinearOvershootCannotDisappearThroughAttributeExtrapolation()
    {
        List<Vector2> positions = [new(0, 0), new(10, 0), new(1, 0)];
        float[] radii = [1, 2, 1.1f];
        float[] pressures = [0, 1, 0.1f];

        positions.SimplifyRdp(0.1f, out var indices, radii: radii, pressures: pressures);

        AssertThat(indices).ContainsExactly(0, 1, 2);
    }
}
