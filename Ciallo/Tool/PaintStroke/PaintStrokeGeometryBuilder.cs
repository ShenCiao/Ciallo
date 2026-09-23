using System;
using System.Collections.Generic;
using Ciallo.Geometry;
using Godot;

namespace Ciallo.Tool;

public readonly record struct PaintStrokeGeometry(
    IReadOnlyList<Vector2> Positions,
    IReadOnlyList<float> Radii,
    IReadOnlyList<float> Pressures,
    IReadOnlyList<Vector2> Tilts);

/// <summary>
/// Builds the rendered/committed stroke from unmodified input samples. Results are
/// views over reusable buffers, valid until the next Build; commit must copy them.
/// </summary>
public sealed class PaintStrokeGeometryBuilder
{
    private readonly List<double> _distances = [];
    private readonly List<Vector2> _positions = [];
    private readonly List<float> _radii = [];
    private readonly List<float> _pressures = [];
    private readonly List<Vector2> _tilts = [];

    public PaintStrokeGeometry Build(
        PolylineSamples input,
        StrokePressureTaper taper,
        Func<float, float> radiusSampler)
    {
        _distances.Clear();
        _positions.Clear();
        _radii.Clear();
        _pressures.Clear();
        _tilts.Clear();
        var result = new PaintStrokeGeometry(_positions, _radii, _pressures, _tilts);
        if (input.Count == 0)
            return result;

        double length = 0;
        _distances.Add(0);
        for (int i = 1; i < input.Count; i++)
        {
            length += input.Positions[i - 1].DistanceTo(input.Positions[i]);
            _distances.Add(length);
        }

        bool active = taper.StartLength > 0 || taper.EndLength > 0;
        if (length == 0)
        {
            // A point is both endpoints. An enabled, nonzero taper forces pp=0.
            float pressure = active ? 0 : input.Pressures[^1];
            Append(new(input.Positions[^1], pressure, radiusSampler(pressure), input.Tilts[^1]));
            return result;
        }

        var (startLength, endLength) = taper.FitTo(length);
        // In the short-path case both boundaries must be exactly the same distance.
        double endStart = (double)taper.StartLength + taper.EndLength >= length
            ? startLength
            : length - endLength;

        for (int i = 0; i < input.Count; i++)
        {
            float pressure = input.Pressures[i] * Factor(_distances[i]);
            var point = new Sample(input.Positions[i], pressure, radiusSampler(pressure), input.Tilts[i]);
            if (i > 0 && active)
            {
                double from = _distances[i - 1];
                double to = _distances[i];
                // Keep the source sampling density. Only add taper shoulders (or
                // the shared short-stroke peak), which must survive sparse input.
                if (startLength > from && startLength < to)
                    Append(Evaluate(startLength));
                if (endStart > from && endStart < to && endStart != startLength)
                    Append(Evaluate(endStart));
            }
            Append(point);

            Sample Evaluate(double distance)
            {
                float t = (float)((distance - _distances[i - 1]) /
                    (_distances[i] - _distances[i - 1]));
                float pp = Mathf.Lerp(input.Pressures[i - 1], input.Pressures[i], t) * Factor(distance);
                return new(
                    input.Positions[i - 1].Lerp(input.Positions[i], t),
                    pp, radiusSampler(pp),
                    input.Tilts[i - 1].Lerp(input.Tilts[i], t));
            }
        }
        return result;

        float Factor(double distance)
        {
            double start = startLength > 0 ? Math.Clamp(distance / startLength, 0, 1) : 1;
            double end = endLength > 0 ? Math.Clamp((length - distance) / endLength, 0, 1) : 1;
            return (float)Math.Min(start, end);
        }
    }

    private void Append(Sample sample)
    {
        // Coincident input positions carry no arc length. Keep the latest stylus
        // state at that position without sending zero-length segments to the shader.
        if (_positions.Count > 0 && _positions[^1] == sample.Position)
        {
            _pressures[^1] = sample.Pressure;
            _radii[^1] = sample.Radius;
            _tilts[^1] = sample.Tilt;
            return;
        }
        _positions.Add(sample.Position);
        _pressures.Add(sample.Pressure);
        _radii.Add(sample.Radius);
        _tilts.Add(sample.Tilt);
    }

    private readonly record struct Sample(Vector2 Position, float Pressure, float Radius, Vector2 Tilt);
}
