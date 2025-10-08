using System.Collections.Generic;
using Ciallo.Data;
using Ciallo.Rendering;
using Godot;
using Massive;

namespace Ciallo.Command;

public class NewStrokeCmd : CommandBase
{
    private Entity _layerE;
    public Entity StrokeE = Entity.Null;
    private readonly List<Node> _refNodes = [];
    
    public NewStrokeCmd(Entity layerE)
    {
        _layerE = layerE;
    }

    public override IEnumerable<Entity> DoRefEntities => ToEnumerable(StrokeE);
    public override IEnumerable<GodotObject> DoRefObjects => _refNodes;

    public override void Do()
    {
        // Creation
        InitEntity();

        // Data
        StrokeE.Add(new ToSerializeTag());
        _layerE.Get<LayerTreeNode>().AddChild(StrokeE);
        StrokeE.Add<StrokeBrush>(Entity.Null);
        
        // View
        if (_refNodes.Count == 0) _refNodes.Add(new StrokeView()
        {
            Material = BrushMaterial.MissingBrushMaterial,
        });
        var strokeView =  (StrokeView)_refNodes[0];
        var layerView = _layerE.Get<PolylineLayerView>();
        layerView.AddChild(strokeView);
        StrokeE.Add(strokeView);
        strokeView.SetOwner(layerView.Owner);

        // Overlay
        if(_refNodes.Count == 1) _refNodes.Add(new StrokeOverlay());
        var strokeOverlay = (StrokeOverlay)_refNodes[1];
        var layerOverlay = _layerE.Get<PolylineLayerOverlay>();
        layerOverlay.AddChild(strokeOverlay);
        StrokeE.Add(strokeOverlay);
    }

    public override void Undo()
    {
        // Overlay
        StrokeE.Remove<StrokeOverlay>();
        var layerOverlay = _layerE.Get<PolylineLayerOverlay>();
        layerOverlay.RemoveChild(_refNodes[1]);
        
        // View
        StrokeE.Remove<StrokeView>();
        var layerView = _layerE.Get<PolylineLayerView>();
        layerView.RemoveChild(_refNodes[0]);
        
        // Data
        StrokeE.Remove<StrokeBrush>();
        _layerE.Get<LayerTreeNode>().RemoveChild(^1);
        StrokeE.Remove<ToSerializeTag>();
    }

    public Entity InitEntity()
    {
        if (StrokeE != Entity.Null) return StrokeE;
        StrokeE = WorkingWorld.Create();
        var node = new LayerTreeNode();
        StrokeE.Add(new StrokeGeometry(), node);
        return StrokeE;
    }
}