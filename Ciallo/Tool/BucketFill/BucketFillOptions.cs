using Ciallo.Widget;
using Godot;
using R3;

namespace Ciallo.Tool;

// Output selected by the shared Bucket Fill button, not a solver selection for live markers.
public enum BucketFillOutput
{
    Marker, // Live marker resolved by the existing arrangement.
    Polygon, // Standalone polygon; the future CDT/query implementation belongs only to this output.
}

public static class BucketFillOptions
{
    public static readonly ReactiveProperty<BucketFillOutput> Mode = new(BucketFillOutput.Marker);

    public static void DrawModeProperty(PropertyContainer container)
    {
        container.AddProperty("Mode", new OptionButton
        {
            CustomMinimumSize = new(128, 32),
            FitToLongestItem = false,
        }.BindEnum(Mode));
    }
}
