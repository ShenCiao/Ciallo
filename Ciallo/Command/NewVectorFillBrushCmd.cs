using Ciallo.Data;
using Ciallo.Rendering;
using Frent;
using Godot;
using R3;

namespace Ciallo.Command;

[CommandBuilder]
public class NewVectorFillBrushCmd : CommandBase
{
    public Entity CopyE { get; }

    public NewVectorFillBrushCmd(Entity copyE = default)
    {
        CopyE = copyE;
    }

    public override void OnDeletedAsDo() => TargetE.Delete();

    public override void BeforeFirstDo(Entity targetE)
    {
        // Data
        var setting = CopyE.IsNull
            ? new FillBrushSetting()
            {
                FillColor = { Value = Colors.LemonChiffon },
                MarkerColor = { Value = Colors.Black },
                MarkerTexture =
                {
                    Value = ImageTexture.CreateFromImage(GD.Load<Image>("res://Rendering/Image/Bullseye0_0.svg"))
                },
            }
            : CopyE.Get<FillBrushSetting>().Clone();
        targetE.Add(setting);

        // View
        var fillMaterial = new FillBrushMaterial();
        targetE.Add(fillMaterial);
        setting.FillColor.Subscribe(color => fillMaterial.Color = color).AddTo(targetE);

        var strokeMaterial = new StrokeBrushMaterial();
        targetE.Add(strokeMaterial);

        setting.MarkerColor.Subscribe(c =>
        {
            strokeMaterial.SetShaderParameter("MaterialColor", c);
        }).AddTo(targetE);
    }

    public override void Do(Entity targetE)
    {
        targetE.Tag<ToSerializeTag>();
        targetE.Document.Get<BrushManager>().VectorFillBrushes.Add(targetE);
    }

    public override void Undo(Entity targetE)
    {
        var document = targetE.Document;
        document.Get<BrushManager>().VectorFillBrushes.Remove(targetE);
        targetE.Detach<ToSerializeTag>();
        document.Get<SelectionManager>().SelectedShapes.Remove(targetE);
    }
}
