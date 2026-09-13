using System;
using System.Diagnostics.Contracts;
using Godot;

namespace Ciallo.Geometry;

/// <summary>
/// Rational quadratic Bézier curve utilities with per-point weights.
/// </summary>
public static class RationalQuadraticBezier
{
    /// <summary>
    /// Sample a rational quadratic Bézier curve with three weights.
    /// </summary>
    /// <param name="p0">Start point</param>
    /// <param name="p1">Control point</param>
    /// <param name="p2">End point</param>
    /// <param name="w0">Weight at p0</param>
    /// <param name="w1">Weight at p1</param>
    /// <param name="w2">Weight at p2</param>
    /// <param name="t">Parameter [0,1]</param>
    [Pure]
    public static Vector2 Sample(Vector2 p0, Vector2 p1, Vector2 p2, float w0, float w1, float w2, float t)
    {
        float u = 1f - t;
        float b0 = u * u * w0;
        float b1 = 2f * u * t * w1;
        float b2 = t * t * w2;
        float sum = b0 + b1 + b2;

        if (Mathf.IsZeroApprox(sum))
            return p0.Lerp(p2, t); // Degenerate case

        return (b0 * p0 + b1 * p1 + b2 * p2) / sum;
    }

    /// <summary>
    /// Sample a rational quadratic Bézier curve with simplified form (w0=w2=1).
    /// </summary>
    [Pure]
    public static Vector2 SampleSimplified(Vector2 p0, Vector2 p1, Vector2 p2, float w1, float t)
    {
        return Sample(p0, p1, p2, 1f, w1, 1f, t);
    }

    /// <summary>
    /// Tessellate a rational quadratic Bézier curve into a polyline.
    /// </summary>
    /// <param name="subdivisions">Number of segments (must be >= 1)</param>
    public static Vector2[] Tessellate(Vector2 p0, Vector2 p1, Vector2 p2, float w0, float w1, float w2, int subdivisions)
    {
        if (subdivisions < 1)
            throw new ArgumentException("Subdivisions must be at least 1", nameof(subdivisions));

        var points = new Vector2[subdivisions + 1];
        for (int i = 0; i <= subdivisions; i++)
        {
            float t = (float)i / subdivisions;
            points[i] = Sample(p0, p1, p2, w0, w1, w2, t);
        }
        return points;
    }

    /// <summary>
    /// Calculate the weight needed for a circular arc of given angle.
    /// </summary>
    /// <param name="halfAngle">Half of the arc's total angle in radians</param>
    [Pure]
    public static float CircularArcWeight(float halfAngle)
    {
        return MathF.Cos(halfAngle);
    }
}
