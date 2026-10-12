using Godot;

namespace Ciallo.GuiControl;

public partial class CanvasSlot : Control
{
    private bool _layoutQueued;

    public override void _EnterTree()
    {
        SetNotifyTransform(true);
        GetViewport().SizeChanged += QueueCanvasLayout;
    }

    public override void _Ready() => QueueCanvasLayout();

    public override void _ExitTree() => GetViewport().SizeChanged -= QueueCanvasLayout;

    public override void _Notification(int what)
    {
        if ((what == NotificationResized || what == NotificationTransformChanged) && IsNodeReady())
            QueueCanvasLayout();
    }

    private void QueueCanvasLayout()
    {
        if (_layoutQueued) return;
        _layoutQueued = true;
        CallDeferred(MethodName.UpdateCanvasLayout);
    }

    private void UpdateCanvasLayout()
    {
        _layoutQueued = false;
        if (!IsInsideTree()) return;

        // UI layout uses logical units; the canvas renders and receives input in
        // window pixels. Cancel the outer scale before the viewport texture is drawn.
        var toPixels = GetViewportTransform() * GetGlobalTransform();
        var begin = toPixels.Origin.Round();
        var end = (toPixels * Size).Round();
        var canvas = GetChild<Control>(0);
        canvas.Scale = Vector2.One / toPixels.Scale;
        canvas.Position = toPixels.AffineInverse() * begin;
        canvas.Size = (end - begin).Max(Vector2.One * 2);
    }
}
