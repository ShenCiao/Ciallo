using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.Serialization;
using Massive;

namespace Ciallo.Data;

/// <summary>
/// Generic entity tree node component providing hierarchical structure.
/// </summary>
/// <typeparam name="T">The derived type of the tree node.</typeparam>
[DataContract]
public class EntityTreeNode<T> where T : EntityTreeNode<T>
{
    [DataMember] public List<Entity> Children = [];
    
    public int ChildCount => Children.Count;
    public int DescendantCount => CountSubtreeNodes((T)this) - 1;
    public bool IsLeaf => Children.Count == 0;

    #region Modify
    
    public void AddChild(Entity child)
    {
        if (!child.Has<T>()) throw new ArgumentException($"Child entity must have {typeof(T).Name} component.");
        Children.Add(child);
    }
    
    public Entity GetChild(Index index) => Children[index];

    public void InsertChild(int idx, Entity child)
    {
        if (!child.Has<T>()) throw new ArgumentException($"Child entity must have {typeof(T).Name} component.");
        Children.Insert(idx, child);
    }
    
    public void MoveChild(int srcIdx, int dstIdx)
    {
        var moving = Children[srcIdx];
        Children.RemoveAt(srcIdx);
        Children.Insert(dstIdx, moving);
    }
    
    public void RemoveChild(Index idx)
    {
        Children.RemoveAt(idx.GetOffset(Children.Count));
    }
    
    public void AddDescendant(IReadOnlyList<int> parentPath, Entity child)
    {
        GetDescendantNode(parentPath).AddChild(child);
    }

    public void InsertDescendant(IReadOnlyList<int> targetPath, Entity descendant)
    {
        int idx = targetPath[^1];
        GetDescendantNode(targetPath.SkipLast(1).ToArray()).InsertChild(idx, descendant);
    }
    
    public Entity RemoveDescendant(IReadOnlyList<int> targetPath)
    {
        var parentNode = GetDescendantNode(targetPath.SkipLast(1).ToArray());
        var removed = parentNode.Children[targetPath[^1]];
        parentNode.Children.RemoveAt(targetPath[^1]);
        return removed;
    }
    
    /// <summary>
    /// Move a descendant node to another position.
    /// </summary>
    /// <param name="srcPath">Which to move.</param>
    /// <param name="dstPath">Insertion path.</param>
    public void MoveDescendant(IReadOnlyList<int> srcPath, IReadOnlyList<int> dstPath)
    {
        // Resolve source
        var srcParentPath = srcPath.SkipLast(1).ToArray();
        var srcParent = GetDescendantNode(srcParentPath);
        int srcIdx = srcPath[^1];
        var moving = srcParent.Children[srcIdx];

        // Resolve destination parent and insertion index before mutating the tree
        var dstParentPath = dstPath.SkipLast(1).ToArray();
        int dstIdx = dstPath[^1];

        // Prevent creating a cycle by moving a node into its own subtree
        bool IsPrefix(IReadOnlyList<int> prefix, IReadOnlyList<int> full)
        {
            if (prefix.Count > full.Count) return false;
            for (int i = 0; i < prefix.Count; i++) if (prefix[i] != full[i]) return false;
            return true;
        }
        if (IsPrefix(srcPath, dstParentPath)) throw new InvalidOperationException("Cannot move a node into its own descendant.");

        var dstParent = GetDescendantNode(dstParentPath);

        if (ReferenceEquals(srcParent, dstParent))
        {
            // the same parent
            srcParent.MoveChild(srcIdx, dstIdx);
            return;
        }

        // Different parents: remove from source, then insert into destination
        srcParent.RemoveChild(srcIdx);
        dstParent.InsertChild(dstIdx, moving);
    }
    
    #endregion
    
    #region Visit

    public Entity GetDescendant([NotNull] IReadOnlyList<int> path)
    {
        if (path.Count == 0) throw new ArgumentException("Path cannot be empty.", nameof(path));
        if (path.Count == 1) return Children[path[0]];
        return Children[path[0]].Get<T>().GetDescendant(path.Skip(1).ToArray());
    }
    
