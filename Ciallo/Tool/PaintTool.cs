using System.Collections.Generic;
using Godot;
using Ciallo.Command;
using Ciallo.NodeControl;

namespace Ciallo.Tool;

/// <summary>
/// Example paint tool that demonstrates how to create drawing commands.
/// This tool would create brush strokes when the user drags the mouse.
/// </summary>
public class PaintTool : InteractiveToolBase
{
    public override string Name => "Paint";
    public override Input.CursorShape Cursor => Input.CursorShape.Cross;

    private readonly List<Vector2> _currentStrokePoints = new();
    private PaintStrokeCmd _currentStrokeCommand;

    // Paint tool settings (could be exposed as properties)
    public float BrushSize { get; set; } = 5.0f;
    public Color BrushColor { get; set; } = Colors.Black;

    protected override void OnOperationStarted(Vector2 startPosition)
    {
        _currentStrokePoints.Clear();
        _currentStrokePoints.Add(startPosition);
        
        // Create a new paint stroke command but don't commit it yet
        _currentStrokeCommand = new PaintStrokeCmd(_currentStrokePoints, BrushSize, BrushColor);
    }

    protected override void OnOperationUpdate(CursorMotionData data)
    {
        // Add point to current stroke
        _currentStrokePoints.Add(data.WorldPosition);
        
        // Update the visual representation (preview) of the stroke
        UpdateStrokePreview();
    }

    protected override void OnOperationCompleted(Vector2 startPosition, Vector2 endPosition)
    {
        if (_currentStrokeCommand != null && _currentStrokePoints.Count > 1)
        {
            // Finalize the stroke command with all collected points
            _currentStrokeCommand.FinalizeStroke(_currentStrokePoints);
            
            // Commit the command to create the actual stroke and add to undo history
            _currentStrokeCommand.Commit();
            _currentStrokeCommand.Free();
            _currentStrokeCommand = null;
        }
        
        ClearStrokePreview();
        _currentStrokePoints.Clear();
    }

    protected override void OnOperationCancelled()
    {
        // Clean up without committing
        if (_currentStrokeCommand != null)
        {
            _currentStrokeCommand.Free();
            _currentStrokeCommand = null;
        }
        
        ClearStrokePreview();
        _currentStrokePoints.Clear();
    }

    private void UpdateStrokePreview()
    {
        // This would update a visual preview of the stroke being drawn
        // Implementation would depend on the rendering system
        GD.Print($"Updating stroke preview with {_currentStrokePoints.Count} points");
    }

    private void ClearStrokePreview()
    {
        // Clear the visual preview
        GD.Print("Clearing stroke preview");
    }
}

/// <summary>
/// Example command for creating paint strokes.
/// Demonstrates how tools create commands that integrate with the undo system.
/// </summary>
public partial class PaintStrokeCmd : CommandBase
{
    private List<Vector2> _points;
    private readonly float _brushSize;
    private readonly Color _brushColor;
    private bool _isFinalized;

    public PaintStrokeCmd(List<Vector2> initialPoints, float brushSize, Color brushColor)
    {
        _points = new List<Vector2>(initialPoints);
        _brushSize = brushSize;
        _brushColor = brushColor;
    }

    public void FinalizeStroke(List<Vector2> finalPoints)
    {
        _points = new List<Vector2>(finalPoints);
        _isFinalized = true;
    }

    public override void Do()
    {
        if (!_isFinalized || _points.Count == 0) return;

        // Create the actual stroke entity in the world
        // This would integrate with your stroke/drawing system
        var strokeEntity = WorkingWorld.Create();
        
        // Add stroke components (this is pseudocode - adapt to your data model)
        // strokeEntity.Add(new StrokeComponent(_points, _brushSize, _brushColor));
        // strokeEntity.Add(new ToSerializeTag());
        
        DoRefEntities.Add(strokeEntity);
        
        GD.Print($"Created paint stroke with {_points.Count} points, brush size {_brushSize}");
    }

    public override void Undo()
    {
        // Remove the stroke from the world
        foreach (var entity in DoRefEntities)
        {
            if (WorkingWorld.IsAlive(entity))
            {
                WorkingWorld.Destroy(entity);
            }
        }
        
        GD.Print("Undid paint stroke");
    }
}