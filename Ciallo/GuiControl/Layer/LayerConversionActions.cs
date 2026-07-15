using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Geometry;
using Frent;
using Godot;
using GodotDictionary = Godot.Collections.Dictionary;

namespace Ciallo.GuiControl;

internal static class LayerConversionActions
{
    private static readonly GodotDictionary VectorizeParameters = new()
    {
        ["threshold"] = 0.01f,
        ["despeckling"] = 4.0f,
        ["max_thickness"] = 0.0f,
    };

    public static void ConvertToShape(Entity layer)
    {
        if (layer.Has<ImageLayerSetting>())
        {
            ConvertImageToShape(layer);
            return;
        }

        ConvertVectorFillToShape(layer);
    }

    private static void ConvertImageToShape(Entity imageLayer)
    {
        var imageSetting = imageLayer.Get<ImageLayerSetting>();
        var strokes = CenterlineVectorizer.VectorizeTexture(
            imageSetting.Texture,
            VectorizeParameters);
        var imageTransform = imageSetting.ImageTransform.Value;
        var halfImageSize = imageSetting.ImageSize * 0.5f;
        var radiusScale = (imageTransform.X.Length() + imageTransform.Y.Length()) * 0.5f;
        var brush = imageLayer.Document.Get<SelectionManager>().WorkingStrokeBrush.Value;

        var layerNode = imageLayer.Get<LayerTreeNode>();
        var shapeLayer = imageLayer.World.Create();
        var command = new CommandBuilder("Convert Image to Shape", shapeLayer)
            .NewShapeLayer()
            .AddToLayerTree(layerNode.ParentValue, layerNode.Index)
            .SetProperty(
                e => e.Get<CommonLayerSetting>().Name,
                imageLayer.Get<CommonLayerSetting>().Name.Value + " Converted");

        foreach (var stroke in strokes)
        {
            var pixelPositions = stroke["positions"].AsVector2Array();
            var pixelRadii = stroke["radii"].AsFloat32Array();
            var positions = new Vector2[pixelPositions.Length];
            var radii = new float[pixelPositions.Length];
            var pressures = new float[pixelPositions.Length];
            var tilts = new Vector2[pixelPositions.Length];

            for (int i = 0; i < pixelPositions.Length; i++)
            {
                positions[i] = imageTransform * (pixelPositions[i] - halfImageSize);
                radii[i] = pixelRadii[i] * radiusScale;
                pressures[i] = 1.0f;
            }

            command.SetTarget(shapeLayer.World.Create())
                .NewStroke()
                .AddToLayerTree(shapeLayer)
                .SetProperty(e => e.Get<StrokeSetting>().Brush, brush)
                .SetSampledPolyline(
                    positions.ToImmutableArray(),
                    radii.ToImmutableArray(),
                    pressures.ToImmutableArray(),
                    tilts.ToImmutableArray());
        }

        ReplaceSourceLayer(command, imageLayer, shapeLayer);
    }

    private static void ConvertVectorFillToShape(Entity vectorFillLayer)
    {
        var arrangement = vectorFillLayer.Get<ArrangementManager>().ArrReady.CurrentValue;
        var layerNode = vectorFillLayer.Get<LayerTreeNode>();
        var markers = layerNode.Children.ToList();
        var shapeLayer = vectorFillLayer.World.Create();
        var command = new CommandBuilder("Convert Vector Fill to Shape", shapeLayer)
            .NewShapeLayer()
            .AddToLayerTree(layerNode.ParentValue, layerNode.Index)
            .SetProperty(
                e => e.Get<CommonLayerSetting>().Name,
                vectorFillLayer.Get<CommonLayerSetting>().Name.Value + " Converted");

        foreach (var marker in markers)
        {
            var markerPosition = marker.Get<SampledPolyline>().Positions.Value[0];
            var face = arrangement.PointQueryFace(markerPosition);
            if (!face.IsValid || arrangement.IsUnboundedFace(face))
                continue;

            var facePolygons = arrangement.GetPolygonFromFace(face);
            if (facePolygons.Count == 0)
                continue;

            IReadOnlyList<Vector2> polygon = facePolygons.Count == 1
                ? facePolygons.Single()
                : facePolygons.ConnectHoles();
            AddFilledPolygon(
                command,
                shapeLayer,
                polygon,
                marker.Get<VectorFillMarkerSetting>().BrushE.Value);
        }

        ReplaceSourceLayer(command, vectorFillLayer, shapeLayer);
    }

    private static void AddFilledPolygon(
        CommandBuilder command,
        Entity shapeLayer,
        IReadOnlyList<Vector2> polygon,
        Entity brush)
    {
        ImmutableArray<Vector2> positions = [.. polygon, polygon[0]];
        ImmutableArray<float> ones = [.. Enumerable.Repeat(1.0f, positions.Length)];
        ImmutableArray<Vector2> zeros = [.. Enumerable.Repeat(Vector2.Zero, positions.Length)];

        command.SetTarget(shapeLayer.World.Create())
            .NewFilledPolygon()
            .AddToLayerTree(shapeLayer)
            .SetSampledPolyline(positions, ones, ones, zeros)
            .SetProperty(e => e.Get<FilledPolygonSetting>().BrushE, brush);
    }

    private static void ReplaceSourceLayer(
        CommandBuilder command,
        Entity sourceLayer,
        Entity shapeLayer)
    {
        command.SetTarget(shapeLayer)
            .SetWorkingLayer()
            .SetTarget(sourceLayer)
            .RemoveFromLayerTree()
            .DeleteLayer()
            .Commit();
    }
}
