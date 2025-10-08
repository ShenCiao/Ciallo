using System.Collections.Generic;
using Arch.Core;
using Arch.Core.Extensions;
using Ciallo.Data;
using Ciallo.Rendering;
using Godot;

namespace Ciallo.Command;

public class NewStrokeCmd : CommandBase
{
    private Entity _layerE;
    public Entity StrokeE = Entity.Null;
    private readonly List<Node> _refNodes = [];

    /// <summary>
    /// Create a new stroke command for a given layer.
    /// This will create a new stroke entity.
    /// </summary>
    public NewStrokeCmd(Entity layerE)
    {
        _layerE = layerE;
    }

    /// <summary>
    /// Create a new stroke command with an existing stroke entity and its parent layer.
    /// This is useful for deserialization where the entity already has data components.
    /// </summary>
    public NewStrokeCmd(Entity layerE, Entity strokeE)
    {
        _layerE = layerE;
        StrokeE = strokeE;
    }

    public override IEnumerable<Entity> DoRefEntities => ToEnumerable(StrokeE);
    public override IEnumerable<GodotObject> DoRefObjects => _refNodes;

    public override void Do()
    {
        // Creation
        InitEntity();

        // Data
        if (!StrokeE.Has<ToSerializeTag>())
        {
            StrokeE.Add(new ToSerializeTag());
            _layerE.Get<LayerTreeNode>().AddChild(StrokeE);
        }
        
        // View
        if (!StrokeE.Has<StrokeView>())
        {
            if (_refNodes.Count == 0) _refNodes.Add(new StrokeView()
            {
                Material = BrushMaterial.MissingBrushMaterial,
            });
            var strokeView = (StrokeView)_refNodes[0];
            var layerView = _layerE.Get<PolylineLayerView>();
            layerView.AddChild(strokeView);
            StrokeE.Add(strokeView);
            strokeView.SetOwner(layerView.Owner);
        }

        // Overlay
        if (!StrokeE.Has<StrokeOverlay>())
        {
            if(_refNodes.Count == 1) _refNodes.Add(new StrokeOverlay());
            var strokeOverlay = (StrokeOverlay)_refNodes[1];
            var layerOverlay = _layerE.Get<PolylineLayerOverlay>();
            layerOverlay.AddChild(strokeOverlay);
            StrokeE.Add(strokeOverlay);
        }
    }

    public override void Undo()
    {
        var layerE = _layerE;
        // Overlay
        StrokeE.Remove<StrokeOverlay>();
        var layerOverlay = layerE.Get<PolylineLayerOverlay>();
        layerOverlay.RemoveChild(_refNodes[1]);
        
        // View
        StrokeE.Remove<StrokeView>();
        var layerView = layerE.Get<PolylineLayerView>();
        layerView.RemoveChild(_refNodes[0]);
        
        // Data
        _layerE.Get<LayerTreeNode>().RemoveChild(^1);
        StrokeE.Remove<ToSerializeTag>();
    }

    public Entity InitEntity()
    {
        if (StrokeE != Entity.Null) return StrokeE;
        StrokeE = WorkingWorld.Create();
        var node = new LayerTreeNode();
        StrokeE.Add(new StrokeGeometry(), node);
        StrokeE.Add<StrokeBrush>(Entity.Null);
        return StrokeE;
    }
}