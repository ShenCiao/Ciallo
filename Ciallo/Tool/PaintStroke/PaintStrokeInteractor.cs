using System;
using System.Collections.Generic;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Frent;
using Godot;

namespace Ciallo.Tool;

public class PaintStrokeInteractor : ActiveInteractionSessionBase
{
    public new PaintStrokeTool Tool => (PaintStrokeTool)base.Tool;
    public Entity BrushE;
    public StrokeView StrokePreview;
    public readonly PolylineInteractiveGenerator Generator = new()
    {
        Mode = PolylineInteractiveGenerator.RadiusMode.Sampled,
    };
    private PaintStrokeSnapTarget? _startSnapTarget;
    private PaintStrokeSnapTarget? _endSnapTarget;
    private readonly List<Vector2> _snapHintPoints = new(2);
    private MultiMeshInstance2D _snapDots;

    public static readonly ToolBase.Trigger PaintEnd = new("PaintEnd");

    public PaintStrokeInteractor()
    {
        MovingMinInterval = TimeSpan.Zero;
    }

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
        var layerView = WorkingLayer.Get<ShapeLayerView>();
        layerView.AddChild(StrokePreview);

        _snapDots = AutoloadRendering.CreateDots();
        Document.Get<WorldOverlay>().AddChild(_snapDots);

        var brushSetting = BrushE.Get<StrokeBrushSetting>();
        Generator.RadiusSampler = brushSetting.ToRadiusSampler();

        _startSnapTarget = Tool.TryFindSnapTarget(data.WorldPosition);
        _endSnapTarget = null;
        Generator.Start(data);
        UpdatePreview();
        UpdateSnapHint();
    }

    public override void Moving(CursorMotionData data)
    {
        Generator.Update(data);
        RefreshEndSnapTarget(data.WorldPosition);
        UpdatePreview();
        UpdateSnapHint();
    }

    public override void End(CursorButtonData data)
    {
        Generator.End(data);
        var geometry = BuildCommitGeometry(data);

        new CommandBuilder("Paint Stroke", WorkingLayer.World.Create())
            .NewStroke()
            .AddToLayerTree(WorkingLayer)
            .SetProperty(e => e.Get<StrokeSetting>().Brush, BrushE)
            .SetSampledPolyline(geometry.Positions, geometry.Radii, geometry.Pressures, geometry.Tilts)
            .Commit();
        Clear();
    }

    public override void Cancel() => Clear();
    public override bool OnKey(InputEventKey key, CursorButtonData data)
    {
        if (AppHotkeys.Global.InteractionConfirm.IsPressedBy(key))
        {
            OnEndPaintButton();
        }
        return true;
    }

    public override bool OnMouseButton(InputEventMouseButton button, CursorButtonData data)
    {
        if (button.ButtonIndex == MouseButton.Left && button.IsReleased())
        {
            OnEndPaintButton();
        }
        return true;
    }

    public void OnEndPaintButton()
    {
        Tool.Machine.Fire(PaintEnd);
    }

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
        return PaintStrokeSnap.BuildRepairedGeometry(
            Tool.Arrangement.ArrReady.CurrentValue,
            Generator.CurrentGeometry,
            _startSnapTarget,
            _endSnapTarget,
            AppPreference.PaintStrokeSnapDistance.Value);
    }

    private void UpdatePreview()
    {
        var generatorGeometry = Generator.CurrentGeometry;
        StrokePreview.SetGeometry(generatorGeometry.Positions, generatorGeometry.Radii, generatorGeometry.Pressures);
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
