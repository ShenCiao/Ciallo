using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Frent;
using Godot;

namespace Ciallo.Tool;

[RegisterState]
public class TrimInteractor : CapturingInteraction
{
    [StateAccess]
    public TrimTool Tool { get; set; }

    // Undercut intentionally leaves a tiny amount of source geometry around cuts so the rebuilt
    // arrangement feels connected. This is a drawing-tool heuristic, not a topology guarantee.
    // Be willing to delete tiny real strokes if that makes the common visual result cleaner.
    private const float UndercutDistanceWorld = 0.1f;
    private const float MinKeptBoundsSize = 1.5f; // world units; bbox shorter side filter
    private const float MinKeptLength = 2f; // world units; aggressively culls tiny epsilon tails

    private readonly List<Vector2> _gesture = [];
    private StrokeView _gestureView;

    // Append-only preview node pool keyed by (source entity, fromT, toT) so we never
    // recompute or re-flush a doomed segment that's already been visualized.
    private readonly Dictionary<(Entity, float, float), StrokeView> _doomedPreviews = [];

    private Arrangement _arrSnapshot;
    private HashSet<Entity> _sourceSnapshot;

    // Throttle the per-frame work (CGAL query + preview update) to ~60Hz.
    public override TimeSpan MovingMinInterval => TimeSpan.FromMilliseconds(16.6);

    public override void Start(CursorButtonData data)
    {
        _arrSnapshot = Tool.Arrangement.ArrReady.CurrentValue;
        _sourceSnapshot = [.. Tool.Arrangement.SourceShapes];
        _gesture.Clear();
        _gesture.Add(data.WorldPosition);

        _gestureView = new StrokeView { Material = AutoloadRendering.DashWireframeMaterial };
        Document.Get<WorldOverlay>().AddChild(_gestureView);

        UpdateGestureView();
    }

    // Our system generates that during interactor moving, no change applies to the arrangement.
    public override void Moving(CursorMotionData data)
    {
        _gesture.Add(data.WorldPosition);
        UpdateGestureView();
        RefreshDoomedPreview();
    }

    public override void End(CursorButtonData data)
    {
        // Final query off the full gesture, then commit.
        var hits = QueryHits();
        if (hits.Length > 0)
            CommitTrim(hits);
        Clear();
    }

    public override void Cancel() => Clear();

    private void UpdateGestureView()
    {
        _gestureView.SetGeometry(_gesture, AppPreference.StrokeWireframeRadius);
    }

    private PolylineEdgeHit[] QueryHits()
    {
        if (_arrSnapshot == null || _gesture.Count < 2) return [];
        return _arrSnapshot.PolylineQueryEdges([.. _gesture]);
    }

    private void RefreshDoomedPreview()
    {
        var hits = QueryHits();
        foreach (var hit in hits)
        {
            var key = (hit.SourceShape, hit.FromT, hit.ToT);
            if (_doomedPreviews.ContainsKey(key)) continue;
            if (!_sourceSnapshot.Contains(hit.SourceShape)) continue;
            if (!hit.SourceShape.IsAlive || !hit.SourceShape.Has<SampledPolyline>()) continue;

            var geom = hit.SourceShape.Get<SampledPolyline>();
            var slicePts = geom.Positions.Value.Slice(hit.FromT, hit.ToT);
            if (slicePts.Length < 2) continue;

            var preview = new StrokeView { Material = AutoloadRendering.DashWireframeMaterial };
            Document.Get<WorldOverlay>().AddChild(preview);
            preview.SetGeometry(slicePts, AppPreference.StrokeWireframeRadius);
            _doomedPreviews[key] = preview;
        }
    }

    private void Clear()
    {
        _gesture.Clear();
        _gestureView?.QueueFree();
        _gestureView = null;
        foreach (var view in _doomedPreviews.Values) view.QueueFree();
        _doomedPreviews.Clear();
        _arrSnapshot = null;
        _sourceSnapshot = null;
    }

