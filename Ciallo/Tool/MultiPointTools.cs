using System.Collections.Generic;
using Godot;
using Ciallo.Command;
using Ciallo.NodeControl;

namespace Ciallo.Tool;

/// <summary>
/// Base class for tools that work with multiple click points to create complex shapes.
/// Examples: Polygon tool, Bezier path tool, multi-point selection tool.
/// </summary>
public abstract class MultiPointToolBase : InteractiveToolBase
{
    protected readonly List<Vector2> Points = new();
    protected bool IsCapturing = false;

    public override Input.CursorShape Cursor => Input.CursorShape.Cross;

    public override void OnMouseButton(MouseButton button, bool pressed, Vector2 position)
    {
        if (button == MouseButton.Left && pressed)
        {
            if (!IsCapturing)
            {
                StartCapture(position);
            }
            else
            {
                AddPoint(position);
            }
        }
        else if (button == MouseButton.Right && pressed)
        {
            if (IsCapturing)
            {
                CompleteCapture();
            }
            else
            {
                Cancel();
            }
        }
        else
        {
            base.OnMouseButton(button, pressed, position);
        }
    }

    public override void OnKeyInput(InputEventKey keyEvent)
    {
        if (keyEvent.Pressed)
        {
            switch (keyEvent.Keycode)
            {
                case Key.Enter:
                    if (IsCapturing)
                        CompleteCapture();
                    break;
                case Key.Escape:
                    Cancel();
                    break;
                case Key.Backspace:
                    if (IsCapturing && Points.Count > 0)
                        RemoveLastPoint();
                    break;
            }
        }
        
        base.OnKeyInput(keyEvent);
    }

    protected virtual void StartCapture(Vector2 firstPoint)
    {
        IsCapturing = true;
        Points.Clear();
        Points.Add(firstPoint);
        OnCaptureStarted();
        UpdatePreview();
    }

    protected virtual void AddPoint(Vector2 point)
    {
        Points.Add(point);
        OnPointAdded(point);
        UpdatePreview();
    }

    protected virtual void RemoveLastPoint()
    {
        if (Points.Count > 1)
        {
            var removedPoint = Points[Points.Count - 1];
            Points.RemoveAt(Points.Count - 1);
            OnPointRemoved(removedPoint);
            UpdatePreview();
        }
    }

    protected virtual void CompleteCapture()
    {
        if (Points.Count >= GetMinimumPointCount())
        {
            var command = CreateShapeCommand(Points);
            if (command != null)
            {
                command.Commit();
                command.Free();
            }
        }
        
        ResetCapture();
    }

    public override void Cancel()
    {
        ResetCapture();
    }

    protected virtual void ResetCapture()
    {
        IsCapturing = false;
        Points.Clear();
        OnCaptureReset();
        HidePreview();
    }

    protected override void OnOperationUpdate(CursorMotionData data)
    {
        if (IsCapturing)
        {
            OnHoverUpdate(data.WorldPosition);
        }
    }

    // Abstract/virtual methods for derived classes

    /// <summary>
    /// Get the minimum number of points required to create the shape.
    /// </summary>
    protected abstract int GetMinimumPointCount();

    /// <summary>
    /// Create the command for the completed shape.
    /// </summary>
    protected abstract CommandBase CreateShapeCommand(List<Vector2> points);

    /// <summary>
    /// Called when point capture starts.
    /// </summary>
    protected virtual void OnCaptureStarted() { }

    /// <summary>
    /// Called when a point is added.
    /// </summary>
    protected virtual void OnPointAdded(Vector2 point) { }

    /// <summary>
    /// Called when a point is removed.
    /// </summary>
    protected virtual void OnPointRemoved(Vector2 point) { }

    /// <summary>
    /// Called when capture is reset/cancelled.
    /// </summary>
    protected virtual void OnCaptureReset() { }

    /// <summary>
    /// Called when mouse hovers during capture (for preview).
    /// </summary>
    protected virtual void OnHoverUpdate(Vector2 hoverPosition) { }

    /// <summary>
    /// Update the visual preview of the shape.
    /// </summary>
    protected virtual void UpdatePreview()
    {
        GD.Print($"Updating {Name} preview with {Points.Count} points");
    }

    /// <summary>
    /// Hide the visual preview.
    /// </summary>
    protected virtual void HidePreview()
    {
        GD.Print($"Hiding {Name} preview");
    }
}

/// <summary>
/// Polygon tool for creating polygonal shapes with multiple vertices.
/// </summary>
public class PolygonTool : MultiPointToolBase
{
    public override string Name => "Polygon";

    protected override int GetMinimumPointCount() => 3;

    protected override CommandBase CreateShapeCommand(List<Vector2> points)
    {
        return new CreatePolygonCmd(new List<Vector2>(points));
    }

    protected override void OnHoverUpdate(Vector2 hoverPosition)
    {
        // Show preview with current hover position as temporary last point
        var previewPoints = new List<Vector2>(Points) { hoverPosition };
        UpdatePreviewWithPoints(previewPoints);
    }

    private void UpdatePreviewWithPoints(List<Vector2> previewPoints)
    {
        GD.Print($"Updating polygon preview with {previewPoints.Count} points (including hover)");
    }
}

/// <summary>
/// Command to create a polygon shape.
/// </summary>
public partial class CreatePolygonCmd : CommandBase
{
    private readonly List<Vector2> _points;

    public CreatePolygonCmd(List<Vector2> points)
    {
        _points = new List<Vector2>(points);
    }

    public override void Do()
    {
        // Create polygon entity
        var polygonEntity = WorkingWorld.Create();
        
        // Add polygon components (pseudocode - adapt to your data model)
        // polygonEntity.Add(new PolygonComponent(_points));
        // polygonEntity.Add(new ToSerializeTag());
        
        DoRefEntities.Add(polygonEntity);
        GD.Print($"Created polygon with {_points.Count} vertices");
    }

    public override void Undo()
    {
        foreach (var entity in DoRefEntities)
        {
            if (WorkingWorld.IsAlive(entity))
            {
                WorkingWorld.Destroy(entity);
            }
        }
        GD.Print("Undid polygon creation");
    }
}