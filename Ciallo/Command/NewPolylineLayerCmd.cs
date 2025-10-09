using System.Collections.Generic;
using Ciallo.Data;
using Ciallo.Misc;
using Ciallo.Rendering;
using Godot;
using Massive;

namespace Ciallo.Command;

public class NewPolylineLayerCmd : CommandBase
{
    public Entity LayerE;
    private readonly List<Node> _refObjects = [];
    private readonly PolylineLayerSetting _setting;

    public NewPolylineLayerCmd(PolylineLayerSetting setting = null)
    {
        _setting = setting?.Clone() ?? new PolylineLayerSetting();
    }

    public override IEnumerable<Entity> DoRefEntities => ToEnumerable(LayerE);
    public override IEnumerable<GodotObject> DoRefObjects => _refObjects;

    public override void Do()
    {
        InitEntity();

        // Data
        var tree = Document.Get<LayerTreeManager>();
        LayerE.Add<ToSerializeTag>();
        tree.Root.AddChild(LayerE);
        
        // Layer panel
        var layerContainer = Document.Get<LayerContainer>();
        layerContainer.CreateAdd(LayerE);
        
        // View
        var worldView = Document.Get<WorldView>();
        if (_refObjects.Count == 0) _refObjects.Add(new PolylineLayerView());
        var layerView = (PolylineLayerView)_refObjects[0];
        worldView.AddChild(layerView);
        LayerE.Set(layerView);
        layerView.SetOwner(worldView);
        
        // Overlay
        var worldOverlay = Document.Get<WorldOverlay>();
        if(_refObjects.Count == 1) _refObjects.Add(new PolylineLayerOverlay());
        var layerOverlay = (PolylineLayerOverlay)_refObjects[1];
        worldOverlay.AddChild(layerOverlay);
        LayerE.Set(layerOverlay);
    }

    public override void Undo()
    {
        // Overlay
        var overlay = Document.Get<WorldOverlay>();
        LayerE.Remove<PolylineLayerOverlay>();
        overlay.RemoveChild(_refObjects[1]);
        
        // View
        LayerE.Remove<PolylineLayerView>();
        var worldView = Document.Get<WorldView>();
        worldView.RemoveChild(_refObjects[0]);
        
        // Layer panel
        var layerTreeControl = Document.Get<LayerContainer>();
        layerTreeControl.RemoveFree(LayerE);
        
        // Data
        var tree = Document.Get<LayerTreeManager>();
        tree.Root.RemoveChild(^1);
        LayerE.Remove<ToSerializeTag>();
    }
    
    public Entity InitEntity()
    {
        var tree = Document.Get<LayerTreeManager>();
        
        if (LayerE.IsNull())
        {
            LayerE = WorkingWorld.CreateEntity();
            var node = new LayerTreeNode()
            {
                Name = { Value = $"{"Line layer".Tr()} {tree.Root.ChildCount+1}" },
            };
            LayerE.Set(_setting);
            LayerE.Set(node);
        }

        return LayerE;
    }
}