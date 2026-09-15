using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Geometry;
using Frent;
using Godot;
using ObservableCollections;
using R3;

namespace Ciallo.Tool;

// All work stays on the main thread. Rebuild lazily after stroke geometry changes.
public sealed class BucketFillContext : IDisposable
{
    private readonly Entity _layer;
    private readonly Dictionary<Entity, ImmutableArray<Vector2>> _strokes = [];
    private readonly CompositeDisposable _subscriptions = new();
    private BucketFillSolver _solver;
    public event Action SourceChanged;

    public BucketFillContext(Entity layer)
    {
        _layer = layer;
        var lookup = layer.Get<ChildShapePolylineLookup>().Polylines;
        foreach (var (shape, polyline) in lookup)
            if (IsBoundaryStroke(shape)) _strokes.Add(shape, polyline.Positions);

        lookup.ObserveDictionaryAdd().Subscribe(e => UpdateStroke(e.Key, e.Value.Positions)).AddTo(_subscriptions);
        lookup.ObserveDictionaryReplace().Subscribe(e => UpdateStroke(e.Key, e.NewValue.Positions)).AddTo(_subscriptions);
        lookup.ObserveDictionaryRemove().Subscribe(e => RemoveStroke(e.Key)).AddTo(_subscriptions);
        lookup.ObserveClear().Subscribe(_ =>
        {
            if (_strokes.Count == 0) return;
            _strokes.Clear();
            Invalidate();
        }).AddTo(_subscriptions);
    }

    private static bool IsBoundaryStroke(Entity shape) => shape.Has<StrokeSetting>() && shape.Has<SampledPolyline>();

    private void UpdateStroke(Entity shape, ImmutableArray<Vector2> positions)
    {
        if (!IsBoundaryStroke(shape))
        {
            RemoveStroke(shape);
            return;
        }
        if (_strokes.TryGetValue(shape, out var previous) && previous == positions) return;
        _strokes[shape] = positions;
        Invalidate();
    }

    private void RemoveStroke(Entity shape)
    {
        if (_strokes.Remove(shape)) Invalidate();
    }

    private void Invalidate()
    {
        _solver?.Dispose();
        _solver = null;
        SourceChanged?.Invoke();
    }

    public BucketFillRegion Query(Vector2 point, BucketFillOptions options)
    {
        _solver ??= BucketFillSolver.Build([.. _strokes.Values]);
        return _solver.Query(point, options.GapAware.Value, options.GapFactor.Value);
    }

    public bool Commit(Vector2 point, BucketFillOptions options, Entity brush, bool placeAtBottom)
    {
        // Release queries the current geometry synchronously; source changes cancel capture.
        var region = Query(point, options);
        if (region.IsEmpty) return false;

        // Bridge only for the single-polyline document format, before creating any entities.
        var polygons = new ImmutableArray<Vector2>[region.Polygons.Length];
        for (int i = 0; i < polygons.Length; i++)
        {
            var boundaries = region.Polygons[i]
                .Select(ring => (IReadOnlyList<Vector2>)ring.AsSpan()[..^1].ToArray()).ToArray();
            var ring = boundaries.ConnectHoles();
            polygons[i] = [.. ring, ring[0]];
        }
        var command = new CommandBuilder("Bucket Fill", _layer);
        for (int i = 0; i < polygons.Length; i++)
        {
            var ring = polygons[i];
            ImmutableArray<float> ones = [.. Enumerable.Repeat(1f, ring.Length)];
            ImmutableArray<Vector2> zeros = [.. Enumerable.Repeat(Vector2.Zero, ring.Length)];
            command.SetTarget(_layer.World.Create())
                .NewFilledPolygon()
                .AddToLayerTree(_layer, placeAtBottom ? i : -1)
                .SetSampledPolyline(ring, ones, ones, zeros)
                .SetProperty(e => e.Get<FilledPolygonSetting>().BrushE, brush);
        }
        command.Commit();
        return true;
    }

    public void Dispose()
    {
        _subscriptions.Dispose();
        _solver?.Dispose();
    }
}
