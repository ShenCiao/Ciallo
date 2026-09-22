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
}
