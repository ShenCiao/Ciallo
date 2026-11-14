using System;
using System.Buffers;
using System.IO;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using DuckDB.NET.Data;
using Frent;
using GdUnit4;
using MessagePack;
using static GdUnit4.Assertions;

namespace Ciallo.Tests;

[TestSuite, RequireGodotRuntime]
public class BrushRadiusPersistenceTests
{
    [TestCase]
    public void NullableRadiusAndLegacyStorageKeepExistingGeometryAndDrawingDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), "ciallo-radius-" + Guid.NewGuid().ToString("N") + ".ciallo");
        var document = AppDocumentManager.Create(new DocumentSetting());
        Entity loaded = default;
        try
        {
            var manager = document.Get<BrushManager>();
            manager.CurrentStrokeRadius.Value = 9f;
            foreach (float? radius in new float?[] { null, 12f })
            {
                var brush = document.World.Create();
                brush.Add(new StrokeBrushSetting { IndependentRadius = { Value = radius } });
                brush.Tag<ToSerializeTag>();
                manager.StrokeBrushes.Add(brush);
            }
            var stroke = document.World.Create();
            new NewStrokeCmd().CreateDataOnly(stroke);
            stroke.Tag<ToSerializeTag>();
            stroke.Get<StrokeSetting>().Brush.Value = manager.StrokeBrushes[0];
            var geometry = stroke.Get<SampledPolyline>();
            geometry.Positions.Value = [new(0, 0), new(20, 0)];
            geometry.Radii.Value = [2f, 5f];
            geometry.Pressures.Value = [0.2f, 0.8f];
            geometry.Tilts.Value = [default, default];

            var snapshot = PersistenceSnapshotCapture.Capture(document, 1);
            manager.CurrentStrokeRadius.Value = 30f;
            manager.StrokeBrushes[0].Get<StrokeBrushSetting>().IndependentRadius.Value = 40f;
            DuckDbProjectSerializer.Save(snapshot, path);
            loaded = DuckDbProjectSerializer.Load(path);
            var restored = loaded.Get<BrushManager>();
            AssertThat(restored.CurrentStrokeRadius.Value).IsEqual(9f);
            AssertThat(restored.StrokeBrushes.Select(e => e.Get<StrokeBrushSetting>().IndependentRadius.Value).ToArray())
                .ContainsExactly((float?)null, 12f);
            AssertThat(restored.StrokeBrushes[0].Get<StrokeBrushSetting>().Clone().IndependentRadius.Value.HasValue).IsFalse();
            AssertThat(restored.StrokeBrushes[1].Get<StrokeBrushSetting>().Clone().IndependentRadius.Value).IsEqual(12f);
            AssertGeometry(loaded);
            DisposeDataWorld(loaded.World);
            loaded = default;

            // Older files have numeric BaseRadius values and no document radius column.
            using (var connection = new DuckDBConnection($"Data Source={path}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE component_StrokeBrushSetting SET BaseRadius = COALESCE(BaseRadius, 1.5); "
                    + "ALTER TABLE component_BrushManager DROP COLUMN CurrentStrokeRadius;";
                command.ExecuteNonQuery();
            }
            loaded = DuckDbProjectSerializer.Load(path);
            restored = loaded.Get<BrushManager>();
            AssertThat(restored.CurrentStrokeRadius.Value).IsEqual(BrushManager.DefaultStrokeRadius);
            AssertThat(restored.StrokeBrushes.Select(e => e.Get<StrokeBrushSetting>().IndependentRadius.Value).ToArray())
                .ContainsExactly((float?)1.5f, 12f);
            AssertGeometry(loaded);

            var buffer = new ArrayBufferWriter<byte>();
            var writer = new MessagePackWriter(buffer);
            writer.WriteMapHeader(1);
            writer.Write("BaseRadius");
            writer.Write(12f);
            writer.Flush();
            var legacyBrush = MessagePackSerializer.Deserialize<StrokeBrushSetting>(buffer.WrittenMemory);
            AssertThat(legacyBrush.IndependentRadius.Value).IsEqual(12f);
        }
        finally
        {
            if (!loaded.IsNull) DisposeDataWorld(loaded.World);
            AppDocumentManager.Remove(document);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void AssertGeometry(Entity document)
    {
        Entity[] strokes = [.. document.World.CreateQuery().With<StrokeSetting>().Build().EnumerateWithEntities()];
        var stroke = strokes.Single();
        AssertThat(stroke.Get<SampledPolyline>().Radii.Value.ToArray()).ContainsExactly(2f, 5f);
    }

    private static void DisposeDataWorld(World world)
    {
        foreach (var entity in world.CreateQuery().Build().EnumerateWithEntities()) entity.Delete();
        world.Dispose();
    }
}
