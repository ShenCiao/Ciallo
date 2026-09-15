using System.Collections.Generic;
using System.Linq;
using Ciallo.Data;
using Ciallo.Geometry;
using Frent;

namespace Ciallo.Command;

[CommandBuilder]
public class DeleteLayerCmd : CommandBase
{
    private CommandBuilder _deleteChildrenCmd;
    private CommandBuilder _removeReferencesCmd;
    private readonly List<Entity> _deletedEntities = [];

    public override void OnDeletedAsUndo()
    {
        foreach (var e in _deletedEntities)
            e.Delete();
    }

    public override void BeforeFirstDo(Entity targetE)
    {
        var node = targetE.Get<LayerTreeNode>();
        if (!node.IsLeaf)
        {
            _deleteChildrenCmd = new CommandBuilder("Delete Child Layers");
            bool targetIsFolder = targetE.Has<FolderLayerSetting>();
            foreach (var childE in node.Children.AsEnumerable().Reverse())
            {
                // By design, a folder layer can only contain layers, other layers can only contain shapes
                _deleteChildrenCmd.SetTarget(childE)
                    .RemoveFromLayerTree();
                if (targetIsFolder)
                    _deleteChildrenCmd.DeleteLayer();
                else _deleteChildrenCmd.DeleteShape();

                CollectAllDescendants(childE, _deletedEntities);
            }
        }
        _deletedEntities.Add(targetE);

        _removeReferencesCmd = new CommandBuilder("Remove Layer References", targetE);
        if (targetE.Has<ShapeLayerSetting>())
        {
            var query = targetE.World.CreateQuery().With<VectorFillLayerSetting>().Tagged<ToSerializeTag>().Build();
            foreach (var fill in query.EnumerateWithEntities())
                if (fill.Get<VectorFillLayerSetting>().ReferenceLayers.Contains(targetE))
                    _removeReferencesCmd.SetTarget(fill).SetObservableCollection(
                        e => e.Get<VectorFillLayerSetting>().ReferenceLayers, references => references.Remove(targetE));
        }
    }

    // Post-order: children before the node itself, matching OnDeletedAsUndo deletion order.
    private static void CollectAllDescendants(Entity e, List<Entity> list)
    {
        foreach (var childE in e.Get<LayerTreeNode>().Children)
            CollectAllDescendants(childE, list);
        list.Add(e);
    }

    public override void Do(Entity targetE)
    {
        targetE.TryGet<ChildShapePolylineLookup>()?.Unsubscribe();
        targetE.TryGet<ArrangementManager>()?.DesyncModification();

        // Delete children
        _deleteChildrenCmd?.Do();

        _removeReferencesCmd.Do();

        targetE.Detach<ToSerializeTag>();
    }

    public override void Undo(Entity targetE)
    {
        targetE.Tag<ToSerializeTag>();

        _deleteChildrenCmd?.Undo();

        targetE.TryGet<ArrangementManager>()?.SyncModification();
        targetE.TryGet<ChildShapePolylineLookup>()?.Subscribe();
        _removeReferencesCmd.Undo();
    }
}
