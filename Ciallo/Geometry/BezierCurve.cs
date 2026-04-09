using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.Serialization;
using Godot;
using Newtonsoft.Json;
using R3;

namespace Ciallo.Geometry;

/// <summary>
/// This class is a combination of Godot's Curve2D and Curve.
/// It's mainly built upon Curve2D's data structure. To be used as a Godot.Curve, it must be X-monotone.
/// It largely changes the interfaces of Curve to fit our usage.
/// </summary>
/// <remarks>
/// For writing clean code, this class only implement cache-related mechanism and curve modification logic.
/// See PolyCubicBezierExtension class for the actual geometry/math calculations.
/// </remarks>
[DataContract]
public class BezierCurve
{
    #region Curve2D

    /// Members from godot's `Curve2D` class.

    // When populating json object, list adds items by default rather than replace existing items. Force replace here.
    [DataMember(Order = 0), JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<Point> Points
    {
        get => _points;
        set
        {
            _points = value;
            OnChanged();
        }
    }

    private List<Point> _points = [];
    public int Count => _points.Count;

    private readonly Subject<Unit> _changed = new();
    public Observable<Unit> Changed => _changed.ThrottleLastFrame(1);

    private bool IsCacheInvalid => _cachedPolyline == null;
    private List<Vector2> _cachedPolyline;
    private List<float> _cachedT; // Fractional T values of the points of the cached polyline

    public Rect2 BoundingBox => _points.GetBoundingBox();

    public BezierCurve() { }

    public BezierCurve(IReadOnlyList<Point> points)
    {
        _points = points.ToList();
    }

    private void OnChanged()
    {
        ClearCache();
        _changed.OnNext(new());
    }

    public void ClearCache()
    {
        _cachedPolyline = null;
        _cachedT = null;
    }

    public void AddPoint(Vector2 position, Vector2 inHandle, Vector2 outHandle, int at = -1)
    {
        var point = new Point(position, inHandle, outHandle);
        if (at >= 0 && at < _points.Count)
            _points.Insert(at, point);
        else
            _points.Add(point);
        OnChanged();
    }

    public void RemovePoint(int index)
    {
        if (index >= 0 && index < _points.Count)
        {
            _points.RemoveAt(index);
            OnChanged();
        }
    }

    public void Clear()
    {
        _points.Clear();
        OnChanged();
    }

    public Point GetPoint(int index)
    {
        if (index < 0 || index >= _points.Count) throw new ArgumentOutOfRangeException();
        return _points[index];
    }

    // Sample curve segment at index, t in [0,1]
    public Vector2 Sample(int index, float t) => _points.Sample(index, t);

    // Sample the whole curve at a fractional t (e.g. 2.5 means between segment 2 and 3 at t=0.5)
    public Vector2 Sample(float polyT) => _points.Sample(polyT);

    /// <summary>
    /// Insert a bezier control point at a fractional t. Do not change the visual shape of the curve.
    /// </summary>
    /// <returns>The index of added point.</returns>
    public int TryInsertPoint(float polyT)
    {
        if (_points.Count < 2) return -1;
        // clamp to valid segment range
        if (polyT <= 0f || polyT >= _points.Count - 1) return -1;
        int idx = (int)Math.Floor(polyT);
        float t = polyT - idx;
        if (t <= 0f || t >= 1f) return -1;

        _points.Insert(polyT);
        OnChanged();
        return idx + 1;
    }

    /// <summary>
    /// Insert a list of bezier control points at a list of fractional t.
    /// </summary>
    public void TryInsertPoints([NotNull] IReadOnlyList<float> polyT)
    {
        if (_points.Count < 2) return;
        // sort splits descending to avoid index shift issues
        var ts = polyT.Where(t => t > 0f && t < _points.Count - 1)
            .Distinct()
            .OrderByDescending(t => t)
            .ToList();
        foreach (var t in ts) TryInsertPoint(t);
    }

    public void Tessellate(int subdivisionsPerSegment = 32)
    {
        (_cachedPolyline, _cachedT) = _points.Tessellate(subdivisionsPerSegment);
    }

    /// <summary>
    /// Change the position of an existing point.
    /// </summary>
    public void SetPointPosition(int index, Vector2 position)
    {
        var pt = _points[index];
        pt.P = position;
        _points[index] = pt;
        OnChanged();
    }

    /// <summary>
    /// Change the incoming handle of an existing point.
    /// </summary>
    public void SetPointIn(int index, Vector2 inHandle)
    {
        var pt = _points[index];
        pt.In = inHandle;
        _points[index] = pt;
        OnChanged();
    }

    public void SetPointInLinearly(int index, Vector2 inHandle)
    {
        var pt = _points[index];
        var angleDelta = inHandle.Angle() - pt.In.Angle();
        var lengthDelta = inHandle.Length() - pt.In.Length();
        pt.In = inHandle;
        pt.Out = pt.Out.Normalized().Rotated(angleDelta) * (pt.Out.Length() + lengthDelta);
        _points[index] = pt;
        OnChanged();
    }
    /// <summary>
    /// Change the outgoing handle of an existing point.
    /// </summary>
    public void SetPointOut(int index, Vector2 outHandle)
    {
        var pt = _points[index];
        pt.Out = outHandle;
        _points[index] = pt;
        OnChanged();
    }

    public void SetPointOutLinearly(int index, Vector2 outHandle)
    {
        var pt = _points[index];
        var angleDelta = outHandle.Angle() - pt.Out.Angle();
        var lengthDelta = outHandle.Length() - pt.Out.Length();
        pt.Out = outHandle;
        pt.In = pt.In.Normalized().Rotated(angleDelta) * (pt.In.Length() + lengthDelta);
        _points[index] = pt;
        OnChanged();
    }

    /// <summary>
    /// Get the closest point on the curve to a given point p.
    /// </summary>
    /// <returns>Point position, output fractional t of the closest point</returns>
    public Vector2 GetClosestPoint(Vector2 p, out float t)
    {
        if (IsCacheInvalid) Tessellate();
        _cachedPolyline.GetClosestPoint(p, out var polyT);
        var (idx, lt) = polyT.ResolvePolyT();
        if (idx >= _cachedT.Count - 1)
        {
            t = _cachedT[^1];
            return _cachedPolyline[^1];
        }
        var deltaT = lt * (_cachedT[idx + 1] - _cachedT[idx]);
        t = _cachedT[idx] + deltaT;
        return Sample(t);
    }

    [DataContract]
    public struct Point(Vector2 p, Vector2 @in, Vector2 @out)
    {
        [DataMember(Order = 0)] public Vector2 P = p;
        [DataMember(Order = 1)] public Vector2 In = @in; // Relative to position
        [DataMember(Order = 2)] public Vector2 Out = @out;

        [Pure] public Point WithIn(Vector2 newIn) => new(P, newIn, Out);
        [Pure] public Point WithOut(Vector2 newOut) => new(P, In, newOut);
        [Pure] public Point WithPoint(Vector2 newP) => new(newP, In, Out);

        /// <remarks>
        /// Duck style design: As it is computed as a control mode, it's the control mode.
        /// </remarks>>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public HandleControlMode EstimatedHandleMode
        {
            get
            {
                bool isEqual = MathF.Abs(In.Length() - Out.Length()) < 1e-5f;
                bool isLinear = MathF.Abs(MathF.Abs(In.Angle() - Out.Angle()) - MathF.PI) < 1e-5f;
                if (isEqual && isLinear)
                    return HandleControlMode.LinearEqual;
                if (isLinear)
                    return HandleControlMode.Linear;
                return HandleControlMode.Free;
            }
            set
            {
                switch (value)
                {
                    case HandleControlMode.LinearEqual:
                        In = -Out;
                        break;
                    case HandleControlMode.Linear:
                        In = -Out.Normalized() * In.Length();
                        break;
                    case HandleControlMode.Free:
                        // Do nothing, already free
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(value), value, null);
                }
            }
        }
    }

