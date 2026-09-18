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
    private const float PressureTolerance = 1f / 1024;
    private const float MaxPressureStep = 1f / 16;
    private const int MaxSubdivisionDepth = 12;
    private readonly List<double> _distances = [];
    private readonly List<Vector2> _positions = [];
    private readonly List<float> _radii = [];
    private readonly List<float> _pressures = [];
    private readonly List<Vector2> _tilts = [];
    private bool _lastIsBoundary;

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
            if (i == 0 || !active || _distances[i] == _distances[i - 1])
            {
                Append(point);
                continue;
            }

            int segment = i;
            double from = _distances[i - 1];
            double to = _distances[i];
            // Preserve the shoulders/peak even when a simplified path has only two points.
            if (startLength > from && startLength < to)
            {
                AppendInterval(from, startLength);
                from = startLength;
            }
            if (endStart > from && endStart < to)
            {
                AppendInterval(from, endStart);
                from = endStart;
            }
            AppendInterval(from, to);

            void AppendInterval(double begin, double end)
            {
                var first = Evaluate(begin);
                var last = Evaluate(end);
                if (begin >= startLength && end <= endStart)
                    Append(last);
                else
                    Subdivide(begin, first, end, last, 0, true);
            }

            Sample Evaluate(double distance)
            {
                float t = (float)((distance - _distances[segment - 1]) /
                    (_distances[segment] - _distances[segment - 1]));
                float pp = Mathf.Lerp(input.Pressures[segment - 1], input.Pressures[segment], t) * Factor(distance);
                return new(
                    input.Positions[segment - 1].Lerp(input.Positions[segment], t),
                    pp, radiusSampler(pp),
                    input.Tilts[segment - 1].Lerp(input.Tilts[segment], t));
            }

            void Subdivide(double begin, Sample first, double end, Sample last, int depth, bool endIsBoundary)
            {
                double middle = (begin + end) * 0.5;
                var mid = Evaluate(middle);
                bool split = Math.Abs(last.Pressure - first.Pressure) > MaxPressureStep ||
                    Deviates(first, last, mid, 0.5f) ||
                    Deviates(first, last, Evaluate(begin + (end - begin) * 0.25), 0.25f) ||
                    Deviates(first, last, Evaluate(begin + (end - begin) * 0.75), 0.75f);
                if (split && depth < MaxSubdivisionDepth)
                {
                    Subdivide(begin, first, middle, mid, depth + 1, false);
                    Subdivide(middle, mid, end, last, depth + 1, endIsBoundary);
                }
                else
                    Append(last, endIsBoundary);
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

    private static bool Deviates(Sample first, Sample last, Sample actual, float t)
    {
        float pressure = Mathf.Lerp(first.Pressure, last.Pressure, t);
        float radius = Mathf.Lerp(first.Radius, last.Radius, t);
        float radiusTolerance = Math.Max(0.0001f, Math.Max(Math.Abs(radius), Math.Abs(actual.Radius)) * 0.001f);
        return Math.Abs(pressure - actual.Pressure) > PressureTolerance ||
            Math.Abs(radius - actual.Radius) > radiusTolerance;
    }

    private void Append(Sample sample, bool isBoundary = true)
    {
        // Coincident input positions carry no arc length. Keep the latest stylus
        // state at that position without sending zero-length segments to the shader.
        if (_positions.Count > 0 && _positions[^1] == sample.Position)
        {
            // At large coordinates, an inserted float position can round onto an
            // endpoint/shoulder/peak. It must not overwrite that exact boundary's pp.
            if (_lastIsBoundary && !isBoundary)
                return;
            _pressures[^1] = sample.Pressure;
            _radii[^1] = sample.Radius;
            _tilts[^1] = sample.Tilt;
            _lastIsBoundary = isBoundary;
            return;
        }
        _positions.Add(sample.Position);
        _pressures.Add(sample.Pressure);
        _radii.Add(sample.Radius);
        _tilts.Add(sample.Tilt);
        _lastIsBoundary = isBoundary;
    }

    private readonly record struct Sample(Vector2 Position, float Pressure, float Radius, Vector2 Tilt);
}
