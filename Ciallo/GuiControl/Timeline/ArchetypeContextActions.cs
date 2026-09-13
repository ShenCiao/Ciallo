using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Frent;

namespace Ciallo.GuiControl;

internal sealed record ArchetypeContext(
    Entity Folder,
    ImmutableArray<string> Names,
    ImmutableArray<ImmutableArray<Entity>> Units)
{
    public ImmutableArray<Entity> Layers => [.. Units.SelectMany(unit => unit)];
}

internal static class ArchetypeContextActions
{
    public static ImmutableArray<string> ContextNames(Entity folder, string name)
    {
        var selection = folder.Document.Get<SelectionManager>();
        var names = folder.Get<FolderLayerSetting>().PreferredNamesForCelSelection.Value;
        return selection.WorkingCelFolder.CurrentValue == folder && names.Contains(name)
            ? [.. names.Where(folder.Get<FolderLayerSetting>().CelChildrenByName.ContainsKey)] : [name];
    }

    public static ArchetypeContext Capture(Entity folder, string name)
    {
        var names = ContextNames(folder, name);
        return new(folder, names, [.. folder.Get<LayerTreeNode>().GetLayerChildren()
            .Select(cel => CelLayerSelection.Resolve(cel, names)).Where(unit => !unit.IsEmpty)]);
    }

    public static void SelectOnly(Entity folder, string name) => Select(folder, [name]);

    public static void Toggle(Entity folder, string name)
    {
        var selection = folder.Document.Get<SelectionManager>();
        var setting = folder.Get<FolderLayerSetting>();
        if (selection.WorkingCelFolder.CurrentValue != folder)
        {
            SelectOnly(folder, name);
            return;
        }

        var names = setting.PreferredNamesForCelSelection.Value;
        if (!names.IsEmpty && names[0] == name) return;
        Select(folder, names.Contains(name) ? names.Remove(name) : names.Add(name));
    }

    private static void Select(Entity folder, ImmutableArray<string> names)
    {
        var layers = CelLayerSelection.Resolve(ExposedCel(folder), names);
        var command = new CommandBuilder("Select Cel Child Archetypes", folder)
            .SetProperty(folder.Get<FolderLayerSetting>().PreferredNamesForCelSelection, names);
        command.SelectLayers(layers: ExposedSelection(folder, layers)).CommitToLatest();
    }

    public static void Rename(Entity folder, string name, string newName)
    {
        var setting = folder.Get<FolderLayerSetting>();
        var command = new CommandBuilder("Rename Cel Child Archetype", folder);
        foreach (var layer in setting.CelChildrenByName[name])
            command.SetTarget(layer).SetProperty(layer.Get<CommonLayerSetting>().Name, newName);
        command.SetTarget(folder).SetProperty(setting.PreferredNamesForCelSelection,
            [.. setting.PreferredNamesForCelSelection.Value.Select(n => n == name ? newName : n).Distinct()]);
        command.Commit();
    }

    public static void SetVisible(Entity folder, string name, bool visible) =>
        LayerSelectionActions.SetProperty("Cel Child Archetype Visibility", Capture(folder, name).Layers,
            setting => setting.IsVisible, visible);

    public static bool CanSplit(ArchetypeContext context) =>
        !context.Units.IsEmpty && context.Layers.All(layer => layer.Has<ShapeLayerSetting>());

    public static bool CanMerge(ArchetypeContext context) =>
        context.Units.Any(unit => unit.Length >= 2)
        && context.Layers.All(layer => layer.Has<ShapeLayerSetting>() || layer.Has<VectorFillLayerSetting>())
        && context.Units.Where(unit => unit.Length >= 2).All(LayerConversionActions.CanMerge);

    public static bool CanGroup(ArchetypeContext context) =>
        !context.Units.IsEmpty && context.Units.All(LayerContextActions.CanGroupLayers);

    public static bool AreFolders(ArchetypeContext context) =>
        !context.Units.IsEmpty && context.Layers.All(layer => layer.Has<FolderLayerSetting>());

    public static void NewLayer(ArchetypeContext context, bool folder)
    {
        string baseName = (folder ? "Folder" : "Shape layer").Tr();
        var existing = context.Folder.Get<FolderLayerSetting>().CelChildrenByName;
        string name = baseName;
        for (int suffix = 2; existing.ContainsKey(name); suffix++) name = $"{baseName} {suffix}";
        var command = new CommandBuilder("New Cel Child Archetype", context.Folder);
        ImmutableArray<Entity> selected = [];
        foreach (var unit in context.Units)
        {
            var anchor = unit[0].Get<LayerTreeNode>();
            var layer = context.Folder.World.Create();
            command.SetTarget(layer);
            if (folder) command.NewFolderLayer();
            else command.NewShapeLayer();
            command.SetProperty(layer => layer.Get<CommonLayerSetting>().Name, name)
                .AddToLayerTree(anchor.ParentValue, anchor.Index + 1);
            if (anchor.ParentValue == ExposedCel(context.Folder)) selected = [layer];
        }
        Complete(command, context, [name], selected);
    }

    public static void Delete(ArchetypeContext context)
    {
        var setting = context.Folder.Get<FolderLayerSetting>();
        bool active = context.Folder.Document.Get<SelectionManager>().WorkingCelFolder.CurrentValue == context.Folder;
        var command = new CommandBuilder("Delete Cel Child Archetypes", context.Folder);
        LayerContextActions.DeleteLayers(command, context.Layers);
        ImmutableArray<string> names = [.. setting.PreferredNamesForCelSelection.Value.Except(context.Names)];
        command.SetTarget(context.Folder).SetProperty(setting.PreferredNamesForCelSelection, names);
        if (active)
            command.SelectLayers(layers: ExposedSelection(context.Folder,
                CelLayerSelection.Resolve(ExposedCel(context.Folder), names)));
        command.Commit();
    }

