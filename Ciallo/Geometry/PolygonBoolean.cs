using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Godot;
using Operation = Godot.Geometry2D.PolyBooleanOperation;

namespace Ciallo.Geometry;

public static class PolygonBoolean
{
    // Results are separate closed document rings, with each hole connected to its own outer ring.
    public static ImmutableArray<ImmutableArray<Vector2>> Apply(
        IEnumerable<ImmutableArray<Vector2>> polygons, Operation operation)
    {
        Vector2[][] contours = [];
        bool first = true;
        foreach (var polygon in polygons)
        {
            var result = Calculate(JoinContours(contours), polygon.AsSpan(), first ? Operation.Union : operation);
            contours = result.ToArray();
            first = false;
        }
        return ToDocumentPolygons(contours);
    }

    public static bool Overlaps(ImmutableArray<Vector2> a, ImmutableArray<Vector2> b)
    {
        var intersection = Geometry2D.IntersectPolygons(a.AsSpan(), b.AsSpan());
        return intersection.Count > 0;
    }

    private static Godot.Collections.Array<Vector2[]> Calculate(
        ReadOnlySpan<Vector2> a, ReadOnlySpan<Vector2> b, Operation operation) => operation switch
    {
        Operation.Union => Geometry2D.MergePolygons(a, b),
        Operation.Difference => Geometry2D.ClipPolygons(a, b),
        Operation.Intersection => Geometry2D.IntersectPolygons(a, b),
        Operation.Xor => Geometry2D.ExcludePolygons(a, b),
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    private static Vector2[] JoinContours(Vector2[][] contours)
    {
        if (contours.Length == 0) return [];
        // Geometry2D accepts one path per operand. Out-and-back bridges cancel under its
        // Even-Odd rule, preserving holes and disconnected islands during the next operation.
        // These temporary bridges are never stored in the document.
        List<Vector2> path = [.. contours[0], contours[0][0]];
        foreach (var contour in contours.Skip(1))
        {
            path.AddRange(contour);
            path.Add(contour[0]);
            path.Add(path[0]);
        }
        return [.. path];
    }

    private static ImmutableArray<ImmutableArray<Vector2>> ToDocumentPolygons(Vector2[][] contours)
    {
        var polygons = contours.Where(ring => !Geometry2D.IsPolygonClockwise(ring))
            .OrderByDescending(Area)
            .Select(ring => new List<IReadOnlyList<Vector2>> { ring }).ToArray();
        foreach (var hole in contours.Where(Geometry2D.IsPolygonClockwise))
        {
            double area = Area(hole);
            // A hole belongs to the smallest enclosing outer contour, including when
            // an island inside another hole has holes of its own.
            polygons.Last(polygon => Area(polygon[0]) > area
                && Geometry2D.IsPointInPolygon(hole[0], (Vector2[])polygon[0])).Add(hole);
        }
        return [.. polygons.Select(polygon =>
        {
            var ring = polygon.ConnectHoles();
            return ImmutableArray.CreateRange(ring.Append(ring[0]));
        })];
    }

    private static double Area(IReadOnlyList<Vector2> ring)
    {
        double area = 0;
        var origin = ring[0];
        for (int i = 1; i + 1 < ring.Count; i++)
            area += ((double)ring[i].X - origin.X) * ((double)ring[i + 1].Y - origin.Y)
                - ((double)ring[i].Y - origin.Y) * ((double)ring[i + 1].X - origin.X);
        return Math.Abs(area) * 0.5;
    }
}
