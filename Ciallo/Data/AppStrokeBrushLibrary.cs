using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using MessagePack;
using ObservableCollections;

namespace Ciallo.Data;

public static partial class AppStrokeBrushLibrary
{
    public static readonly ObservableList<StrokeBrushSetting> BrushSettings = [];

    public static void ResetBuiltInBrushes()
    {
        var userBrushes = BrushSettings.ToList();
        userBrushes.RemoveAll(b => b.Labels.Contains(BrushLabel.BuiltIn));
        var builtInBrushes = CreateBuiltInBrushes();
        BrushSettings.Clear();
        BrushSettings.AddRange(builtInBrushes);
        BrushSettings.AddRange(userBrushes);
    }

    public static readonly string BrushFolder = "user://Brush/";

    public static void Save()
    {
        if (AppCommandLineOptions.FactoryStartup) return;
        SaveBrushes(ProjectSettings.GlobalizePath(BrushFolder), BrushSettings.ToArray());
    }

    internal static void SaveBrushes(string folder, IReadOnlyList<StrokeBrushSetting> brushes)
    {
        // New filenames keep the previous manifest and its files usable until commit.
        var snapshot = brushes.Select(brush => (
            Name: Guid.NewGuid().ToString("N"), Content: MessagePackSerializer.Serialize(brush))).ToArray();
        foreach (var entry in snapshot)
            UserDataFiles.WriteAtomic(Path.Combine(folder, entry.Name + ".bin"), entry.Content);

        var names = snapshot.Select(entry => entry.Name).ToArray();
        UserDataFiles.WriteAtomic(Path.Combine(folder, "manifest"), MessagePackSerializer.Serialize(names));
        var retained = names.ToHashSet(StringComparer.Ordinal);
        foreach (var path in Directory.EnumerateFiles(folder, "*.bin"))
            if (!retained.Contains(Path.GetFileNameWithoutExtension(path)))
                UserDataFiles.TryDelete(path);
    }

    internal static List<StrokeBrushSetting> LoadBrushes(string folder)
    {
        if (!Directory.Exists(folder)) return [];
        var files = Directory.EnumerateFiles(folder, "*.bin")
            .ToDictionary(Path.GetFileNameWithoutExtension, StringComparer.Ordinal);
        string manifestPath = Path.Combine(folder, "manifest");
        string[] names = files.Keys.Order(StringComparer.Ordinal).ToArray();
        if (File.Exists(manifestPath))
        {
            try
            {
                names = MessagePackSerializer.Deserialize<string[]>(File.ReadAllBytes(manifestPath));
                if (names == null || names.Any(string.IsNullOrWhiteSpace))
                    throw new InvalidDataException("Invalid brush manifest.");
            }
            catch (Exception exception) when (exception is MessagePackSerializationException or InvalidDataException)
            {
                GD.PrintErr($"Discarding invalid brush manifest '{manifestPath}': {exception.Message}");
                UserDataFiles.TryDelete(manifestPath);
                names = files.Keys.Order(StringComparer.Ordinal).ToArray();
            }
        }

        List<StrokeBrushSetting> loaded = [];
        foreach (var name in names.Distinct(StringComparer.Ordinal))
        {
            // Resolve only enumerated files: manifest entries cannot escape the brush folder.
            if (!files.TryGetValue(name, out var path))
            {
                GD.PrintErr($"Skipping missing brush '{name}' in '{folder}'.");
                continue;
            }
            try
            {
                var brush = MessagePackSerializer.Deserialize<StrokeBrushSetting>(File.ReadAllBytes(path));
                ValidateLoadedBrush(brush);
                loaded.Add(brush);
            }
            catch (Exception exception)
            {
                GD.PrintErr($"Cannot load brush '{path}': {exception.Message}");
                if (exception is MessagePackSerializationException or InvalidDataException)
                    UserDataFiles.TryDelete(path);
            }
        }
        return loaded;
    }

    private static void ValidateLoadedBrush(StrokeBrushSetting brush)
    {
        if (brush?.Name?.Value == null || brush.Labels == null)
            throw new InvalidDataException("Brush is missing its name or labels.");
        foreach (var curve in new[] { brush.Pressure2RadiusCurve, brush.Pressure2FlowCurve, brush.DiskOpacityCurve, brush.FalloffCurve })
            if (curve == null || curve.Value.IsDefaultOrEmpty || curve.Value.Length < 2 ||
                curve.Value.Any(point => !point.P.IsFinite() || !point.In.IsFinite() || !point.Out.IsFinite()))
                throw new InvalidDataException("Brush contains an invalid mapping curve.");
    }

    public static bool TryLoad()
    {
        if (AppCommandLineOptions.FactoryStartup) return false;
        try
        {
            var loaded = LoadBrushes(ProjectSettings.GlobalizePath(BrushFolder));
            BrushSettings.Clear();
            BrushSettings.AddRange(loaded);
            return loaded.Count > 0;
        }
        catch (Exception exception)
        {
            GD.PrintErr($"Cannot load brush library: {exception.Message}");
            return false;
        }
    }
}
