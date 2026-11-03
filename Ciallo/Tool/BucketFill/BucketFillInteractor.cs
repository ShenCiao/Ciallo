using System;
using Ciallo.Data;
using Ciallo.Rendering;
using Frent;
using Godot;

namespace Ciallo.Tool;

[RegisterState]
public class BucketFillInteractor : CapturingInteraction
{
    [StateAccess] internal BucketFillTool Tool;
    private BucketFillContext _context;
    private BucketFillPreview _preview;
    private Entity _brush;
    private bool _atBottom;
    public override TimeSpan MovingMinInterval => TimeSpan.FromMilliseconds(30);

    public override void Start(CursorButtonData data)
    {
        _context = Tool.Context;
        _context.SourceChanged += CancelForSourceChange;
        _brush = Document.Get<SelectionManager>().WorkingVectorFillBrush.Value;
        _atBottom = Tool.PlaceAtBottom.Value;
        _preview = new BucketFillPreview(PrimaryLayer);
        Query(data.WorldPosition);
    }

    public override void Moving(CursorMotionData data) => Query(data.WorldPosition);

    private void Query(Vector2 point)
    {
        var region = _context.Query(point, Tool);
        _preview.Show(region, _brush, _atBottom);
        Document.Get<WorldBody>().DefaultCursorShape = region.IsEmpty ? Control.CursorShape.Forbidden : Control.CursorShape.Cross;
    }

    private void CancelForSourceChange() => Fire(InteractionManager.CancelRequested);

    public override void End(CursorButtonData data)
    {
        Clear();
        _context.Commit(data.WorldPosition, Tool, _brush, _atBottom);
    }

    public override void Cancel() => Clear();

    private void Clear()
    {
        _context.SourceChanged -= CancelForSourceChange;
        _preview.Dispose();
        Document.Get<WorldBody>().DefaultCursorShape = default;
    }
}
