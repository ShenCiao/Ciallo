using System.Collections.Generic;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Godot;

namespace Ciallo.GuiControl;

public partial class MenuFile : PopupMenu
{
    public static readonly OrderedDictionary<string, Hotkey> MenuItems = new()
    {
        { "New document", AppHotkeys.Global.FileNewDocument },
        { "Open document", AppHotkeys.Global.FileOpenDocument },
        { "Close document", null },
        { "-1", null },
        { "Save", AppHotkeys.Global.FileSave },
        { "Save As...", AppHotkeys.Global.FileSaveAs },
        { "-2", null },
        { "Export as image", null },
        { "Export frame sequence", null },
    };

    public override void _Ready()
    {
        foreach (var (i, item) in MenuItems.Index())
        {
            if (item.Key.StartsWith('-'))
            {
                AddSeparator();
                continue;
            }
            AddItem(item.Key);
            if (item.Value != null) SetItemShortcut(i, item.Value.Shortcut);
        }

        IndexPressed += id => OnIndexPressed((int)id);
    }

    public void OnIndexPressed(int id)
    {
        switch (id)
        {
            case 0: // New Document
                AppGuiCommand.PopupNewDocumentDialog();
                break;
            case 1: // Open Document
                AppGuiCommand.PopupOpenDocumentDialog();
                break;
            case 2: // Close Document
                if (AppDocumentManager.WorkingDocument.Value.IsNull) break;
                _ = AppDocumentManager.UserCloseWorkingDocument();
                break;
            case 4: // Save
                if (AppDocumentManager.WorkingDocument.Value.IsNull) break;
                AppDocumentManager.SaveWorkingDocument();
                break;
            case 5: // Save as
                if (AppDocumentManager.WorkingDocument.Value.IsNull) break;
                var setting = AppDocumentManager.WorkingDocument.CurrentValue.Get<DocumentSetting>();
                AppDialogHost.SaveAsDialog.CurrentDir = setting.FilePath.CurrentValue.GetBaseDir();
                AppDialogHost.SaveAsDialog.Popup();
                break;

            case 7: // Export as image
                if (AppDocumentManager.WorkingDocument.Value.IsNull) break;
                AppDialogHost.ExportImage.Init();
                AppDialogHost.ExportImage.Popup();
                break;

            case 8: // Export frame sequence
                if (AppDocumentManager.WorkingDocument.Value.IsNull) break;
                AppDialogHost.ExportFrameSequence.Popup(AppDocumentManager.WorkingDocument.CurrentValue);
                break;

            default:
                GD.PrintErr($"Unhandled menu item index: {id}");
                break;
        }
    }
}
