using System;
using Ciallo.Rendering;
using Ciallo.Widget;
using Godot;
using R3;

namespace Ciallo.Tool;

[RegisterState]
public class BucketFillHover : Interaction, IPropertyProvider
{
    [StateAccess] internal BucketFillTool Tool;
    private BucketFillContext _context;
    private BucketFillContourPreview _preview;
    private CompositeDisposable _subscriptions;
    private Vector2 _point;
    public override TimeSpan MovingMinInterval => TimeSpan.FromMilliseconds(30);

    public override void Start(CursorButtonData data)
    {
        _context = Tool.Context;
        _context.SourceChanged += Refresh;
        _preview = new BucketFillContourPreview(WorkingLayer);
        _subscriptions = new();
        AppPreference.BucketFill.GapAware.Skip(1).Subscribe(_ => Refresh()).AddTo(_subscriptions);
        AppPreference.BucketFill.GapFactor.Skip(1).Subscribe(_ => Refresh()).AddTo(_subscriptions);
        _point = data.WorldPosition;
        Refresh();
    }

    public override void Moving(CursorMotionData data)
    {
        _point = data.WorldPosition;
        Refresh();
    }
    public override void End(CursorButtonData data) => Cancel();
    public override void Cancel()
    {
        _context.SourceChanged -= Refresh;
        _subscriptions.Dispose();
        _preview.Dispose();
        Document.Get<WorldBody>().DefaultCursorShape = default;
    }
    public override bool OnKey(InputEventKey key, CursorButtonData data) => false;

    private void Refresh()
    {
        if (!_point.IsFinite())
        {
            Document.Get<WorldBody>().DefaultCursorShape = Control.CursorShape.Cross;
            return;
        }
        var region = _context.Query(_point, AppPreference.BucketFill);
        _preview.Show(region);
        Document.Get<WorldBody>().DefaultCursorShape = region.IsEmpty ? Control.CursorShape.Forbidden : Control.CursorShape.Cross;
    }

    public void DrawPropertyBeforeSubstates(PropertyContainer container) =>
        AppPreference.BucketFill.DrawPolygonProperties(container, Document);
}
