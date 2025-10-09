using Ciallo.Data;
using Ciallo.Misc;
using Godot;
using Massive;

namespace Ciallo.NodeControl;

/// <summary>
/// Show layers, toggle LayerTree scenes' visibility according to current working document
/// </summary>
public partial class LayerPanel : VBoxContainer
{
    public override void _Ready()
    {
        GetNode<Node>("%LayerContainerPreview").QueueFree();
    }

    public void CreateAddLayerContainer(Entity document)
    {
        var layerContainer = LayerContainer.Instantiate();
        layerContainer.VisibleIf(AppWorldManager.WorkingDocument, document);
        AddChild(layerContainer);
        document.Set(layerContainer);
    }

    public void RemoveFreeLayerContainer(Entity document)
    {
        var layerContainer = document.Get<LayerContainer>();
        document.Remove<LayerContainer>();
        layerContainer.QueueFree();
    }
}
