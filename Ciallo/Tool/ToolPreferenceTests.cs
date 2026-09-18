using System;
using System.Linq;
using Ciallo.Data;
using GdUnit4;
using Godot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
                    "PaintStroke": {"SnapDistance": 37, "PressureTaperStartEnabled": true},
                    "VectorFill": {"LayerBoundedAreaColor": null},
                    "PolylineSelect": {"Mode": 1},
                    "Liquify": {"Radius": 96},
                    "RemovedTool": {"Mode": 1}
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
            AssertThat(stroke.PressureTaperStartEnabled.Value).IsTrue();
            AssertThat(tools.PolylineSelect.Mode.Value).IsEqual(PolylineSelectTool.EditMode.BezierDeform);
            AssertThat(tools.Liquify.Radius.Value).IsEqual(96f);
            AssertThat(ReferenceEquals(InteractionManager.StateMachine.State, state)).IsTrue();

            var saved = JObject.Parse(JsonConvert.SerializeObject(preference, Preference.JsonOptions));
            var savedTools = (JObject)saved["Tools"]!;
            AssertThat(savedTools.Properties().Select(p => p.Name).Order().ToArray()).ContainsExactly(
                "BucketFill", "GapBridge", "Liquify", "PaintStroke", "PolylineSelect", "VectorFill", "VectorFillLayerCreation");
            AssertThat(((JObject)savedTools["PaintStroke"]!).Properties().Select(p => p.Name).Order().ToArray())
                .ContainsExactly("Mode", "PressureTaperEndEnabled", "PressureTaperEndLength",
                    "PressureTaperStartEnabled", "PressureTaperStartLength", "SnapDistance", "SnapEnabled");
            AssertThat(savedTools["VectorFill"]!["LayerBoundedAreaColor"]!.Type).IsEqual(JTokenType.Null);

            distance.Value = 5;
            JsonConvert.PopulateObject(saved.ToString(), preference, Preference.JsonOptions);
            AssertThat(distance.Value).IsEqual(37f);
            AssertThat(ReferenceEquals(stroke.SnapDistance, distance)).IsTrue();
        }
        finally
        {
            JsonConvert.PopulateObject(original, tools, Preference.JsonOptions);
        }
    }

    [TestCase]
    public void OldFilesKeepApplicationSettingsAndLeaveToolsAtTheirDefaults()
    {
        var preference = new Preference();
        var toolsBefore = JsonConvert.SerializeObject(preference.Tools, Preference.JsonOptions);
        JsonConvert.PopulateObject("""
            {
                "Language": "zh_CN",
                "PaintStrokeSnapDistance": 99,
                "BucketFill": {"Mode": 1},
                "VectorFillLayerBoundedAreaColor": null
            }
            """, preference, Preference.JsonOptions);

        AssertThat(preference.Language.Value).IsEqual("zh_CN");
        AssertThat(JsonConvert.SerializeObject(preference.Tools, Preference.JsonOptions)).IsEqual(toolsBefore);
    }
}
