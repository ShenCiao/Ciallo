using System;
using System.Collections.Generic;
using System.Linq;
using Ciallo.Data;
using Ciallo.Rendering;
using Godot;
using ObservableCollections;
using R3;

namespace Ciallo.Tool;

[RegisterState]
public class LiquifyHover : Interaction
{
    [StateAccess]
    public LiquifyTool Tool { get; set; }

    private StrokeView _brushCircle;
    private readonly List<StrokeView> _selectionWireframes = [];
    private IDisposable _selectionWireframeSubscription;

    public override void Start(CursorButtonData data)
    {
        Document.Get<WorldBody>().DefaultCursorShape = Control.CursorShape.Cross;
        _brushCircle = new StrokeView { Material = AutoloadRendering.DashWireframeMaterial };
        Document.Get<WorldOverlay>().AddChild(_brushCircle);
        UpdateBrushCircle(data.WorldPosition);

        var selectedShapes = Document.Get<SelectionManager>().SelectedShapes;
        _selectionWireframeSubscription = Tool.ShowSelectionWireframe
            .CombineLatest(selectedShapes.ObserveChanged().Select(_ => Unit.Default).Prepend(Unit.Default),
                (visible, _) => visible)
            .Subscribe(RebuildSelectionWireframes);
    }

    public override void Moving(CursorMotionData data)
    {
        UpdateBrushCircle(data.WorldPosition);
    }

    public override void End(CursorButtonData data) => Cancel();

    public override void Cancel()
    {
        _brushCircle.QueueFree();
        _selectionWireframeSubscription.Dispose();
        ClearSelectionWireframes();
        Document.Get<WorldBody>().DefaultCursorShape = default;
    }

    public override bool OnKey(InputEventKey key, CursorButtonData data) => false;

    private void UpdateBrushCircle(Vector2 center)
    {
        var liquifyTool = Tool;
        _brushCircle.SetGeometry(CreateCircle(center, liquifyTool.Radius.Value, 64), AppPreference.StrokeWireframeRadius);
    }

    // Only an explicit shape selection narrows the liquify target, so only it gets an outline.
    private void RebuildSelectionWireframes(bool visible)
    {
        ClearSelectionWireframes();
        if (!visible) return;

        var worldOverlay = Document.Get<WorldOverlay>();
        foreach (var shape in Document.Get<SelectionManager>().SelectedShapes.Where(LiquifyTargetScope.CanLiquify))
        {
            var wireframe = new StrokeView { Material = AutoloadRendering.WireframeMaterial };
            wireframe.SetInstanceShaderParameter("overridingColor", AppPreference.StrokeWireframeColor);
            wireframe.SetGeometry(shape.Get<SampledPolyline>().Positions.Value, AppPreference.StrokeWireframeRadius);
            worldOverlay.AddChild(wireframe);
            _selectionWireframes.Add(wireframe);
        }
    }

    private void ClearSelectionWireframes()
    {
        _selectionWireframes.ForEach(wireframe => wireframe.QueueFree());
        _selectionWireframes.Clear();
    }

    private static Vector2[] CreateCircle(Vector2 center, float radius, int segmentCount)
    {
        var points = new Vector2[segmentCount + 1];
        for (int i = 0; i <= segmentCount; i++)
        {
            float angle = Mathf.Tau * i / segmentCount;
            points[i] = center + Vector2.Right.Rotated(angle) * radius;
        }
        return points;
    }
}
