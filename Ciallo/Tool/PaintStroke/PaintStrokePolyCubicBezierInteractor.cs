using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Frent;
using Godot;

namespace Ciallo.Tool;

[RegisterState]
public class PaintStrokePolyCubicBezierInteractor : PolyCubicBezierInteractor
{
    [StateAccess] public PaintStrokeTool Tool { get; set; }
    private Entity _brush;
    private StrokeView _preview;
    private readonly PaintStrokeGeometryBuilder _geometryBuilder = new();
    private Func<float, float> _radiusSampler;
    private PolylineSamples _previewSamples;
    private float _previewPressure;
    private readonly List<float> _previewPressures = [];
    private readonly List<Vector2> _previewTilts = [];
    private readonly List<Vector2> _snapHintPoints = new(2);
    private MultiMeshInstance2D _snapDots;

    protected override float InputPressure => Tool.CurvePressure.Value;
    protected override PaintStrokeSnapTarget? FindSnapTarget(Vector2 position) => Tool.TryFindSnapTarget(position);

    protected override void CreatePreview()
    {
        _brush = Document.Get<SelectionManager>().WorkingStrokeBrush.Value;
        _radiusSampler = _brush.Get<StrokeBrushSetting>().ToRadiusSampler();
        _preview = new StrokeView { Material = _brush.Get<StrokeBrushMaterial>() };
        PrimaryLayer.Get<ShapeLayerView>().AddChild(_preview);
        _snapDots = AutoloadRendering.CreateDots();
        Document.Get<WorldOverlay>().AddChild(_snapDots);
    }

    protected override void UpdatePreview(IReadOnlyList<Vector2> points)
    {
        if (_previewPressures.Count > points.Count)
        {
            _previewPressures.RemoveRange(points.Count, _previewPressures.Count - points.Count);
            _previewTilts.RemoveRange(points.Count, _previewTilts.Count - points.Count);
        }
        while (_previewPressures.Count < points.Count)
        {
            _previewPressures.Add(_previewPressure);
            _previewTilts.Add(Vector2.Zero);
        }
        _previewSamples = new(points, _previewPressures, _previewTilts);
        RefreshPressurePreview();
        _snapHintPoints.Clear();
        if (StartSnapTarget is { } start)
            _snapHintPoints.Add(start.HitPoint);
        if (PreviewSnapTarget is { } end &&
            (StartSnapTarget is not { } first || !first.HitPoint.IsEqualApprox(end.HitPoint)))
            _snapHintPoints.Add(end.HitPoint);
        _snapDots.Visible = _snapHintPoints.Count > 0;
        if (_snapHintPoints.Count > 0)
            _snapDots.SetDotGeometry(_snapHintPoints, AppPreference.StrokeDotRadius);
    }

    internal void RefreshPressurePreview()
    {
        if (_previewPressure != InputPressure)
        {
            _previewPressure = InputPressure;
            CollectionsMarshal.AsSpan(_previewPressures).Fill(_previewPressure);
        }
        var geometry = _geometryBuilder.Build(_previewSamples, Tool.PressureTaper, _radiusSampler);
        _preview.SetGeometry(geometry.Positions, geometry.Radii, geometry.Pressures);
    }

    protected override void Commit(PolylineSamples samples, bool closed)
    {
        var targetLayer = Tool.ResolveStrokeTargetLayer();
        if (targetLayer.IsNull || targetLayer.IsDyingOrDead) return;
        // Closed curves have no free endpoints to extend or trim against other strokes.
        if (!closed)
            samples = PaintStrokeSnap.BuildRepairedGeometry(
                Tool.Arrangement.ArrReady.CurrentValue, samples,
                StartSnapTarget, EndSnapTarget, Tool.SnapDistance.Value);
        var geometry = _geometryBuilder.Build(samples, Tool.PressureTaper, _radiusSampler);
        new CommandBuilder("Paint Stroke (Poly Cubic Bézier)", PrimaryLayer.World.Create())
            .NewStroke().AddToLayerTree(targetLayer)
            .SetProperty(e => e.Get<StrokeSetting>().Brush, _brush)
            .SetSampledPolyline(geometry.Positions.ToImmutableArray(), geometry.Radii.ToImmutableArray(),
                geometry.Pressures.ToImmutableArray(), geometry.Tilts.ToImmutableArray())
            .Commit();
    }

    protected override void ClearPreview()
    {
        _preview.QueueFree();
        _preview = null;
        _snapDots.QueueFree();
        _snapDots = null;
        _previewSamples = default;
        _previewPressures.Clear();
        _previewTilts.Clear();
    }
}