    private void CommitTrim(PolylineEdgeHit[] hits)
    {
        // Group by parent layer, then process shapes by descending sibling index so each
        // AddToLayerTreeCmd's static insertion index stays valid within that layer.
        var groups = hits
            .Where(h => _sourceSnapshot.Contains(h.SourceShape))
            .Where(h => h.SourceShape.IsAlive && h.SourceShape.Has<SampledPolyline>())
            .GroupBy(h => h.SourceShape)
            .Select(g =>
            {
                var sourceLayer = g.Key.Get<LayerTreeNode>().ParentValue;
                var sourceLayerNode = sourceLayer.Get<LayerTreeNode>();
                return new
                {
                    SourceE = g.Key,
                    Hits = g.ToList(),
                    SourceLayer = sourceLayer,
                    SourceLayerNode = sourceLayerNode,
                    Index = sourceLayerNode.Children.IndexOf(g.Key),
                };
            })
            .Where(x => x.Index >= 0)
            .GroupBy(x => x.SourceLayer)
            .OrderByDescending(layerGroup => layerGroup.Key.PackedValue)
            .SelectMany(layerGroup => layerGroup.OrderByDescending(x => x.Index))
            .ToArray();

        var cmd = new CommandBuilder("Trim");
        bool any = false;

        foreach (var entry in groups)
        {
            var sourceE = entry.SourceE;
            if (!CanRebuildTrimmedShape(sourceE)) continue;

            var geom = sourceE.Get<SampledPolyline>();
            var sourcePositions = geom.Positions.Value;
            int n = sourcePositions.Length;
            if (n < 2) continue;

            var keptRanges = TrimGeometry.InvertDoomedRanges(
                entry.Hits,
                sourcePositions,
                UndercutDistanceWorld);
            int originalIndex = entry.Index;

            cmd.SetTarget(sourceE).RemoveFromLayerTree().DeleteShape();
            any = true;

            int insertOffset = 0;
            foreach (var (from, to) in keptRanges)
            {
                if (to - from < 1e-4f) continue; // zero-length kept piece

                var pieceGeom = SliceGeometry(geom, from, to);
                if (pieceGeom.positions.Length < 2) continue;

                if (PieceTooSmall(pieceGeom.positions)) continue;

                var newE = PrimaryLayer.World.Create();
                AddShapeCreation(cmd.SetTarget(newE), sourceE)
                    .AddToLayerTree(entry.SourceLayer, originalIndex + insertOffset)
                    .SetSampledPolyline(
                        pieceGeom.positions,
                        pieceGeom.radii,
                        pieceGeom.pressures,
                        pieceGeom.tilts);
                insertOffset++;
            }
        }

        if (any) cmd.Commit();
    }

    private static bool CanRebuildTrimmedShape(Entity sourceE) =>
        sourceE.Has<StrokeSetting>() || sourceE.Has<FilledPolygonSetting>();

    private static CommandBuilder AddShapeCreation(CommandBuilder cmd, Entity sourceE)
    {
        if (sourceE.Has<StrokeSetting>())
            return cmd.NewStroke(sourceE);
        if (sourceE.Has<FilledPolygonSetting>())
            return cmd.NewFilledPolygon(sourceE);
        throw new InvalidOperationException("Unsupported trim source shape.");
    }

    private static (
        ImmutableArray<Vector2> positions,
        ImmutableArray<float> radii,
        ImmutableArray<float> pressures,
        ImmutableArray<Vector2> tilts) SliceGeometry(SampledPolyline geom, float from, float to)
    {
        var pos = geom.Positions.Value.Slice(from, to);
        var rad = geom.Radii.Value.Length == geom.Positions.Value.Length
            ? geom.Radii.Value.Slice(from, to)
            : geom.Radii.Value;
        var pr = geom.Pressures.Value.Length == geom.Positions.Value.Length
            ? geom.Pressures.Value.Slice(from, to)
            : geom.Pressures.Value;
        var ti = geom.Tilts.Value.Length == geom.Positions.Value.Length
            ? geom.Tilts.Value.Slice(from, to)
            : geom.Tilts.Value;
        return (pos, rad, pr, ti);
    }

    private static bool PieceTooSmall(ImmutableArray<Vector2> positions)
    {
        if (positions.Length < 2) return true;
        var b = positions.GetBoundingBox();
        return (b.Size.X < MinKeptBoundsSize && b.Size.Y < MinKeptBoundsSize)
            || positions.GetLength() < MinKeptLength;
    }
}
