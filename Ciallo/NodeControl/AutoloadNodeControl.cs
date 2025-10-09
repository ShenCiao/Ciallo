using System.Linq;
using Massive;
using Ciallo.Data;
using Ciallo.Rendering;
using Godot;
using ObservableCollections;
using R3;

namespace Ciallo.NodeControl;

public partial class AutoloadNodeControl : Node
{
    public override void _Ready()
    {
        // // Create layer tree control
        // var layerPanel = SceneTree.GetNodesInGroup("UncategorizedControl").OfType<LayerPanel>().Single();
        // layerPanel.CreateAddLayerContainer(document);
        //
        // // Create paint panel
        // var paintPanelContainer = SceneTree.GetNodesInGroup("UncategorizedControl").OfType<PaintPanelContainer>().Single();
        // var paintPanel = paintPanelContainer.CreateAddPaintPanel(document);
        //
        // // Add world view
        // var worldView = paintPanel.GetNode<WorldView>("%WorldView");
        // document.Add(worldView);
        //
        // // Add world overlay
        // var worldOverlay = paintPanel.GetNode<WorldOverlay>("%WorldOverlay");
        // document.Add(worldOverlay);
        
        AppWorldManager.LoadedWorlds.ObserveAdd().Subscribe(et =>
        {
            var document = et.Value.Document();
            
            // Layer tree control
            var layerPanel = GetTree().GetNodesInGroup("UncategorizedControl").OfType<LayerPanel>().Single();
            layerPanel.CreateAddLayerContainer(document);
            
            // Paint panel
            var paintPanelContainer = GetTree().GetNodesInGroup("UncategorizedControl").OfType<PaintPanelContainer>().Single();
            var paintPanel = paintPanelContainer.CreateAddPaintPanel(document);
            
            // World view
            var worldView = paintPanel.GetNode<WorldView>("%WorldView");
            document.Set(worldView);
            
            // World overlay
            var worldOverlay = paintPanel.GetNode<WorldOverlay>("%WorldOverlay");
            document.Set(worldOverlay);
        }).AddTo(this);
        
        AppWorldManager.LoadedWorlds.ObserveRemove().Subscribe(et =>
        {
            var document = et.Value.Document();
            
            // View and overlay are contained in paint panel
            
            // Paint panel
            var paintPanelContainer = GetTree().GetNodesInGroup("UncategorizedControl").OfType<PaintPanelContainer>().Single();
            paintPanelContainer.RemoveFreePaintPanel(document);
            
            // Layer tree control
            var layerPanel = GetTree().GetNodesInGroup("UncategorizedControl").OfType<LayerPanel>().Single();
            layerPanel.RemoveFreeLayerContainer(document);
        }).AddTo(this);
    }
}