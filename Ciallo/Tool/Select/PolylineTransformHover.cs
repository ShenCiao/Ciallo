using System;
using System.Collections.Generic;
using System.Linq;
using Ciallo.Data;
using Ciallo.Geometry;
using Ciallo.Rendering;
using Godot;

namespace Ciallo.Tool;

[RegisterState]
public class PolylineTransformHover : PolylineNoSelectionHover
{
    public Body RotationBody;
    public Body[] CornerBodies = [];

    private TransformOverlayBox _transformBox;
    private readonly List<Node2D> _wireframes = [];

    public bool CanTransform
    {
        get
        {
            bool shapeHovered = !CurrHoveredShape.IsNull;
            bool rotationDotHovered = RotationBody?.IsHovered == true;
            bool cornerDotsHovered = CornerBodies.Any(a => a.IsHovered);
            return shapeHovered || rotationDotHovered || cornerDotsHovered;
        }
    }

    public override void Start(CursorButtonData data)
    {
        base.Start(data);

        var selectionManager = Document.Get<SelectionManager>();
        var worldBody = Document.Get<WorldBody>();

        // --- Polyline transform
        var worldOverlay = Document.Get<WorldOverlay>();
        var selectedShapes = selectionManager.SelectedShapes;

        // transform box
        Rect2 rect = default;
        foreach (var (i, e) in selectedShapes.Index())
        {
            var wire = (Node2D)e.Get<PolylineWireframe>().Duplicate(0); // 0 means avoid duplicating script. Script duplication call constructor.
            worldOverlay.AddChild(wire);
            wire.Visible = true;
            _wireframes.Add(wire);

            // transform box overlay
            var bound = e.Get<SampledPolyline>().Positions.Value.GetBoundingBox();
            rect = i == 0 ? bound : rect.Merge(bound);
        }
        if (!rect.IsEqualApprox(default) && !rect.Size.IsZeroApprox())
        {
            _transformBox = new TransformOverlayBox(rect.Size, rect.GetCenter());
            worldOverlay.AddChild(_transformBox);

            // transform cursor bodies
            Body[] bodies = worldBody.CreateAddTransformAreas(rect.Size, rect.GetCenter());
            RotationBody = bodies[0];
            bodies[1].QueueFree();
            CornerBodies = bodies[2..6];
        }
    }

    public override void Cancel()
    {
        // cursor bodies
        RotationBody?.QueueFree();
        RotationBody = null;
        Array.ForEach(CornerBodies, b => b.QueueFree());
        CornerBodies = [];

        _wireframes.ForEach(node => node.QueueFree());
        _wireframes.Clear();
        _transformBox?.QueueFree();
        _transformBox = null;

        base.Cancel();
    }
}