    public static void Split(ArchetypeContext context)
    {
        var command = new CommandBuilder("Split Cel Child Archetypes", context.Folder);
        var results = new Dictionary<Entity, ImmutableArray<(Entity Layer, string Name)>>();
        foreach (var layer in LayerContextActions.OperationRoots(context.Layers).Reverse())
        {
            var (stroke, fill) = LayerContextActions.SplitStrokeAndFill(command, layer);
            var name = layer.Get<CommonLayerSetting>().Name.Value;
            results.Add(layer, [(fill, name + " fill"), (stroke, name + " stroke")]);
        }
        ImmutableArray<string> names = [.. context.Names.Select(name => name + " stroke")];
        // Include existing siblings with a result name, preserving the fill-below-stroke order.
        var selected = CelLayerSelection.Resolve([.. ExposedChildren(context.Folder).SelectMany(layer =>
            results.TryGetValue(layer, out var result) ? result : [(layer, layer.Get<CommonLayerSetting>().Name.Value)])], names);
        Complete(command, context, names, selected);
    }

    public static void Merge(ArchetypeContext context)
    {
        var command = new CommandBuilder("Merge Cel Child Archetypes", context.Folder);
        ImmutableArray<Entity> selected = [];
        foreach (var unit in context.Units)
        {
            var result = unit.Length >= 2 ? LayerConversionActions.Merge(command, unit) : unit[0];
            if (unit[0].Get<LayerTreeNode>().ParentValue == ExposedCel(context.Folder)) selected = [result];
        }
        Complete(command, context, ResultNames(context), selected);
    }

    public static void Group(ArchetypeContext context)
    {
        var command = new CommandBuilder("Group Cel Child Archetypes", context.Folder);
        ImmutableArray<Entity> selected = [];
        foreach (var unit in context.Units)
        {
            var result = LayerContextActions.GroupLayers(command, unit);
            if (unit[0].Get<LayerTreeNode>().ParentValue == ExposedCel(context.Folder)) selected = [result];
        }
        Complete(command, context, ResultNames(context), selected);
    }

    private static ImmutableArray<string> ResultNames(ArchetypeContext context)
    {
        var results = context.Units.Select(unit => unit[0].Get<CommonLayerSetting>().Name.Value).ToHashSet();
        return [.. context.Names.Where(results.Contains)];
    }

    public static void Ungroup(ArchetypeContext context)
    {
        var names = context.Names.SelectMany(name => context.Layers
            .Where(layer => layer.Get<CommonLayerSetting>().Name.Value == name)
            .SelectMany(layer => layer.Get<LayerTreeNode>().GetLayerChildren().AsEnumerable().Reverse())
            .Select(layer => layer.Get<CommonLayerSetting>().Name.Value)).Distinct().ToImmutableArray();
        var command = new CommandBuilder("Ungroup Cel Child Archetypes", context.Folder).SelectLayers();
        var promoted = new Dictionary<Entity, ImmutableArray<Entity>>();
        foreach (var layer in LayerContextActions.OperationRoots(context.Layers).Reverse())
            promoted.Add(layer, LayerContextActions.UngroupFolder(command, layer));
        // Promoted children share the cel with untouched siblings, which can have the same names.
        var candidates = ExposedChildren(context.Folder).SelectMany(layer => promoted.GetValueOrDefault(layer, [layer]));
        var selected = CelLayerSelection.Resolve(
            [.. candidates.Select(layer => (layer, layer.Get<CommonLayerSetting>().Name.Value))], names);
        Complete(command, context, names, selected);
    }

    public static void WrapChildren(ArchetypeContext context)
    {
        var command = new CommandBuilder("Wrap Cel Child Archetype Children", context.Folder);
        foreach (var layer in LayerContextActions.OperationRoots(context.Layers))
            LayerContextActions.WrapChildrenInFolders(command, layer);
        Complete(command, context, context.Names, CelLayerSelection.Resolve(ExposedCel(context.Folder), context.Names));
    }

    private static void Complete(CommandBuilder command, ArchetypeContext context,
        ImmutableArray<string> names, ImmutableArray<Entity> selected)
    {
        command.SetTarget(context.Folder)
            .SetProperty(context.Folder.Get<FolderLayerSetting>().PreferredNamesForCelSelection, names)
            .SelectLayers(layers: ExposedSelection(context.Folder, selected))
            .Commit();
    }

    private static Entity ExposedCel(Entity folder) => folder.Get<FolderLayerSetting>().CurrentExposedCel.CurrentValue;

    private static IReadOnlyList<Entity> ExposedChildren(Entity folder)
    {
        var cel = ExposedCel(folder);
        return cel.IsNull || cel.IsCelFolder || !cel.Has<FolderLayerSetting>()
            ? [] : cel.Get<LayerTreeNode>().GetLayerChildren();
    }

    private static ImmutableArray<Entity> ExposedSelection(Entity folder, ImmutableArray<Entity> selected)
    {
        var cel = ExposedCel(folder);
        if (!cel.IsNull && !cel.IsCelFolder && !cel.Has<FolderLayerSetting>()) return [cel];
        return selected.IsEmpty ? [folder] : selected;
    }
}
