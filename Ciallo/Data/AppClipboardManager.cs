using System.Collections.Generic;
using System.Linq;
using Ciallo.Command;
using Frent;

namespace Ciallo.Data;

/// <summary>
/// App-level clipboard for copied shape data.
/// </summary>
/// <remarks>
/// The clipboard must not know which concrete data components make up a copied
/// shape. It stores data-only entities and delegates shape-specific copying and
/// remapping to <see cref="NewShapeCmdBase"/>. This keeps copy/paste on the same
/// data creation path as new objects and serialization/deserialization.
///
/// This is intentionally a compromise, not the final object-copy architecture:
/// the shape command base currently acts as the data materializer. A cleaner
/// future design would move data materialization, runtime materialization,
/// reference walking/remapping, and paste compatibility into separate per-object
/// registrations.
/// </remarks>
/// <remarks>
/// Further more, we should try Entity refs as foreign keys in relational databases
/// </remarks>
public static class AppClipboardManager
{
    private static readonly World ClipboardWorld = new();

    public static readonly List<Entity> Shapes = [];

    public static void Clear()
    {
        foreach (var shapeE in Shapes)
            shapeE.Delete();
        Shapes.Clear();
    }

    public static void CopyShapes(IEnumerable<Entity> shapeEs)
    {
        Clear();

        // Flatten into the paste target in document stacking order, independent of click order.
        foreach (var shapeE in shapeEs.OrderBy(shape => shape.Document.Get<LayerTreeNode>().FindPathTo(shape),
                     NodeIndexPathComparer.Instance))
        {
            var clipboardE = ClipboardWorld.Create();
            NewShapeCmdBase.CreateCopyCommand(shapeE).CreateDataOnly(clipboardE);
            Shapes.Add(clipboardE);
        }
    }

    public static List<Entity> PasteShapes(Entity targetLayerE)
    {
        var entityMap = CreateTargetBrushMap(targetLayerE.Document.Get<BrushManager>());
        var builder = new CommandBuilder("Paste Shapes", targetLayerE);
        List<Entity> pastedShapes = [];

        foreach (var shapeDataE in Shapes)
        {
            if (!NewShapeCmdBase.CanPasteToLayer(shapeDataE, targetLayerE)) continue;

            var newShapeE = targetLayerE.World.Create();
            var newShapeCmd = NewShapeCmdBase.CreateCopyCommand(shapeDataE, entityMap);
            newShapeCmd.TargetE = newShapeE;
            builder.Commands.Add(newShapeCmd);
            builder.SetTarget(newShapeE).AddToLayerTree(targetLayerE);
            pastedShapes.Add(newShapeE);
        }

        builder.Commit();
        return pastedShapes;
    }

    private static Dictionary<Entity, Entity> CreateTargetBrushMap(BrushManager brushManager)
    {
        return brushManager.StrokeBrushes
            .Concat(brushManager.VectorFillBrushes)
            .ToDictionary(brushE => brushE, brushE => brushE);
    }
}
