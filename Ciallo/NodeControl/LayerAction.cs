using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Godot;
using Massive;

public partial class LayerAction : Control
{
    public void OnNewLayer()
    {
        if (AppWorldManager.WorkingWorld.Value == null) return;
        var cmd = new NewPolylineLayerCmd();
        var e = cmd.InitEntity();
        cmd.Combine(new ChangeWorkingLayerCmd(e)).Commit();
    }

    public void OnRemoveLayer()
    {
        if (AppWorldManager.WorkingWorld.Value == null) return;
        var document = AppWorldManager.WorkingDocument.CurrentValue;
        var workingLayerE = document.Get<SelectionManager>().WorkingLayer.Value;
        if (workingLayerE.IsNull()) return;
        var workingLayerPath = document.Get<SelectionManager>().WorkingLayerPath;
        var nextLayerPath = document.Get<LayerTreeManager>().GetNextFocusPathAfterDeletion(workingLayerPath);
        
        ChangeWorkingLayerCmd cmd = nextLayerPath.IsEmpty ? new(new Entity()) : new(nextLayerPath.Single());
        cmd.Combine(new DeletePolylineLayerCmd(workingLayerE)).Commit();
    }
}