    #endregion

    #region Curve

    /// All methods in this region assume the curve is X-monotone.
    /// <summary>
    /// Check if the curve is a monotone in X.
    /// So that each element of the function's domain X maps to a single element of its range Y.
    /// </summary>
    public bool IsXMonotone => _points.IsXMonotone();

    // Members from Godot's `Curve` class.
    public float MinX => BoundingBox.Position.X;
    public float MaxX => BoundingBox.End.X;
    public float MinY => BoundingBox.Position.Y;
    public float MaxY => BoundingBox.End.Y;
    public float XRange => BoundingBox.Size.X;
    public float YRange => BoundingBox.Size.Y;

    /// <summary>
    /// Returns the Y value for the point at the X position.
    /// </summary>
    public float SampleX(float x)
    {
        if (IsCacheInvalid) Tessellate(64);
        return _cachedPolyline.SampleX(x);
    }

    /// <summary>
    /// Sample by the given x ordered list from small to large.
    /// </summary>
    public List<float> SampleXList(IReadOnlyList<float> xs)
    {
        if (IsCacheInvalid) Tessellate(64);
        return _cachedPolyline.SampleXList(xs);
    }

    public Vector2 GetPointPosition(int i) => GetPoint(i).P;
    public float GetPointInTangent(int i) => GetPoint(i).In.Y / GetPoint(i).In.X;
    public float GetPointOutTangent(int i) => GetPoint(i).Out.Y / GetPoint(i).Out.X;
    public void SetPointInTangent(int i, float tangent)
    {
        var pt = GetPoint(i);
        var oldHandle = pt.In;
        float length = oldHandle.Length();
        if (length > 0f)
        {
            // preserve handle orientation
            float sign = oldHandle.X != 0f ? MathF.Sign(oldHandle.X) : MathF.Sign(oldHandle.Y);
            // unit vector matching requested slope
            var baseDir = new Vector2(1f, tangent).Normalized();
            // assign new in-handle with original length
            pt.In = baseDir * sign * length;
        }
        _points[i] = pt;
        OnChanged();
    }
    public void SetPointOutTangent(int i, float tangent)
    {
        var pt = GetPoint(i);
        var oldHandle = pt.Out;
        float length = oldHandle.Length();
        if (length > 0f)
        {
            float sign = oldHandle.X != 0f ? MathF.Sign(oldHandle.X) : MathF.Sign(oldHandle.Y);
            var baseDir = new Vector2(1f, tangent).Normalized();
            pt.Out = baseDir * sign * length;
        }
        _points[i] = pt;
        OnChanged();
    }

