namespace Ciallo.Tool;

[RegisterState]
public class BucketFillInteractor : CapturingInteraction
{
    // TODO: Build/query the CDT and commit a standalone polygon here. The placeholder exercises
    // capture, release and cancellation without creating geometry or a command-history entry.
    public override void Start(CursorButtonData data) { }
    public override void Moving(CursorMotionData data) { }
    public override void End(CursorButtonData data) { }
    public override void Cancel() { }
}
