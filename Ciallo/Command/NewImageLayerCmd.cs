using Ciallo.Data;
using Ciallo.GuiControl;
using Ciallo.Rendering;
using Frent;
using Godot;
using R3;

namespace Ciallo.Command;

[CommandBuilder]
public class NewImageLayerCmd : CommandBase
{
    private readonly ImageLayerSetting _imageLayerSetting;
    public readonly Entity CopyE;

    public override void OnDeletedAsDo() => TargetE.Delete();

    public NewImageLayerCmd(Image image)
    {
        image.GenerateMipmaps();
        _imageLayerSetting = new ImageLayerSetting
        {
            Texture = ImageTexture.CreateFromImage(image)
        };
    }

    public NewImageLayerCmd(Entity copyE = default)
    {
        CopyE = copyE;
    }

    public override void BeforeFirstDo(Entity targetE)
    {
        // Data
        var layerNode = new LayerTreeNode();
        targetE.Add(layerNode);

        var commonSetting = CopyE.IsNull
            ? new CommonLayerSetting { Name = { Value = "Image".Tr() } }
            : CopyE.Get<CommonLayerSetting>().Clone();
        targetE.Add(commonSetting);

        var setting = _imageLayerSetting ?? (CopyE.IsNull
            ? new ImageLayerSetting()
            : CopyE.Get<ImageLayerSetting>().Clone());
        targetE.Add(setting);

        // View
        var sprite = new Sprite2D
        {
            Texture = setting.Texture,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps,
        };
        targetE.AddNode(sprite);

        commonSetting.IsVisible.Subscribe(sprite.SetVisible).AddTo(targetE);
        commonSetting.Opacity.Subscribe(v =>
        {
            var color = sprite.SelfModulate;
            color.A = v;
            sprite.SelfModulate = color;
        }).AddTo(targetE);
        commonSetting.BlendMode.Subscribe(v => sprite.SetLayerBlendMode(ToGodotBlendMode(v))).AddTo(targetE);
        commonSetting.ClippingMask.Subscribe(sprite.SetClippingMask).AddTo(targetE);
        setting.ImageTransform.Subscribe(sprite.SetTransform).AddTo(targetE);

        // Overlay
        var layerOverlay = new TransformOverlayBox(setting.ImageSize) { Visible = false };
        targetE.AddNode(layerOverlay);
        setting.ImageTransform.Subscribe(t =>
        {
            layerOverlay.LocalTransform = t;
            layerOverlay.UpdateGeometry();
        }).AddTo(targetE);

        // Layer panel
        targetE.Document.Get<LayerTree>().Create(targetE);

        // Timeline track
        targetE.Document.Get<TrackTree>().Create(targetE);

        // Layer tree events
        var events = layerNode.MovedReparentedAsAddedRemoved;

        events.Added.Subscribe(et =>
        {
            // Layer panel
            et.Parent.Get<LayerWrapper>().InsertNodeAt(targetE.Get<LayerWrapper>(), et.Index);

            // Timeline track
            et.Parent.Get<TrackRowWrapper>().InsertNodeAt(targetE.Get<TrackRowWrapper>(), et.Index);

            // View
            var folderLayerView = et.Parent.Get<FolderLayerView>();
            folderLayerView.InsertNodeAt(sprite, et.Index);

            // Overlay
            et.Parent.Get<OverlayHolder>().InsertNodeAt(layerOverlay, et.Index);
        }).AddTo(targetE);

        events.Removed.Subscribe(_ =>
        {
            // Layer panel
            targetE.Get<LayerWrapper>().RemoveFromParent();

            // Timeline track
            targetE.Get<TrackRowWrapper>().RemoveFromParent();

            // Overlay
            layerOverlay.RemoveFromParent();

            // View
            sprite.RemoveFromParent();
        }).AddTo(targetE);
    }

    public override void Do(Entity targetE)
    {
        targetE.Tag<ToSerializeTag>();
    }

    public override void Undo(Entity targetE)
    {
        targetE.Detach<ToSerializeTag>();
    }

    private static Sprite2D.LayerBlendModeEnum ToGodotBlendMode(LayerBlendMode mode) => mode switch
    {
        LayerBlendMode.Normal => Sprite2D.LayerBlendModeEnum.Normal,
        LayerBlendMode.Add => Sprite2D.LayerBlendModeEnum.Add,
        LayerBlendMode.Multiply => Sprite2D.LayerBlendModeEnum.Multiply,
        _ => throw new System.ArgumentOutOfRangeException(nameof(mode), mode, null),
    };
}
