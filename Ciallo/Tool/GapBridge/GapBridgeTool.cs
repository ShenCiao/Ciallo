using System;
using System.Collections.Immutable;
using System.Runtime.Serialization;
using Ciallo.Command;
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
[RequestedByToolButton(ToolButton.Type.GapBridge)]
public class GapBridgeTool : InteractionScope, IPropertyProvider, ILayerDependent
{
    [DataMember]
    public readonly ReactiveProperty<float> DetectMaxGapLength = new(24f);
    [DataMember]
    public readonly ReactiveProperty<float> HitRadius = new(6f);

    [Substate]
    internal GapBridgeHover Hover;

    public ArrangementManager Arrangement { get; private set; }

    private GapBridgePreviewManager _preview;
    private IDisposable _arrReadySub;

    public override void ConfigureStateMachine(StateMachine sm)
    {
        sm.Configure(this)
            .InitialTransition(Hover);
        sm.Configure(Hover)
            .InternalTransition(Trigger.Press(MouseButton.Left), OnClick)
            .PermitReentry(Trigger.Refresh);
    }

    public static bool CanHandleLayers(ImmutableArray<Entity> layers) =>
        (layers[0].Has<ShapeLayerSetting>() || layers[0].Has<VectorFillLayerSetting>());

    public void DrawPropertyBeforeSubstates(PropertyContainer container)
    {
        container.AddProperty("Max gap length",
            new SpinSlider
            {
                MinValue = 1f,
                MaxValue = 128,
                Step = 1f,
                ExpEdit = true,
                AllowGreater = true,
            }.BindNumber(DetectMaxGapLength));
    }

    protected override void OnActivated()
    {
        Arrangement = PrimaryLayer.Get<ArrangementManager>();
        _preview = new GapBridgePreviewManager(Document.Get<WorldOverlay>(), Arrangement.SourceShapes, this);
        _preview.Refresh(Arrangement.ArrReady.CurrentValue);

        _arrReadySub = Arrangement.ArrReady.Subscribe(arr =>
        {
            _preview.Refresh(arr);
            if (InteractionManager.StateMachine.State is GapBridgeHover hover)
                hover.RefreshCursor();
        });
    }

    protected override void OnDeactivated()
    {
        _arrReadySub.Dispose();
        _arrReadySub = null;
        _preview.Dispose();
        _preview = null;
        Arrangement = null;
    }

    public bool TryPickBridge(Vector2 worldPosition, out GapBridge bridge)
    {
        return _preview.TryPickBridge(worldPosition, out bridge);
    }

    private void OnClick()
    {
        if (Arrangement.ArrReady.CurrentValue == null)
            return;

        var clickPosition = LatestCursor.WorldPosition;
        if (!TryPickBridge(clickPosition, out var bridge) &&
            !Hover.TryGetHoveredBridge(out bridge))
            return;

        CommitBridge(bridge);
        if (InteractionManager.StateMachine.State is GapBridgeHover hover)
            hover.RefreshHover(clickPosition);
    }

    private void CommitBridge(GapBridge bridge)
    {
        var sourceGeometry = bridge.SourceCurve.Get<SampledPolyline>();
        var repairedPositions = GapBridgeRepairGeometry.BuildRepairedPositions(Arrangement.ArrReady.CurrentValue, bridge);

        new CommandBuilder("Gap Bridge", bridge.SourceCurve)
            .SetSampledPolyline(
                repairedPositions,
                sourceGeometry.Radii.Value,
                sourceGeometry.Pressures.Value,
                sourceGeometry.Tilts.Value)
            .Commit();
    }
}
