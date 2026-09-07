using Ciallo.Rendering;
using Ciallo.Widget;
using Godot;

namespace Ciallo.Tool;

[RegisterState]
public class BucketFillHover : Interaction, IPropertyProvider
{
    public override void Start(CursorButtonData data)
    {
        Document.Get<WorldBody>().DefaultCursorShape = BucketFillTool.CanFillLayers(WorkingLayers)
            ? Control.CursorShape.Cross
            : Control.CursorShape.Forbidden;
    }

    public override void Moving(CursorMotionData data) { }
    public override void End(CursorButtonData data) => Cancel();
    public override void Cancel() => Document.Get<WorldBody>().DefaultCursorShape = default;
    public override bool OnKey(InputEventKey key, CursorButtonData data) => false;

    public void DrawPropertyBeforeSubstates(PropertyContainer container) =>
        BucketFillOptions.DrawModeProperty(container);
}
