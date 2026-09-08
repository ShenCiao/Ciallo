using System.Runtime.Serialization;
using Ciallo.Data;
using Ciallo.GuiControl;
using Ciallo.Widget;
using Frent;
using Godot;
using R3;

namespace Ciallo.Tool;

// Output selected by the shared Bucket Fill button, not a solver selection for live markers.
public enum BucketFillOutput
{
    Marker, // Live marker resolved by the existing arrangement.
    Polygon, // Standalone polygon produced by CDT region selection.
}

[DataContract]
public class BucketFillOptions
{
    [DataMember]
    public ReactiveProperty<BucketFillOutput> Mode = new(BucketFillOutput.Marker);
    [DataMember]
    public ReactiveProperty<bool> PlaceAtBottom = new(true);
    [DataMember]
    public ReactiveProperty<bool> GapAware = new(true);
    [DataMember]
    public ReactiveProperty<float> GapFactor = new(0.5f);

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
