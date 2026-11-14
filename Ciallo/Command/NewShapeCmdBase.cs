using System.Collections.Generic;
using Ciallo.Data;
using Frent;

namespace Ciallo.Command;

/// <summary>
/// Base command for selectable shape creation.
/// </summary>
/// <remarks>
/// The key design goal is reuse: new shape creation, clipboard copy/paste, and
/// serialization/deserialization must share the same data-copy logic. Clipboard
/// code in particular must not know whether a stroke, filled polygon, or marker
/// is currently represented by specific internal data components.
///
/// This class is the current compromise that enforces a split between
/// data-only creation and runtime creation for shape commands. <see cref="CreateDataOnly"/>
/// may only add ECS data components and remap entity references; it must not
/// create Godot nodes, subscribe runtime behavior, read document singleton
/// services, or attach the entity to a layer tree. <see cref="CreateRuntime"/>
/// owns view/body/overlay/subscription wiring for normal document entities.
///
/// This is not meant to become a broad object-copy framework. If copy/paste grows
/// beyond selectable shapes, prefer extracting explicit per-object data
/// materializers instead of teaching clipboard code about each object's concrete
/// component layout.
/// </remarks>
public abstract class NewShapeCmdBase : CommandBase
{
    public Entity CopyE { get; }
    protected IReadOnlyDictionary<Entity, Entity> EntityMap { get; }

    protected NewShapeCmdBase(Entity copyE = default, IReadOnlyDictionary<Entity, Entity> entityMap = null)
    {
        CopyE = copyE;
        EntityMap = entityMap;
    }

    public sealed override void BeforeFirstDo(Entity targetE)
    {
        CreateDataOnly(targetE);
        CreateRuntime(targetE);
    }

    public void CreateDataOnly(Entity targetE)
    {
        targetE.Add(CopyE.IsNull ? new ShapeSetting() : CopyE.Get<ShapeSetting>().Clone());
        AddDataComponents(targetE);
    }

    protected abstract void AddDataComponents(Entity targetE);
    protected abstract void CreateRuntime(Entity targetE);

    protected Entity MapEntityRef(Entity refE)
    {
        if (refE.IsNull) return Entity.Null;
        if (EntityMap == null) return refE;
        return EntityMap.TryGetValue(refE, out var mappedE) ? mappedE : Entity.Null;
    }

    public static NewShapeCmdBase CreateCopyCommand(Entity copyE, IReadOnlyDictionary<Entity, Entity> entityMap = null)
    {
        if (copyE.Has<StrokeSetting>()) return new NewStrokeCmd(copyE, entityMap);
        if (copyE.Has<FilledPolygonSetting>()) return new NewFilledPolygonCmd(copyE, entityMap);
        if (copyE.Has<VectorFillMarkerSetting>()) return new NewVectorFillMarkerCmd(copyE, entityMap);

        throw new KeyNotFoundException("Unsupported shape entity.");
    }

    public static bool CanPasteToLayer(Entity shapeDataE, Entity targetLayerE)
    {
        if (targetLayerE.Has<ShapeLayerSetting>())
            return shapeDataE.Has<StrokeSetting>() || shapeDataE.Has<FilledPolygonSetting>();

        if (targetLayerE.Has<VectorFillLayerSetting>())
            return shapeDataE.Has<VectorFillMarkerSetting>();

        return false;
    }
}
