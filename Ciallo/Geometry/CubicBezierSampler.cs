using System;
using System.Collections.Generic;
using Godot;

namespace Ciallo.Geometry;

/// <summary>Reusable buffers for approximately equal arc-length samples of one cubic.</summary>
public sealed class CubicBezierSampler
{
    private readonly List<Vector2> _curve = [];
    private readonly List<double> _lengths = [];
    private readonly List<Vector2> _samples = [];

    // Appends the endpoint but not the start, so adjacent cubics share their anchor.
    public void AppendTo(List<Vector2> result, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
        float spacing, float maxDeviation)
    {
        // Work relative to the start to avoid losing precision on translated curves.
        Vector2 origin = p0;
        Vector2 endpoint = p3;
        p1 -= origin;
        p2 -= origin;
        p3 -= origin;
        float tolerance = Math.Min(spacing, maxDeviation) * 0.01f;
        _curve.Clear();
        _curve.Add(Vector2.Zero);
        Flatten(Vector2.Zero, p1, p2, p3, tolerance * tolerance, 0);

        _lengths.Clear();
        _lengths.Add(0);
        for (int i = 1; i < _curve.Count; i++)
            _lengths.Add(_lengths[^1] + (double)_curve[i - 1].DistanceTo(_curve[i]));

        double length = _lengths[^1];
        if (length == 0)
        {
            result.Add(endpoint);
            return;
        }

        int count = Math.Max(1, (int)Math.Ceiling(length / spacing));
        float allowedDeviation = maxDeviation - tolerance;
        while (true)
        {
            Sample(length, count);
            if (WithinDeviation(length / count, allowedDeviation * allowedDeviation))
                break;
            // Increase the whole segment's density to keep its arc intervals uniform.
            count *= 2;
        }
        for (int i = 1; i < _samples.Count - 1; i++)
            result.Add(origin + _samples[i]);
        // Preserve the authored endpoint exactly, including after translating back.
        result.Add(endpoint);
    }

    private void Flatten(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
        float toleranceSquared, int depth)
    {
        float chord = p0.DistanceTo(p3);
        float perimeter = p0.DistanceTo(p1) + p1.DistanceTo(p2) + p2.DistanceTo(p3);
        // The control hull bounds deviation from the chord. The length bound also
        // catches collinear reversals, which a distance-to-infinite-line test misses.
        if (depth == 20 ||
            (DistanceSquaredToSegment(p1, p0, p3) <= toleranceSquared &&
             DistanceSquaredToSegment(p2, p0, p3) <= toleranceSquared &&
             perimeter - chord <= perimeter * 0.0001f))
        {
            _curve.Add(p3);
            return;
        }

        Vector2 a = (p0 + p1) * 0.5f;
        Vector2 b = (p1 + p2) * 0.5f;
        Vector2 c = (p2 + p3) * 0.5f;
        Vector2 d = (a + b) * 0.5f;
        Vector2 e = (b + c) * 0.5f;
        Vector2 middle = (d + e) * 0.5f;
        Flatten(p0, a, d, middle, toleranceSquared, depth + 1);
        Flatten(middle, e, c, p3, toleranceSquared, depth + 1);
    }

    private void Sample(double length, int count)
    {
        _samples.Clear();
        _samples.Add(Vector2.Zero);
        int edge = 1;
        for (int i = 1; i < count; i++)
        {
            double distance = length * i / count;
            while (_lengths[edge] < distance)
                edge++;
            float t = (float)((distance - _lengths[edge - 1]) /
                              (_lengths[edge] - _lengths[edge - 1]));
            _samples.Add(_curve[edge - 1].Lerp(_curve[edge], t));
        }
        _samples.Add(_curve[^1]);
    }

    private bool WithinDeviation(double step, float toleranceSquared)
    {
        int segment = 0;
        for (int i = 1; i < _curve.Count - 1; i++)
        {
            while (segment < _samples.Count - 2 && _lengths[i] > (segment + 1) * step)
                segment++;
            // Each intervening edge is inside the same convex tolerance capsule
            // when its endpoints are. Flattening consumed the remaining error budget.
            if (DistanceSquaredToSegment(_curve[i], _samples[segment], _samples[segment + 1]) >
                toleranceSquared)
                return false;
        }
        return true;
    }

    private static float DistanceSquaredToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 edge = end - start;
        float lengthSquared = edge.LengthSquared();
        if (lengthSquared == 0) return point.DistanceSquaredTo(start);
        float t = Math.Clamp((point - start).Dot(edge) / lengthSquared, 0f, 1f);
        return point.DistanceSquaredTo(start + edge * t);
    }
}
