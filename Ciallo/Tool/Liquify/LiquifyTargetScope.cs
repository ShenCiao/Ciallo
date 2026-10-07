using System.Collections.Immutable;
using System.Linq;
using Ciallo.Data;
using Frent;

namespace Ciallo.Tool;

public static class LiquifyTargetScope
{
    public static Entity[] Resolve(Entity document, ImmutableArray<Entity> selectedLayers)
    {
        var selectionManager = document.Get<SelectionManager>();
        return selectionManager.SelectedShapes.Count > 0
            ? selectionManager.SelectedShapes.Where(CanLiquify).ToArray()
            : selectedLayers.SelectMany(layer => layer.Get<LayerTreeNode>().Children)
                .Where(CanLiquify).ToArray();
    }

    public static bool CanLiquify(Entity shapeE)
    {
        if (shapeE.IsDyingOrDead || !shapeE.Has<SampledPolyline>())
            return false;

        return shapeE.Has<StrokeSetting>() || shapeE.Has<FilledPolygonSetting>();
    }
}
