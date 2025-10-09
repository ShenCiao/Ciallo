using System;
using System.Linq;
using Massive;
using Ciallo.Data;
using Ciallo.Misc;
using Godot;

namespace Ciallo.NodeControl;

public partial class OpenDocumentDialog : FileDialog
{
    public override void _Ready()
    {
        FileSelected += path => LoadWorldFile(path);
    }

    public static bool LoadWorldFile(string path)
    {
        World dataWorld;
        Entity dataDocument;
        try
        {
            dataWorld = AppWorldManager.Load(path, out dataDocument);
        }
        catch (Exception _)
        {
            var dialog = ((SceneTree)Engine.GetMainLoop()).GetNodesInGroup("Dialog").OfType<AcceptDialog>().Single(n => n.Name == "WarnUser");
            dialog.DialogText = "Cannot open document: the file is corrupted".Tr();
            dialog.Popup();
            return false;
        }
        AppWorldManager.CopyWorldByData(dataDocument);
        dataWorld.Clear();
        if(!AppPreference.RecentFiles.Contains(path)) AppPreference.RecentFiles.Add(path);
        return true;
    }
}
