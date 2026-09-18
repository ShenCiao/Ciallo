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
        AppPreference.PaintStrokePressureTaperStartEnabled.Value ? AppPreference.PaintStrokePressureTaperStartLength.Value : 0,
        AppPreference.PaintStrokePressureTaperEndEnabled.Value ? AppPreference.PaintStrokePressureTaperEndLength.Value : 0);

    public override void ConfigureStateMachine(StateMachine sm)
    {
        sm.Configure(this)
            .InitialTransition(Hover)
            .InternalTransition(Trigger.Press(AppHotkeys.Tool.PaintStrokeToggleStartPressureTaper),
                () => AppPreference.PaintStrokePressureTaperStartEnabled.Value = !AppPreference.PaintStrokePressureTaperStartEnabled.Value)
            .InternalTransition(Trigger.Press(AppHotkeys.Tool.PaintStrokeToggleEndPressureTaper),
                () => AppPreference.PaintStrokePressureTaperEndEnabled.Value = !AppPreference.PaintStrokePressureTaperEndEnabled.Value);
        sm.Configure(Hover)
            .PermitDynamicIf(Trigger.Press(MouseButton.Left), () =>
            {
                // Route to bezier or freehand based on mode
                if (AppPreference.PaintStrokeMode.Value == 1)
                    return quadBezier;

                if (AppPreference.PaintStrokeMode.Value == 2)
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
            .PermitStandardExits(Hover);

    }

    public static bool CanHandleLayers(ImmutableArray<Entity> layers) =>
        (layers[0].Has<ShapeLayerSetting>() || layers[0].Has<VectorFillLayerSetting>());

    public void DrawPropertyAfterSubstates(PropertyContainer container)
    {
        var pressureTaperEffect = "Affects all brush properties mapped from pressure. Brush mappings determine changes in width and opacity.".Tr();
        container.AddProperty("Start pressure taper", new CheckBox
        {
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Multiplies input pressure by a factor rising from 0 to 1 over the start interval.".Tr()
                + "\n" + pressureTaperEffect,
        }.BindBool(AppPreference.PaintStrokePressureTaperStartEnabled));
        container.AddProperty("Start taper length", CreatePressureTaperLengthSlider()
            .BindNumber(AppPreference.PaintStrokePressureTaperStartLength))
            .VisibleIf(AppPreference.PaintStrokePressureTaperStartEnabled, enabled => enabled);

        container.AddProperty("End pressure taper", new CheckBox
        {
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = "Multiplies input pressure by a factor falling from 1 to 0 over the end interval.".Tr()
                + "\n" + pressureTaperEffect,
        }.BindBool(AppPreference.PaintStrokePressureTaperEndEnabled));
        container.AddProperty("End taper length", CreatePressureTaperLengthSlider()
            .BindNumber(AppPreference.PaintStrokePressureTaperEndLength))
            .VisibleIf(AppPreference.PaintStrokePressureTaperEndEnabled, enabled => enabled);

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

        AppPreference.PaintStrokePressureTaperStartEnabled.CombineLatest(
                AppPreference.PaintStrokePressureTaperEndEnabled,
                AppPreference.PaintStrokePressureTaperStartLength,
                AppPreference.PaintStrokePressureTaperEndLength,
                (_, _, _, _) => Unit.Default)
            .Skip(1)
            .TakeUntil(DeactivateSignal)
            .Subscribe(_ => RefreshPressureTaper());

        if (!PrimaryLayer.Has<VectorFillLayerSetting>()) return;

        var referenceLayers = PrimaryLayer.Get<VectorFillLayerSetting>().ReferenceLayers;
        AppPreference.ShowVectorFillReferenceLayerWireframe
            .TakeUntil(DeactivateSignal)
            .Subscribe(visible => VectorFillTool.SetWireframeVisibility(referenceLayers, visible),
                _ => VectorFillTool.SetWireframeVisibility(referenceLayers, false));
    }

    private void RefreshPressureTaper()
    {
        // Rebuild from the active interaction's source samples, without routing a
        // synthetic movement or reentering its state (which would end the stroke).
        switch (InteractionManager.StateMachine.State)
        {
            case PaintStrokeInteractor stroke: stroke.RefreshPressureTaper(); break;
            case PaintStrokeBezierInteractor bezier: bezier.RefreshPressureTaper(); break;
            case PaintStrokePolyCubicBezierInteractor cubic: cubic.RefreshPressureTaper(); break;
        }
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
