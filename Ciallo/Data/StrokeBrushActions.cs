using System.Linq;
using Ciallo.Command;
using Frent;
using Godot;
using ObservableCollections;

namespace Ciallo.Data;

internal static class StrokeBrushActions
{
    public static Entity AddToDocument(Entity document, StrokeBrushSetting source, int index)
    {
        var brushes = document.Get<BrushManager>().StrokeBrushes;
        var brush = document.World.Create();
        new CommandBuilder("Add Document Brush", brush)
            .NewStrokeBrush(source)
            .SetProperty(e => e.Get<StrokeBrushSetting>().Name,
                UniqueName(source.Name.Value, brushes.Select(e => e.Get<StrokeBrushSetting>())))
            .SetObservableCollection(brushes, list => list.Move(list.Count - 1, index))
            .SetProperty(document.Get<SelectionManager>().WorkingStrokeBrush, brush)
            .Commit();
        return brush;
    }

    public static StrokeBrushSetting AddToLibrary(StrokeBrushSetting source, int index, CommandManager history)
    {
        var copy = source.Clone();
        copy.Name.Value = UniqueName(copy.Name.Value, AppStrokeBrushLibrary.BrushSettings);
        Commit(history, new CommandBuilder("Add Library Brush")
            .SetObservableCollection(AppStrokeBrushLibrary.BrushSettings, list => list.Insert(index, copy)));
        return copy;
    }

    private static string UniqueName(string name, System.Collections.Generic.IEnumerable<StrokeBrushSetting> brushes)
    {
        var names = brushes.Select(s => s.Name.Value).ToHashSet();
        if (!names.Contains(name)) return name;
        for (int n = 2; ; n++)
        {
            string candidate = $"{name} ({n})";
            if (!names.Contains(candidate)) return candidate;
        }
    }

    public static void DeleteDocumentBrush(Entity document, Entity brush)
    {
        var brushes = document.Get<BrushManager>().StrokeBrushes;
        int index = brushes.IndexOf(brush);
        var next = brushes.Count == 1 ? Entity.Null : brushes[index == brushes.Count - 1 ? index - 1 : index + 1];
        var command = new CommandBuilder("Delete Stroke Brush", brush);
        var working = document.Get<SelectionManager>().WorkingStrokeBrush;
        if (working.Value == brush) command.SetProperty(working, next);
        command.DeleteBrush().Commit();
    }

    public static void MoveDocumentBrush(Entity document, int from, int to)
    {
        if (from == to) return;
        new CommandBuilder("Reorder Document Brushes", document)
            .SetObservableCollection(document.Get<BrushManager>().StrokeBrushes, list => list.Move(from, to)).Commit();
    }

    public static void MoveLibraryBrush(int from, int to, CommandManager history)
    {
        if (from == to) return;
        Commit(history, new CommandBuilder("Reorder Library Brushes")
            .SetObservableCollection(AppStrokeBrushLibrary.BrushSettings, list => list.Move(from, to)));
    }

    public static void DeleteLibraryBrush(StrokeBrushSetting brush, CommandManager history) => Commit(history, new CommandBuilder("Delete Library Brush")
        .SetObservableCollection(AppStrokeBrushLibrary.BrushSettings, list => list.Remove(brush)));

    public static void RestoreBuiltIns(CommandManager history)
    {
        var originals = AppStrokeBrushLibrary.CreateBuiltInBrushes();
        Commit(history, new CommandBuilder("Restore Built-in Brushes")
            .SetObservableCollection(AppStrokeBrushLibrary.BrushSettings, list =>
            {
                for (int i = list.Count - 1; i >= 0; i--)
                    if (list[i].Labels.Contains(BrushLabel.BuiltIn)) list.RemoveAt(i);
                for (int i = 0; i < originals.Count; i++) list.Insert(i, originals[i]);
            }));
    }

    public static void ReplaceDocumentBrush(Entity target, StrokeBrushSetting source) =>
        Replace(new CommandBuilder("Replace Document Brush", target), target.Get<StrokeBrushSetting>(), source).Commit();

    public static void ReplaceLibraryBrush(StrokeBrushSetting target, StrokeBrushSetting source, CommandManager history) =>
        Commit(history, Replace(new CommandBuilder("Replace Library Brush"), target, source));

    // Preserve both the entity and its reactive properties: strokes, materials and open views keep observing them.
    internal static CommandBuilder Replace(CommandBuilder cmd, StrokeBrushSetting target, StrokeBrushSetting source)
    {
        var copy = source.Clone();
        return cmd.SetProperty(target.Name, copy.Name.Value)
            .SetProperty(target.Color, copy.Color.Value)
            .SetProperty(target.ActiveBrushFlags, copy.ActiveBrushFlags.Value)
            .SetProperty(target.BlendMode, copy.BlendMode.Value)
            .SetProperty(target.IndependentRadius, copy.IndependentRadius.Value)
            .SetProperty(target.Pressure2RadiusCurve, copy.Pressure2RadiusCurve.Value)
            .SetProperty(target.RenderingType, copy.RenderingType.Value)
            .SetProperty(target.Pressure2FlowCurve, copy.Pressure2FlowCurve.Value)
            .SetProperty(target.DashLength, copy.DashLength.Value)
            .SetProperty(target.GapLength, copy.GapLength.Value)
            .SetProperty(target.DashForwardSpeed, copy.DashForwardSpeed.Value)
            .SetProperty(target.ActiveStampFlags, copy.ActiveStampFlags.Value)
            .SetProperty(target.StampInterval, copy.StampInterval.Value)
            .SetProperty(target.StampTexture, copy.StampTexture.Value)
            .SetProperty(target.DiskOpacityCurve, copy.DiskOpacityCurve.Value)
            .SetProperty(target.StampRotation, copy.StampRotation.Value)
            .SetProperty(target.MaskTexture, copy.MaskTexture.Value)
            .SetProperty(target.RotationNoiseAmplitude, copy.RotationNoiseAmplitude.Value)
            .SetProperty(target.FalloffCurve, copy.FalloffCurve.Value)
            .SetProperty(target.AlphaDensity, copy.AlphaDensity.Value)
            .SetObservableCollection(target.Labels, labels =>
            {
                for (int i = labels.Count - 1; i >= 0; i--) labels.RemoveAt(i);
                foreach (var label in copy.Labels) labels.Add(label);
            });
    }

    private static void Commit(CommandManager history, CommandBuilder command) =>
        history.Commit(command.ActionName, command.Commands);
}
