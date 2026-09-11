using System.Collections.Immutable;
using System.Linq;
using Frent;

namespace Ciallo.Data;

internal static class CelLayerSelection
{
    /// <summary>Each name expands to every matching direct child, in layer order.</summary>
    public static ImmutableArray<Entity> Resolve(Entity cel, ImmutableArray<string> names)
    {
        if (cel.IsNull || cel.IsCelFolder || !cel.Has<FolderLayerSetting>()) return [];
        return Resolve([.. cel.Get<LayerTreeNode>().GetLayerChildren()
            .Select(child => (child, child.Get<CommonLayerSetting>().Name.Value))], names);
    }

    /// <summary>Orders known layer identities and names without reading their components or parents.</summary>
    public static ImmutableArray<Entity> Resolve(
        ImmutableArray<(Entity Layer, string Name)> layers, ImmutableArray<string> names) =>
        [.. names.Distinct().SelectMany(name => layers.Where(layer => layer.Name == name).Select(layer => layer.Layer))];

    public static ImmutableArray<string> Names(ImmutableArray<Entity> layers) =>
        [.. layers.Select(layer => layer.Get<CommonLayerSetting>().Name.Value).Distinct()];
}
