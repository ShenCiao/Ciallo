using Ciallo.Data;
using GdUnit4;
using Godot;
using Newtonsoft.Json;
using R3;
using static GdUnit4.Assertions;

namespace Ciallo.Tool;

[TestSuite, RequireGodotRuntime]
public class ToolPreferenceTests
{
    [TestCase]
    public void PopulateKeepsStateGraphReferencesAndSubscriptions()
    {
        var preference = new Preference();
        var tools = preference.Tools;
        var original = JsonConvert.SerializeObject(tools, Preference.JsonOptions);
        var state = InteractionManager.StateMachine.State;
        var stroke = tools.PaintStroke;
        var distance = stroke.SnapDistance;
        var color = tools.VectorFill.LayerBoundedAreaColor;
        try
        {
            distance.Value = 24;
            color.Value = Colors.Red;
            int distanceChanges = 0, colorChanges = 0;
            using var distanceSubscription = distance.Skip(1).Subscribe(_ => distanceChanges++);
            using var colorSubscription = color.Skip(1).Subscribe(_ => colorChanges++);
            JsonConvert.PopulateObject("""
                {"Tools": {
                    "PaintStroke": {"SnapDistance": 37},
                    "VectorFill": {"LayerBoundedAreaColor": null},
                    "PolylineSelect": {"Mode": 1}
                }}
                """, preference, Preference.JsonOptions);

            AssertThat(ReferenceEquals(preference.Tools, tools)).IsTrue();
            AssertThat(ReferenceEquals(tools.PaintStroke, stroke)).IsTrue();
            AssertThat(ReferenceEquals(stroke.Hover.Tool, stroke)).IsTrue();
            AssertThat(ReferenceEquals(stroke.SnapDistance, distance)).IsTrue();
            AssertThat(ReferenceEquals(tools.VectorFill.LayerBoundedAreaColor, color)).IsTrue();
            AssertThat(distance.Value).IsEqual(37f);
            AssertThat(distanceChanges).IsEqual(1);
            AssertThat(color.Value.HasValue).IsFalse();
            AssertThat(colorChanges).IsEqual(1);
            AssertThat(tools.PolylineSelect.Mode.Value).IsEqual(PolylineSelectTool.EditMode.BezierDeform);
            AssertThat(ReferenceEquals(InteractionManager.StateMachine.State, state)).IsTrue();
        }
        finally
        {
            JsonConvert.PopulateObject(original, tools, Preference.JsonOptions);
        }
    }

}
