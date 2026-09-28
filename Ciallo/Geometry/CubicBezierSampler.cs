using System;
using System.Collections.Generic;
using Godot;

namespace Ciallo.Geometry;

/// <summary>Samples cubics at a length-based density, then locally refines chord error and tangent rotation.</summary>
public static class CubicBezierSampler
{
    // Each length-based interval permits at most 8 segments / 15 recursive calls.
    private const int MaxRefinementDepth = 3;

    // Appends the endpoint but not the start, so adjacent cubics share their anchor.
    // Distances use document units; maxTurnAngle is in radians. A real cusp or an
    // authored corner keeps its discontinuity. Refinement stops at its depth limit.
    public static void AppendTo(List<Vector2> result, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
        float maxSpacing, float maxDeviation, float maxTurnAngle)
    {
        float deviationSquared = maxDeviation * maxDeviation;
        float tangentSlope = MathF.Tan(maxTurnAngle * 0.5f);
        Vector2 previous = p0;
        var curve = new Cubic(p0, p1, p2, p3);
        // Establish the baseline before counting refinement depth: a long, gently
        // bending cubic needs more samples than a short one. The control polygon
        // bounds curve length, including loops whose endpoints coincide.
        int intervals = Math.Max(1, (int)MathF.Ceiling(curve.ControlPolygonLength / maxSpacing));
        for (int remaining = intervals; remaining > 1; remaining--)
        {
            var (left, right) = curve.Split(1f / remaining);
            Append(left, 0);
            curve = right;
        }
        Append(curve, 0);

        void Append(Cubic segment, int depth)
        {
            if (depth == MaxRefinementDepth ||
                segment.WithinBounds(maxSpacing, deviationSquared, tangentSlope))
            {
                if (segment.P3 != previous)
                {
                    result.Add(segment.P3);
                    previous = segment.P3;
                }
                return;
            }

            // De Casteljau splits the curve without changing its shape.
            // Only the failing region is refined.
            var (left, right) = segment.Split();
            Append(left, depth + 1);
            Append(right, depth + 1);
        }
    }

    private readonly record struct Cubic(Vector2 P0, Vector2 P1, Vector2 P2, Vector2 P3)
    {
        public float ControlPolygonLength => P0.DistanceTo(P1) + P1.DistanceTo(P2) + P2.DistanceTo(P3);

        public bool WithinBounds(float maxSpacing, float deviationSquared, float tangentSlope)
        {
            Vector2 a = P1 - P0, b = P2 - P1, c = P3 - P2;
            // Control-polygon length bounds arc length, even for loops/reversals.
            if (a.Length() + b.Length() + c.Length() > maxSpacing)
                return false;

            Vector2 chord = P3 - P0;
            float chordSquared = chord.LengthSquared();
            if (chordSquared == 0)
                return a.LengthSquared() == 0 && b.LengthSquared() == 0 && c.LengthSquared() == 0;

            // The derivative is a positive blend of these three control edges.
            // Keeping all of them in the chord's half-angle cone bounds every
            // tangent, including inside S-bends. At a smooth join, the two chords
            // can each differ from the shared tangent by at most half the limit.
            if (!InTangentCone(a, chord, tangentSlope) ||
                !InTangentCone(b, chord, tangentSlope) ||
                !InTangentCone(c, chord, tangentSlope))
                return false;

            // The convex hull bounds the whole subcurve, not just its midpoint.
            return DistanceSquaredToSegment(P1, P0, chord, chordSquared) <= deviationSquared &&
                DistanceSquaredToSegment(P2, P0, chord, chordSquared) <= deviationSquared;
        }

        public (Cubic Left, Cubic Right) Split(float t = 0.5f)
        {
            Vector2 a = P0.Lerp(P1, t), b = P1.Lerp(P2, t), c = P2.Lerp(P3, t);
            Vector2 d = a.Lerp(b, t), e = b.Lerp(c, t);
            Vector2 middle = d.Lerp(e, t);
            return (new(P0, a, d, middle), new(middle, e, c, P3));
        }
    }

    private static bool InTangentCone(Vector2 edge, Vector2 chord, float tangentSlope)
    {
        // Zero handles do not constrain direction; the remaining derivative
        // controls supply the one-sided tangent at a stationary endpoint.
        float forward = edge.Dot(chord);
        return forward >= 0 && MathF.Abs(edge.Cross(chord)) <= forward * tangentSlope;
    }

    private static float DistanceSquaredToSegment(Vector2 point, Vector2 start, Vector2 chord, float chordSquared)
    {
        Vector2 offset = point - start;
        float t = Math.Clamp(offset.Dot(chord) / chordSquared, 0, 1);
        return (offset - chord * t).LengthSquared();
    }
}
