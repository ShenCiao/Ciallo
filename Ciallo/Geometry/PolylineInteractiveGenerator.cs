using System;
using System.Collections.Generic;
using Godot;
using InkStrokeModeler;
using NumericsVector2 = System.Numerics.Vector2;

namespace Ciallo.Geometry;

/// <summary>
/// Generates interactive polyline geometry from cursor input.
/// </summary>
/// <remarks>
/// CurrentSamples is a short-lived view over internal buffers. During Moving it
/// may include transient stroke prediction; after End it contains stable samples
/// only. Callers should consume it immediately and persist copies only.
/// </remarks>
public class PolylineInteractiveGenerator
{
    public float CommitSimplificationScreenTolerancePx = 0.15f;
    public float CommitSimplificationMaxSegmentScreenPx = 32f;

    private readonly StrokeModeler _modeler = new();
    private readonly StrokeModelParams _modelerParams = StrokeModelParams.CreateCialloDefault();
    private readonly List<ModelerResult> _modelerResults = new(256);

    private readonly List<Vector2> _stablePositions = new(2048);
    private readonly List<float> _stablePressures = new(2048);
    private readonly List<Vector2> _stableTilts = new(2048);

    private readonly List<Vector2> _predictionPositions = new(256);
    private readonly List<float> _predictionPressures = new(256);
    private readonly List<Vector2> _predictionTilts = new(256);

    private readonly List<Vector2> _positions = new(2304);
    private readonly List<float> _pressures = new(2304);
    private readonly List<Vector2> _tilts = new(2304);
    private readonly List<RawStylusSample> _rawSamples = new(256);

    private CursorButtonData _pendingDown;
    private ModelerInput? _lastModelerInput;
    private double _elapsedSeconds;
    private float _worldUnitsPerPixel = 1f;
    private bool _strokeStarted;

    public PolylineSamples CurrentSamples => new(_positions, _pressures, _tilts);

    // Upper bound for the per-input dt handed to the modeler. Past this bound the
    // spring integrator's sub-step exceeds the explicit-Euler stability limit and
    // diverges to NaN, silently freezing the stroke. At the bound the sub-step is
    // 1 / MinOutputRate (normal drawing cadence), which is stable.
    private double MaxModelerInputDeltaSeconds =>
        _modelerParams.Sampling.MaxOutputsPerCall / _modelerParams.Sampling.MinOutputRate;

    public void Start(CursorButtonData data)
    {
        Clear();
        _modeler.Reset(_modelerParams);
        _pendingDown = data;
        ComposeCurrentGeometry();
    }

    public void Update(CursorMotionData data)
    {
        if (!_strokeStarted)
            BeginStroke(data.Pressure, data.Tilt);

        // Clamp the advance so one stalled input cannot open a huge modeler
        // time gap that diverges the spring integrator (see MaxModelerInputDeltaSeconds).
        _elapsedSeconds += Math.Clamp(data.TimeDelta.TotalSeconds, 0, MaxModelerInputDeltaSeconds);
        UpdateWorldUnitsPerPixel(data);
        AddRawSample(data.WorldPosition, data.Tilt, _elapsedSeconds);
        UpdateModeler(new ModelerInput(
            InputEventType.Move,
            ToNumericsVector2(data.WorldPosition),
            TimeSpan.FromSeconds(_elapsedSeconds),
            data.Pressure,
            -1,
            -1));
        RebuildPrediction();
        ComposeCurrentGeometry();
    }

    public void End(CursorButtonData data)
    {
        if (!_strokeStarted)
            BeginStroke(data.Pressure, data.Tilt);

        AddRawSample(data.WorldPosition, data.Tilt, _elapsedSeconds);
        ClearPredictionGeometry();
        UpdateModeler(new ModelerInput(
            InputEventType.Up,
            ToNumericsVector2(data.WorldPosition),
            TimeSpan.FromSeconds(_elapsedSeconds),
            data.Pressure,
            -1,
            -1));
        SimplifyStableGeometryForCommit();
        ComposeCurrentGeometry();

        _strokeStarted = false;
    }

    public void Clear()
    {
        _stablePositions.Clear();
        _stablePressures.Clear();
        _stableTilts.Clear();
        ClearPredictionGeometry();
        _positions.Clear();
        _pressures.Clear();
        _tilts.Clear();
        _rawSamples.Clear();
        _modelerResults.Clear();
        _elapsedSeconds = 0;
        _worldUnitsPerPixel = 1f;
        _strokeStarted = false;
        _lastModelerInput = null;
    }

    private void BeginStroke(float initialPressure, Vector2 initialTilt)
    {
        _strokeStarted = true;
        _elapsedSeconds = 0;
        _rawSamples.Clear();
        AddRawSample(_pendingDown.WorldPosition, initialTilt, _elapsedSeconds);
        UpdateModeler(new ModelerInput(
            InputEventType.Down,
            ToNumericsVector2(_pendingDown.WorldPosition),
            TimeSpan.Zero,
            initialPressure,
            -1,
            -1));
    }

