using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ciallo.Data;
using Ciallo.GuiControl;
using Frent;
using Godot;
using Godot.Collections;
using R3;

namespace Ciallo.Rendering;

/// <summary>
/// Root node holding all the physical bodies on canvas.
/// </summary>
/// <remarks>
/// Physical bodies (Body class) are used for click detection, and other selection operations on canvas.
/// </remarks>
public partial class WorldBody : BodyHolder
{
    private CanvasLayer _canvasLayer;
    private Control _cursorSwitcher; // This is supposed to be the job of ViewportContainer, but it doesn't respond even if changing MouseDefaultCursorShape.
    private PaintPanel _paintPanel;

    private readonly ReactiveProperty<Body> _hoveringBody = new();
    public ReadOnlyReactiveProperty<Body> HoveringBody => _hoveringBody;

    public bool EnableHoverDetection
    {
        get;
        set
        {
            field = value;
            SetHoveringBody(null);
        }
    } = false;
    public Control.CursorShape DefaultCursorShape
    {
        get;
        set
        {
            field = value;
            _cursorSwitcher.MouseDefaultCursorShape = value;
        }
    }
    public Vector2 CursorWorldPosition { get; set; } // Received from interactor

    public override void _EnterTree()
    {
        _canvasLayer = GetChild<CanvasLayer>(0);
        _cursorSwitcher = _canvasLayer.GetChild<Control>(0);
        _paintPanel = (PaintPanel)Owner;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!EnableHoverDetection) return;
        // See following pages for the point query method:
        // https://docs.godotengine.org/en/stable/tutorials/physics/ray-casting.html#space
        // https://godotforums.org/d/34175-collision-with-point
        // Note this is different to RayCast2D node, which is a ray on XY plane. We want a top-down cast here (a point on XY plane).
        var pointQuery = new PhysicsPointQueryParameters2D()
        {
            CollideWithBodies = true,
            Position = _paintPanel.ToCameraWorldPosition(CursorWorldPosition),
            CollisionMask = (uint)AppGodotLayers.Physics2DLayerMask.Stroke,
        };
        List<Body> hits = [];
        Array<Rid> exclude = [];
        const int batchSize = 32;
        while (true)
        {
            var batch = GetWorld2D().DirectSpaceState.IntersectPoint(pointQuery, batchSize);
            foreach (var hit in batch.Select(hit => (Body)hit["collider"]).Distinct())
            {
                hits.Add(hit);
                exclude.Add(hit.GetRid());
            }
            if (batch.Count < batchSize) break;
            pointQuery.Exclude = exclude;
        }
        var body = hits.Count > 0 ? TopMostFromHits(hits) : null;

