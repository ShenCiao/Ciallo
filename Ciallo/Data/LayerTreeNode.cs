using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.Serialization;
using Frent;

namespace Ciallo.Data;

[DataContract, ToSerialize]
public class LayerTreeNode : EntityTreeNode<LayerTreeNode>
{
    public List<Entity> GetLayerChildren()
    {
        return GetFilteredChildren(IsLayerChild);
    }

    public IEnumerable<Entity> EnumerateLayerOrder()
    {
        foreach (var layer in GetLayerChildren())
        {
            yield return layer;
            if (layer.Has<FolderLayerSetting>())
                foreach (var descendant in layer.Get<LayerTreeNode>().EnumerateLayerOrder())
                    yield return descendant;
        }
    }

    public ImmutableArray<Entity> GetOperationRoots(ImmutableArray<Entity> layers)
    {
        if (layers.IsEmpty) return [];
        var selected = layers.ToHashSet();
        return [.. EnumerateLayerOrder().Where(e => selected.Contains(e)
            && !e.Get<LayerTreeNode>().EnumerateAncestors().Any(selected.Contains))];
    }

    public static bool IsCoveredBy(Entity layer, IReadOnlySet<Entity> roots) =>
        roots.Contains(layer) || layer.Get<LayerTreeNode>().EnumerateAncestors().Any(roots.Contains);

    public Entity GetNextFocusAfterDeletion(Entity target, IReadOnlySet<Entity> deleted)
    {
        var node = target.Get<LayerTreeNode>();
        var parent = node.ParentValue;
        if (parent.IsNull || parent.IsDocument)
            return Entity.Null;

        var siblings = parent.Get<LayerTreeNode>().GetLayerChildren();
        int index = siblings.IndexOf(target);
        for (int i = index + 1; i < siblings.Count; i++)
            if (!IsCoveredBy(siblings[i], deleted)) return siblings[i];
        for (int i = index - 1; i >= 0; i--)
            if (!IsCoveredBy(siblings[i], deleted)) return siblings[i];
        return IsCoveredBy(parent, deleted) ? Entity.Null : parent;
    }

    /// <summary>
    /// Returns the direct layer child whose name equals <paramref name="name"/>,
    /// or <see cref="Entity.Null"/> when none matches (including when the name is empty
    /// and no child happens to be named empty). On duplicate names the first match wins.
    /// </summary>
    public Entity GetLayerChildByName(string name)
    {
        foreach (var childE in GetLayerChildren())
            if (childE.Get<CommonLayerSetting>().Name.Value == name)
                return childE;

        return Entity.Null;
    }

    /// <summary>
    /// Assume the given node at path is focused and going to be deleted, return the path to the next node that should have focus.
    /// e.g. Used at deletion of primary layer to determine the new primary layer.
    /// </summary>
    /// <param name="path">The given node path.</param>
    /// <returns>
    /// Return priority: next sibling > previous sibling > parent > empty array (no nodes after deletion)
    /// If the deleted node is root (path is empty), return empty array.
    /// </returns>
    public ImmutableArray<int> GetNextFocusPathAfterDeletion(IReadOnlyList<int> path)
    {
        // If deleting the root, nothing to focus next.
        if (path.Count == 0)
            return [];

        // Build the parent path and resolve the parent node
        var parentPath = path
            .Take(path.Count - 1)
            .ToList();
        var parentNode = GetDescendantNode(parentPath);

        // Determine indices
        int idx = path[^1];
        int childCount = parentNode.Children.Count;

        // Next sibling
        if (idx + 1 < childCount)
        {
            parentPath.Add(idx + 1);
            return [.. parentPath];
        }

        // Previous sibling
        if (idx - 1 >= 0)
        {
            parentPath.Add(idx - 1);
            return [.. parentPath];
        }

        // Fallback to parent
        return [.. parentPath];
    }

    private static bool IsLayerChild(Entity child)
    {
        return !child.IsNull
               && child.IsAlive
               && child.Has<LayerTreeNode>()
               && child.Has<CommonLayerSetting>();
    }
}
