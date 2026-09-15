using System;
using System.Linq;
using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Frent;
using Godot;

namespace Ciallo.Tool;

// Actual fill shown during capture, using the same polygon geometry as the committed shape.
internal sealed class BucketFillPreview : IDisposable
{
    private readonly ShapeLayerView _parent;
    private readonly Polygon2D _view;
    private BucketFillRegion _cachedRegion;

    public BucketFillPreview(Entity layer)
    {
        _parent = layer.Get<ShapeLayerView>();
        _view = new Polygon2D { Antialiased = true };
        _parent.AddChild(_view);
    }

    public void Show(BucketFillRegion region, Entity brush, bool atBottom)
    {
        _parent.MoveChild(_view, atBottom ? 0 : -1);
        _view.Color = brush.IsNull ? Colors.White : brush.Get<FillBrushSetting>().FillColor.Value;
        _view.Material = brush.IsNull ? AutoloadRendering.MissingFillBrushMaterial : null;
        _view.Texture = brush.IsNull ? AutoloadRendering.DummyTextureForUV : null;
        if (ReferenceEquals(_cachedRegion, region)) return;
        _cachedRegion = region;

        if (region.IsEmpty) _view.Clear();
        else _view.SetPolygonFromRawRings(new Godot.Collections.Array<Vector2[]>(region.Contours.Select(ring => ring.ToArray())));
    }

    public void Dispose()
    {
        _parent.RemoveChild(_view);
        _view.QueueFree();
        _cachedRegion = null;
    }
}
