using System;
using System.IO;
using System.Linq;
using Ciallo.Data;
using GdUnit4;
using MessagePack;
using Newtonsoft.Json;
using static GdUnit4.Assertions;

namespace Ciallo.Tests;

[TestSuite, RequireGodotRuntime]
public class UserDataRecoveryTests
{
    [TestCase]
    public void InvalidBrushFilesAndMissingManifestEntriesCannotCreateNullSlots()
    {
        string folder = CreateFolder();
        try
        {
            WriteBrush(folder, "first", new StrokeBrushSetting { Name = { Value = "First" } });
            WriteBrush(folder, "second", new StrokeBrushSetting { Name = { Value = "Second" } });
            File.WriteAllBytes(Path.Combine(folder, "broken.bin"), [0xc1]);
            WriteBrush(folder, "null", null);
            WriteBrush(folder, "curve", new StrokeBrushSetting { Pressure2RadiusCurve = { Value = [] } });
            File.WriteAllBytes(Path.Combine(folder, "manifest"), MessagePackSerializer.Serialize(
                new[] { "second", "missing", "broken", "null", "curve", "first", "second", "../outside" }));

            var loaded = AppStrokeBrushLibrary.LoadBrushes(folder);

            AssertThat(loaded.Select(brush => brush.Name.Value).ToArray()).ContainsExactly("Second", "First");
            AssertThat(File.Exists(Path.Combine(folder, "broken.bin"))).IsFalse();
            AssertThat(File.Exists(Path.Combine(folder, "null.bin"))).IsFalse();
            AssertThat(File.Exists(Path.Combine(folder, "curve.bin"))).IsFalse();
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [TestCase]
    public void FailedManifestReplacementKeepsPreviousSnapshotAndIgnoresUncommittedBrushes()
    {
        if (!OperatingSystem.IsWindows()) return; // Windows denies replacement of an open file without delete sharing.
        string folder = CreateFolder();
        try
        {
            AppStrokeBrushLibrary.SaveBrushes(folder, [new StrokeBrushSetting { Name = { Value = "Original" } }]);
            bool failed = false;
            using (File.Open(Path.Combine(folder, "manifest"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                try
                {
                    AppStrokeBrushLibrary.SaveBrushes(folder, [new StrokeBrushSetting { Name = { Value = "Replacement" } }]);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    failed = true;
                }
            }
            AssertThat(failed).IsTrue();
            AssertThat(AppStrokeBrushLibrary.LoadBrushes(folder).Select(brush => brush.Name.Value).ToArray())
                .ContainsExactly("Original");
            AssertThat(Directory.GetFiles(folder, "*.tmp").Length).IsEqual(0);
            AppStrokeBrushLibrary.SaveBrushes(folder, [new StrokeBrushSetting { Name = { Value = "Recovered" } }]);
            AssertThat(Directory.GetFiles(folder, "*.bin").Length).IsEqual(1);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [TestCase]
    public void InvalidPreferencesRollBackRootCollectionsAndSharedToolProperties()
    {
        var preference = new Preference();
        preference.RecentFiles.Add("original.ciallo");
        string before = JsonConvert.SerializeObject(preference, Preference.JsonOptions);
        var toolProperty = preference.Tools.PaintStroke.SnapDistance;
        foreach (string invalidCurve in new[] { "{\"Points\":[]}", "[]", "null" })
        {
            bool failed = false;
            try
            {
                preference.PopulateFromJson("{\"UIScale\":1.75,\"RecentFiles\":[\"partial.ciallo\"]," +
                    "\"Tools\":{\"PaintStroke\":{\"SnapDistance\":99}},\"PenPressureRemapCurve\":" + invalidCurve + "}");
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException)
            {
                failed = true;
            }
            AssertThat(failed).IsTrue();
            AssertThat(JsonConvert.SerializeObject(preference, Preference.JsonOptions)).IsEqual(before);
            AssertThat(ReferenceEquals(toolProperty, preference.Tools.PaintStroke.SnapDistance)).IsTrue();
        }
    }

    private static string CreateFolder()
    {
        string folder = Path.Combine(Path.GetTempPath(), "ciallo-user-data-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static void WriteBrush(string folder, string name, StrokeBrushSetting brush) =>
        File.WriteAllBytes(Path.Combine(folder, name + ".bin"), MessagePackSerializer.Serialize(brush));
}