    private const float L = 0.4f;

    // Returns the in/out handle pair aligned along the direction from (0, y0) to (1, y1).
    private static (Vector2 In, Vector2 Out) AlignedHandles(float y0, float y1)
    {
        var v = new Vector2(1f, y1 - y0);
        var dl = v / v.Length() * L;
        return (-dl, dl);
    }

    public static BezierCurve Constant(float y = 0.0f) =>
        new([
            new(new(0f, y), new(-L, 0f), new(L, 0f)),
            new(new(1f, y), new(-L, 0f), new(L, 0f))
        ]);

    public static BezierCurve Linear(float y0 = 0.0f, float y1 = 1.0f)
    {
        var (inHandle, outHandle) = AlignedHandles(y0, y1);
        return new([
            new(new(0f, y0), inHandle, outHandle),
            new(new(1f, y1), inHandle, outHandle)
        ]);
    }

    public static BezierCurve EaseInOut(float y0 = 0.0f, float y1 = 1.0f)
    {
        // horizontal handles produce zero slope at start/end → S‐curve in between
        return new BezierCurve([
            new(new(0f, y0), new(-L, 0f), new(L, 0f)),
            new(new(1f, y1), new(-L, 0f), new(L, 0f))
        ]);
    }

    /// <summary>
    /// Creates a curve that starts slow (horizontal tangent) and ends at the natural slope of the line — ease in.
    /// </summary>
    public static BezierCurve EaseIn(float y0 = 0.0f, float y1 = 1.0f)
    {
        var (inHandle, outHandle) = AlignedHandles(y0, y1);
        return new([
            new(new(0f, y0), new(-L, 0f), new(L, 0f)), // horizontal: slow start
            new(new(1f, y1), inHandle, outHandle)         // aligned to slope: fast end
        ]);
    }

    /// <summary>
    /// Creates a curve that starts at the natural slope of the line and ends slow (horizontal tangent) — ease out.
    /// </summary>
    public static BezierCurve EaseOut(float y0 = 0.0f, float y1 = 1.0f)
    {
        var (inHandle, outHandle) = AlignedHandles(y0, y1);
        return new([
            new(new(0f, y0), inHandle, outHandle),         // aligned to slope: fast start
            new(new(1f, y1), new(-L, 0f), new(L, 0f))    // horizontal: slow end
        ]);
    }

    #endregion

    /// <summary>
    /// Shen: Godot's Curve2D's tangent mode is very, very unintuitive. I guess who programed it have never used Adobe Illustrator or Inkscape.
    /// Sep 17, 2025. Shen: Seem fixed in 4.5? No, only available in animation editor, not for runtime.
    /// </summary>
    public enum HandleControlMode
    {
        LinearEqual, // Two handles are equal in length and tangent (opposite direction)
        Linear, // Equal in tangent only
        Free
    }
}