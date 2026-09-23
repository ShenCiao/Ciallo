using System.Collections.Immutable;
using System.Runtime.Serialization;
using Ciallo.Data;
using Ciallo.GuiControl;
using Ciallo.Widget;
using Frent;
using Godot;
using R3;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

// Output selected by the shared Bucket Fill button.
public enum BucketFillOutput
{
    Marker,
    Polygon,
}

// Polygon bucket fill owns its interaction lifecycle independently of live marker filling.
[DataContract, RegisterState]
public class BucketFillTool : InteractionScope, ILayerDependent
{
    [DataMember]
    public readonly ReactiveProperty<BucketFillOutput> Mode = new(BucketFillOutput.Marker);
    [DataMember]
    public readonly ReactiveProperty<bool> PlaceAtBottom = new(true);
    [DataMember]
    public readonly ReactiveProperty<bool> GapAware = new(true);
    [DataMember]
    public readonly ReactiveProperty<float> GapFactor = new(0.5f);

    internal BucketFillContext Context { get; private set; }
    [Substate]
    internal BucketFillHover Hover;

    [Substate]
    internal BucketFillInteractor Left;

    public override void ConfigureStateMachine(StateMachine sm)
    {
        sm.Configure(this)
            .InitialTransition(Hover);
        sm.Configure(Hover)
            .Permit(Trigger.Press(MouseButton.Left), Left)
            .PermitReentry(Trigger.Refresh);
        sm.Configure(Left)
            .Permit(Trigger.Release(MouseButton.Left), Hover)
            .PermitStandardExits(Hover);
    }

    public static bool CanHandleLayers(ImmutableArray<Entity> layers) =>
        layers[0].Has<ShapeLayerSetting>();

    protected override void OnActivated() => Context = new BucketFillContext(PrimaryLayer);

    protected override void OnDeactivated()
    {
        Context.Dispose();
        Context = null;
    }

    public void DrawPolygonProperties(PropertyContainer container, Entity document)
    {
        DrawModeProperty(container);
        container.AddProperty("Place at bottom", new CheckBox().BindBool(PlaceAtBottom));
        container.AddProperty("Gap aware", new CheckBox().BindBool(GapAware));
        container.AddProperty("Gap factor", new SpinSlider { MinValue = 0, MaxValue = 1, Step = 0.01 }
            .BindNumber(GapFactor)).VisibleIf(GapAware, value => value);
        var brushes = VectorFillBrushPreviewList.New(document);
        brushes.CustomMinimumSize = new(0, 256);
        container.AddChild(brushes);
        brushes.DrawNameProperty(container);
        var selection = document.Get<SelectionManager>();
        var color = selection.WorkingVectorFillBrush.Select(e => e.TryGet<FillBrushSetting>()?.FillColor).Flatten();
        container.AddProperty("Fill color", new ColorPickerButton().BindColor(color))
            .VisibleIf(selection.WorkingVectorFillBrush, Entity.IsNotNull);
    }

    public Container DrawModeProperty(PropertyContainer container)
    {
        return container.AddProperty("Mode", new OptionButton
        {
            CustomMinimumSize = new(128, 32),
            FitToLongestItem = false,
        }.BindEnum(Mode));
    }
}
