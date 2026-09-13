using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.GuiControl;
using Ciallo.Rendering;
using Frent;
using Godot;
using R3;

namespace Ciallo.Command;

[CommandBuilder]
public class NewShapeLayerCmd : CommandBase
{
    public readonly Entity CopyE;

    public NewShapeLayerCmd(Entity copyE = default)
    {
        CopyE = copyE;
    }

    public override void OnDeletedAsDo() => TargetE.Delete();

    public override void BeforeFirstDo(Entity targetE)
    {
        // Data
        var layerNode = new LayerTreeNode();
        targetE.Add(layerNode);

        var commonSetting = CopyE.IsNull
            ? new CommonLayerSetting
            {
                Name = { Value = "Shape layer".Tr() }
            }
            : CopyE.Get<CommonLayerSetting>().Clone();
        targetE.Add(commonSetting);

        var shapeLayerSetting = !CopyE.IsNull && CopyE.Has<ShapeLayerSetting>()
            ? CopyE.Get<ShapeLayerSetting>().Clone()
            : new ShapeLayerSetting();
        targetE.Add(shapeLayerSetting);
        var polylineLookup = new ChildShapePolylineLookup();
        targetE.Add(polylineLookup);

        var manager = new ArrangementManager().AddTo(targetE);
        targetE.Add(manager);
        manager.Observe(polylineLookup);
        targetE.Get<ArrangementManager>().SyncModification();

        // Others
        CreateNonDataComponents(targetE);
    }

    public override void Do(Entity targetE)
    {
        targetE.Tag<ToSerializeTag>();
    }

    public override void Undo(Entity targetE)
    {
        targetE.Detach<ToSerializeTag>();
    }

    public static void CreateNonDataComponents(Entity targetE)
    {
        var layerNode = targetE.Get<LayerTreeNode>();
        // View
        var shapeLayerView = new ShapeLayerView();
        shapeLayerView.ObserveLayerSetting(targetE.Get<CommonLayerSetting>()).AddTo(targetE);
        targetE.AddNode(shapeLayerView);

        // Overlay
        var overlayHolder = new OverlayHolder();
        targetE.AddNode(overlayHolder);

        // Body
        var bodyHolder = new BodyHolder() { ProcessMode = Node.ProcessModeEnum.Disabled };
        targetE.AddNode(bodyHolder);

        // Layer panel
        targetE.Document.Get<LayerTree>().Create(targetE);

        // Timeline track
        targetE.Document.Get<TrackTree>().Create(targetE);

        // Layer tree events
        var events = layerNode.MovedReparentedAsAddedRemoved;
        events.Added.Subscribe(et => InsertIntoParent(et.Parent, et.Index)).AddTo(targetE);
        events.Removed.Subscribe(_ => DetachFromParent()).AddTo(targetE);

        return;

        void InsertIntoParent(Entity parentE, int index)
        {
            // Layer panel
            parentE.Get<LayerWrapper>().InsertNodeAt(targetE.Get<LayerWrapper>(), index);

            // Timeline track
            parentE.Get<TrackRowWrapper>().InsertNodeAt(targetE.Get<TrackRowWrapper>(), index);

            // View
            var folderLayerView = parentE.Get<FolderLayerView>();
            folderLayerView.InsertNodeAt(shapeLayerView, index);

            // Overlay
            parentE.Get<OverlayHolder>().InsertNodeAt(overlayHolder, index);

            // Body
            parentE.Get<BodyHolder>().InsertNodeAt(bodyHolder, index);
        }

        void DetachFromParent()
        {
            // Layer panel
            targetE.Get<LayerWrapper>().RemoveFromParent();

            // Timeline track
            targetE.Get<TrackRowWrapper>().RemoveFromParent();

            // Body
            bodyHolder.RemoveFromParent();

            // Overlay
            overlayHolder.RemoveFromParent();

            // View
            shapeLayerView.RemoveFromParent();
        }
    }
}
