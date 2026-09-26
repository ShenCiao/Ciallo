using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.Serialization;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.GuiControl;
using Ciallo.Rendering;
using Ciallo.Widget;
using Frent;
using Godot;
using ObservableCollections;
using R3;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

[DataContract, RegisterState]
[RequestedByToolButton(ToolButton.Type.Select)]
public class PolylineSelectTool : InteractionScope, IPropertyProvider, ILayerDependent
{
    public enum EditMode { RectTransform, BezierDeform, }

    [DataMember]
    public readonly ReactiveProperty<EditMode> Mode = new(EditMode.RectTransform);
    [DataMember]
    public readonly ReactiveProperty<float> SimplificationTolerance = new(0.5f);
    [DataMember]
    public readonly ReactiveProperty<float> StrokeRadiusMultiplier = new(1f);

    [Substate]
    internal PolylineNoSelectionHover HoverWithoutSelection;

    [Substate]
    internal PolylineTransformHover TransformHover;

    [Substate]
    internal PolylineBezierDeformHover BezierDeformHover;

    [Substate]
    internal PolylineRectSelectInteractor Select;

    [Substate]
    internal PolylineTransformInteractor RectTransform;

    [Substate]
    internal PolylineBezierDeformInteractor BezierDeform;

    public Trigger EditModeChanged = new("EditModeChanged");

    private IDisposable _modeChangedSubscription;

    public override void ConfigureStateMachine(StateMachine sm)
    {
        sm.Configure(this)
            .InitialTransitionDynamic(TransToHover)
            .PermitReentry(Trigger.Refresh);

        sm.Configure(HoverWithoutSelection)
            .PermitDynamic(Trigger.Press(MouseButton.Left), () =>
            {
                if (HoverWithoutSelection.CanTranslate && !Input.IsKeyPressed(Key.Shift))
                    return RectTransform;
                return Select;
            })
            .Ignore(EditModeChanged);

        sm.Configure(TransformHover)
            .PermitDynamic(Trigger.Press(MouseButton.Left), () =>
            {
                if (TransformHover.CanTransform && !Input.IsKeyPressed(Key.Shift))
                    return RectTransform;
                return Select;
            })
            .PermitDynamic(EditModeChanged, TransToHover);

        sm.Configure(BezierDeformHover)
            .PermitDynamic(Trigger.Press(MouseButton.Left), () =>
            {
                if (BezierDeformHover.CanDeform && !Input.IsKeyPressed(Key.Shift))
                    return BezierDeform;
                return Select;
            })
            .PermitDynamic(EditModeChanged, TransToHover);

        sm.Configure(BezierDeform)
            .PermitDynamic(Trigger.Release(MouseButton.Left), TransToHover)
            .PermitStandardExitsDynamic(TransToHover);

        sm.Configure(RectTransform)
            .PermitDynamic(Trigger.Release(MouseButton.Left), TransToHover)
            .PermitStandardExitsDynamic(TransToHover);

        sm.Configure(Select)
            .PermitDynamic(Trigger.Release(MouseButton.Left), TransToHover)
            .PermitStandardExitsDynamic(TransToHover);

        InteractionState TransToHover()
        {
            var shapes = Document.Get<SelectionManager>().SelectedShapes;
            if (shapes.Count <= 0)
                return HoverWithoutSelection;

            // A single-point selection has no meaningful Bezier deformation.
            // Route it through the existing transform workflow so it remains movable
            // without creating degenerate curve or transform-box controls.
            bool canBezierDeform = shapes.Sum(e => e.Get<SampledPolyline>().Count) > 1;
            if (Mode.Value == EditMode.RectTransform || !canBezierDeform)
                return TransformHover;
            if (Mode.Value == EditMode.BezierDeform)
                return BezierDeformHover;
            throw new NotImplementedException();
        }
    }

    public static bool CanHandleLayers(ImmutableArray<Entity> layers) =>
        (layers[0].Has<ShapeLayerSetting>() || layers[0].Has<VectorFillLayerSetting>());

    protected override void OnActivated()
    {
        _modeChangedSubscription = Mode.Skip(1).Subscribe(_ => Fire(EditModeChanged));
        if (PrimaryLayer.Has<VectorFillLayerSetting>())
            PrimaryLayer.Get<OverlayHolder>().Visible = true;
        PrimaryLayer.Get<BodyHolder>().ProcessMode = Node.ProcessModeEnum.Inherit;
        // Guard selection
        var selectedShapes = Document.Get<SelectionManager>().SelectedShapes;
        var deselect = selectedShapes
            .Where(e => e.Get<LayerTreeNode>().ParentValue != PrimaryLayer).Reverse().ToArray();
        foreach (var e in deselect)
            selectedShapes.Remove(e);
    }