        SetHoveringBody(body);
    }

    // Note: not implement screen position, world size
    public Body CreateAddRectBody(Vector2 size, Vector2 position, CursorRectFlags flags = default)
    {
        var body = CreateAddBody(flags);
        body.AddChild(new CollisionShape2D()
        {
            Shape = new RectangleShape2D { Size = size },
        });
        body.Position = flags.HasFlag(CursorRectFlags.CornerPosition) ? position + size * 0.5f : position;

        return body;
    }

    public Body CreateAddRectBody(Vector2 size, Transform2D transform)
    {
        var body = CreateAddBody();
        body.AddChild(new CollisionShape2D()
        {
            Shape = new RectangleShape2D { Size = size },
            Transform = transform,
        });
        return body;
    }

    private Body CreateAddBody(CursorRectFlags flags = default)
    {
        var body = new Body();

        if (flags.HasFlag(CursorRectFlags.ScreenPosition))
            _canvasLayer.AddChild(body);
        else
            AddChild(body);

        if (flags.HasFlag(CursorRectFlags.ScreenSize))
            MakeScreenSize(body);

        return body;
    }

    public void MakeScreenSize(Body body)
    {
        _paintPanel.CameraZoom
            .CombineLatest(body.HitTestActive, (zoom, active) => (zoom, active))
            .Where(v => v.active)
            .Subscribe(v =>
        {
            body.Scale = Vector2.One / v.zoom;
        }).AddTo(body);// Note:Exittree will dispose this but we don't move body between layers for now. 
    }

    public static Body CreateRect(Vector2 size, Vector2 center)
    {
        var body = new Body();
        body.AddChild(new CollisionShape2D()
        {
            Shape = new RectangleShape2D { Size = size },
        });
        body.Position = center;

        return body;
    }

    public void ForceUpdateCursor()
    {
        var body = _hoveringBody.Value;
        if (body == null) return;

        _cursorSwitcher.MouseDefaultCursorShape = body.MouseDefaultCursorShape;
        GetViewport()?.UpdateMouseCursorState();
    }

    private void SetHoveringBody(Body value)
    {
        var body = _hoveringBody.Value;
        if (body == value) return;

        _cursorSwitcher.MouseDefaultCursorShape = value?.MouseDefaultCursorShape ?? DefaultCursorShape;
        GetViewport()?.UpdateMouseCursorState();

        body?.IsHovered = false;
        value?.IsHovered = true;
        _hoveringBody.Value = value;
    }

    private static int GetCanvasLayer(Node n)
    {
        while (n is not null && n is not CanvasLayer) n = n.GetParent();
        return (n as CanvasLayer)?.Layer ?? 0;
    }

    private ImmutableArray<int> GetIndexPath(Node n)
    {
        return n.GetIndexPathTo(this);
    }

    private Body TopMostFromHits(IEnumerable<Body> hits)
    {
        return hits
            .OrderByDescending(GetCanvasLayer)
            .ThenByDescending(n => n.ZIndex)
            .ThenByDescending(GetIndexPath, NodeIndexPathComparer.Instance) // child parent hierarchy
            .ThenByDescending(n => n.GetIndex())
            .First();
    }

    public List<Entity> RectQuery(Rect2 rect)
    {
        var rectQuery = new PhysicsShapeQueryParameters2D()
        {
            CollideWithBodies = true,
            Shape = new RectangleShape2D() { Size = rect.Size.Abs() },
            Transform = new Transform2D(0, rect.GetCenter()),
            CollisionMask = (uint)AppGodotLayers.Physics2DLayerMask.Stroke,
        };
        List<Entity> result = [];
        Array<Rid> exclude = [];
        const int batchSize = 64;
        // A body may produce many shape hits; exclude complete bodies between batches.
        while (true)
        {
            var batch = GetWorld2D().DirectSpaceState.IntersectShape(rectQuery, batchSize);
            foreach (var body in batch.Select(hit => (Body)hit["collider"]).Distinct())
            {
                if (!body.SelfEntity.IsNull) result.Add(body.SelfEntity);
                exclude.Add(body.GetRid());
            }
            if (batch.Count < batchSize) break;
            rectQuery.Exclude = exclude;
        }
        return result;
    }

    public Body CreateAddStrokeCenterline(IReadOnlyList<Vector2> polyline, float radius)
    {
        var body = new Body();
        body.SetStrokeCenterline(polyline, radius);
        AddChild(body);
        _paintPanel.CameraZoom.Subscribe(v =>
        {
            body.UpdateStrokeCenterlineShape(polyline, radius / v);
        }).AddTo(body);
        return body;
    }

    public Body[] CreateAddTransformAreas(Vector2 size, Transform2D transform)
    {
        var half = size * 0.5f;
        Vector2[] corners =
        [
            transform * -half,
            transform * new Vector2(-half.X, half.Y),
            transform * half,
            transform * new Vector2(half.X, -half.Y)
        ];
        var dotAreaSize = Vector2.One * 100.0f / 3;

        var barDir = (corners[0] - corners[1]).Normalized();
        var barLength = AppPreference.StrokeDotRadius * 4f;
        var topMid = (corners[0] + corners[3]) * 0.5f;
        Vector2 rotationDotPos = topMid + barLength * barDir;

        var rotation = CreateAddRectBody(dotAreaSize, rotationDotPos, CursorRectFlags.ScreenSize);
        rotation.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        var translation = CreateAddRectBody(size, transform);
        translation.MouseDefaultCursorShape = Control.CursorShape.Move;

        var cornerBodies = new Body[corners.Length];
        foreach (var (idx, pos) in corners.Index())
        {
            var body = CreateAddRectBody(dotAreaSize, pos, CursorRectFlags.ScreenSize);
            body.MouseDefaultCursorShape = idx % 2 == 0 ? Control.CursorShape.Fdiagsize : Control.CursorShape.Bdiagsize;
            cornerBodies[idx] = body;
        }

        return [rotation, translation, .. cornerBodies];
    }

    public Body[] CreateAddTransformAreas(Vector2 size, Vector2 position)
    {
        return CreateAddTransformAreas(size, new Transform2D(0, position));
    }
}

// Note: not implement screen position, world size
[Flags]
public enum CursorRectFlags
{
    None = 0,

    /// <summary>Interpret the position as screen coordinates.</summary>
    ScreenPosition = 1 << 0,

    /// <summary>Interpret the size as a screen pixel measurement.</summary>
    ScreenSize = 1 << 1,

    /// <summary>Given position is the upper left corner of a button.</summary>
    CornerPosition = 1 << 2
}
