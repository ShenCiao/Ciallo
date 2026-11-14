using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.GuiControl;
using DuckDB.NET.Data;
using Frent;
using GdUnit4;
using Godot;
using MessagePack;
using static GdUnit4.Assertions;

namespace Ciallo.Tests;

[TestSuite, RequireGodotRuntime]
public class ShapeColorPersistenceTests
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void SnapshotKeepsChannelSourcesHiddenValuesAndPaletteAcrossCopyAndReload(bool shapeRgb, bool shapeAlpha)
    {
        var path = Path.Combine(Path.GetTempPath(), "ciallo-colors-" + Guid.NewGuid().ToString("N") + ".ciallo");
        var document = AppDocumentManager.Create(new DocumentSetting());
        Entity loaded = default;
        using var copyWorld = new World();
        try
        {
            var materialColor = new Color(0.3f, 0.6f, 0.9f, 0.4f);
            var strokeBrush = AddBrush(document, true, materialColor);
            var fillBrush = AddBrush(document, false, materialColor);
            var strokeFlags = BrushFlags.Pressure2Flow
                | (shapeRgb ? BrushFlags.UseShapeRgb : 0) | (shapeAlpha ? BrushFlags.UseShapeAlpha : 0);
            var fillFlags = (shapeRgb ? FillBrushFlags.UseShapeRgb : 0) | (shapeAlpha ? FillBrushFlags.UseShapeAlpha : 0);
            strokeBrush.Get<StrokeBrushSetting>().ActiveBrushFlags.Value = strokeFlags;
            fillBrush.Get<FillBrushSetting>().ActiveBrushFlags.Value = fillFlags;
            var stroke = AddShape(document, strokeBrush, 0, Colors.Red);
            AddShape(document, fillBrush, 1, Colors.Green);
            AddShape(document, fillBrush, 2, new Color(0.2f, 0.4f, 0.8f, 0.3f));
            var colors = document.Get<ColorManager>();
            colors.CurrentColor.Value = Colors.Orange;
            colors.Swatches.Value = [Colors.Red, new Color(0.2f, 0.4f, 0.8f, 0.3f), Colors.Transparent];
            var snapshot = PersistenceSnapshotCapture.Capture(document, 10);
            strokeBrush.Get<StrokeBrushSetting>().Color.Value = Colors.White;
            strokeBrush.Get<StrokeBrushSetting>().ActiveBrushFlags.Value = 0;
            stroke.Get<ShapeSetting>().Color.Value = Colors.Blue;
            colors.Swatches.Value = [];
            colors.CurrentColor.Value = Colors.Black;
            DuckDbProjectSerializer.Save(snapshot, path);
            loaded = DuckDbProjectSerializer.Load(path);
            AssertThat(loaded.Get<ColorManager>().CurrentColor.Value).IsEqual(Colors.Orange);
            AssertThat(loaded.Get<ColorManager>().Swatches.Value.ToArray())
                .ContainsExactly(Colors.Red, new Color(0.2f, 0.4f, 0.8f, 0.3f), Colors.Transparent);

            var shapes = Shapes(loaded);
            AssertThat(shapes.Length).IsEqual(3);
            foreach (var shape in shapes)
            {
                var expected = shape.Has<StrokeSetting>() ? Colors.Red
                    : shape.Has<FilledPolygonSetting>() ? Colors.Green : new Color(0.2f, 0.4f, 0.8f, 0.3f);
                AssertThat(shape.Get<ShapeSetting>().Color.Value).IsEqual(expected);
                var effective = new Color(shapeRgb ? expected : materialColor) { A = shapeAlpha ? expected.A : materialColor.A };
                if (shape.Has<StrokeSetting>())
                {
                    var brush = shape.Get<StrokeSetting>().Brush.Value.Get<StrokeBrushSetting>();
                    AssertThat(brush.ActiveBrushFlags.Value).IsEqual(strokeFlags);
                    AssertThat(brush.ResolveColor(expected)).IsEqual(effective);
                    AssertThat(brush.Clone().ResolveColor(expected)).IsEqual(effective);
                    AssertThat(brush.Color.Value).IsEqual(materialColor);
                }
                else
                {
                    var brushEntity = shape.Has<FilledPolygonSetting>() ? shape.Get<FilledPolygonSetting>().BrushE.Value
                        : shape.Get<VectorFillMarkerSetting>().BrushE.Value;
                    var brush = brushEntity.Get<FillBrushSetting>();
                    AssertThat(brush.ActiveBrushFlags.Value).IsEqual(fillFlags);
                    AssertThat(brush.ResolveColor(expected)).IsEqual(effective);
                    AssertThat(brush.Clone().ResolveColor(expected)).IsEqual(effective);
                    AssertThat(brush.FillColor.Value).IsEqual(materialColor);
                }
                var copy = copyWorld.Create();
                NewShapeCmdBase.CreateCopyCommand(shape, new Dictionary<Entity, Entity>()).CreateDataOnly(copy);
                AssertThat(copy.Get<ShapeSetting>().Color.Value).IsEqual(expected);
                copy.Get<ShapeSetting>().Color.Value = Colors.Magenta;
                AssertThat(shape.Get<ShapeSetting>().Color.Value).IsEqual(expected);
                copy.Delete();
            }
        }
        finally
        {
            if (!loaded.IsNull) DisposeDataWorld(loaded.World);
            AppDocumentManager.Remove(document);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestCase]
    public void Version004ProjectAndBrushLibraryKeepMaterialChannelsAndDefaultNewComponents()
    {
        var path = Path.Combine(Path.GetTempPath(), "ciallo-legacy-colors-" + Guid.NewGuid().ToString("N") + ".ciallo");
        Entity loaded = default;
        var oldColor = new Color(0.3f, 0.6f, 0.9f, 0.4f);
        try
        {
            // Construct the relevant v0.0.4 tables directly, independent of today's writer.
            using (var connection = new DuckDBConnection($"Data Source={path}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE metadata(key VARCHAR PRIMARY KEY, value VARCHAR NOT NULL);
                    INSERT INTO metadata VALUES ('format','ciallo-project-duckdb'), ('format_version','1'),
                        ('created_by','Ciallo'), ('ciallo_version','0.0.4');
                    CREATE TABLE entities(id INTEGER PRIMARY KEY);
                    INSERT INTO entities VALUES (0),(1),(2),(3),(4),(5);
                    CREATE TABLE component_DocumentSetting(entity_id INTEGER PRIMARY KEY, DocumentId UUID);
                    INSERT INTO component_DocumentSetting VALUES (0,'b74c3838-6b4a-46ad-aeef-a408a1056dd3');
                    CREATE TABLE component_StrokeBrushSetting(entity_id INTEGER PRIMARY KEY,
                        Color STRUCT(r FLOAT,g FLOAT,b FLOAT,a FLOAT), ActiveBrushFlags INTEGER, BaseRadius FLOAT);
                    INSERT INTO component_StrokeBrushSetting VALUES (1,{'r':0.3,'g':0.6,'b':0.9,'a':0.4},5,12);
                    CREATE TABLE component_FillBrushSetting(entity_id INTEGER PRIMARY KEY,
                        FillColor STRUCT(r FLOAT,g FLOAT,b FLOAT,a FLOAT));
                    INSERT INTO component_FillBrushSetting VALUES (2,{'r':0.3,'g':0.6,'b':0.9,'a':0.4});
                    CREATE TABLE component_StrokeSetting(entity_id INTEGER PRIMARY KEY, Brush INTEGER,
                        OverrideColor STRUCT(r FLOAT,g FLOAT,b FLOAT,a FLOAT));
                    INSERT INTO component_StrokeSetting VALUES (3,1,NULL);
                    CREATE TABLE component_FilledPolygonSetting(entity_id INTEGER PRIMARY KEY, BrushE INTEGER);
                    INSERT INTO component_FilledPolygonSetting VALUES (4,2);
                    CREATE TABLE component_VectorFillMarkerSetting(entity_id INTEGER PRIMARY KEY, BrushE INTEGER);
                    INSERT INTO component_VectorFillMarkerSetting VALUES (5,2);
                    """;
                command.ExecuteNonQuery();
            }
            loaded = DuckDbProjectSerializer.Load(path);
            AssertThat(loaded.Get<ColorManager>().Swatches.Value.Length).IsEqual(0);
            var shapes = Shapes(loaded);
            AssertThat(shapes.Length).IsEqual(3);
            foreach (var shape in shapes)
            {
                AssertThat(shape.Get<ShapeSetting>().Color.Value).IsEqual(Colors.Black);
                var resolved = shape.Has<StrokeSetting>()
                    ? shape.Get<StrokeSetting>().Brush.Value.Get<StrokeBrushSetting>().ResolveColor(Colors.Transparent)
                    : (shape.Has<FilledPolygonSetting>()
                        ? shape.Get<FilledPolygonSetting>().BrushE.Value
                        : shape.Get<VectorFillMarkerSetting>().BrushE.Value).Get<FillBrushSetting>().ResolveColor(Colors.Transparent);
                AssertThat(resolved).IsEqual(oldColor);
            }
            var oldStrokeBrush = shapes.Single(e => e.Has<StrokeSetting>()).Get<StrokeSetting>().Brush.Value.Get<StrokeBrushSetting>();
            AssertThat(oldStrokeBrush.ActiveBrushFlags.Value).IsEqual(BrushFlags.Pressure2Flow | BrushFlags.Pressure2Radius);
            AssertThat(oldStrokeBrush.IndependentRadius.Value).IsEqual(12f);

            // The old brush-library contract wrote a non-nullable Color under this map key.
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            writer.WriteMapHeader(3);
            writer.Write("Color");
            MessagePackSerializer.Serialize(ref writer, oldColor);
            writer.Write("ActiveBrushFlags");
            writer.Write(5);
            writer.Write("BaseRadius");
            writer.Write(12f);
            writer.Flush();
            var brush = MessagePackSerializer.Deserialize<StrokeBrushSetting>(buffer.WrittenMemory);
            AssertThat(brush.Color.Value).IsEqual(oldColor);
            AssertThat(brush.ResolveColor(Colors.Transparent)).IsEqual(oldColor);
            AssertThat(brush.ActiveBrushFlags.Value).IsEqual(oldStrokeBrush.ActiveBrushFlags.Value);
            AssertThat(brush.Clone().ResolveColor(Colors.Transparent)).IsEqual(oldColor);
            AssertThat(brush.IndependentRadius.Value).IsEqual(12f);
        }
        finally
        {
            if (!loaded.IsNull) DisposeDataWorld(loaded.World);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestCase]
    public void WorkingColorRoutesChannelsAtomicallyAndSwitchingBrushDoesNotWriteHistory()
    {
        var document = AppDocumentManager.Create(new DocumentSetting());
        var initialGlobal = new Color(0.2f, 0.4f, 0.6f, 0.2f);
        var initialMaterial = new Color(0.3f, 0.6f, 0.9f, 0.4f);
        var first = AddBrush(document, true, initialMaterial);
        var second = AddBrush(document, true, Colors.Green);
        var setting = first.Get<StrokeBrushSetting>();
        setting.ActiveBrushFlags.Value = BrushFlags.UseShapeRgb;
        var global = document.Get<ColorManager>().CurrentColor;
        global.Value = initialGlobal;
        var selected = document.Get<SelectionManager>().WorkingStrokeBrush;
        selected.Value = first;
        var history = document.Get<CommandManager>();
        var button = StrokeBrushToolControls.CreateColor(document);
        ((SceneTree)Engine.GetMainLoop()).Root.AddChild(button);
        try
        {
            AssertThat(button.Color).IsEqual(new Color(initialGlobal) { A = initialMaterial.A });
            var edit = new Color(1, 0, 0, 0.6f);
            button.EmitSignal(ColorPickerButton.SignalName.ColorChanged, edit);
            AssertThat(global.Value).IsEqual(new Color(edit) { A = initialGlobal.A });
            AssertThat(setting.Color.Value).IsEqual(new Color(initialMaterial) { A = edit.A });
            history.Undo();
            AssertThat(global.Value).IsEqual(initialGlobal);
            AssertThat(setting.Color.Value).IsEqual(initialMaterial);
            history.Redo();
            AssertThat(button.Color).IsEqual(edit);
            long epoch = history.PersistenceEpoch;
            selected.Value = second;
            AssertThat(button.Color).IsEqual(Colors.Green);
            selected.Value = first;
            AssertThat(button.Color).IsEqual(edit);
            AssertThat(history.PersistenceEpoch).IsEqual(epoch);

            new CommandBuilder("Swap Color Sources", document)
                .SetProperty(setting.ActiveBrushFlags, BrushFlags.UseShapeAlpha).Commit();
            edit = new Color(0, 0, 1, 0);
            button.EmitSignal(ColorPickerButton.SignalName.ColorChanged, edit);
            AssertThat(setting.Color.Value).IsEqual(new Color(0, 0, 1, 0.6f));
            AssertThat(global.Value).IsEqual(new Color(1, 0, 0, 0));
            history.Undo();
            AssertThat(setting.Color.Value).IsEqual(new Color(initialMaterial) { A = 0.6f });
            AssertThat(global.Value).IsEqual(new Color(1, 0, 0, 0.2f));
            history.Undo();
            AssertThat(button.Color).IsEqual(new Color(1, 0, 0, 0.6f));
        }
        finally
        {
            button.Free();
            AppDocumentManager.Remove(document);
        }
    }

    private static Entity AddBrush(Entity document, bool stroke, Color color)
    {
        var brush = document.World.Create();
        brush.Tag<ToSerializeTag>();
        if (stroke) brush.Add(new StrokeBrushSetting { Color = { Value = color } });
        else brush.Add(new FillBrushSetting { FillColor = { Value = color } });
        return brush;
    }

    private static Entity AddShape(Entity document, Entity brush, int kind, Color color)
    {
        var shape = document.World.Create();
        NewShapeCmdBase command = kind == 0 ? new NewStrokeCmd()
            : kind == 1 ? new NewFilledPolygonCmd() : new NewVectorFillMarkerCmd();
        command.CreateDataOnly(shape);
        shape.Tag<ToSerializeTag>();
        shape.Get<ShapeSetting>().Color.Value = color;
        if (kind == 0) shape.Get<StrokeSetting>().Brush.Value = brush;
        else if (kind == 1) shape.Get<FilledPolygonSetting>().BrushE.Value = brush;
        else shape.Get<VectorFillMarkerSetting>().BrushE.Value = brush;
        return shape;
    }

    private static Entity[] Shapes(Entity document) =>
        [.. document.World.CreateQuery().With<ShapeSetting>().Build().EnumerateWithEntities()];

    private static void DisposeDataWorld(World world)
    {
        foreach (var entity in world.CreateQuery().Build().EnumerateWithEntities()) entity.Delete();
        world.Dispose();
    }
}
