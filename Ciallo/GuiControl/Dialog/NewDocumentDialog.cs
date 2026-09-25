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
    }

    public void OnCreate()
    {
        var docNameControl = GetNode<LineEdit>("%DocumentNameControl");
        var docName = docNameControl.Text;
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
        AppDocumentManager.InitialEmptyAnimationDocumentForUser(document);
        AppDocumentManager.SaveWorkingDocument();
        Hide();

        AppPreference.RecentFiles.Add(filePath);
    }
}
