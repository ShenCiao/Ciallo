using System.Collections.Generic;
using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Frent;
using Godot;

namespace Ciallo.Tool;

[RegisterState]
public class PaintFillPolyCubicBezierInteractor : PolyCubicBezierInteractor
{
    [StateAccess] public PaintFillTool Tool { get; set; }

    private Entity _fillBrush;
    private Geometry2D.PolyBooleanOperation? _operation;
    private StrokeView _preview;

    protected override bool CloseOnConfirm => true;

    protected override void CreatePreview()
    {
        _fillBrush = Document.Get<SelectionManager>().WorkingVectorFillBrush.Value;
        _operation = Tool.BooleanOperation.Value;
        _preview = PaintFillInteractor.CreatePreview(PrimaryLayer);
    }

    protected override void UpdatePreview(IReadOnlyList<Vector2> points) =>
        _preview.SetGeometry(points, AppPreference.StrokeWireframeRadius);

    protected override void Commit(PolylineSamples samples, bool closed) =>
        PaintFillInteractor.CommitPolygon(PrimaryLayer, _fillBrush, samples, _operation);

    protected override void ClearPreview()
    {
        _preview.QueueFree();
        _preview = null;
    }
}
