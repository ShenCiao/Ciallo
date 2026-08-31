using Ciallo.Rendering;
using Godot;

namespace Ciallo.Tool;

[RegisterState]
public class GapBridgeHover : Interaction
{
    [StateAccess]
    public GapBridgeTool Tool { get; set; }

    private Vector2 _lastWorldPosition = Vector2.Inf;
    private GapBridge _hoveredBridge;
    private bool _hasHoveredBridge;

    public override void Start(CursorButtonData data) => RefreshHover(data.WorldPosition);
    public override void Moving(CursorMotionData data) => RefreshHover(data.WorldPosition);
    public override void End(CursorButtonData data) => Cancel();

    public override void Cancel()
    {
        _lastWorldPosition = Vector2.Inf;
        _hasHoveredBridge = false;
        Document.Get<WorldBody>().DefaultCursorShape = default;
    }

    public override bool OnKey(InputEventKey key, CursorButtonData data) => false;

    public bool TryGetHoveredBridge(out GapBridge bridge)
    {
        bridge = _hoveredBridge;
        return _hasHoveredBridge;
    }

    public bool RefreshHover(Vector2 worldPosition)
    {
        _lastWorldPosition = worldPosition;
        RefreshTarget();
        ApplyCursor();
        return _hasHoveredBridge;
    }

    public void RefreshCursor()
    {
        RefreshTarget();
        ApplyCursor();
    }

    private void RefreshTarget()
    {
        _hasHoveredBridge = false;
        if (!IsFinite(_lastWorldPosition))
            return;

        _hasHoveredBridge = Tool.TryPickBridge(_lastWorldPosition, out _hoveredBridge);
    }

    private void ApplyCursor()
    {
        var body = Document.Get<WorldBody>();
        if (Tool.Arrangement.ArrReady.CurrentValue == null)
        {
            body.DefaultCursorShape = Control.CursorShape.Wait;
            return;
        }

        body.DefaultCursorShape = _hasHoveredBridge
            ? Control.CursorShape.PointingHand
            : default;
    }

    private static bool IsFinite(Vector2 v) => float.IsFinite(v.X) && float.IsFinite(v.Y);
}
