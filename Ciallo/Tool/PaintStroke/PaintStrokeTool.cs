using System;
using System.Collections.Immutable;
using System.Runtime.Serialization;
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

[DataContract, RegisterState]
[RequestedByToolButton(ToolButton.Type.PaintStroke)]
public class PaintStrokeTool : InteractionScope, IPropertyProvider, ILayerDependent
{
    [DataMember]
    public readonly ReactiveProperty<bool> SnapEnabled = new(false);
    [DataMember]
    public readonly ReactiveProperty<float> SnapDistance = new(24f);
    [DataMember]
    public readonly ReactiveProperty<int> Mode = new(0); // 0 = Freehand, 1 = Bezier, 2 = PolyCubicBezier
    [DataMember]
    public readonly ReactiveProperty<float> CurvePressure = new(1f);
    [DataMember]
    public readonly ReactiveProperty<bool> PressureTaperStartEnabled = new(false);
    [DataMember]
    public readonly ReactiveProperty<bool> PressureTaperEndEnabled = new(false);
    [DataMember]
    public readonly ReactiveProperty<float> PressureTaperStartLength = new(24f);
    [DataMember]
    public readonly ReactiveProperty<float> PressureTaperEndLength = new(24f);

    [StateAccess]
    internal VectorFillTool VectorFill;

    [Substate]
    internal PaintStrokeHover Hover;

    [Substate]
    internal PaintStrokeInteractor freehand;

    [Substate]
    internal PaintStrokeBezierInteractor quadBezier;

    [Substate]
    internal PaintStrokePolyCubicBezierInteractor polyCubicBezier;

    private readonly PaintStrokeSnap _snap = new();
    public ArrangementManager Arrangement { get; private set; }

    public readonly Subject<Unit> DeactivateSignal = new();

    internal StrokePressureTaper PressureTaper => new(
        PressureTaperStartEnabled.Value ? PressureTaperStartLength.Value : 0,
        PressureTaperEndEnabled.Value ? PressureTaperEndLength.Value : 0);

    public override void ConfigureStateMachine(StateMachine sm)
    {
        sm.Configure(this)
            .InitialTransition(Hover)
            .InternalTransition(Trigger.Press(AppHotkeys.Tool.PaintStrokeToggleStartPressureTaper),
                () => PressureTaperStartEnabled.Value = !PressureTaperStartEnabled.Value)
            .InternalTransition(Trigger.Press(AppHotkeys.Tool.PaintStrokeToggleEndPressureTaper),
                () => PressureTaperEndEnabled.Value = !PressureTaperEndEnabled.Value)
            .InternalTransition(Trigger.Press(AppHotkeys.Tool.PaintStrokeToggleSnapping),
                () => SnapEnabled.Value = !SnapEnabled.Value);
        sm.Configure(Hover)
            .PermitDynamicIf(Trigger.Press(MouseButton.Left), () =>
            {
                // Route to bezier or freehand based on mode
                if (Mode.Value == 1)
                    return quadBezier;

                if (Mode.Value == 2)
                    return polyCubicBezier;

                return freehand;
            }, () =>
            {
                var brushE = Document.Get<SelectionManager>().WorkingStrokeBrush.Value;
                return !brushE.IsDyingOrDead || AppStrokeBrushLibrary.HasSelection;
            })
            .PermitReentry(Trigger.Refresh);

        sm.Configure(freehand)
            .Permit(Trigger.Release(MouseButton.Left), Hover)
            .PermitStandardExits(Hover);

        sm.Configure(quadBezier)
            .Permit(PaintStrokeBezierInteractor.QuadBezierEnd, Hover)
            .Permit(InteractionManager.CancelRequested, Hover)
            .Permit(InteractionManager.InputCaptureLost, Hover);

        sm.Configure(polyCubicBezier)
            .Permit(PolyCubicBezierInteractor.Closed, Hover)
            .PermitStandardExits(Hover);

    }

    public static bool CanHandleLayers(ImmutableArray<Entity> layers) =>
        (layers[0].Has<ShapeLayerSetting>() || layers[0].Has<VectorFillLayerSetting>());

