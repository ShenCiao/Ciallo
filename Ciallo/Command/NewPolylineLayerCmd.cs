using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using Ciallo.Data;
using Ciallo.Misc;
using Ciallo.Rendering;
using Godot;

namespace Ciallo.Command;

public class NewPolylineLayerCmd : CommandBase
{
    public Entity LayerE = Entity.Null;
    private readonly List<Node> _refObjects = [];
    private readonly PolylineLayerSetting _setting;

    /// <summary>
    /// Create a new polyline layer command with an optional setting.
    /// If setting is provided, it will be cloned and used for the new layer.
    /// </summary>
    public NewPolylineLayerCmd(PolylineLayerSetting setting = null)
    {
        _setting = setting?.Clone() ?? new PolylineLayerSetting();
    }

    /// <summary>
    /// Create a new polyline layer command with an existing entity.
    /// This is useful for deserialization where the entity already has data components.
    /// </summary>
    public NewPolylineLayerCmd(Entity layerE)
    {
        LayerE = layerE;
        _setting = layerE.Has<PolylineLayerSetting>() ? layerE.Get<PolylineLayerSetting>() : new PolylineLayerSetting();
    }

    public override IEnumerable<Entity> DoRefEntities => ToEnumerable(LayerE);
    public override IEnumerable<GodotObject> DoRefObjects => _refObjects;

    public override void Do()
    {
        InitEntity();

        // Data
        var tree = Document.Get<LayerTreeManager>();
        if (!LayerE.Has<ToSerializeTag>())
        {
            LayerE.Add(new ToSerializeTag());
            tree.Root.AddChild(LayerE);
        }
        
        // Layer panel
        var layerContainer = Document.Get<LayerContainer>();
        layerContainer.CreateAdd(LayerE);
        
        // View
        if (!LayerE.Has<PolylineLayerView>())
        {
            var worldView = Document.Get<WorldView>();
            if (_refObjects.Count == 0) _refObjects.Add(new PolylineLayerView());
            var layerView = (PolylineLayerView)_refObjects[0];
            worldView.AddChild(layerView);
            LayerE.Add(layerView);
            layerView.SetOwner(worldView);
        }
        
        // Overlay
        if (!LayerE.Has<PolylineLayerOverlay>())
        {
            var worldOverlay = Document.Get<WorldOverlay>();
            if(_refObjects.Count == 1) _refObjects.Add(new PolylineLayerOverlay());
            var layerOverlay = (PolylineLayerOverlay)_refObjects[1];
            worldOverlay.AddChild(layerOverlay);
            LayerE.Add(layerOverlay);
        }
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
        
        if (LayerE == Entity.Null)
        {
            LayerE = WorkingWorld.Create();
            var node = new LayerTreeNode()
            {
                Name = { Value = $"{"Line layer".Tr()} {tree.Root.ChildCount+1}" },
            };
            LayerE.Add(_setting, node);
        }

        return LayerE;
    }
}