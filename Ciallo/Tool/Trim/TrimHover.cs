using Ciallo.Rendering;
using Godot;

namespace Ciallo.Tool;

[RegisterState]
public class TrimHover : Interaction
{
    [StateAccess]
    public TrimTool Tool { get; set; } = null!;

    public override void Start(CursorButtonData data) => RefreshCursor();
    public override void Moving(CursorMotionData data) { }
    public override void End(CursorButtonData data) => Cancel();

    public override void Cancel()
    {
        Document.Get<WorldBody>().DefaultCursorShape = default;
    }

    public override bool OnKey(InputEventKey key, CursorButtonData data) => false;

    public void RefreshCursor()
    {
        var body = Document.Get<WorldBody>();
        body.DefaultCursorShape = Tool.Arrangement?.ArrReady.CurrentValue == null
            ? Control.CursorShape.Wait
            : Control.CursorShape.Cross;
    }
}