    protected override void OnDeactivated()
    {
        _modeChangedSubscription.Dispose();
        if (PrimaryLayer.Has<VectorFillLayerSetting>())
            PrimaryLayer.Get<OverlayHolder>().Visible = false;
        PrimaryLayer.Get<BodyHolder>().ProcessMode = Node.ProcessModeEnum.Disabled;
    }

    public void DrawPropertyBeforeSubstates(PropertyContainer container)
    {
        // --- Select/Deselect all buttons
        var selectionManager = Document.Get<SelectionManager>();
        var selectionButtonGroup = container.CreateHContainer().AddToChildOf(container);
        var selectAllButton = container.CreateButton("Select all").AddToChildOf(selectionButtonGroup);
        selectAllButton.Pressed += () =>
        {
            var layerE = selectionManager.PrimaryLayer.CurrentValue;
            if (layerE.IsDyingOrDead) return;
            selectionManager.SelectedShapes.Clear();
            selectionManager.SelectedShapes.AddRange(layerE.Get<LayerTreeNode>().Children);
            Fire(Trigger.Refresh);
        };
        var deselectAllButton = container.CreateButton("Deselect").AddToChildOf(selectionButtonGroup);
        deselectAllButton.Pressed += () =>
        {
            selectionManager.SelectedShapes.Clear();
            Fire(Trigger.Refresh);
        };

        // --- Edit mode
        var editModeButtons = EditModeSwitcher.New().Bind(Mode);
        container.AddProperty("Edit mode", editModeButtons);


        var selectedShapes = Document.Get<SelectionManager>().SelectedShapes;
        var selectionChanged = selectedShapes.ObserveChanged().Select(_ => Unit.Default).Prepend(Unit.Default);

        var booleanBox = container.CreateBox().AddToChildOf(container)
            .VisibleIf(selectionChanged, _ => selectedShapes.Count(shape => shape.Has<FilledPolygonSetting>()) >= 2);
        booleanBox.Name = "PolygonBooleanOperations";
        var booleanButtons = new GridContainer { Columns = 2, Name = "Buttons" }.AddToChildOf(booleanBox);
        foreach (var operation in Enum.GetValues<Geometry2D.PolyBooleanOperation>())
        {
            var button = container.CreateButton(PolygonBooleanActions.Label(operation)).AddToChildOf(booleanButtons);
            button.Name = operation.ToString();
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.TooltipText = operation == Geometry2D.PolyBooleanOperation.Difference
                ? "Subtract all selected upper polygons from the bottom polygon. The result keeps the bottom polygon's fill brush."
                : "Combine the selected polygons. The result keeps the bottom polygon's fill brush.";
            selectionChanged.Subscribe(_ => button.Disabled =
                !PolygonBooleanActions.CanApplySelection(selectionManager.PrimaryLayer.CurrentValue, selectedShapes.ToArray())).AddTo(button);
            button.Pressed += () =>
            {
                if (PolygonBooleanActions.ApplySelection(PrimaryLayer, operation)) Fire(Trigger.Refresh);
            };
        }

        // --- Stroke brush switcher
        var strokeBrushSwitcher = StrokeBrushPreviewList.New().AddToChildOf(container);
        strokeBrushSwitcher.CustomMinimumSize = new(0, 256);
        strokeBrushSwitcher.PreviewList.ActionMode = BaseButton.ActionModeEnum.Release;
        strokeBrushSwitcher.Document = Document;
        strokeBrushSwitcher.BindBrushes(Document.Get<BrushManager>().StrokeBrushes);
        strokeBrushSwitcher.VisibleIf(
            selectionChanged.CombineLatest(selectionManager.PrimaryLayer, (_, layer) => layer),
            layer => selectedShapes.Count > 0
                ? selectedShapes.All(e => e.Has<StrokeSetting>())
                : !layer.IsNull && layer.Has<ShapeLayerSetting>());

        ObserveSelectionBrush(selectedShapes, e => e.Has<StrokeSetting>(), e => e.Get<StrokeSetting>().Brush)
            .Subscribe(strokeBrushSwitcher.Select).AddTo(strokeBrushSwitcher);

        var strokeRadiusEditBox = container.CreateBox().AddToChildOf(container);
        strokeRadiusEditBox.Name = "StrokeRadiusMultiplier";
        strokeRadiusEditBox.VisibleIf(selectionChanged,
            _ => selectedShapes.Count > 0 && selectedShapes.All(e => e.Has<StrokeSetting>()));

        var strokeRadiusMultiplierEdit = new SpinSlider
        {
            MinValue = 0.01f,
            MaxValue = 100f,
            Step = 0.01f,
            ExpEdit = true,
            AllowGreater = true,
            TooltipText = "Multiply every selected stroke radius by this factor. For example, 0.5 halves each radius.",
        }.BindNumber(StrokeRadiusMultiplier);
        container.CreatePropertyBox("Radius multiplier", strokeRadiusMultiplierEdit)
            .AddToChildOf(strokeRadiusEditBox);

        var applyStrokeRadiusMultiplier = container.CreateButton("Apply to selected strokes")
            .AddToChildOf(strokeRadiusEditBox);
        applyStrokeRadiusMultiplier.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        applyStrokeRadiusMultiplier.Pressed += () =>
        {
            float multiplier = StrokeRadiusMultiplier.Value;
            if (multiplier == 1f) return;

            var command = new CommandBuilder("Scale Selected Stroke Radii");
            foreach (var strokeE in selectedShapes)
            {
                var radii = strokeE.Get<SampledPolyline>().Radii.Value;
                command.SetTarget(strokeE).SetSampledPolyline(
                    radii: radii.Select(radius => radius * multiplier).ToImmutableArray());
            }
            command.Commit();
        };

        strokeBrushSwitcher.BrushClicked.Subscribe(brushE =>
        {
            if (selectedShapes.Count == 0)
            {
                var strokes = selectionManager.PrimaryLayer.CurrentValue.Get<LayerTreeNode>().Children
                    .Where(e => e.Has<StrokeSetting>() && e.Get<StrokeSetting>().Brush.Value == brushE)
                    .ToArray();
                if (strokes.Length == 0) return;

                selectedShapes.AddRange(strokes);
                Fire(Trigger.Refresh);
                return;
            }

            if (selectedShapes.All(e => e.Get<StrokeSetting>().Brush.Value == brushE))
            {
                selectedShapes.Clear();
                Fire(Trigger.Refresh);
                return;
            }

            var cmd = new CommandBuilder("Set Selected Stroke Brush");
            foreach (var shapeE in selectedShapes)
                cmd.SetTarget(shapeE).SetProperty(e => e.Get<StrokeSetting>().Brush, brushE);
            cmd.Commit();
        }).AddTo(strokeBrushSwitcher);

        // --- Vector fill brush switcher
        var vectorFillBrushSwitcher = VectorFillBrushPreviewList.New().AddToChildOf(container);
        vectorFillBrushSwitcher.CustomMinimumSize = new(0, 256);
        vectorFillBrushSwitcher.PreviewList.ActionMode = BaseButton.ActionModeEnum.Release;
        vectorFillBrushSwitcher.Document = Document;
        vectorFillBrushSwitcher.BindBrushes(Document.Get<BrushManager>().VectorFillBrushes);
        vectorFillBrushSwitcher.VisibleIf(selectionChanged,
            _ => selectedShapes.Count > 0 && selectedShapes.All(e => e.Has<VectorFillMarkerSetting>() || e.Has<FilledPolygonSetting>()));

        ObserveSelectionBrush(selectedShapes,
                e => e.Has<VectorFillMarkerSetting>() || e.Has<FilledPolygonSetting>(), GetVectorFillBrushE)
            .Subscribe(vectorFillBrushSwitcher.Select).AddTo(vectorFillBrushSwitcher);

        vectorFillBrushSwitcher.BrushClicked.Subscribe(brushE =>
        {
            if (selectedShapes.Count > 0 && selectedShapes.All(e => GetVectorFillBrushE(e).Value == brushE))
            {
                selectedShapes.Clear();
                Fire(Trigger.Refresh);
                return;
            }

            var cmd = new CommandBuilder("Set Selected Vector Fill Brush");
            foreach (var shapeE in selectedShapes)
            {
                if (shapeE.Has<VectorFillMarkerSetting>())
                    cmd.SetTarget(shapeE).SetProperty(e => e.Get<VectorFillMarkerSetting>().BrushE, brushE);
                else
                    cmd.SetTarget(shapeE).SetProperty(e => e.Get<FilledPolygonSetting>().BrushE, brushE);
            }
            cmd.Commit();
        }).AddTo(vectorFillBrushSwitcher);

        var polylineEditBox = container.CreateBox()
            .AddToChildOf(container)
            .VisibleIf(selectionManager.SelectedShapes.ObserveCountChanged().Prepend(0),
            count =>
            {
                if (count <= 0) return false;
                var e = selectionManager.SelectedShapes[0];
                return e.Get<SampledPolyline>().Count > 1; // naively ignore single point.
            });

        var simplificationToleranceEdit = new SpinSlider()
        {
            MinValue = 0,
            MaxValue = 10,
            Step = 0.1,
            AllowGreater = true,
            TooltipText = "Position and radius tolerance in canvas units. Zero keeps all points.",
        };
        simplificationToleranceEdit.BindNumber(SimplificationTolerance);
        container.CreatePropertyBox("Simplification tolerance", simplificationToleranceEdit).AddToChildOf(polylineEditBox);

        var simplifyButton = container.CreateButton("Simplify").AddToChildOf(polylineEditBox);
        simplifyButton.Pressed += () =>
        {
            var builder = new CommandBuilder("Simplify Shapes", Entity.Null);
            foreach (var polylineE in selectionManager.SelectedShapes)
            {
                var geom = polylineE.Get<SampledPolyline>();
                geom.Positions.Value.SimplifyRdp(
                    SimplificationTolerance.Value, out var indices, radii: geom.Radii.Value);
                if (indices.Count == geom.Count) continue;

                var positions = ImmutableArray.CreateBuilder<Vector2>(indices.Count);
                var radii = ImmutableArray.CreateBuilder<float>(indices.Count);
                var pressures = ImmutableArray.CreateBuilder<float>(indices.Count);
                var tilts = ImmutableArray.CreateBuilder<Vector2>(indices.Count);

                foreach (var idx in indices)
                {
                    positions.Add(geom.Positions.Value[idx]);
                    radii.Add(geom.Radii.Value[idx]);
                    pressures.Add(geom.Pressures.Value[idx]);
                    tilts.Add(geom.Tilts.Value[idx]);
                }
                builder.SetTarget(polylineE).SetSampledPolyline(
                    positions.MoveToImmutable(),
                    radii.MoveToImmutable(),
                    pressures.MoveToImmutable(),
                    tilts.MoveToImmutable()
                );
            }
            builder.Commit();
        };

        var smoothSubdivideButton = container.CreateButton("Smooth subdivide").AddToChildOf(polylineEditBox);
        smoothSubdivideButton.Pressed += () =>
        {
            var builder = new CommandBuilder("Smooth Subdivide Shapes");
            foreach (var polylineE in selectionManager.SelectedShapes)
            {
                var geom = polylineE.Get<SampledPolyline>();
                if (geom.Length < 2) continue;

                var resultLength = geom.Length * 2 - 1;
                var positions = ImmutableArray.CreateBuilder<Vector2>(resultLength);
                var radii = ImmutableArray.CreateBuilder<float>(resultLength);
                var pressures = ImmutableArray.CreateBuilder<float>(resultLength);
                var tilts = ImmutableArray.CreateBuilder<Vector2>(resultLength);

                var oldPositions = geom.Positions.Value;
                var oldRadii = geom.Radii.Value;
                var oldPressures = geom.Pressures.Value;
                var oldTilts = geom.Tilts.Value;

                for (int i = 0; i < geom.Length; i++)
                {
                    positions.Add(oldPositions[i]);
                    radii.Add(oldRadii[i]);
                    pressures.Add(oldPressures[i]);
                    tilts.Add(oldTilts[i]);

                    if (i == geom.Length - 1) continue;
                    var idx1 = i;
                    int idx0 = idx1 == 0 ? idx1 : idx1 - 1;
                    int idx2 = idx1 >= geom.Length - 1 ? idx1 : idx1 + 1;
                    int idx3 = idx2 >= geom.Length - 1 ? idx2 : idx2 + 1;

                    float t = 0.5f;
                    var p = oldPositions[idx0].CatmullRomInterpolation(oldPositions[idx1], oldPositions[idx2], oldPositions[idx3], t);
                    var r = oldRadii[idx0].CatmullRomInterpolation(oldRadii[idx1], oldRadii[idx2], oldRadii[idx3], t);
                    var pp = oldPressures[idx0].CatmullRomInterpolation(oldPressures[idx1], oldPressures[idx2], oldPressures[idx3], t);
                    var tilt = oldTilts[idx0].CatmullRomInterpolation(oldTilts[idx1], oldTilts[idx2], oldTilts[idx3], t);
                    positions.Add(p);
                    radii.Add(r);
                    pressures.Add(pp);
                    tilts.Add(tilt);
                }
                builder.SetTarget(polylineE).SetSampledPolyline(
                    positions.MoveToImmutable(),
                    radii.MoveToImmutable(),
                    pressures.MoveToImmutable(),
                    tilts.MoveToImmutable()
                );
            }
            builder.Commit();
        };

        var linearSubdivideButton = container.CreateButton("Linear subdivide").AddToChildOf(polylineEditBox);
        linearSubdivideButton.Pressed += () =>
        {
            var cmd1 = new CommandBuilder("Linear Subdivide Shapes");
            foreach (var polylineE in selectionManager.SelectedShapes)
            {
                var geom = polylineE.Get<SampledPolyline>();
                if (geom.Length < 2) continue;
                List<float> polyTs = new() { Capacity = geom.Length * 2 - 1 };
                for (int i = 0; i < geom.Length - 1; i++)
                {
                    polyTs.Add(i);
                    polyTs.Add(i + 0.5f);
                }
                polyTs.Add(geom.Length - 1);

                var positions = ImmutableArray.CreateBuilder<Vector2>(polyTs.Count);
                var radii = ImmutableArray.CreateBuilder<float>(polyTs.Count);
                var pressures = ImmutableArray.CreateBuilder<float>(polyTs.Count);
                var tilts = ImmutableArray.CreateBuilder<Vector2>(polyTs.Count);

                foreach (var polyT in polyTs)
                {
                    var (idx, t) = polyT.Modf();
                    int nIdx = int.Min(idx + 1, geom.Count - 1);
                    positions.Add(geom.Positions.Value[idx].Lerp(geom.Positions.Value[nIdx], t));
                    radii.Add(float.Lerp(geom.Radii.Value[idx], geom.Radii.Value[nIdx], t));
                    pressures.Add(float.Lerp(geom.Pressures.Value[idx], geom.Pressures.Value[nIdx], t));
                    tilts.Add(geom.Tilts.Value[idx].Lerp(geom.Tilts.Value[nIdx], t));
                }

                cmd1.SetTarget(polylineE).SetSampledPolyline(
                    positions.MoveToImmutable(),
                    radii.MoveToImmutable(),
                    pressures.MoveToImmutable(),
                    tilts.MoveToImmutable()
                );
            }
            cmd1.Commit();
        };

        var smoothButton = container.CreateButton("Smooth").AddToChildOf(polylineEditBox);
        smoothButton.Pressed += () =>
        {
            var builder = new CommandBuilder("Smooth Shapes", Entity.Null);
            foreach (var polylineE in selectionManager.SelectedShapes)
            {
                var geom = polylineE.Get<SampledPolyline>();
                if (geom.Length < 3) continue;

                // Apply Laplacian smoothing only to positions.
                const int iterations = 1;
                const float lambda = 0.5f;
                var smoothedPositions = geom.Positions.Value.SmoothLaplacian(iterations, lambda);
                builder.SetTarget(polylineE).SetSampledPolyline([.. smoothedPositions]); // copy but fine
            }
            builder.Commit();
        };

    }

    private static Observable<Entity> ObserveSelectionBrush(ObservableList<Entity> selectedShapes,
        Func<Entity, bool> hasBrush, Func<Entity, ReactiveProperty<Entity>> getBrush)
    {
        return selectedShapes.ObserveChanged().Select(_ => Unit.Default).Prepend(Unit.Default)
            .Select(_ =>
            {
                if (selectedShapes.Count == 0 || !selectedShapes.All(hasBrush))
                    return Observable.Return(Entity.Null);

                return Observable.CombineLatest(selectedShapes.Select(e => getBrush(e).AsObservable()))
                    .Select(brushes => brushes.All(e => e == brushes[0]) ? brushes[0] : Entity.Null);
            })
            .Switch()
            .DistinctUntilChanged();
    }

    private static ReactiveProperty<Entity> GetVectorFillBrushE(Entity e)
    {
        if (e.Has<VectorFillMarkerSetting>()) return e.Get<VectorFillMarkerSetting>().BrushE;
        return e.Get<FilledPolygonSetting>().BrushE;
    }
}