    public void DrawPropertyAfterSubstates(PropertyContainer container)
    {
        container.AddProperty("Curve pressure", new SpinSlider
        {
            MinValue = 0,
            MaxValue = 1,
            Step = 0.01,
            TooltipText = "Input pressure for Bézier strokes before pressure taper and brush mappings.".Tr(),
        }.BindNumber(CurvePressure))
            .VisibleIf(Mode, mode => mode != 0);

        var pressureTaperEffect = "Affects all brush properties mapped from pressure. Brush mappings determine changes in width and opacity.".Tr();
        container.AddProperty("Start pressure taper", new CheckBox
        {
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Multiplies input pressure by a factor rising from 0 to 1 over the start interval.".Tr()
                + "\n" + pressureTaperEffect,
        }.BindBool(PressureTaperStartEnabled));
        container.AddProperty("Start taper length", CreatePressureTaperLengthSlider()
            .BindNumber(PressureTaperStartLength))
            .VisibleIf(PressureTaperStartEnabled, enabled => enabled);

        container.AddProperty("End pressure taper", new CheckBox
        {
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Multiplies input pressure by a factor falling from 1 to 0 over the end interval.".Tr()
                + "\n" + pressureTaperEffect,
        }.BindBool(PressureTaperEndEnabled));
        container.AddProperty("End taper length", CreatePressureTaperLengthSlider()
            .BindNumber(PressureTaperEndLength))
            .VisibleIf(PressureTaperEndEnabled, enabled => enabled);

        container.AddProperty("Snapping", new CheckBox
        {
            FocusMode = Control.FocusModeEnum.None,
        }.BindBool(SnapEnabled));

        container.AddProperty("Snap distance",
            new SpinSlider
            {
                MinValue = 1f,
                MaxValue = 128f,
                Step = 1f,
                ExpEdit = true,
                AllowGreater = true,
            }.BindNumber(SnapDistance))
            .VisibleIf(SnapEnabled, enabled => enabled);
    }

    private static SpinSlider CreatePressureTaperLengthSlider() => new()
    {
        MinValue = 0,
        MaxValue = 256,
        Step = 1,
        ExpEdit = true,
        AllowGreater = true,
        TooltipText = "Distance along the stroke in canvas units. Zero disables this end.".Tr(),
    };

    protected override void OnActivated()
    {
        Arrangement = PrimaryLayer.Get<ArrangementManager>();

        PressureTaperStartEnabled.CombineLatest(
                PressureTaperEndEnabled,
                PressureTaperStartLength,
                PressureTaperEndLength,
                CurvePressure,
                (_, _, _, _, _) => Unit.Default)
            .Skip(1)
            .TakeUntil(DeactivateSignal)
            .Subscribe(_ => RefreshPressurePreview());

        SnapEnabled.CombineLatest(SnapDistance, (_, _) => Unit.Default)
            .Skip(1)
            .TakeUntil(DeactivateSignal)
            .Subscribe(_ => RefreshSnapping());

        if (!PrimaryLayer.Has<VectorFillLayerSetting>()) return;

        var referenceLayers = PrimaryLayer.Get<VectorFillLayerSetting>().ReferenceLayers;
        VectorFill.ShowReferenceLayerWireframe
            .TakeUntil(DeactivateSignal)
            .Subscribe(visible => VectorFillTool.SetWireframeVisibility(referenceLayers, visible),
                _ => VectorFillTool.SetWireframeVisibility(referenceLayers, false));
    }

    private void RefreshPressurePreview()
    {
        // Rebuild from the active interaction's source samples, without routing a
        // synthetic movement or reentering its state (which would end the stroke).
        switch (InteractionManager.StateMachine.State)
        {
            case PaintStrokeInteractor stroke: stroke.RefreshPressurePreview(); break;
            case PaintStrokeBezierInteractor bezier: bezier.RefreshPressurePreview(); break;
            case PaintStrokePolyCubicBezierInteractor cubic: cubic.RefreshPressurePreview(); break;
        }
    }

    private void RefreshSnapping()
    {
        switch (InteractionManager.StateMachine.State)
        {
            case PaintStrokeHover hover: hover.RefreshSnapping(); break;
            case PaintStrokeInteractor stroke: stroke.RefreshSnapping(); break;
            case PaintStrokeBezierInteractor bezier: bezier.RefreshSnapping(); break;
            case PaintStrokePolyCubicBezierInteractor cubic: cubic.RefreshSnapping(); break;
        }
    }

    protected override void OnDeactivated()
    {
        Arrangement = null;
        DeactivateSignal.OnNext(Unit.Default);
    }

    public PaintStrokeSnapTarget? TryFindSnapTarget(Vector2 worldPosition)
    {
        if (!SnapEnabled.Value)
            return null;

        var arr = Arrangement.ArrReady.CurrentValue;
        if (arr == null)
            return null;

        return _snap.TryFindTarget(
            arr,
            worldPosition,
            SnapDistance.Value);
    }

    internal Entity ResolveStrokeTargetLayer()
    {
        if (!PrimaryLayer.Has<VectorFillLayerSetting>())
            return PrimaryLayer;

        var referenceLayers = PrimaryLayer.Get<VectorFillLayerSetting>().ReferenceLayers;
        foreach (var referenceLayer in referenceLayers)
            return referenceLayer;
        return Entity.Null;
    }
}