    private void UpdateModeler(ModelerInput input)
    {
        if (_lastModelerInput.HasValue && _lastModelerInput.Value == input)
            return;

        _modelerResults.Clear();
        _modeler.Update(input, _modelerResults);
        _lastModelerInput = input;
        AppendStableResults(_modelerResults);
    }

    private void RebuildPrediction()
    {
        ClearPredictionGeometry();

        if (_modelerParams.Prediction is PredictionParams.Disabled)
        {
            // Zero extra lag
            AppendRawPrediction(_lastModelerInput.Value);
            return;
        }

        _modelerResults.Clear();
        _modeler.Predict(_modelerResults);
        AppendResults(_modelerResults, _predictionPositions, _predictionPressures, _predictionTilts);
    }

    private void AppendRawPrediction(ModelerInput input)
    {
        Vector2 position = ToVector2(input.Position);
        if (_stablePositions.Count > 0 && _stablePositions[^1] == position)
            return;

        float pressure = input.Pressure < 0 ? 1f : Mathf.Clamp(input.Pressure, 0f, 1f);
        _predictionPositions.Add(position);
        _predictionPressures.Add(pressure);
        _predictionTilts.Add(TiltAt(input.Time.TotalSeconds));
    }

    private void ClearPredictionGeometry()
    {
        _predictionPositions.Clear();
        _predictionPressures.Clear();
        _predictionTilts.Clear();
    }

    private void AppendStableResults(IReadOnlyList<ModelerResult> results) =>
        AppendResults(results, _stablePositions, _stablePressures, _stableTilts);

    private void AppendResults(
        IReadOnlyList<ModelerResult> results,
        List<Vector2> positions,
        List<float> pressures,
        List<Vector2> tilts)
    {
        foreach (var result in results)
        {
            // Safety net: a non-finite sample poisons the bounding box (Godot culls the
            // stroke) and feeds back into the next update, freezing the stroke. Drop it so
            // the stroke self-recovers. The dt clamp prevents divergence; this backs it up.
            var position = ToVector2(result.Position);
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
                continue;

            float pressure = result.Pressure < 0 ? 1f : Mathf.Clamp(result.Pressure, 0f, 1f);
            positions.Add(position);
            pressures.Add(pressure);
            tilts.Add(TiltAt(result.Time.TotalSeconds));
        }
    }

    private void ComposeCurrentGeometry()
    {
        _positions.Clear();
        _pressures.Clear();
        _tilts.Clear();
        _positions.AddRange(_stablePositions);
        _pressures.AddRange(_stablePressures);
        _tilts.AddRange(_stableTilts);
        _positions.AddRange(_predictionPositions);
        _pressures.AddRange(_predictionPressures);
        _tilts.AddRange(_predictionTilts);
    }

    private void UpdateWorldUnitsPerPixel(CursorMotionData data)
    {
        float screenDistance = data.ScreenDelta.Length();
        if (screenDistance > 0f)
            _worldUnitsPerPixel = data.WorldDelta.Length() / screenDistance;
    }

    private void SimplifyStableGeometryForCommit()
    {
        if (CommitSimplificationScreenTolerancePx <= 0f || _stablePositions.Count <= 2)
            return;

        float worldTolerance = CommitSimplificationScreenTolerancePx * _worldUnitsPerPixel;
        float maxSegmentLength = CommitSimplificationMaxSegmentScreenPx * _worldUnitsPerPixel;
        var simplifiedPositions = _stablePositions.SimplifyRdp(worldTolerance, out var originalIndices, maxSegmentLength);

        _stablePositions.Clear();
        _stablePositions.AddRange(simplifiedPositions);
        KeepOriginalIndices(_stablePressures, originalIndices);
        KeepOriginalIndices(_stableTilts, originalIndices);
    }

    private static void KeepOriginalIndices<T>(List<T> values, IReadOnlyList<int> originalIndices)
    {
        var kept = new List<T>(originalIndices.Count);
        foreach (int index in originalIndices)
            kept.Add(values[index]);

        values.Clear();
        values.AddRange(kept);
    }

    private void AddRawSample(Vector2 position, Vector2 tilt, double time) =>
        _rawSamples.Add(new RawStylusSample(position, tilt, time));

    private Vector2 TiltAt(double time)
    {
        if (_rawSamples.Count == 0) return Vector2.Zero;
        if (time <= _rawSamples[0].Time) return _rawSamples[0].Tilt;

        for (int i = 1; i < _rawSamples.Count; i++)
        {
            var end = _rawSamples[i];
            if (time > end.Time) continue;

            var start = _rawSamples[i - 1];
            if (end.Time == start.Time) return end.Tilt;
            float t = (float)((time - start.Time) / (end.Time - start.Time));
            return start.Tilt.Lerp(end.Tilt, t);
        }

        return _rawSamples[^1].Tilt;
    }

    private static NumericsVector2 ToNumericsVector2(Vector2 value) => new(value.X, value.Y);

    private static Vector2 ToVector2(NumericsVector2 value) => new(value.X, value.Y);

    private readonly record struct RawStylusSample(Vector2 Position, Vector2 Tilt, double Time);
}
