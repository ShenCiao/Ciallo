using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
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
    [TestCase]
    public void SnapshotKeepsHiddenShapeColorsPaletteAndNullableOverridesAcrossCopyAndReload()
    {
        var path = Path.Combine(Path.GetTempPath(), "ciallo-colors-" + Guid.NewGuid().ToString("N") + ".ciallo");
        var document = AppDocumentManager.Create(new DocumentSetting());
        Entity loaded = default;
        using var copyWorld = new World();
        try
        {
            var strokeBrush = AddBrush(document, true, null);
            var fillBrush = AddBrush(document, false, Colors.White);
            var stroke = AddShape(document, strokeBrush, 0, Colors.Red);
            AddShape(document, fillBrush, 1, Colors.Green);
            AddShape(document, fillBrush, 2, new Color(0.2f, 0.4f, 0.8f, 0.3f));
            var colors = document.Get<ColorManager>();
            colors.CurrentColor.Value = Colors.Orange;
            colors.Swatches.Value = [Colors.Red, new Color(0.2f, 0.4f, 0.8f, 0.3f), Colors.Transparent];
            var snapshot = PersistenceSnapshotCapture.Capture(document, 10);
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
                var copy = copyWorld.Create();
                NewShapeCmdBase.CreateCopyCommand(shape, new Dictionary<Entity, Entity>()).CreateDataOnly(copy);
                AssertThat(copy.Get<ShapeSetting>().Color.Value).IsEqual(expected);
                copy.Get<ShapeSetting>().Color.Value = Colors.Magenta;
                AssertThat(shape.Get<ShapeSetting>().Color.Value).IsEqual(expected);
                copy.Delete();
            }
            var loadedStroke = shapes.Single(e => e.Has<StrokeSetting>());
            AssertThat(loadedStroke.Get<StrokeSetting>().Brush.Value.Get<StrokeBrushSetting>().Color.Value.HasValue).IsFalse();
            var loadedFill = shapes.Single(e => e.Has<FilledPolygonSetting>());
            AssertThat(loadedFill.Get<FilledPolygonSetting>().BrushE.Value.Get<FillBrushSetting>().FillColor.Value).IsEqual(Colors.White);
        }
        finally
        {
            if (!loaded.IsNull) DisposeDataWorld(loaded.World);
            AppDocumentManager.Remove(document);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [TestCase]
    public void LegacyFileAndBrushLibraryKeepBrushOverridesAndDefaultNewComponents()
    {
        var path = Path.Combine(Path.GetTempPath(), "ciallo-legacy-colors-" + Guid.NewGuid().ToString("N") + ".ciallo");
        var document = AppDocumentManager.Create(new DocumentSetting());
        Entity loaded = default;
        var oldColor = new Color(0.3f, 0.6f, 0.9f, 0.4f);
        try
        {
            var strokeBrush = AddBrush(document, true, oldColor);
            var fillBrush = AddBrush(document, false, oldColor);
            AddShape(document, strokeBrush, 0, Colors.Black);
            AddShape(document, fillBrush, 1, Colors.Black);
            AddShape(document, fillBrush, 2, Colors.Black);
            AppDocumentManager.Save(document, path);
            using (var connection = new DuckDBConnection($"Data Source={path}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "DROP TABLE component_ShapeSetting; DROP TABLE component_ColorManager;";
                command.ExecuteNonQuery();
            }
            loaded = DuckDbProjectSerializer.Load(path);
            AssertThat(loaded.Get<ColorManager>().Swatches.Value.Length).IsEqual(0);
            var shapes = Shapes(loaded);
            AssertThat(shapes.Length).IsEqual(3);
            foreach (var shape in shapes)
            {
                AssertThat(shape.Get<ShapeSetting>().Color.Value).IsEqual(Colors.Black);
                var brushColor = shape.Has<StrokeSetting>()
                    ? shape.Get<StrokeSetting>().Brush.Value.Get<StrokeBrushSetting>().Color.Value
                    : (shape.Has<FilledPolygonSetting>()
                        ? shape.Get<FilledPolygonSetting>().BrushE.Value
                        : shape.Get<VectorFillMarkerSetting>().BrushE.Value).Get<FillBrushSetting>().FillColor.Value;
                AssertThat(brushColor).IsEqual(oldColor);
            }

            // The old brush-library contract wrote a non-nullable Color under this map key.
            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            writer.WriteMapHeader(1);
            writer.Write("Color");
            MessagePackSerializer.Serialize(ref writer, oldColor);
            writer.Flush();
            var brush = MessagePackSerializer.Deserialize<StrokeBrushSetting>(buffer.WrittenMemory);
            AssertThat(brush.Color.Value).IsEqual(oldColor);
            brush.Color.Value = null;
            AssertThat(brush.Clone().Color.Value.HasValue).IsFalse();
        }
        finally
        {
            if (!loaded.IsNull) DisposeDataWorld(loaded.World);
            AppDocumentManager.Remove(document);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static Entity AddBrush(Entity document, bool stroke, Color? color)
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
