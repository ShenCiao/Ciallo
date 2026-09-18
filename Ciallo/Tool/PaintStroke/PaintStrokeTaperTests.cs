using System;
using System.Linq;
using Ciallo.Geometry;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace Ciallo.Tool;

[TestSuite]
[RequireGodotRuntime]
public class PaintStrokeTaperTests
{
    [TestCase]
    public void SparseShortStrokeRetainsAsymmetricPeakAndInputPressure()
    {
        var input = new PolylineSamples([new(0, 0), new(30, 0)], [0.2f, 0.8f], [Vector2.Zero, Vector2.One]);
        var geometry = new PaintStrokeGeometryBuilder().Build(input, new(40, 20), p => 0.1f + p * p);
        int peak = geometry.Positions.ToList().IndexOf(new(20, 0));
        AssertThat(peak > 0 && peak < geometry.Positions.Count - 1).IsTrue();
        Near(geometry.Pressures[peak], 0.6f);
        Near(geometry.Radii[peak], 0.46f);
        Near(geometry.Tilts[peak].X, 2f / 3);
        Near(geometry.Pressures[0], 0);
        Near(geometry.Pressures[^1], 0);
        AssertThat(input.Pressures.ToArray()).ContainsExactly(0.2f, 0.8f);
    }

    [TestCase]
    public void ResamplingPreservesVaryingPressureBeforeNonlinearRadiusMapping()
    {
        var input = new PolylineSamples([new(0, 0), new(10, 0)], [0.2f, 0.8f], [Vector2.Zero, Vector2.Zero]);
        static float Radius(float p) => 0.2f + 2 * p * p;
        var geometry = new PaintStrokeGeometryBuilder().Build(input, new(10, 0), Radius);
        // The renderer interpolates stored pp/radii linearly between samples.
        for (int i = 1; i < geometry.Positions.Count; i++)
        {
            float t = (geometry.Positions[i - 1].X + geometry.Positions[i].X) / 20;
            float expected = (0.2f + 0.6f * t) * t;
            Near((geometry.Pressures[i - 1] + geometry.Pressures[i]) / 2, expected, 0.001f);
            Near((geometry.Radii[i - 1] + geometry.Radii[i]) / 2, Radius(expected), 0.002f);
        }
    }

    [TestCase]
    public void RoundedSubsamplesCannotOverwriteEndpointsOrTheShortStrokePeak()
    {
        var input = PolylineSamples.Uniform([new(100_000_000, 0), new(100_000_032, 0)]);
        var geometry = new PaintStrokeGeometryBuilder().Build(input, new(32, 32), p => 0.2f + p);
        Near(geometry.Pressures[0], 0);
        Near(geometry.Pressures[^1], 0);
        Near(geometry.Pressures.Max(), 1);
        int peak = geometry.Positions.ToList().IndexOf(new(100_000_016, 0));
        AssertThat(peak > 0).IsTrue();
        Near(geometry.Pressures[peak], 1);
    }

    private static void Near(float actual, float expected, float tolerance = 0.0001f) =>
        AssertThat(Math.Abs(actual - expected) <= tolerance).IsTrue();
}