    public T GetDescendantNode([NotNull] IReadOnlyList<int> path)
    {
        if (path.Count == 0) return (T)this;
        return Children[path[0]].Get<T>().GetDescendantNode(path.Skip(1).ToArray());
    }
    
    public T GetNodeOrNull([NotNull] IReadOnlyList<int> path)
    {
        if (path.Count == 0) return (T)this;
        int idx = path[0];
        if (idx < 0 || idx >= Children.Count) return null;
        var childNode = Children[idx].Get<T>();
        return childNode.GetNodeOrNull(path.Skip(1).ToArray());
    }
    
    public List<int> FindPathTo(Entity target)
    {
        var node = target.Get<T>();
        BreadthFirstSearch((T)this, node, out var path);
        return path;
    }
    
    #endregion

    #region utility

    /// <summary>
    /// Breadth first search for the entity.
    /// <returns>Entity list to the target, in parent first order.</returns>
    /// </summary>
    /// <param name="node"></param>
    /// <param name="targetNode"></param>
    /// <param name="path">Indices list to the target, in parent to child order</param>
    /// <returns></returns>
    public static List<Entity> BreadthFirstSearch(T node, T targetNode, out List<int> path)
    {
        if (node == targetNode)
        {
            path = [];
            return [];
        }
        
        var childNodes = node.Children.Select(e => e.Get<T>()).ToList();
        var index = childNodes.IndexOf(targetNode);
        if (index >= 0) // found
        {
            path = [index];
            return [node.Children[index]];
        }
        // not found, searching in children branches
        foreach (var (i, childNode) in childNodes.Index())
        {
            var ePath = BreadthFirstSearch(childNode, targetNode, out path);
            if (path == null) continue;
            path.Insert(0, i);
            ePath.Insert(0, node.Children[i]);
            return ePath;
        }
        // neither found in children
        path = null;
        return null;
    }

    /// <summary>
    /// Turn a preorder index into a path.
    /// </summary>
    /// <param name="preorderIdx">Root-Left-Right ordered flatten tree index.</param>
    /// <returns>Path to the node.</returns>
    public List<int> PreorderIndexToPath(int preorderIdx)
    {
        // Preorder enumeration (parent before its children), excluding the current node (root of this subtree).
        if (preorderIdx < 0) throw new ArgumentOutOfRangeException(nameof(preorderIdx), "Index cannot be negative.");
        int remaining = preorderIdx;
        List<int> path = [];

        bool Dfs(T node)
        {
            for (int i = 0; i < node.Children.Count; i++)
            {
                // Visit this child.
                path.Add(i);
                if (remaining == 0) return true;
                remaining--;

                // Traverse its subtree in preorder.
                var childNode = node.Children[i].Get<T>();
                if (Dfs(childNode)) return true;

                // Backtrack and continue with next sibling.
                path.RemoveAt(path.Count - 1);
            }
            return false;
        }

        return Dfs((T)this) ? path :
            throw new ArgumentOutOfRangeException(nameof(preorderIdx), "Index exceeds the number of nodes in the tree.");
    }

    /// <summary>
    /// Turn a path to a preorder index.
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public int PathToPreorderIndex([NotNull] IReadOnlyList<int> path)
    {
        int index = 0;
        var node = (T)this;

        for (int depth = 0; depth < path.Count; depth++)
        {
            int idx = path[depth];
            for (int i = 0; i < idx; i++)
                index += CountSubtreeNodes(node.Children[i].Get<T>());

            if (depth > 0) index++;

            node = node.Children[idx].Get<T>();
        }

        return index;
    }

    // compute total nodes in subtree (including the root of that subtree)
    public static int CountSubtreeNodes(T n)
    {
        int cnt = 1;
        foreach (var e in n.Children)
            cnt += CountSubtreeNodes(e.Get<T>());
        return cnt;
    }

    #endregion
}