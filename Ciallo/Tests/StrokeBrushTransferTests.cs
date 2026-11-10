using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.GuiControl;
using Frent;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace Ciallo.Tests;

[TestSuite, RequireGodotRuntime]
public class StrokeBrushTransferTests
{
    [TestCase]
    public void SwitchingEditedBrushDisconnectsOldBindingsAndUsesCurrentHistory()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var source = new StrokeBrushSetting { Name = { Value = "Original" } };
        var copy = new StrokeBrushSetting { Name = { Value = "Copy" } };
        var oldHistory = new CommandManager();
        var currentHistory = new CommandManager();
        var editor = AutoFree(StrokeBrushEditor.New(Entity.Null));
        editor.InitSetting(source, Entity.Null, oldHistory, library: true);
        tree.Root.AddChild(editor);
        AppStrokeBrushLibrary.BrushSettings.Add(source);
        AppStrokeBrushLibrary.BrushSettings.Add(copy);
        try
        {
            editor.PopupCenteredClamped();
            var name = editor.FindChildren("*", nameof(LineEdit), owned: false)
                .OfType<LineEdit>().Single(edit => edit.Text == "Original");
            editor.Hide();
            source.Name.Value = "Changed while closed";
            AssertThat(name.Text).IsEqual("Original");
            editor.InitSetting(copy, Entity.Null, currentHistory, library: true);
            editor.PopupCenteredClamped();
            source.Name.Value = "Original changed elsewhere";
            AssertThat(name.Text).IsEqual("Copy");
            AssertThat(currentHistory.HasUndo).IsFalse();
            name.Text = "Edited copy";
            name.EmitSignal(LineEdit.SignalName.TextSubmitted, name.Text);
            AssertThat(copy.Name.Value).IsEqual("Edited copy");
            AssertThat(source.Name.Value).IsEqual("Original changed elsewhere");
            AssertThat(oldHistory.HasUndo).IsFalse();
            currentHistory.Undo();
            AssertThat(name.Text).IsEqual("Copy");
            AssertThat(currentHistory.HasUndo).IsFalse();
            AssertThat(currentHistory.HasRedo).IsTrue();
            currentHistory.Redo();
            AssertThat(name.Text).IsEqual("Edited copy");
        }
        finally
        {
            editor.Hide();
            AppStrokeBrushLibrary.BrushSettings.Remove(source);
            AppStrokeBrushLibrary.BrushSettings.Remove(copy);
        }
    }

    [TestCase]
    public void InterleavedLibraryAndDocumentChangesUndoInOneTimelineAndKeepStrokeReferences()
    {
        var previous = AppStrokeBrushLibrary.BrushSettings.ToArray();
        var document = AppDocumentManager.Create(new DocumentSetting());
        document.AddNode(AutoFree(new SubViewportHolder()));
        var history = document.Get<CommandManager>();
        var library = AppStrokeBrushLibrary.BrushSettings;
        library.Clear();
        var original = new StrokeBrushSetting { Name = { Value = "Original" }, AlphaDensity = { Value = 2f } };
        library.Add(original);
        try
        {
            var a = StrokeBrushActions.AddToDocument(document, original, 0);
            var b = StrokeBrushActions.AddToDocument(document, original, 0);
            var brushes = document.Get<BrushManager>().StrokeBrushes;
            var aSetting = a.Get<StrokeBrushSetting>();
            var densityProperty = aSetting.AlphaDensity;
            var stroke = document.World.Create();
            stroke.Add(new StrokeSetting { Brush = { Value = a } });
            stroke.Tag<ToSerializeTag>();

            var exported = StrokeBrushActions.AddToLibrary(aSetting, 0, history);
            exported.AlphaDensity.Value = 5f;
            StrokeBrushActions.ReplaceDocumentBrush(a, exported);
            AssertThat(ReferenceEquals(aSetting.AlphaDensity, densityProperty)).IsTrue();
            AssertThat(stroke.Get<StrokeSetting>().Brush.Value).IsEqual(a);
            StrokeBrushActions.MoveLibraryBrush(0, 1, history);
            StrokeBrushActions.DeleteDocumentBrush(document, a);
            AssertThat(stroke.Get<StrokeSetting>().Brush.Value.IsNull).IsTrue();
            history.Undo();
            AssertThat(brushes.ToArray()).ContainsExactly(b, a);
            AssertThat(stroke.Get<StrokeSetting>().Brush.Value).IsEqual(a);
            history.Undo();
            AssertThat(library.ToArray()).ContainsExactly(exported, original);
            history.Undo();
            AssertThat(aSetting.AlphaDensity.Value).IsEqual(2f);
            AssertThat(exported.AlphaDensity.Value).IsEqual(5f);
            history.Undo();
            AssertThat(library.ToArray()).ContainsExactly(original);
            for (int i = 0; i < 4; i++) history.Redo();
            AssertThat(stroke.Get<StrokeSetting>().Brush.Value.IsNull).IsTrue();
            AssertThat(aSetting.AlphaDensity.Value).IsEqual(5f);
            AssertThat(library.ToArray()).ContainsExactly(original, exported);
        }
        finally
        {
            library.Clear();
            library.AddRange(previous);
            AppDocumentManager.Remove(document);
        }
    }

    [TestCase]
    public void ReplacementKeepsTextureSnapshotAfterSourceMutationAndRedo()
    {
        var image = Image.CreateEmpty(2, 2, false, Image.Format.Rgba8);
        image.Fill(Colors.White);
        var source = new StrokeBrushSetting
        {
            StampTexture = { Value = ImageTexture.CreateFromImage(image) },
        };
        var target = new StrokeBrushSetting();
        var history = new CommandManager();
        var command = StrokeBrushActions.Replace(new CommandBuilder("Replace"), target, source);
        history.Commit(command.ActionName, command.Commands);
        image.Fill(Colors.Black);
        source.StampTexture.Value.Update(image);
        AssertThat(target.StampTexture.Value.GetImage().GetPixel(0, 0)).IsEqual(Colors.White);
        history.Undo();
        AssertThat(target.StampTexture.Value).IsNull();
        history.Redo();
        AssertThat(target.StampTexture.Value.GetImage().GetPixel(0, 0)).IsEqual(Colors.White);
    }
}
