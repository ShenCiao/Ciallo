using Ciallo.Command;
using Ciallo.Data;
using Ciallo.GuiControl;
using Frent;
using GdUnit4;
using R3;
using static GdUnit4.Assertions;

namespace Ciallo.Tests;

[TestSuite, RequireGodotRuntime]
public class LayerHistoryTests
{
    private Entity _document;
    private CommandManager _history;
    private int _historyLimit;

    [BeforeTest]
    public void SetUp()
    {
        _historyLimit = AppPreference.CommandHistoryLimit.Value;
        AppPreference.CommandHistoryLimit.Value = 50;
        _document = AppDocumentManager.Create(new DocumentSetting());
        _history = _document.Get<CommandManager>();
    }

    [AfterTest]
    public void TearDown()
    {
        AppDocumentManager.Remove(_document);
        AppPreference.CommandHistoryLimit.Value = _historyLimit;
    }

    [TestCase]
    public void OverlappingLayerAndArchetypeVisibilityEditsRetainEveryUndoAndRedoEndpoint()
    {
        var folder = Layer("Animation", folder: true);
        folder.Get<FolderLayerSetting>().IsCelFolder = true;
        var firstCel = Layer("1", folder, folder: true);
        var secondCel = Layer("2", folder, folder: true);
        var a = Layer("Ink", firstCel);
        var b = Layer("Ink", secondCel);
        var other = Layer("Other");
        Setting(b).IsVisible.Value = false;

        LayerSelectionActions.SetVisible([other], false);
        ArchetypeContextActions.SetVisible(folder, "Ink", false);
        LayerSelectionActions.SetVisible([a], true);
        // Unchanged submissions and empty batches must not split the open action.
        LayerSelectionActions.Rename(a, "Ink");
        ArchetypeContextActions.Rename(folder, "Ink", "Ink");
        LayerSelectionActions.SetVisible([], false);
        ArchetypeContextActions.SetVisible(folder, "Ink", true);
        AssertThat(_history.PersistenceEpoch).IsEqual(4L);
        AssertThat(_history.DocumentModified.CurrentValue).IsTrue();
        CheckVisibility(a, b, other, true, true, false);

        _history.Undo();
        CheckVisibility(a, b, other, true, false, true);
        AssertThat(_history.HasUndo).IsFalse();
        AssertThat(_history.DocumentModified.CurrentValue).IsFalse();
        _history.Redo();
        CheckVisibility(a, b, other, true, true, false);
        AssertThat(_history.HasRedo).IsFalse();
    }

    [TestCase]
    public void VisibilityGroupsStaySeparateFromTimelineGroupsAndRename()
    {
        var layer = Layer("Ink");
        using var frame = new ReactiveProperty<int>(0);
        using var playbackEnd = new ReactiveProperty<int>(10);
        new CommandBuilder(_document).SetProperty(frame, 1)
            .CommitOpenSequence(HistorySequenceKind.TimelineInteraction);
        new CommandBuilder(_document).SetProperty(playbackEnd, 20)
            .CommitOpenSequence(HistorySequenceKind.TimelineInteraction);
        LayerSelectionActions.SetVisible([layer], false);
        new CommandBuilder(_document).SetProperty(frame, 2)
            .CommitOpenSequence(HistorySequenceKind.TimelineInteraction);
        LayerSelectionActions.SetVisible([layer], true);
        LayerSelectionActions.Rename(layer, "Outline");
        LayerSelectionActions.SetVisible([layer], false);

        _history.Undo();
        AssertThat(Setting(layer).IsVisible.Value).IsTrue();
        AssertThat(Setting(layer).Name.Value).IsEqual("Outline");
        _history.Undo();
        AssertThat(Setting(layer).Name.Value).IsEqual("Ink");
        AssertThat(Setting(layer).IsVisible.Value).IsTrue();
        _history.Undo();
        AssertThat(Setting(layer).IsVisible.Value).IsFalse();
        AssertThat(frame.Value).IsEqual(2);
        _history.Undo();
        AssertThat(frame.Value).IsEqual(1);
        AssertThat(Setting(layer).IsVisible.Value).IsFalse();
        _history.Undo();
        AssertThat(Setting(layer).IsVisible.Value).IsTrue();
        AssertThat(frame.Value).IsEqual(1);
        AssertThat(playbackEnd.Value).IsEqual(20);
        _history.Undo();
        AssertThat(frame.Value).IsEqual(0);
        AssertThat(playbackEnd.Value).IsEqual(10);
        AssertThat(_history.HasUndo).IsFalse();

        while (_history.HasRedo) _history.Redo();
        AssertThat(Setting(layer).Name.Value).IsEqual("Outline");
        AssertThat(Setting(layer).IsVisible.Value).IsFalse();
        AssertThat(frame.Value).IsEqual(2);
        AssertThat(playbackEnd.Value).IsEqual(20);
    }

    [TestCase]
    public void HistoryNavigationClosesVisibilityGroupsBeforeAppendingOrBranching()
    {
        var a = Layer("A");
        var b = Layer("B");
        LayerSelectionActions.SetVisible([a], false);
        _history.Undo();
        _history.Redo();
        LayerSelectionActions.SetVisible([b], false);

        _history.Undo();
        AssertThat(Setting(a).IsVisible.Value).IsFalse();
        AssertThat(Setting(b).IsVisible.Value).IsTrue();
        _history.Undo();
        AssertThat(Setting(a).IsVisible.Value).IsTrue();
        AssertThat(_history.HasUndo).IsFalse();

        LayerSelectionActions.SetVisible([b], false);
        AssertThat(_history.HasRedo).IsFalse();
        _history.Undo();
        AssertThat(Setting(a).IsVisible.Value).IsTrue();
        AssertThat(Setting(b).IsVisible.Value).IsTrue();
        AssertThat(_history.HasUndo).IsFalse();
    }

    private Entity Layer(string name, Entity parent = default, bool folder = false)
    {
        var layer = _document.World.Create();
        layer.Add(new LayerTreeNode());
        layer.Add(new CommonLayerSetting { Name = { Value = name } });
        if (folder) layer.Add(new FolderLayerSetting());
        if (!parent.IsNull) parent.Get<LayerTreeNode>().AddChild(layer);
        return layer;
    }

    private static CommonLayerSetting Setting(Entity layer) => layer.Get<CommonLayerSetting>();

    private static void CheckVisibility(Entity a, Entity b, Entity other, bool aVisible, bool bVisible, bool otherVisible)
    {
        AssertThat(Setting(a).IsVisible.Value).IsEqual(aVisible);
        AssertThat(Setting(b).IsVisible.Value).IsEqual(bVisible);
        AssertThat(Setting(other).IsVisible.Value).IsEqual(otherVisible);
    }
}
