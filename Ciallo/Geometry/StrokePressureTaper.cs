using System;

namespace Ciallo.Geometry;

/// <summary>Requested start/end distances in document coordinates. Zero disables an end.</summary>
public readonly record struct StrokePressureTaper(float StartLength, float EndLength)
{
    public (double Start, double End) FitTo(double pathLength)
    {
        double sum = (double)StartLength + EndLength;
        double scale = sum > 0 ? Math.Min(1, pathLength / sum) : 1;
        return (StartLength * scale, EndLength * scale);
    }
}
