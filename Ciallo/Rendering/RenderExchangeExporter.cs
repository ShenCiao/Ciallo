using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Ciallo.Data;
using Frent;
using Godot;

namespace Ciallo.Rendering;

/// <summary>Current-frame, display-only RIFF export. The C2DI draft is consumed by the web gallery.</summary>
public static class RenderExchangeExporter
{
    public static void Save(Entity document, string path)
    {
        var bytes = Capture(document);
        var staging = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllBytes(staging, bytes);
            File.Move(staging, path, true);
        }
        finally
        {
            if (File.Exists(staging)) File.Delete(staging);
        }
    }

    public static byte[] Capture(Entity document)
    {
        using var capture = new CaptureContext(document.Get<SelectionManager>().CurrentFrame.Value);
        var nodes = capture.Children(document);
        var setting = document.Get<DocumentSetting>();
        var size = setting.ReferenceSize.Value;
        var header = JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = 1,
            profile = "ciallo-airbrush-1",
            name = setting.Name.Value,
            width = size.X,
            height = size.Y,
            frame = capture.Frame,
            colorSpace = "linear-srgb",
            brushes = capture.Brushes,
            nodes,
        });
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Encoding.UTF8, true);
        writer.Write("RIFF"u8);
        writer.Write(0u);
        writer.Write("C2DI"u8);
        WriteChunk(writer, "JSON", header);
        WriteChunk(writer, "BIN0", capture.Data.ToArray());
        output.Position = 4;
        writer.Write(checked((uint)(output.Length - 8)));
        return output.ToArray();
    }

    private static void WriteChunk(BinaryWriter writer, string tag, byte[] bytes)
    {
        writer.Write(Encoding.ASCII.GetBytes(tag));
        writer.Write(checked((uint)bytes.Length));
        writer.Write(bytes);
        if ((bytes.Length & 1) != 0) writer.Write((byte)0);
    }

    private sealed class CaptureContext(int frame) : IDisposable
    {
        public int Frame { get; } = frame;
        public MemoryStream Data { get; } = new();
        public List<object> Brushes { get; } = [];
        private readonly Dictionary<Entity, int> _brushIndices = [];

        private object Floats(IEnumerable<float> values)
        {
            var offset = checked((int)Data.Position);
            using var writer = new BinaryWriter(Data, Encoding.UTF8, true);
            int count = 0;
            foreach (var value in values)
            {
                if (!float.IsFinite(value)) throw new InvalidDataException("Non-finite render geometry or brush data.");
                writer.Write(value);
                count++;
            }
            return new { offset, count };
        }

        private object Points(IEnumerable<Vector2> values) => Floats(values.SelectMany(p => new[] { p.X, p.Y }));

        private object Indices(IEnumerable<int> values)
        {
            var offset = checked((int)Data.Position);
            using var writer = new BinaryWriter(Data, Encoding.UTF8, true);
            int count = 0;
            foreach (var value in values) { writer.Write(checked((uint)value)); count++; }
            return new { offset, count };
        }

        private static float[] ColorValues(Color color)
        {
            var linear = color.SrgbToLinear();
            return [linear.R, linear.G, linear.B, linear.A];
        }

        private int Brush(Entity entity)
        {
            if (_brushIndices.TryGetValue(entity, out int index)) return index;
            if (entity.IsNull) throw new NotSupportedException("A stroke has no brush. Assign a brush before exporting.");
            var brush = entity.Get<StrokeBrushSetting>();
            if (brush.RenderingType.Value != BrushRenderingType.Airbrush || brush.ActiveBrushFlags.Value.HasFlag(BrushFlags.Dash))
                throw new NotSupportedException($"Brush '{brush.Name.Value}' is not supported by the current web profile. It supports Airbrush without dashes.");
            index = Brushes.Count;
            _brushIndices.Add(entity, index);
            using var pressure = StrokeBrushMaterial.BakeCurve(brush.Pressure2FlowCurve.Value);
            using var falloff = StrokeBrushMaterial.BakeCurve(brush.FalloffCurve.Value);
            Brushes.Add(new
            {
                color = ColorValues(brush.Color.Value),
                density = brush.AlphaDensity.Value,
                pressureFlow = brush.ActiveBrushFlags.Value.HasFlag(BrushFlags.Pressure2Flow),
                erase = brush.BlendMode.Value == BlendMode.Erase,
                // Only mip zero; runtime uses explicit level-zero interpolation.
                pressure = Floats(Enumerable.Range(0, 512).Select(i => pressure.GetPixel(i, 0).R)),
                falloff = Floats(Enumerable.Range(0, 512).Select(i => falloff.GetPixel(i, 0).R)),
            });
            return index;
        }

        public List<object> Children(Entity parent)
        {
            IEnumerable<Entity> children = parent.Get<LayerTreeNode>().Children;
            if (parent.Has<FolderLayerSetting>())
            {
                var folder = parent.Get<FolderLayerSetting>();
                if (folder.IsCelFolder)
                {
                    var exposed = Entity.Null;
                    foreach (var pair in folder.Exposures)
                    {
                        if (pair.Key > Frame) break;
                        exposed = pair.Value;
                    }
                    children = exposed.IsNull || exposed == parent ? [] : [exposed];
                }
            }
            var ordered = children.ToArray();
            List<object> result = [];
            for (int i = 0; i < ordered.Length; i++)
            {
                var entity = ordered[i];
                if (!entity.Has<CommonLayerSetting>())
                {
                    result.Add(Draw(entity));
                    continue;
                }
                var setting = entity.Get<CommonLayerSetting>();
                // An orphaned clipped layer has zero coverage, matching Layer2D.
                if (setting.ClippingMask.Value) continue;
                int end = i + 1;
                while (end < ordered.Length && ordered[end].Has<CommonLayerSetting>() &&
                       ordered[end].Get<CommonLayerSetting>().ClippingMask.Value) end++;
                if (setting.IsVisible.Value && setting.Opacity.Value > 0)
                {
                    var node = Layer(entity);
                    if (end > i + 1)
                    {
                        var clips = ordered[(i + 1)..end]
                            .Where(e => e.Get<CommonLayerSetting>().IsVisible.Value && e.Get<CommonLayerSetting>().Opacity.Value > 0)
                            .Select(Layer).ToArray();
                        result.Add(new { kind = "clip", @base = node, clips });
                    }
                    else result.Add(node);
                }
                i = end - 1;
            }
            return result;
        }

        private object Layer(Entity entity)
        {
            if (!entity.Has<FolderLayerSetting>() && !entity.Has<ShapeLayerSetting>())
                throw new NotSupportedException($"Layer '{entity.Get<CommonLayerSetting>().Name.Value}' is not supported by the web export profile.");
            var setting = entity.Get<CommonLayerSetting>();
            return new
            {
                kind = "group",
                name = setting.Name.Value,
                opacity = setting.Opacity.Value,
                blend = (int)setting.BlendMode.Value,
                children = Children(entity),
            };
        }

        private object Draw(Entity entity)
        {
            var polyline = entity.Get<SampledPolyline>();
            if (entity.Has<StrokeSetting>())
            {
                var setting = entity.Get<StrokeSetting>();
                return new
                {
                    kind = "stroke",
                    brush = Brush(setting.Brush.Value),
                    positions = Points(polyline.Positions.Value),
                    radii = Floats(polyline.Radii.Value),
                    pressures = Floats(polyline.Pressures.Value),
                    tilts = Points(polyline.Tilts.Value),
                };
            }
            if (entity.Has<FilledPolygonSetting>())
            {
                var brush = entity.Get<FilledPolygonSetting>().BrushE.Value;
                if (brush.IsNull) throw new NotSupportedException("A fill has no brush. Assign a brush before exporting.");
                using var triangles = Arrangement2D.RepairAndTriangulate(new Godot.Collections.Array<Vector2[]> { polyline.Positions.Value.ToArray() });
                return new
                {
                    kind = "fill",
                    color = ColorValues(brush.Get<FillBrushSetting>().FillColor.Value),
                    positions = Points((Vector2[])triangles["vertices"]),
                    indices = Indices((int[])triangles["indices"]),
                };
            }
            throw new NotSupportedException("This drawing type is not supported by the web export profile.");
        }

        public void Dispose() => Data.Dispose();
    }
}
