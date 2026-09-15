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
using FileAccess = Godot.FileAccess;

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

    private static string SanitizeFileName(string fileName)
    {
        var invalids = Path.GetInvalidFileNameChars();
        foreach (var c in invalids)
            fileName = fileName.Replace(c, '_');
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "Unknown name brush";
        return fileName;
    }

    public static void Save()
    {
        if (AppCommandLineOptions.FactoryStartup) return;
        // Ensure folder exists
        using var baseDir = DirAccess.Open("user://");
        if (!baseDir.DirExists("Brush"))
            baseDir.MakeDir("Brush");

        // Clear
        using var dirAccess = DirAccess.Open(BrushFolder);
        dirAccess.ListDirBegin();
        string fileName;
        while ((fileName = dirAccess.GetNext()) != "")
            if (fileName.EndsWith(".bin"))
                dirAccess.Remove(fileName);
        dirAccess.ListDirEnd();

        HashSet<string> seenNames = new();
        // Save
        List<string> fileNames = [];
        foreach (var brush in BrushSettings)
        {
            var name = SanitizeFileName(brush.Name.Value);
            if (seenNames.Contains(name))
            {
                int suffix = 1;
                while (seenNames.Contains(name + "_" + suffix))
                    suffix++;
                name = name + "_" + suffix;
            }
            var path = BrushFolder + name + ".bin";
            seenNames.Add(name);

            var content = MessagePackSerializer.Serialize(brush);
            using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            file.StoreBuffer(content);
            fileNames.Add(name);
        }
        using var manifest = FileAccess.Open(BrushFolder + "manifest", FileAccess.ModeFlags.Write);
        manifest.StoreBuffer(MessagePackSerializer.Serialize(fileNames));
    }

    public static bool TryLoad()
    {
        if (AppCommandLineOptions.FactoryStartup) return false;
        // Check folder
        using var baseDir = DirAccess.Open("user://");
        if (!baseDir.DirExists("Brush")) return false;

        // Get manifest
        List<string> manifestFileNames = [];
        if (FileAccess.FileExists(BrushFolder + "manifest"))
        {
            using var file = FileAccess.Open(BrushFolder + "manifest", FileAccess.ModeFlags.Read);
            var manifestByte = file.GetBuffer((long)file.GetLength());
            manifestFileNames = MessagePackSerializer.Deserialize<List<string>>(manifestByte);
        }

        // List files
        using var brushDir = DirAccess.Open(BrushFolder);
        var fileNames = new List<string>();
        brushDir.ListDirBegin();
        string fileEntry;
        while ((fileEntry = brushDir.GetNext()) != "")
            if (fileEntry.EndsWith(".bin"))
                fileNames.Add(fileEntry.GetBaseName());
        brushDir.ListDirEnd();

        if (fileNames.Count == 0) return false;

        // Load
        BrushSettings.Clear();
        BrushSettings.AddRange(Enumerable.Repeat<StrokeBrushSetting>(null, manifestFileNames.Count));

        foreach (var name in fileNames)
        {
            StrokeBrushSetting strokeBrush;
            try
            {
                using var file = FileAccess.Open(BrushFolder + name + ".bin", FileAccess.ModeFlags.Read);
                var content = file.GetBuffer((long)file.GetLength());
                strokeBrush = MessagePackSerializer.Deserialize<StrokeBrushSetting>(content);
            }
            catch (Exception)
            {
                continue;
            }
            if (strokeBrush == null)
                continue;
            var index = manifestFileNames.IndexOf(name);
            if (index >= 0)
                BrushSettings[index] = strokeBrush;
            else
                BrushSettings.Add(strokeBrush);
        }
        return true;
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
