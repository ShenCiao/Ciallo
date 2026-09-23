using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Ciallo.Geometry;
using Ciallo.GuiControl;
using Ciallo.Rendering;
using Godot;
using MessagePack;
using ObservableCollections;
using R3;

namespace Ciallo.Data;

public static partial class AppStrokeBrushLibrary
{
    public static ReactiveProperty<int> SelectedIndex;
    public static readonly ObservableList<StrokeBrushSetting> BrushSettings = [];
    public static ReadOnlyReactiveProperty<StrokeBrushSetting> SelectedBrushSetting;

    public static bool HasSelection => SelectedBrushSetting?.CurrentValue != null;

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

    public static void BindToGui(BrushPanel panel)
    {
        // Setup brush library panel
        SelectedIndex = panel.SelectedIndex;
        panel.BindBrushSetting(BrushSettings, s => s);

        // Note about `BrushSettings.ObserveChanged().ToReadOnlyReactiveProperty()`
        // ToReadOnlyReactiveProperty() is necessary to trigger the initial value of observable.
        // Or CombineLatest lacks of the first value to get to work. Or use `Prepend` function.
        SelectedBrushSetting = SelectedIndex
            .CombineLatest(BrushSettings.ObserveChanged().ToReadOnlyReactiveProperty(), (idx, _) => idx)
            .Select(idx => idx < 0 || idx >= BrushSettings.Count ? null : BrushSettings[idx])
            .ToReadOnlyReactiveProperty();

        // Create stroke preview
        var preview = new StrokeView();
        panel.BrushPreviewViewport.AddChild(preview);
        // Note: Lazy on clearing these caches on destruction. I don't believe user will view 1e5 brushes in one session.
        Dictionary<StrokeBrushSetting, StrokeBrushMaterial> materialCache = new();
        SerialDisposable curveChangeSub = new();
        curveChangeSub.AddTo(panel);
        SelectedBrushSetting.Subscribe(setting =>
        {
            if (setting == null)
            {
                preview.Material = null;
                curveChangeSub.Disposable = null;
                return;
            }
            materialCache.TryGetValue(setting, out var material);
            if (material == null)
            {
                material = new();
                material.ObserveBrushSetting(setting);
                materialCache[setting] = material;
            }
            preview.Material = material;

            // ponytail: resubscribe per selection so the initial value re-fires for this brush; preview geometry is shared
            curveChangeSub.Disposable = setting.BaseRadius
                .CombineLatest(setting.Pressure2RadiusCurve, setting.ActiveBrushFlags, ValueTuple.Create)
                .Subscribe(t => UpdateStrokePreview(preview, t.Item2, t.Item1, t.Item3.HasFlag(BrushFlags.Pressure2Radius)));
        }).AddTo(panel);

        // Brush list operations and buttons
        int count = 1;
        panel.Add.Pressed += () =>
        {
            var newBrush = new StrokeBrushSetting()
            {
                Name = { Value = "New brush".Tr() + " " + count++ },
            };
            BrushSettings.Add(newBrush);
            SelectedIndex.Value = BrushSettings.Count - 1;
        };

        panel.Remove.Pressed += () =>
        {
            if (SelectedIndex.Value < 0)
                return;
            var idx = SelectedIndex.Value;
            BrushSettings.RemoveAt(idx);
            if (BrushSettings.Count == 0)
                SelectedIndex.Value = -1;
            else if (idx >= BrushSettings.Count)
                SelectedIndex.Value = BrushSettings.Count - 1;
            else
                SelectedIndex.OnNext(idx);
        };

        panel.Copy.Pressed += () =>
        {
            int idx = SelectedIndex.Value;
            if (idx < 0) return;
            var newBrush = BrushSettings[idx].Clone();
            newBrush.Name.Value += " " + count++;
            BrushSettings.Add(newBrush);
            SelectedIndex.Value = BrushSettings.Count - 1;
        };

        panel.Reset.Pressed += () =>
        {
            int prev = SelectedIndex.Value;
            ResetBuiltInBrushes();
            if (BrushSettings.Count == 0)
                SelectedIndex.Value = -1;
            else if (prev < 0)
                SelectedIndex.Value = 0;
            else if (prev >= BrushSettings.Count)
                SelectedIndex.Value = BrushSettings.Count - 1;
            else
                SelectedIndex.OnNext(prev);
        };

        panel.Up.Pressed += () =>
        {
            int idx = SelectedIndex.Value;
            if (idx <= 0) return;
            BrushSettings.Move(idx, idx - 1);
            SelectedIndex.Value = idx - 1;
        };

        panel.Down.Pressed += () =>
        {
            int idx = SelectedIndex.Value;
            if (idx < 0 || idx >= BrushSettings.Count - 1) return;
            BrushSettings.Move(idx, idx + 1);
            SelectedIndex.Value = idx + 1;
        };

        panel.Top.Pressed += () =>
        {
            int idx = SelectedIndex.Value;
            if (idx <= 0) return;
            BrushSettings.Move(idx, 0);
            SelectedIndex.Value = 0;
        };

        panel.Bottom.Pressed += () =>
        {
            int idx = SelectedIndex.Value;
            if (idx < 0 || idx >= BrushSettings.Count - 1) return;
            BrushSettings.Move(idx, BrushSettings.Count - 1);
            SelectedIndex.Value = BrushSettings.Count - 1;
        };
    }

    private static void UpdateStrokePreview(StrokeView view, ImmutableArray<BezierPoint> pressureCurve, float baseRadius = 0, bool pressure2Radius = true)
    {
        int n = 64;
        float gr = (1 + Mathf.Sqrt(5)) / 2; // golden ratio
        var xs = Enumerable.Range(0, n)
            .Select(i => i / (n - 1f))
            .Select(i => (i * 2 - 1f) * Mathf.Pi)
            .ToImmutableArray(); // [-pi, pi]

        var positions = xs.Select(x => new Vector2(x, Mathf.Sin(x) / gr)).ToImmutableArray();
        // prefix sum on length
        var lengths = new float[positions.Length];
        for (int i = 0; i < positions.Length - 1; i++)
        {
            var p0 = positions[i];
            var p1 = positions[i + 1];
            var l = (p1 - p0).Length();
            lengths[i + 1] = lengths[i] + l;
        }
        var midL = lengths[^1] / 2;
        var pressures = lengths
            .Select(l => (l - midL) / midL * float.Pi * 0.5f)
            .Select(Mathf.Cos)
            .ToImmutableArray();
        float targetRadius = baseRadius.SigmoidRemap(2.0f, 16f, 0.25f / gr, 0.75f / gr);
        var radii = pressures
            .Select(p => pressure2Radius ? pressureCurve.SampleX(p) : 1.0f)
            .Select(radiusRatio => radiusRatio * targetRadius)
            .ToImmutableArray();
        view.SetGeometry(positions, radii, pressures);
    }
}
