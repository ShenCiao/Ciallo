using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Frent;
using Godot;

namespace Ciallo.Tool;

[RegisterState]
public class PaintVectorFillMarkerInteractor : CapturingInteraction
{
    private VectorFillMarkerView _markerPreview;
    private Polygon2D _fillPreview;
    private Entity _fillBrush;

    private float MarkerRadius => AppPreference.VectorFillMarkerRadius.Value;

    public override void Start(CursorButtonData data)
    {
        Input.MouseMode = Input.MouseModeEnum.Hidden;

        _fillBrush = Document.Get<SelectionManager>().WorkingVectorFillBrush.Value;

        // To preview marker
        _markerPreview = new();
        PrimaryLayer.Get<OverlayHolder>().AddChild(_markerPreview);
        _markerPreview.SetGeometry([data.WorldPosition], [MarkerRadius]);
        var arr = PrimaryLayer.Get<ArrangementManager>().ArrReady.CurrentValue;
        _fillPreview = new() { Antialiased = true };
        VectorFillMarkerView.ApplyBrush(_fillPreview, _markerPreview, _fillBrush);
        PrimaryLayer.Get<ShapeLayerView>().AddChild(_fillPreview);
        if (arr != null)
        {
            _fillPreview.SetPolygonWithQueryResult(arr, data.WorldPosition);
        }
    }

    public override void Moving(CursorMotionData data)
    {
        _markerPreview?.SetGeometry([data.WorldPosition], [MarkerRadius]);
        var arr = PrimaryLayer.Get<ArrangementManager>().ArrReady.CurrentValue;
        if (arr == null) return;
        _fillPreview?.SetPolygonWithQueryResult(arr, data.WorldPosition);
    }

    public override void End(CursorButtonData data)
    {
        ClearPreview();
        // Allow to place marker even if the arrangement is not ready or fill on unbounded area or brush is null.
        new CommandBuilder("Paint Vector Fill Marker", PrimaryLayer.World.Create())
            .NewVectorFillMarker()
            .AddToLayerTree(PrimaryLayer)
            .SetSampledPolyline([data.WorldPosition], [MarkerRadius], [1.0f], [Vector2.Zero])
            .SetProperty(e => e.Get<VectorFillMarkerSetting>().BrushE, _fillBrush)
            .Commit();

        Clear();
    }

    public override void Cancel() => Clear();

    public void ClearPreview()
    {
        _markerPreview?.Free();
        _markerPreview = null;
        _fillPreview?.Free();
        _fillPreview = null;
    }

    public void Clear()
    {
        ClearPreview();
        Input.MouseMode = Input.MouseModeEnum.Visible;
        _fillBrush = Entity.Null;
    }
}
