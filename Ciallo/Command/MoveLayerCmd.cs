using System.Collections.Generic;
using System.Collections.Immutable;
using Massive;
using Ciallo.Data;
using Ciallo.Rendering;
using Godot;

namespace Ciallo.Command;

// ReSharper disable once Godot.MissingParameterlessConstructor
public class MoveLayerCmd : CommandBase
{
    private readonly ImmutableArray<int> _src;
    private readonly ImmutableArray<int> _dst;

    public MoveLayerCmd(IReadOnlyList<int> src, IReadOnlyList<int> dst)
    {
        _src = [..src];
        _dst = [..dst];
    }

    public override void Do()
    {
        // Layer tree data
        var tree = Document.Get<LayerTreeManager>();
        tree.Root.MoveDescendant(_src, _dst);
        
        // layer panel
        var layerTreeControl = Document.Get<LayerContainer>();
        layerTreeControl.Move(_src, _dst);
        
        // View
        var worldView = Document.Get<WorldView>();
        worldView.MoveNode(_src, _dst);
        
        // Overlay
        var worldOverlay = Document.Get<WorldOverlay>();
        worldOverlay.MoveNode(_src, _dst);
    }

    public override void Undo()
    {
        // Overlay
        var worldOverlay = Document.Get<WorldOverlay>();
        worldOverlay.MoveNode(_dst, _src);
        
        // View
        var worldView = Document.Get<WorldView>();
        worldView.MoveNode(_dst, _src);
        
        // layer panel
        var layerTreeControl = Document.Get<LayerContainer>();
        layerTreeControl.Move(_dst, _src);
        
        // layer tree data
        var tree = Document.Get<LayerTreeManager>();
        tree.Root.MoveDescendant(_dst, _src);
    }
}