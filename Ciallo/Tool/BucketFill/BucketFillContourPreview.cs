using System;
using System.Collections.Generic;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Frent;

namespace Ciallo.Tool;

internal sealed class BucketFillContourPreview : IDisposable
{
    private readonly OverlayHolder _parent;
    private readonly List<StrokeView> _contours = [];
    private BucketFillRegion _cachedRegion;

    public BucketFillContourPreview(Entity layer)
    {
        _parent = layer.Get<OverlayHolder>();
    }

    public void Show(BucketFillRegion region)
    {
        if (ReferenceEquals(_cachedRegion, region)) return;
        _cachedRegion = region;

        // Match VectorFillHover's animated dashed boundary, including its wireframe width.
        float radius = AppPreference.StrokeWireframeRadius * 1.5f;
        int index = 0;
        foreach (var ring in region.Contours)
        {
            if (index == _contours.Count)
            {
                var contour = new StrokeView { Material = AutoloadRendering.DashWireframeMaterial };
                _parent.AddChild(contour);
                _contours.Add(contour);
            }
            _contours[index++].SetGeometry(ring, radius);
        }
        while (_contours.Count > index) RemoveLastContour();
    }

    private void RemoveLastContour()
    {
        var contour = _contours[^1];
        _contours.RemoveAt(_contours.Count - 1);
        _parent.RemoveChild(contour);
        contour.QueueFree();
    }

    public void Dispose()
    {
        while (_contours.Count > 0) RemoveLastContour();
        _cachedRegion = null;
    }
}
