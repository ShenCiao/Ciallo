using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using Ciallo.Command;
using Frent;
using Godot;

namespace Ciallo.Data;

public static class ResearchAnimationImporter
{
    private const float FitScale = 0.95f;
    private const float MinimumRadius = 0.01f;

    public static void Import(Entity document, string selectedPath)
    {
        var directoryPath = ResolveDirectoryPath(selectedPath);
        var frameFiles = GetFrameFiles(directoryPath);
        var frames = frameFiles.Select(ReadFrame).ToList();
        var allSamples = frames.SelectMany(frame => frame.Strokes).SelectMany(stroke => stroke.Samples).ToList();
        if (allSamples.Count == 0)
            throw new InvalidOperationException("Selected animation contains no stroke samples.");

        var selectionManager = document.Get<SelectionManager>();
        var brushE = selectionManager.WorkingStrokeBrush.Value;
        if (brushE.IsNull)
            throw new InvalidOperationException("No working stroke brush is selected.");

        var transform = CalculateFitTransform(allSamples, document.Get<DocumentSetting>().ReferenceSize.Value);
        var maxPressure = allSamples.Max(sample => sample.Pressure);
        var radiusSampler = ResolveRadiusSampler(brushE);

        var celFolder = document.World.Create();
        var firstFrame = frames[0];
        var firstCel = document.World.Create();
        var firstShapeLayer = document.World.Create();
        var celsByFrame = new Dictionary<int, Entity>
        {
            [firstFrame.Frame] = firstCel
        };

        var cmd = new CommandBuilder("Import research animation", celFolder)
            .NewCelFolder()
            .SetProperty(e => e.Get<CommonLayerSetting>().Name, Path.GetFileName(directoryPath))
            .AddToLayerTree(document);

        AddFrame(cmd, firstCel, firstShapeLayer, celFolder, firstFrame, brushE, transform, maxPressure, radiusSampler);

        foreach (var frame in frames.Skip(1))
        {
            var cel = document.World.Create();
            var shapeLayer = document.World.Create();
            celsByFrame.Add(frame.Frame, cel);
            AddFrame(cmd, cel, shapeLayer, celFolder, frame, brushE, transform, maxPressure, radiusSampler);
        }

        cmd.SetTarget(celFolder)
            .SetObservableCollection(
                e => e.Get<FolderLayerSetting>().Exposures,
                exposures =>
                {
                    foreach (var frame in frames)
                    {
                        exposures.Add(frame.Frame, celsByFrame[frame.Frame]);
                    }
                })
            .SetTarget(firstShapeLayer)
            .SetLayerSelection()
            .SetTarget(document)
            .SetProperty(e => e.Get<SelectionManager>().CurrentFrame, firstFrame.Frame)
            .Commit();
    }

    private static string ResolveDirectoryPath(string selectedPath)
    {
        if (Directory.Exists(selectedPath))
            return selectedPath;

        if (File.Exists(selectedPath) && string.Equals(Path.GetExtension(selectedPath), ".csv", StringComparison.OrdinalIgnoreCase))
            return Path.GetDirectoryName(selectedPath);

        throw new InvalidOperationException("Select a folder containing numeric frame CSV files.");
    }

    private static List<FrameFile> GetFrameFiles(string directoryPath)
    {
        var frameFiles = Directory.EnumerateFiles(directoryPath, "*.csv")
            .Select(filePath => new
            {
                FilePath = filePath,
                Frame = int.TryParse(Path.GetFileNameWithoutExtension(filePath), NumberStyles.Integer, CultureInfo.InvariantCulture, out var frame)
                    ? frame - 1
                    : (int?)null
            })
            .Where(file => file.Frame.HasValue)
            .OrderBy(file => file.Frame.Value)
            .Select(file => new FrameFile(file.Frame.Value, file.FilePath))
            .ToList();

        if (frameFiles.Count == 0)
            throw new InvalidOperationException("Selected folder contains no numeric frame CSV files.");

        return frameFiles;
    }

