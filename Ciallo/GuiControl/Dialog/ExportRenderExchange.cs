using System;
using Ciallo.Data;
using Ciallo.Rendering;
using Godot;

namespace Ciallo.GuiControl;

public partial class ExportRenderExchange : FileDialog
{
    public ExportRenderExchange()
    {
        Title = "Export current frame for web";
        FileMode = FileModeEnum.SaveFile;
        Access = AccessEnum.Filesystem;
        Filters = ["*.glb2dim ; Ciallo web render draft"];
        Size = new Vector2I(900, 600);
        FileSelected += Export;
    }

    public void Open()
    {
        var setting = AppDocumentManager.WorkingDocument.CurrentValue.Get<DocumentSetting>();
        if (!string.IsNullOrEmpty(setting.FilePath.Value)) CurrentDir = setting.FilePath.Value.GetBaseDir();
        CurrentFile = setting.Name.Value + ".glb2dim";
        PopupCentered();
    }

    private void Export(string path)
    {
        try
        {
            RenderExchangeExporter.Save(AppDocumentManager.WorkingDocument.CurrentValue, path);
        }
        catch (Exception error)
        {
            GD.PrintErr(error);
            AppDialogHost.WarnUser.DialogText = "Cannot export current frame.".Tr() + " " + error.Message;
            AppDialogHost.WarnUser.Popup();
        }
    }
}
