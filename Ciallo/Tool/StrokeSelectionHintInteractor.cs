using Ciallo.Command;
using Ciallo.NodeControl;
using Ciallo.Rendering;
using Godot;
using Massive;

namespace Ciallo.Tool;

public class StrokeSelectionHintInteractor : InteractorBase
{
    private StrokeOverlay _hintingOverlay;

    public override bool CanInteract => SelectionManager.WorkingLayer.Value.IsNotNull();
    
    public override void Interacting(CursorMotionData data)
    {
        // See 2D ray cast for the method:
        // https://docs.godotengine.org/en/stable/tutorials/physics/ray-casting.html
        // https://godotforums.org/d/34175-collision-with-point
        var pp = new PhysicsPointQueryParameters2D()
        {
            CollideWithBodies = true,
            Position = data.WorldPosition,
            CollisionMask = (uint)AppGodotLayers.Physics2DLayerMask.Stroke
        };
        var points = Document.Get<WorldOverlay>().GetWorld2D().DirectSpaceState.IntersectPoint(pp, 1);
        if(points.Count > 0)
        {
            var hit = points[0];
            var collider = (Node)hit["collider"];
            var overlay = (StrokeOverlay)collider.GetParent();
            if(overlay != _hintingOverlay)
            {
                _hintingOverlay?.SetColor(AppPreference.StrokeWireframeColor);
                overlay.SetColor(AppPreference.StrokeWireframeHintColor);
                _hintingOverlay = overlay;
            }
        }
        else
        {
            _hintingOverlay?.SetColor(AppPreference.StrokeWireframeColor);
            _hintingOverlay = null;
        }
    }

    public override void End(CursorButtonData _)
    {
        _hintingOverlay?.SetColor(AppPreference.StrokeWireframeColor);
        _hintingOverlay = null;
    }

    public override void Cancel()
    {
        _hintingOverlay?.SetColor(AppPreference.StrokeWireframeColor);
        _hintingOverlay = null;
    }
}