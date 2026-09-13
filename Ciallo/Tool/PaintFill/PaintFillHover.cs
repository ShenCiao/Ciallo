using Ciallo.Data;
using Ciallo.GuiControl;
using Ciallo.Rendering;
using Ciallo.Widget;
using Frent;
using Godot;
using R3;

namespace Ciallo.Tool;

[RegisterState]
public class PaintFillHover : Interaction, IPropertyProvider
{
    public override void Start(CursorButtonData data)
    {
        Document.Get<WorldBody>().DefaultCursorShape = Control.CursorShape.Cross;
    }

    public override void Moving(CursorMotionData data) { }
    public override void End(CursorButtonData data) => Cancel();
    public override void Cancel()
    {
        Document.Get<WorldBody>().DefaultCursorShape = default;
    }
    public override bool OnKey(InputEventKey key, CursorButtonData data) => false;

    public void DrawPropertyBeforeSubstates(PropertyContainer container)
    {
        container.AddChild(new Label
        {
            Text = "Fill brush".Tr(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var brushPreview = VectorFillBrushPreviewList.New(Document);
        brushPreview.CustomMinimumSize = new(0, 256);
        container.AddChild(brushPreview);

        var sm = Document.Get<SelectionManager>();

        var fillColor = sm.WorkingVectorFillBrush
            .Select(e => e.TryGet<FillBrushSetting>()?.FillColor)
            .Flatten();
        container.AddProperty("Fill color",
            new ColorPickerButton().BindColor(fillColor)
        ).VisibleIf(sm.WorkingVectorFillBrush, Entity.IsNotNull);
    }
}