    private static AnimationFrame ReadFrame(FrameFile file)
    {
        var strokes = new List<ResearchStroke>();
        var samples = new List<ResearchSample>();

        foreach (var line in File.ReadLines(file.Path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                FlushStroke();
                continue;
            }

            var values = line.Split(',', StringSplitOptions.TrimEntries);
            if (values.Length < 3)
                throw new InvalidOperationException($"Invalid CSV row in {file.Path}: {line}");

            samples.Add(new(
                float.Parse(values[0], CultureInfo.InvariantCulture),
                float.Parse(values[1], CultureInfo.InvariantCulture),
                float.Parse(values[2], CultureInfo.InvariantCulture)));
        }

        FlushStroke();
        return new(file.Frame, strokes);

        void FlushStroke()
        {
            if (samples.Count == 0) return;
            strokes.Add(new ResearchStroke([.. samples]));
            samples.Clear();
        }
    }

    private static FitTransform CalculateFitTransform(IReadOnlyList<ResearchSample> samples, Vector2 referenceSize)
    {
        var min = new Vector2(samples.Min(sample => sample.X), samples.Min(sample => sample.Y));
        var max = new Vector2(samples.Max(sample => sample.X), samples.Max(sample => sample.Y));
        var size = max - min;
        var scaleX = size.X > 0 ? referenceSize.X / size.X : float.PositiveInfinity;
        var scaleY = size.Y > 0 ? referenceSize.Y / size.Y : float.PositiveInfinity;
        var scale = Mathf.Min(scaleX, scaleY);
        if (float.IsInfinity(scale))
            scale = 1.0f;

        return new((min + max) * 0.5f, scale * FitScale);
    }

    private static Func<float, float> ResolveRadiusSampler(Entity brushE)
    {
        if (brushE.TryGet<StrokeBrushSetting>() is { } setting)
            return setting.ToRadiusSampler();

        return _ => 1.0f;
    }

    private static void AddFrame(
        CommandBuilder cmd,
        Entity cel,
        Entity shapeLayer,
        Entity celFolder,
        AnimationFrame frame,
        Entity brushE,
        FitTransform transform,
        float maxPressure,
        Func<float, float> radiusSampler)
    {
        cmd.SetTarget(cel)
            .NewFolderLayer()
            .SetProperty(e => e.Get<CommonLayerSetting>().Name, $"Frame {frame.Frame}")
            .AddToLayerTree(celFolder)
            .SetTarget(shapeLayer)
            .NewShapeLayer()
            .SetProperty(e => e.Get<CommonLayerSetting>().Name, "Shape layer")
            .AddToLayerTree(cel);

        foreach (var stroke in frame.Strokes)
        {
            AddStroke(cmd, shapeLayer, stroke, brushE, transform, maxPressure, radiusSampler);
        }
    }

    private static void AddStroke(
        CommandBuilder cmd,
        Entity cel,
        ResearchStroke stroke,
        Entity brushE,
        FitTransform transform,
        float maxPressure,
        Func<float, float> radiusSampler)
    {
        var positions = stroke.Samples.Select(sample => transform.Apply(sample.Position)).ToImmutableArray();
        var pressures = stroke.Samples.Select(sample => NormalizePressure(sample.Pressure, maxPressure)).ToImmutableArray();
        var radii = pressures.Select(pressure => Mathf.Max(MinimumRadius, radiusSampler(Mathf.Pow(pressure, 1.5f)))).ToImmutableArray();
        var tilts = Enumerable.Repeat(Vector2.Zero, stroke.Samples.Length).ToImmutableArray();

        cmd.SetTarget(cel.World.Create())
            .NewStroke()
            .AddToLayerTree(cel)
            .SetProperty(e => e.Get<StrokeSetting>().Brush, brushE)
            .SetSampledPolyline(positions, radii, pressures, tilts);
    }

    private static float NormalizePressure(float pressure, float maxPressure)
    {
        if (maxPressure <= 0)
            return 1.0f;

        return Mathf.Clamp(pressure / maxPressure, 0.0f, 1.0f);
    }

    private readonly record struct FrameFile(int Frame, string Path);
    private readonly record struct AnimationFrame(int Frame, List<ResearchStroke> Strokes);
    private readonly record struct ResearchStroke(ImmutableArray<ResearchSample> Samples);

    private readonly record struct ResearchSample(float X, float Y, float Pressure)
    {
        public Vector2 Position => new(X, Y);
    }

    private readonly record struct FitTransform(Vector2 Pivot, float Scale)
    {
        public Vector2 Apply(Vector2 point) => (point - Pivot) * Scale;
    }
}
