using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ciallo.Data;
using Ciallo.Tool;
using Frent;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace Ciallo.Geometry;

[TestSuite]
[RequireGodotRuntime]
public class BucketFillSolverTests
{
    [TestCase]
    public void ClosedRegionRejectsBoundaryAndExterior()
    {
        using var solver = BucketFillSolver.Build([Rectangle(0, 0, 10, 10)]);
        var region = solver.Query(new(3, 6), gapAware: true, gapFactor: 0.5);
        AssertThat(region.Polygons.Length).IsEqual(1);
        CheckArea(region, 100);
        AssertThat(solver.Query(new(0, 5), gapAware: true, gapFactor: 0.5).IsEmpty).IsTrue();
        AssertThat(solver.Query(new(12, 5), gapAware: true, gapFactor: 0.5).IsEmpty).IsTrue();
        using var empty = BucketFillSolver.Build([]);
        AssertThat(empty.Query(Vector2.Zero, gapAware: true, gapFactor: 0.5).IsEmpty).IsTrue();
    }

    [TestCase]
    public void FrameRejectionIsCachedUntilGapParametersChange()
    {
        using var solver = BucketFillSolver.Build([[new(0, 10), new(5, 0), new(10, 10)]]);
        var point = new Vector2(5, 5);
        // This seed is inside a non-frame triangle. A low gap factor lets propagation reach the frame.
        AssertThat(solver.Query(point, gapAware: true, gapFactor: 0.5).IsEmpty).IsFalse();
        AssertThat(solver.Query(point, gapAware: true, gapFactor: 0.25).IsEmpty).IsTrue();
        AssertThat(solver.Query(point, gapAware: true, gapFactor: 0.2).IsEmpty).IsTrue();
        solver.Query(point, gapAware: true, gapFactor: 0.25);

        // Empty is a singleton even without caching. Compare query allocations to detect repeated
        // graph work, without depending on private fields or wall-clock timing.
        long cachedAllocations = MeasureAllocations(alternateParameters: false);
        long uncachedAllocations = MeasureAllocations(alternateParameters: true);
        AssertThat(cachedAllocations < uncachedAllocations).IsTrue();

        AssertThat(solver.Query(point, gapAware: false, gapFactor: 0.25).IsEmpty).IsFalse();
        AssertThat(solver.Query(point, gapAware: true, gapFactor: 0.25).IsEmpty).IsTrue();
        AssertThat(solver.Query(point, gapAware: true, gapFactor: 0.5).IsEmpty).IsFalse();

        long MeasureAllocations(bool alternateParameters)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 64; i++)
                solver.Query(point, gapAware: true, gapFactor: alternateParameters && i % 2 == 0 ? 0.2 : 0.25);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
    }

    [TestCase]
    public void SmallExteriorGapUsesGraphBottleneck()
    {
        ImmutableArray<Vector2> open = [new(4.8f, 0), new(0, 0), new(0, 10), new(10, 10), new(10, 0), new(5.2f, 0)];
        using var solver = BucketFillSolver.Build([open]);
        var region = solver.Query(new(3, 6), gapAware: true, gapFactor: 0.5);
        CheckArea(region, 100);
        AssertThat(Contains(region, new(5, 5))).IsTrue();
        AssertThat(Contains(region, new(5, -2))).IsFalse();
    }

    [TestCase]
    public void GapAwareOptionSeparatesConnectedRooms()
    {
        using var solver = BucketFillSolver.Build([
            Rectangle(0, 0, 20, 10),
            [new(10, 0), new(10, 4.8f)], [new(10, 5.2f), new(10, 10)]]);
        var connected = solver.Query(new(4, 5), gapAware: false, gapFactor: 0.5);
        CheckArea(connected, 200);
        var separated = solver.Query(new(4, 5), gapAware: true, gapFactor: 0.5);
        AssertThat(Contains(separated, new(4, 5))).IsTrue();
        AssertThat(Contains(separated, new(16, 5))).IsFalse();
        CheckArea(separated, 100);
        // Changing gap values for the same seed must invalidate the region cache.
        CheckArea(solver.Query(new(4, 5), gapAware: true, gapFactor: 0), 200);
        CheckArea(solver.Query(new(4, 5), gapAware: true, gapFactor: 0.5), 100);
    }

    [TestCase]
    public void MultipleHolesRemainGroupedUntilMaterialization()
    {
        using var solver = BucketFillSolver.Build([
            Rectangle(0, 0, 20, 10), Rectangle(3, 3, 5, 5), Rectangle(12, 3, 14, 5)]);
        // Test the whole connected region; gap segmentation may split the passages between holes.
        var region = solver.Query(new(1, 6), gapAware: false, gapFactor: 0.5);
        CheckArea(region, 192);
        AssertThat(Contains(region, new(4, 4))).IsFalse();
        AssertThat(Contains(region, new(13, 4))).IsFalse();
        // The preview includes the outer perimeter and two hole perimeters, without bridge edges.
        AssertThat(region.Polygons.Length).IsEqual(1);
        AssertThat(region.Polygons[0].Length).IsEqual(3);
        double perimeter = 0;
        foreach (var contour in region.Contours)
        {
            AssertThat(contour[0]).IsEqual(contour[^1]);
            for (int i = 1; i < contour.Length; i++)
                perimeter += contour[i].DistanceTo(contour[i - 1]);
        }
        AssertThat(Math.Abs(perimeter - 76) < 0.001).IsTrue();

        // Only document materialization needs to connect the holes into one ring.
        var boundaries = region.Polygons[0]
            .Select(ring => (IReadOnlyList<Vector2>)ring.AsSpan()[..^1].ToArray()).ToArray();
        var bridged = boundaries.ConnectHoles();
        using var repaired = Arrangement2D.RepairAndTriangulate([bridged.ToArray()]);
        AssertThat(Math.Abs(Area((Vector2[])repaired["vertices"], (int[])repaired["indices"]) - 192) < 0.001).IsTrue();
    }

    [TestCase]
    public void CrossingsOverlapsAndDanglingEdgesKeepTopology()
    {
        using var solver = BucketFillSolver.Build([
            Rectangle(0, 0, 10, 10), [new(0, 0), new(10, 10)],
            [new(0, 10), new(10, 0)], [new(2, 2), new(8, 8)]]);
        CheckArea(solver.Query(new(5, 2), gapAware: false, gapFactor: 0.5), 25);
        using var dangling = BucketFillSolver.Build([Rectangle(0, 0, 10, 10), [new(2, 2), new(8, 8)]]);
        // A dangling constraint does not disconnect the graph; gap segmentation is tested separately.
        CheckArea(dangling.Query(new(2, 7), gapAware: false, gapFactor: 0.5), 100);
    }

    [TestCase]
    public void TouchingContoursKeepSeparateRegionsAndTransparentHoles()
    {
        using var holes = BucketFillSolver.Build([
            Rectangle(0, 0, 12, 12), Rectangle(3, 3, 6, 6), Rectangle(6, 6, 9, 9)]);
        CheckArea(holes.Query(new(1, 8), gapAware: true, gapFactor: 0.5), 126);
        using var touchingOuter = BucketFillSolver.Build([
            Rectangle(0, 0, 10, 10), [new(0, 5), new(3, 3), new(3, 7), new(0, 5)]]);
        CheckArea(touchingOuter.Query(new(7, 5), gapAware: true, gapFactor: 0.5), 94);
        using var touchingRegions = BucketFillSolver.Build([Rectangle(0, 0, 10, 10), Rectangle(10, 10, 20, 20)]);
        CheckArea(touchingRegions.Query(new(3, 6), gapAware: true, gapFactor: 0.5), 100);
    }

    [TestCase]
    public void ContextFiltersShapesAndInvalidatesOnlyStrokeChanges()
    {
        using var world = new World();
        var layer = world.Create();
        layer.Add(new LayerTreeNode());
        layer.Add(new ShapeLayerSetting());
        layer.Add(new ChildShapePolylineLookup());
        var stroke = AddShape(layer, Rectangle(0, 0, 20, 20), true);
        var polygon = AddShape(layer, Rectangle(5, 5, 15, 15), false);
        using (var context = new BucketFillContext(layer))
        {
            var options = new BucketFillTool();
            int sourceChanges = 0;
            context.SourceChanged += () => sourceChanges++;
            var result = context.Query(new(3, 6), options);
            CheckArea(result, 400);
            polygon.Get<SampledPolyline>().Positions.Value = Rectangle(1, 1, 19, 19);
            AssertThat(sourceChanges).IsEqual(0);
            AssertThat(ReferenceEquals(context.Query(new(3, 6), options), result)).IsTrue();
            stroke.Get<SampledPolyline>().Positions.Value = Rectangle(0, 0, 10, 10);
            AssertThat(sourceChanges).IsEqual(1);
            CheckArea(context.Query(new(3, 6), options), 100);
            AssertThat(context.Commit(new(-50, -50), options, Entity.Null, true)).IsFalse();
        }
        polygon.Delete();
        stroke.Delete();
        layer.Delete();
    }

    private static Entity AddShape(Entity layer, ImmutableArray<Vector2> positions, bool stroke)
    {
        var shape = layer.World.Create();
        shape.Add(new LayerTreeNode());
        shape.Add(new SampledPolyline { Positions = { Value = positions } });
        if (stroke) shape.Add(new StrokeSetting());
        else shape.Add(new FilledPolygonSetting());
        layer.Get<LayerTreeNode>().InsertChild(layer.Get<LayerTreeNode>().Children.Count, shape);
        return shape;
    }

    private static ImmutableArray<Vector2> Rectangle(float x0, float y0, float x1, float y1) =>
        [new(x0, y0), new(x1, y0), new(x1, y1), new(x0, y1), new(x0, y0)];

    private static bool Contains(BucketFillRegion region, Vector2 point)
    {
        return region.Contours.Count(contour => Geometry2D.IsPointInPolygon(point, contour.ToArray())) % 2 == 1;
    }

    private static void CheckArea(BucketFillRegion region, double expected)
    {
        AssertThat(region.IsEmpty).IsFalse();
        double contourArea = 0;
        foreach (var contour in region.Contours)
        {
            var origin = contour[0];
            for (int i = 1; i + 1 < contour.Length; i++)
                contourArea += (((double)contour[i].X - origin.X) * ((double)contour[i + 1].Y - origin.Y)
                    - ((double)contour[i].Y - origin.Y) * ((double)contour[i + 1].X - origin.X)) * 0.5;
        }
        AssertThat(Math.Abs(contourArea - expected) < 0.001).IsTrue();
        // Solid preview consumes raw contours without bridging them first.
        var rings = new Godot.Collections.Array<Vector2[]>(region.Contours.Select(ring => ring.ToArray()));
        using var repaired = Arrangement2D.RepairAndTriangulate(rings);
        AssertThat(Math.Abs(Area((Vector2[])repaired["vertices"], (int[])repaired["indices"]) - expected) < 0.001).IsTrue();
    }

    private static double Area(Vector2[] vertices, int[] triangles)
    {
        double area = 0;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            var a = vertices[triangles[i]];
            var b = vertices[triangles[i + 1]];
            var c = vertices[triangles[i + 2]];
            area += Math.Abs(((double)b.X - a.X) * ((double)c.Y - a.Y) - ((double)b.Y - a.Y) * ((double)c.X - a.X)) * 0.5;
        }
        return area;
    }
}
