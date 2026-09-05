using System;
using System.Diagnostics.Contracts;
using Godot;

namespace Ciallo.Geometry;

/// <summary>Rational cubic Bézier utilities.</summary>
public static class RationalCubicBezier
{
    [Pure]
    public static Vector2 Sample(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
        float w0, float w1, float w2, float w3, float t)
    {
        float u = 1f - t;
        float b0 = u * u * u * w0;
        float b1 = 3f * u * u * t * w1;
        float b2 = 3f * u * t * t * w2;
        float b3 = t * t * t * w3;
        float sum = b0 + b1 + b2 + b3;
        if (Mathf.IsZeroApprox(sum))
            return p0.Lerp(p3, t);
        return (b0 * p0 + b1 * p1 + b2 * p2 + b3 * p3) / sum;
    }

    public static Vector2[] Tessellate(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3,
        float w0, float w1, float w2, float w3, int subdivisions)
    {
        if (subdivisions < 1)
            throw new ArgumentException("Subdivisions must be at least 1", nameof(subdivisions));
        var points = new Vector2[subdivisions + 1];
        for (int i = 0; i <= subdivisions; i++)
            points[i] = Sample(p0, p1, p2, p3, w0, w1, w2, w3, (float)i / subdivisions);
        return points;
    }
}
