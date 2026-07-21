using System;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Ciallo.Widget;
using Frent;
using Godot;
using R3;

namespace Ciallo.Tool;

[RegisterTool(ToolButton.Paint)]
public class PaintStrokeTool : ToolBase
{
    public readonly ReactiveProperty<Entity> BrushE = new(Entity.Null);

    public readonly PaintStrokeHover Hover = new();
    public readonly PaintStrokeInteractor Left = new();
    public readonly PaintStrokeOnVectorFill LeftOnFill = new();
    private readonly PaintStrokeSnap _snap = new();
    public ArrangementManager Arrangement { get; private set; }

    protected override void ConfigureStateMachine()
    {
        ConfigureInitial(Hover)
            .PermitDynamicIf(Press(MouseButton.Left), () =>
            {
                if (WorkingLayer.Has<ShapeLayerSetting>())
                    return Left;
                if (WorkingLayer.Has<VectorFillLayerSetting>())
                    return LeftOnFill;
                throw new InvalidOperationException("Unreachable code: layer type is guaranteed by CanHandleLayer");
            }, () =>
            {
                var brushE = Document.Get<SelectionManager>().WorkingStrokeBrush.Value;
                return !brushE.IsDyingOrDead || AppStrokeBrushLibrary.HasSelection;
            });

        Configure(Left)
            .Permit(Press(AppHotkeys.Global.InteractionCancel), Hover)
            .Permit(PaintStrokeInteractor.PaintEnd, Hover);

        Configure(LeftOnFill)
            .Permit(Release(MouseButton.Left), Hover)
            .Permit(PaintStrokeInteractor.PaintEnd, Hover);
    }

    public override bool CanHandleLayer(params Entity[] layerEs)
    {
        if (layerEs.Length != 1) return false;
        var e = layerEs.Single();
        bool isShapeLayer = e.Has<ShapeLayerSetting>();
        bool isVectorFillLayer = e.Has<VectorFillLayerSetting>();
        return isShapeLayer || isVectorFillLayer;
    }

    public readonly Subject<Unit> DeactivateSignal = new();

    public override void DrawProperty(PropertyContainer container)
    {
        base.DrawProperty(container);

        container.AddProperty("Snapping", new CheckBox
        {
            ToggleMode = true,
        }.BindBool(AppPreference.PaintStrokeSnapEnabled));

        container.AddProperty("Snap distance",
            new SpinSlider
            {
                MinValue = 1f,
                MaxValue = 128f,
                Step = 1f,
                ExpEdit = true,
                AllowGreater = true,
            }.BindNumber(AppPreference.PaintStrokeSnapDistance));
    }

    public override void OnActivated()
    {
        Arrangement = WorkingLayer.Get<ArrangementManager>();

        if (!WorkingLayer.Has<VectorFillLayerSetting>()) return;

        var referenceLayers = WorkingLayer.Get<VectorFillLayerSetting>().ReferenceLayers;
        AppPreference.ShowVectorFillReferenceLayerWireframe
            .TakeUntil(DeactivateSignal)
            .Subscribe(visible => VectorFillTool.SetWireframeVisibility(referenceLayers, visible),
                _ => VectorFillTool.SetWireframeVisibility(referenceLayers, false));
    }

    public override void OnDeactivated()
    {
        Arrangement = null;
        DeactivateSignal.OnNext(Unit.Default);
    }

    public PaintStrokeSnapTarget? TryFindSnapTarget(Vector2 worldPosition)
    {
        if (!AppPreference.PaintStrokeSnapEnabled.Value)
            return null;

        var arr = Arrangement.ArrReady.CurrentValue;
        if (arr == null)
            return null;

        return _snap.TryFindTarget(
            arr,
            worldPosition,
            AppPreference.PaintStrokeSnapDistance.Value);
    }
}
