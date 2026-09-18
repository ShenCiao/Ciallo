using System.Linq;
using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Ciallo.Tool;
using Frent;
using Godot;
using ObservableCollections;
using R3;

namespace Ciallo.Command;

[CommandBuilder]
public class NewVectorFillLayerCmd : CommandBase
{
    public Entity CopyE { get; }

    public override void OnDeletedAsDo() => TargetE.Delete();

    public NewVectorFillLayerCmd(Entity copyE = default)
    {
        CopyE = copyE;
    }

    public override void BeforeFirstDo(Entity targetE)
    {
        // Data
        var layerNode = new LayerTreeNode();
        targetE.Add(layerNode);

        var commonSetting = CopyE.IsNull
            ? new CommonLayerSetting
            {
                Name = { Value = $"{"Vector fill layer".Tr()}" }
            }
            : CopyE.Get<CommonLayerSetting>().Clone();
        targetE.Add(commonSetting);

        var vectorFillLayerSetting = CopyE.IsNull
            ? new VectorFillLayerSetting()
            : CopyE.Get<VectorFillLayerSetting>().Clone();
        // The copy path deliberately drops ReferenceLayers. References are cel-local (a fill layer
        // points at shape-layer siblings inside its own cel), so a blind clone would leave the new
        // layer pointing at the source's siblings — wrong for both copy-based creation paths:
        // deserialization rebuilds references from stored entity ids, and cel-from-archetype remaps
        // them by name into the new cel (see ADR-0001). Neither relies on Clone carrying them over,
        // so clearing here also removes the need for the old cross-World reference guard.
        vectorFillLayerSetting.ReferenceLayers.Clear();
        targetE.Add(vectorFillLayerSetting);

        var manager = new ArrangementManager().AddTo(targetE);
        vectorFillLayerSetting.ReferenceLayers.ObserveChanged().Subscribe(_ =>
        {
            var refLayers = vectorFillLayerSetting.ReferenceLayers;
            manager.Observe([.. refLayers.Select(e => e.Get<ChildShapePolylineLookup>())]);
        }).AddTo(targetE);
        manager.Observe([.. vectorFillLayerSetting.ReferenceLayers.Select(e => e.Get<ChildShapePolylineLookup>())]);
        targetE.Add(manager);

        // Others
        NewShapeLayerCmd.CreateNonDataComponents(targetE);

        // Bounded area
        var boundedAreaPreview = new Polygon2D
        {
            Name = "BoundedArea",
            Antialiased = true,
        };
        targetE.AddNode(boundedAreaPreview);
        targetE.Get<ShapeLayerView>().AddChild(boundedAreaPreview, false, Node.InternalMode.Front);
        // Color & visibility — independent of arrangement state.
        InteractionManager.Tools.VectorFill.LayerBoundedAreaColor.Subscribe(color =>
        {
            if (!color.HasValue)
            {
                boundedAreaPreview.Visible = false;
                return;
            }
            boundedAreaPreview.Visible = true;
            boundedAreaPreview.Color = color.Value;
        }).AddTo(targetE);

        // Shape — ArrReady emits whenever the arrangement is settled and safe to query.
        // null means mid-rebuild; keep the last frame's triangles to avoid flicker.
        manager.ArrReady.Subscribe(arr =>
        {
            if (arr == null) return;
            boundedAreaPreview.SetTriangleResult(arr.GetTrianglesFromFace(arr.GetUnboundedFace()));
        }).AddTo(targetE);

        // Overlay extra
        var overlayHolder = targetE.Get<OverlayHolder>();
        overlayHolder.Visible = false;
        overlayHolder.AddChild(new OverlayHolder()); // hold stroke overlay 
        overlayHolder.AddChild(new OverlayHolder()); // hold wireframe overlay
    }

    public override void Do(Entity targetE)
    {
        targetE.Get<ArrangementManager>().SyncModification();
        targetE.Tag<ToSerializeTag>();
    }
    public override void Undo(Entity targetE)
    {
        targetE.Detach<ToSerializeTag>();
        targetE.Get<ArrangementManager>().DesyncModification();
    }
}
