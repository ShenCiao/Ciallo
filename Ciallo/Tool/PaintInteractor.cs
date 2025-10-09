using System.Collections.Generic;
using System.Diagnostics;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.NodeControl;
using Ciallo.Rendering;
using Godot;
using Massive;

namespace Ciallo.Tool;

public class PaintInteractor : InteractorBase
{
    public override bool CanInteract
    {
        get
        {
            var l = SelectionManager.WorkingLayer.Value;
            bool layerAvailable = l.IsNotNull() && l.Has<PolylineLayerSetting>();
            bool brushAvailable = SelectionManager.WorkingBrush.Value.IsNotNull() || AppBrushLibrary.HasSelection;
            
            return layerAvailable && brushAvailable;
        }
    }

    private Entity _brushE;
    private StrokeView _strokePreview;
    private readonly List<Vector2> _points = new(){Capacity = 2048};
    private readonly List<float> _radii = new(){Capacity = 2048};

    private bool _justSavePoint = false;
    private Vector2 _lastScreenPoint;
    private Vector2 _lastDirection;
    private float _lastPressure = -1.0f;
    private Stopwatch _interactStopwatch;
    private readonly float _minDistance = 3f; // in pixel
    private readonly float _maxDistance = 15f; // in pixel
    private readonly float _minCosAngle = Mathf.Cos(Mathf.DegToRad(5f));

    public override void Start(CursorButtonData data)
    {
        // Shen: I guess this will improve graphics responsiveness
        OS.LowProcessorUsageMode = false;

        _interactStopwatch = Stopwatch.StartNew();

        // Selection in brush library has higher priority
        if (AppBrushLibrary.HasSelection)
        {
            var setting = AppBrushLibrary.SelectedBrushSetting.CurrentValue;
            new NewBrushCmd(setting).Combine(new ChangeWorkingBrushCmd(^1)).Commit();
        }
        _brushE = SelectionManager.WorkingBrush.Value;
        var brushMaterial = _brushE.Get<BrushMaterial>();
        
        _strokePreview = new StrokeView();
        _strokePreview.Material = brushMaterial;
        var layerE = SelectionManager.WorkingLayer.Value;
        var layerView = layerE.Get<PolylineLayerView>();
        layerView.AddChild(_strokePreview);
        
        var brushS = _brushE.Get<BrushSetting>();
        var t = brushS.Pressure2RadiusRatioCurve.SampleX(0);
        float radius = brushS.BaseRadius.Value * t;
        
        _points.Add(data.WorldPosition);
        _radii.Add(radius);
        _lastScreenPoint = data.ScreenPosition;
        _lastDirection = Vector2.FromAngle(0);
        _lastPressure = -1.0f;
        _strokePreview.SetGeometry(_points, _radii);
    }
    
    public override void Interacting(CursorMotionData data)
    {
        long deltaMs = _interactStopwatch.ElapsedMilliseconds;
        // GD.Print($"[PaintInteractor] Interacting delta: {deltaMs} ms");
        _interactStopwatch.Restart();

        var setting = _brushE.Get<BrushSetting>();
        var transformedPressure = setting.Pressure2RadiusRatioCurve.SampleX(data.Pressure);
        float radius = setting.BaseRadius.Value * transformedPressure;
        var position = data.WorldPosition;
        
        // Always preview the last point to give a smooth drawing experience
        if (!_justSavePoint)
        {
            _points.RemoveAt(_points.Count - 1);
            _radii.RemoveAt(_radii.Count - 1);
        }
        _justSavePoint = false;
        _points.Add(position);
        _radii.Add(radius);
        
        bool isSmaller = data.ScreenPosition.DistanceTo(_lastScreenPoint) < _minDistance;
        bool isLarger = data.ScreenPosition.DistanceTo(_lastScreenPoint) > _maxDistance;
        bool isPressureChange = Mathf.Abs(data.Pressure - _lastPressure) > 0.08f;
        bool isWinding = data.ScreenPosition.DirectionTo(_lastScreenPoint).Dot(_lastDirection) < _minCosAngle;
        bool saveThisPoint = !isSmaller && (isLarger || isWinding || isPressureChange);
        
        if (saveThisPoint)
        {
            // Basic smoothing
            const float smoothingFactor = 0.15f;
            for(int i = 0; i < 5; i++)
            {
                int idx = _points.Count - 1 - i;
                if (idx < 2) break;
                
                // Don't smooth if two segments have large angle
                var dir1 = (_points[idx] - _points[idx - 1]).Normalized();
                var dir2 = (_points[idx - 1] - _points[idx - 2]).Normalized();
                if (dir1.Dot(dir2) < Mathf.Cos(Mathf.DegToRad(30f)))
                    break;
                
                _radii[idx] = Mathf.Lerp(_radii[idx], _radii[idx - 1], smoothingFactor);
                _points[idx] = _points[idx].Lerp(_points[idx - 1], smoothingFactor);
            }

            _lastDirection = data.ScreenPosition.DirectionTo(_lastScreenPoint).Normalized();
            _lastScreenPoint = data.ScreenPosition;
            _lastPressure = transformedPressure;
            _justSavePoint = true;
        }
        
        _strokePreview.SetGeometry(_points, _radii);
    }

    public override void End(CursorButtonData data)
    {
        var layerE = SelectionManager.WorkingLayer.Value;
        var cmd = new NewStrokeCmd(layerE);
        var strokeE = cmd.InitEntity();
        cmd.Combine(new ChangeStrokeBrushCmd(strokeE, _brushE))
            .Combine(new SetStrokeGeometryCmd(strokeE, _points, _radii))
            .Commit();
        Clear();
    }

    public override void Cancel()
    {
        Clear();
    }

    public void Clear()
    {
        _points.Clear();
        _radii.Clear();
        _strokePreview.QueueFree();
        OS.LowProcessorUsageMode = true;
    }
}