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
        AssertThat(geometry.Positions.Count).IsEqual(3);
        int peak = geometry.Positions.ToList().IndexOf(new(20, 0));
        AssertThat(peak > 0 && peak < geometry.Positions.Count - 1).IsTrue();
        Near(geometry.Pressures[peak], 0.6f);
        Near(geometry.Radii[peak], 0.46f);
        Near(geometry.Tilts[peak].X, 2f / 3);
        Near(geometry.Pressures[0], 0);
        Near(geometry.Pressures[^1], 0);
        AssertThat(input.Pressures.ToArray()).ContainsExactly(0.2f, 0.8f);
    }

    [TestCase(1)]
    [TestCase(8)]
    [TestCase(32)]
    public void TaperKeepsSourceDensityEvenWithNonlinearRadiusMapping(int spacing)
    {
        var positions = Enumerable.Range(0, 320 / spacing + 1)
            .Select(i => new Vector2(i * spacing, 0)).ToArray();
        var input = new PolylineSamples(positions,
            positions.Select(p => 0.2f + 0.8f * p.X / 320).ToArray(),
            new Vector2[positions.Length]);
        var geometry = new PaintStrokeGeometryBuilder().Build(input, new(25, 17), p => 0.2f + 2 * p * p);

        // Only the two shoulders can add points, regardless of source density,
        // stylus pressure variation, or the brush's pressure-to-radius mapping.
        var expected = positions.Concat([new Vector2(25, 0), new Vector2(303, 0)])
            .Distinct().OrderBy(p => p.X).ToArray();
        AssertThat(geometry.Positions.ToArray()).ContainsExactly(expected);
        Near(geometry.Pressures[0], 0);
        Near(geometry.Pressures[^1], 0);
    }

    [TestCase]
    public void RoundedTaperShouldersPreserveEndpointsAndPeak()
    {
        var input = PolylineSamples.Uniform([new(100_000_000, 0), new(100_000_032, 0)]);
        var geometry = new PaintStrokeGeometryBuilder().Build(input, new(15, 15), p => 0.2f + p);
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
