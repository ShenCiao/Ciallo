using System;
using System.Diagnostics.Contracts;
using Godot;

namespace Ciallo.Geometry;

public static class FloatExtension
{
    [Pure] public static float Remap(this float value, float fromMin, float fromMax, float toMin, float toMax)
    {
        return Mathf.Lerp(toMin, toMax, Mathf.InverseLerp(fromMin, fromMax, value));
    }

    [Pure] public static float SmoothRemap(this float value, float fromMin, float fromMax, float toMin, float toMax)
    {
        float t = Mathf.InverseLerp(fromMin, fromMax, value);
        t = t * t * (3f - 2f * t); // Smoothstep
        return Mathf.Lerp(toMin, toMax, t);
    }

    [Pure] public static (int i, float f) Modf(this float v)
    {
        int i = (int)Math.Floor(v);
        float frac = v - i;
        return (i, frac);
    }
}
