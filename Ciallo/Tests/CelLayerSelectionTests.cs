using System.Linq;
using Ciallo.Data;
using Ciallo.GuiControl;
using Frent;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Ciallo.Tests;

[TestSuite]
public class CelLayerSelectionTests
{
    [TestCase]
    public void PlannedSelectionUsesResultNamesWithoutReadingUninitializedEntities()
    {
        using var world = new World();
        var created = world.Create();
        var retained = Layer(world, "Old name");
        var collision = Layer(world, "A stroke");
        var selected = CelLayerSelection.Resolve(
            [(created, "B"), (collision, "A stroke"), (retained, "A stroke")], ["A stroke", "B"]);

        AssertThat(selected.ToArray()).ContainsExactly(collision, retained, created);
    }

    [TestCase]
    public void MissingPrimaryAndDuplicateNamesPreserveTheOrderedTemplate()
    {
        using var world = new World();
        var folder = Layer(world, "Animation", folder: true);
        folder.Get<FolderLayerSetting>().IsCelFolder = true;
        var setting = folder.Get<FolderLayerSetting>();
        setting.PreferredNamesForCelSelection.Value = ["A", "B", "C"];
        var partialCel = Layer(world, "Partial", folder: true);
        var c = Layer(world, "C", parent: partialCel);
        var b1 = Layer(world, "B", parent: partialCel);
        var b2 = Layer(world, "B", parent: partialCel);
        var fullCel = Layer(world, "Full", folder: true);
        var fullC = Layer(world, "C", parent: fullCel);
        var fullA = Layer(world, "A", parent: fullCel);
        var fullB = Layer(world, "B", parent: fullCel);

        AssertThat(SelectionManager.ResolveLayersForCelSelection(folder, partialCel).ToArray())
            .ContainsExactly(b1, b2, c);
        AssertThat(SelectionManager.ResolveLayersForCelSelection(folder, fullCel).ToArray())
            .ContainsExactly(fullA, fullB, fullC);
        AssertThat(setting.PreferredNamesForCelSelection.Value.ToArray()).ContainsExactly("A", "B", "C");
    }

    [TestCase]
    public void BlankAndWhollyMissingCelsKeepTheNavigationContext()
    {
        using var world = new World();
        var folder = Layer(world, "Animation", folder: true);
        folder.Get<FolderLayerSetting>().IsCelFolder = true;
        folder.Get<FolderLayerSetting>().PreferredNamesForCelSelection.Value = ["A"];
        var cel = Layer(world, "Missing", folder: true);
        Layer(world, "Other", parent: cel);

        foreach (var target in new[] { Entity.Null, folder, cel })
            AssertThat(SelectionManager.ResolveLayersForCelSelection(folder, target).ToArray()).ContainsExactly(folder);
        AssertThat(folder.Get<FolderLayerSetting>().PreferredNamesForCelSelection.Value.ToArray()).ContainsExactly("A");
    }

    [TestCase]
    public void BatchMergeSkipsSingletonsButRejectsAnIncompatibleCel()
    {
        using var world = new World();
        var cel1 = Layer(world, "1", folder: true);
        var cel2 = Layer(world, "2", folder: true);
        var a = Layer(world, "A", parent: cel1);
        var b = Layer(world, "B", parent: cel1);
        var loneB = Layer(world, "B", parent: cel2);
        var incompatible = Layer(world, "A", parent: cel2, folder: true);

        AssertThat(ArchetypeContextActions.CanMerge(new(Entity.Null, ["A", "B"], [[a, b], [loneB]]))).IsTrue();
        AssertThat(ArchetypeContextActions.CanMerge(new(Entity.Null, ["A", "B"], [[a, b], [incompatible, loneB]]))).IsFalse();
    }

    private static Entity Layer(World world, string name, Entity parent = default, bool folder = false)
    {
        var layer = world.Create();
        layer.Add(new LayerTreeNode());
        layer.Add(new CommonLayerSetting { Name = { Value = name } });
        if (folder) layer.Add(new FolderLayerSetting());
        else layer.Add(new ShapeLayerSetting());
        if (!parent.IsNull) parent.Get<LayerTreeNode>().AddChild(layer);
        return layer;
    }
}
