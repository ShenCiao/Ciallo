using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Frent;
using Godot;

namespace Ciallo.Tool;

[RegisterState]
public class PaintStrokeInteractor : CapturingInteraction
{
    [StateAccess]
    public PaintStrokeTool Tool { get; set; }
    public Entity BrushE;
    public StrokeView StrokePreview;
    public readonly PolylineInteractiveGenerator Generator = new();
    private readonly PaintStrokeGeometryBuilder _geometryBuilder = new();
    private Func<float, float> _radiusSampler;
    private PaintStrokeSnapTarget? _startSnapTarget;
    private PaintStrokeSnapTarget? _endSnapTarget;
    private readonly List<Vector2> _snapHintPoints = new(2);
    private MultiMeshInstance2D _snapDots;

    public override TimeSpan MovingMinInterval => TimeSpan.Zero;

    public override void Start(CursorButtonData data)
    {
        Input.MouseMode = Input.MouseModeEnum.Hidden;

        // Selection in brush library has higher priority
        if (AppStrokeBrushLibrary.HasSelection)
        {
            var setting = AppStrokeBrushLibrary.SelectedBrushSetting.CurrentValue;
            new CommandBuilder("Use Library Stroke Brush", Document.World.Create())
                .NewStrokeBrush(setting).SetWorkingStrokeBrush().Commit();
            AppStrokeBrushLibrary.SelectedIndex.Value = -1;
        }
        BrushE = Document.Get<SelectionManager>().WorkingStrokeBrush.Value;

        var brushMaterial = BrushE.Get<StrokeBrushMaterial>();

        StrokePreview = new StrokeView
        {
            Material = brushMaterial
        };
        var layerView = PrimaryLayer.Get<ShapeLayerView>();
        layerView.AddChild(StrokePreview);

        _snapDots = AutoloadRendering.CreateDots();
        Document.Get<WorldOverlay>().AddChild(_snapDots);

        var brushSetting = BrushE.Get<StrokeBrushSetting>();
        _radiusSampler = brushSetting.ToRadiusSampler();

        _startSnapTarget = Tool.TryFindSnapTarget(data.WorldPosition);
        _endSnapTarget = null;
        Generator.Start(data);
        RefreshPressurePreview();
        UpdateSnapHint();
    }

    public override void Moving(CursorMotionData data)
    {
        Generator.Update(data);
        RefreshEndSnapTarget(data.WorldPosition);
        RefreshPressurePreview();
        UpdateSnapHint();
    }

    public override void End(CursorButtonData data)
    {
        Generator.End(data);
        var geometry = BuildCommitGeometry(data);
        var targetLayer = Tool.ResolveStrokeTargetLayer();
        if (targetLayer.IsNull || targetLayer.IsDyingOrDead)
        {
            Clear();
            return;
        }

        new CommandBuilder("Paint Stroke", PrimaryLayer.World.Create())
            .NewStroke()
            .AddToLayerTree(targetLayer)
            .SetProperty(e => e.Get<StrokeSetting>().Brush, BrushE)
            .SetSampledPolyline(geometry.Positions.ToImmutableArray(), geometry.Radii.ToImmutableArray(),
                geometry.Pressures.ToImmutableArray(), geometry.Tilts.ToImmutableArray())
            .Commit();
        Clear();
    }

    public override void Cancel() => Clear();

    public void Clear()
    {
        Generator.Clear();
        StrokePreview.QueueFree();
        StrokePreview = null;
        _startSnapTarget = null;
        _endSnapTarget = null;
        _snapDots?.QueueFree();
        _snapDots = null;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    protected PaintStrokeGeometry BuildCommitGeometry(CursorButtonData data)
    {
        RefreshEndSnapTarget(data.WorldPosition);
        var samples = PaintStrokeSnap.BuildRepairedGeometry(
            Tool.Arrangement.ArrReady.CurrentValue,
            Generator.CurrentSamples,
            _startSnapTarget,
            _endSnapTarget,
            Tool.SnapDistance.Value);
        return _geometryBuilder.Build(samples, Tool.PressureTaper, _radiusSampler);
    }

    internal void RefreshPressurePreview()
    {
        var geometry = _geometryBuilder.Build(Generator.CurrentSamples, Tool.PressureTaper, _radiusSampler);
        StrokePreview.SetGeometry(geometry.Positions, geometry.Radii, geometry.Pressures);
    }

    private void RefreshEndSnapTarget(Vector2 worldPosition)
    {
        _endSnapTarget = Tool.TryFindSnapTarget(worldPosition);
    }

    private void UpdateSnapHint()
    {
        _snapHintPoints.Clear();
        if (_startSnapTarget is { } startTarget)
            _snapHintPoints.Add(startTarget.HitPoint);
        if (_endSnapTarget is { } endTarget)
            _snapHintPoints.Add(endTarget.HitPoint);

        _snapDots.Visible = _snapHintPoints.Count > 0;
        if (_snapHintPoints.Count > 0)
            _snapDots.SetDotGeometry(_snapHintPoints, AppPreference.StrokeDotRadius);
    }
}
