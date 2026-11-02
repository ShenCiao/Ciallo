using System.IO;
using Ciallo.Data;
using Ciallo.Widget;
using Godot;

namespace Ciallo.GuiControl;

public partial class NewDocumentDialog : ConfirmationDialog
{
    public override void _Ready()
    {
        Confirmed += OnCreate;
        var presetControl = GetNode<OptionButton>("%PresetControl");
        presetControl.ItemSelected += _ =>
            GetNode<Vector2Edit>("%ReferenceSizeControl").Value = presetControl.GetSelectedId() == 1
                ? new Vector2(3000, 3000)
                : new Vector2(1920, 1080);
        AboutToPopup += () =>
        {
            GetNode<Label>("%ErrorMessage").Hide();
            var nameControl = GetNode<LineEdit>("%DocumentNameControl");
            nameControl.GrabFocus();
            nameControl.SelectAll();
        };
    }

    public void OnCreate()
    {
        var docNameControl = GetNode<LineEdit>("%DocumentNameControl");
        var docName = docNameControl.Text.Trim();
        var saveFolderControl = GetNode<FilePathPicker>("%SaveFolderControl");
        var saveFolder = saveFolderControl.Path;
        var referenceSizeControl = GetNode<Vector2Edit>("%ReferenceSizeControl");
        var referenceSize = referenceSizeControl.Value;
        var bgControl = GetNode<ColorPickerButton>("%BackgroundColorControl");
        var bgColor = bgControl.Color;
        var errorMessage = GetNode<Label>("%ErrorMessage");
        errorMessage.Visible = false;

        // Sanity checks
        if (string.IsNullOrEmpty(docName))
        {
            errorMessage.Text = "Document name cannot be empty.";
            errorMessage.Visible = true;
            return;
        }
        if (!Directory.Exists(saveFolder))
        {
            errorMessage.Text = "Save folder does not exist.";
            errorMessage.Visible = true;
            return;
        }

        if (!docName.IsValidFileName())
        {
            errorMessage.Text = "[Invalid Document Name]";
            errorMessage.Visible = true;
            return;
        }

        // Compute file path
        string filePath = saveFolder.PathJoin(docName) + ".ciallo";
        int index = 1;
        while (File.Exists(filePath))
        {
            filePath = saveFolder.PathJoin(docName) + index++ + ".ciallo";
        }

        // Create the document
        var setting = new DocumentSetting
        {
            Name = { Value = docName },
            ReferenceSize = { Value = referenceSize },
            BackgroundColor = { Value = bgColor },
            FilePath = { Value = filePath },
        };
        var document = AppDocumentManager.Create(setting);
        AppDocumentManager.WorkingDocument.Value = document;
        AppDocumentManager.InitializeEmptyDocumentForUser(document);
        var dock = GetTree().CurrentScene.GetNode<MainDockableContainer>(MainDockableContainer.ScenePath);
        dock.SetAuxiliaryPanelVisible(MainDockableContainer.TimelinePanelName,
            GetNode<OptionButton>("%PresetControl").GetSelectedId() == 0);
        bool saved = AppDocumentManager.SaveWorkingDocument();
        Hide();

        if (saved)
            AppPreference.RecentFiles.Add(filePath);
    }
}
