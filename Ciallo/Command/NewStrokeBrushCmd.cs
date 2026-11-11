using Ciallo.Data;
using Ciallo.GuiControl;
using Ciallo.Rendering;
using Frent;

namespace Ciallo.Command;

[CommandBuilder]
public class NewStrokeBrushCmd : CommandBase
{
    private StrokeBrushSetting _setting;
    public readonly Entity CopyE;

    public NewStrokeBrushCmd(StrokeBrushSetting setting = null)
    {
        _setting = setting?.Clone();
    }

    public NewStrokeBrushCmd(Entity copyE = default)
    {
        CopyE = copyE;
    }

    public override void OnDeletedAsDo() => TargetE.Delete();

    public override void BeforeFirstDo(Entity targetE)
    {
        _setting ??= CopyE.IsNull
            ? StrokeBrushSetting.CreateDefault()
            : CopyE.Get<StrokeBrushSetting>().Clone();
        targetE.Add(_setting);

        // material
        var material = new StrokeBrushMaterial();
        material.ObserveBrushSetting(_setting);
        targetE.Add(material);

        var viewport = BrushPreview.CreateStrokeViewport(_setting, material,
            Document.Get<ColorManager>().CurrentColor,
            Document.Get<BrushManager>().CurrentStrokeRadius).QueueFreeWith(targetE);

        Document.Get<SubViewportHolder>().AddChild(viewport);
        targetE.Add(viewport.GetTexture());
    }

    public override void Do(Entity targetE)
    {
        // Data
        targetE.Tag<ToSerializeTag>();
        var bm = Document.Get<BrushManager>();
        bm.StrokeBrushes.Add(targetE);
    }

    public override void Undo(Entity brushE)
    {
        var bm = Document.Get<BrushManager>();
        bm.StrokeBrushes.Remove(brushE);
        brushE.Detach<ToSerializeTag>();
    }

}
