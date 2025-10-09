using System.Collections.Generic;
using System.Linq;
using Ciallo.Data;
using Ciallo.Rendering;
using Godot;
using Massive;

namespace Ciallo.Command;

public class DeletePolylineLayerCmd : CommandBase
{
    private Entity _targetE;
    private int _targetIndex;
    private readonly List<Node> _refNodes = [];

    public DeletePolylineLayerCmd(Entity targetE)
    {
        // Hierarchy not implemented, always remove from root.
        _targetE = targetE;
        _targetIndex = Document.Get<LayerTreeManager>().Root.FindPathTo(_targetE).Single();
    }

    public override IEnumerable<Entity> UndoRefEntities => ToEnumerable(_targetE);
    public override IEnumerable<GodotObject> UndoRefObjects => _refNodes;

    public override void Do()
    {
        // Data
        // TODO: Remove children's ToSerializeTag.
        var tree = Document.Get<LayerTreeManager>();
        tree.Root.RemoveChild(_targetIndex);
        _targetE.Remove<ToSerializeTag>();
        
        // Layer panel
        var layerTreeControl = Document.Get<LayerContainer>();
        layerTreeControl.RemoveFree(_targetE);
        
        // View
        var worldView = Document.Get<WorldView>();
        var node = worldView.GetChild(_targetIndex);
        worldView.RemoveChild(node);
        if(_refNodes.Count == 0) _refNodes.Add(node);
        
        // Overlay
        var worldOverlay = Document.Get<WorldOverlay>();
        var overlayNode = worldOverlay.GetChild(_targetIndex);
        worldOverlay.RemoveChild(overlayNode);
        if(_refNodes.Count == 1) _refNodes.Add(overlayNode);
    }

    public override void Undo()
    {
        // Overlay
        var worldOverlay = Document.Get<WorldOverlay>();
        var overlayNode = _refNodes[1];
        worldOverlay.InsertNodeAt(overlayNode, _targetIndex);
        
        // View
        var worldView = Document.Get<WorldView>();
        var node = _refNodes[0];
        worldView.InsertNodeAt(node, _targetIndex);
        
        // Layer panel
        var layerTreeControl = Document.Get<LayerContainer>();
        layerTreeControl.CreateInsert(_targetE, _targetIndex);
        
        // Data
        _targetE.Add<ToSerializeTag>();
        var tree = Document.Get<LayerTreeManager>();
        tree.Root.InsertChild(_targetIndex, _targetE);
    }
}