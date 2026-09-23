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
    [StateAccess] public PaintFillTool Tool { get; set; }

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
        var mode = new OptionButton
        {
            CustomMinimumSize = new(0, 32),
            TooltipText = "Poly Cubic Bézier: press Enter to close with a straight line. Click the first point to close with a curve; drag before releasing to align the handles at the join.".Tr(),
        };
        mode.AddItem("Freehand");
        mode.AddItem("Poly Cubic Bézier");
        container.AddProperty("Mode", mode.BindSelectionIndex(Tool.Mode));

        var operation = new OptionButton
        {
            Name = "PaintFillBooleanOperation",
            CustomMinimumSize = new(0, 32),
            TooltipText = "Modify overlapping polygons using the same fill brush in this layer. If polygons are selected, modify only those. Subtract removes the drawn area; Intersect keeps it.".Tr(),
        };
        operation.BindValue<Geometry2D.PolyBooleanOperation?>(
            [null, Geometry2D.PolyBooleanOperation.Union, Geometry2D.PolyBooleanOperation.Difference,
                Geometry2D.PolyBooleanOperation.Intersection, Geometry2D.PolyBooleanOperation.Xor],
            Tool.BooleanOperation, value => PolygonBooleanActions.Label(value).Tr());
        container.AddProperty("Operation", operation);

        container.AddChild(new Label
        {
            Text = "Fill brush".Tr(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });
        var brushPreview = VectorFillBrushPreviewList.New(Document);
        brushPreview.CustomMinimumSize = new(0, 256);
        container.AddChild(brushPreview);
        brushPreview.DrawNameProperty(container);

        var sm = Document.Get<SelectionManager>();

        var fillColor = sm.WorkingVectorFillBrush
            .Select(e => e.TryGet<FillBrushSetting>()?.FillColor)
            .Flatten();
        container.AddProperty("Fill color",
            new ColorPickerButton().BindColor(fillColor)
        ).VisibleIf(sm.WorkingVectorFillBrush, Entity.IsNotNull);
    }
}
