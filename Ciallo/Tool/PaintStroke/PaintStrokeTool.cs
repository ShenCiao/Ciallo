using System;
using System.Collections.Immutable;
using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Ciallo.Widget;
using Frent;
using Godot;
using R3;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

[RegisterState]
[RequestedByToolButton(ToolButton.Type.PaintStroke)]
public class PaintStrokeTool : InteractionScope, IPropertyProvider, ILayerDependent
{
    [Substate]
    internal PaintStrokeHover Hover = null!;

    [Substate]
    internal PaintStrokeInteractor Left = null!;

    [Substate]
    internal PaintStrokeOnVectorFill LeftOnFill = null!;

    private readonly PaintStrokeSnap _snap = new();
    public ArrangementManager Arrangement { get; private set; }

    public readonly Subject<Unit> DeactivateSignal = new();

    public override void ConfigureStateMachine(StateMachine sm)
    {
        sm.Configure(this)
            .InitialTransition(Hover);
        sm.Configure(Hover)
            .PermitDynamicIf(Trigger.Press(MouseButton.Left), () =>
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
            })
            .PermitReentry(Trigger.Refresh);

        sm.Configure(Left)
            .Permit(InteractionManager.CancelRequested, Hover)
            .Permit(InteractionManager.ConfirmRequested, Hover)
            .Permit(InteractionManager.InputCaptureLost, Hover)
            .Permit(PaintStrokeInteractor.PaintEnd, Hover);

        sm.Configure(LeftOnFill)
            .Permit(Trigger.Release(MouseButton.Left), Hover)
            .Permit(InteractionManager.CancelRequested, Hover)
            .Permit(InteractionManager.ConfirmRequested, Hover)
            .Permit(InteractionManager.InputCaptureLost, Hover)
            .Permit(PaintStrokeInteractor.PaintEnd, Hover);
    }

    public static bool CanHandleLayers(ImmutableArray<Entity> layers) =>
        layers.Length == 1 &&
        (layers[0].Has<ShapeLayerSetting>() || layers[0].Has<VectorFillLayerSetting>());

    public void DrawPropertyAfterSubstates(PropertyContainer container)
    {
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

    protected override void OnActivated()
    {
        Arrangement = WorkingLayer.Get<ArrangementManager>();

        if (!WorkingLayer.Has<VectorFillLayerSetting>()) return;

        var referenceLayers = WorkingLayer.Get<VectorFillLayerSetting>().ReferenceLayers;
        AppPreference.ShowVectorFillReferenceLayerWireframe
            .TakeUntil(DeactivateSignal)
            .Subscribe(visible => VectorFillTool.SetWireframeVisibility(referenceLayers, visible),
                _ => VectorFillTool.SetWireframeVisibility(referenceLayers, false));
    }

    protected override void OnDeactivated()
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
