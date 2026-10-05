using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Frent;
using Godot;

namespace Ciallo.Tool;

[RegisterState]
public class LiquifyInteractor : CapturingInteraction
{
    [StateAccess]
    public LiquifyTool Tool { get; set; }

    private Entity[] _processingEs;
    private Vector2[][] _origPolylines;
    private Vector2[][] _currPolylines;
    private float[][] _origRadii;
    private float[][] _currRadii;
    private float[][] _origPressures;
    private float[][] _currPressures;
    private LiquifyMode _mode;
    private Rect2[] _aabbs;
    private bool[] _dirty;

    public override void Start(CursorButtonData data)
    {
        _mode = Tool.Mode.Value;
        _processingEs = LiquifyTargetScope.Resolve(Document, PrimaryLayer);
        _origPolylines = new Vector2[_processingEs.Length][];
        _currPolylines = new Vector2[_processingEs.Length][];
        _origRadii = new float[_processingEs.Length][];
        _currRadii = new float[_processingEs.Length][];
        _origPressures = new float[_processingEs.Length][];
        _currPressures = new float[_processingEs.Length][];
        _aabbs = new Rect2[_processingEs.Length];
        _dirty = new bool[_processingEs.Length];

        for (int i = 0; i < _processingEs.Length; i++)
        {
            var geom = _processingEs[i].Get<SampledPolyline>();
            var positions = geom.Positions.Value;
            _origPolylines[i] = [.. positions];
            _currPolylines[i] = [.. positions];

            if (_processingEs[i].Has<StrokeSetting>())
            {
                var radii = geom.Radii.Value;
                _origRadii[i] = [.. radii];
                _currRadii[i] = [.. radii];
                _origPressures[i] = [.. geom.Pressures.Value];
                _currPressures[i] = [.. geom.Pressures.Value];
            }
            else
            {
                _origRadii[i] = [];
                _currRadii[i] = [];
                _origPressures[i] = [];
                _currPressures[i] = [];
            }

            _aabbs[i] = _currPolylines[i].GetBoundingBox();
        }

        ApplyDab(data.WorldPosition, Vector2.Inf, data.Pressure);
    }

    public override void Moving(CursorMotionData data)
    {
        ApplyDab(data.WorldPosition, data.WorldDelta, data.Pressure);
    }

    public override void End(CursorButtonData data)
    {
        var cmd = new CommandBuilder("Liquify", Document);
        for (int i = 0; i < _processingEs.Length; i++)
        {
            if (!_dirty[i]) continue;

            cmd.SetTarget(_processingEs[i]);
            if (_mode == LiquifyMode.Thickness)
                cmd.SetSampledPolyline(radii: _currRadii[i].ToImmutableArray());
            else if (_mode == LiquifyMode.Pressure)
                cmd.SetSampledPolyline(pressures: _currPressures[i].ToImmutableArray());
            else
                cmd.SetSampledPolyline(_currPolylines[i].ToImmutableArray());
        }
        cmd.Commit();

        Clear();
    }

    public override void Cancel()
    {
        RestoreViews();
        Clear();
    }

    private void ApplyDab(Vector2 brushCenter, Vector2 brushDelta, float pressure)
    {
        var liquifyTool = Tool;
        var dab = new LiquifyDab(
            brushCenter,
            brushDelta,
            liquifyTool.Radius.Value,
            liquifyTool.StrengthFor(_mode).Value,
            pressure);

        float cullRadius = dab.Radius;
        bool scalarMode = _mode is LiquifyMode.Thickness or LiquifyMode.Pressure;

        for (int i = 0; i < _processingEs.Length; i++)
        {
            if (!CircleIntersectsAabb(brushCenter, cullRadius, _aabbs[i]))
                continue;

            if (scalarMode)
            {
                if (!_processingEs[i].Has<StrokeSetting>())
                    continue;

                var points = _currPolylines[i];
                var values = _mode == LiquifyMode.Thickness ? _currRadii[i] : _currPressures[i];
                bool changed = false;
                for (int j = 0; j < points.Length; j++)
                {
                    var oldValue = values[j];
                    var newValue = _mode == LiquifyMode.Thickness
                        ? LiquifySculpt.ApplyThickness(points[j], oldValue, dab)
                        : LiquifySculpt.ApplyPressure(points[j], oldValue, dab);
                    if (newValue == oldValue)
                        continue;

                    values[j] = newValue;
                    changed = true;
                }

                if (changed)
                {
                    _dirty[i] = true;
                    UpdateView(_processingEs[i], points, _currRadii[i], _currPressures[i]);
                }

                continue;
            }

            var polyline = _currPolylines[i];
            bool moved = false;
            for (int j = 0; j < polyline.Length; j++)
            {
                var oldPoint = polyline[j];
                var newPoint = LiquifySculpt.ApplyPosition(_mode, oldPoint, dab);
                if (newPoint.IsEqualApprox(oldPoint)) continue;
                polyline[j] = newPoint;
                moved = true;
            }

            if (moved)
            {
                _dirty[i] = true;
                _aabbs[i] = polyline.GetBoundingBox();
                UpdateView(_processingEs[i], polyline, _currRadii[i], _currPressures[i]);
            }
        }
    }

    private void RestoreViews()
    {
        foreach (var (i, e) in _processingEs.Index())
            UpdateView(e, _origPolylines[i], _origRadii[i], _origPressures[i]);
    }

    private static void UpdateView(Entity e, IReadOnlyList<Vector2> positions, IReadOnlyList<float> radii,
        IReadOnlyList<float> pressures)
    {
        if (e.Has<StrokeSetting>())
            e.Get<StrokeView>().SetGeometry(positions, radii, pressures);
        if (e.Has<FilledPolygonSetting>())
            e.Get<Polygon2D>().SetPolygonFromRawRing(positions.ToImmutableArray());
    }

    private void Clear()
    {
        _processingEs = null;
        _origPolylines = null;
        _currPolylines = null;
        _origRadii = null;
        _currRadii = null;
        _origPressures = null;
        _currPressures = null;
        _aabbs = null;
        _dirty = null;
    }

    private static bool CircleIntersectsAabb(Vector2 center, float radius, Rect2 aabb)
    {
        float closestX = Mathf.Clamp(center.X, aabb.Position.X, aabb.Position.X + aabb.Size.X);
        float closestY = Mathf.Clamp(center.Y, aabb.Position.Y, aabb.Position.Y + aabb.Size.Y);
        float dx = center.X - closestX;
        float dy = center.Y - closestY;
        return dx * dx + dy * dy <= radius * radius;
    }
}
