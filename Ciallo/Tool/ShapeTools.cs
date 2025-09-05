using Godot;
using Ciallo.Command;
using Ciallo.NodeControl;

namespace Ciallo.Tool;

/// <summary>
/// Base class for tools that create geometric shapes through click-and-drag operations.
/// Examples: Rectangle, Ellipse, Line tools.
/// </summary>
public abstract class ShapeToolBase : InteractiveToolBase
{
    protected Vector2 ShapeStartPoint;
    protected Vector2 ShapeEndPoint;
    
    public override Input.CursorShape Cursor => Input.CursorShape.Cross;

    protected override void OnOperationStarted(Vector2 startPosition)
    {
        ShapeStartPoint = startPosition;
        ShapeEndPoint = startPosition;
        ShowShapePreview();
    }

    protected override void OnOperationUpdate(CursorMotionData data)
    {
        ShapeEndPoint = data.WorldPosition;
        UpdateShapePreview();
    }

    protected override void OnOperationCompleted(Vector2 startPosition, Vector2 endPosition)
    {
        var shapeCmd = CreateShapeCommand(ShapeStartPoint, ShapeEndPoint);
        if (shapeCmd != null)
        {
            shapeCmd.Commit();
            shapeCmd.Free();
        }
        
        HideShapePreview();
    }

    protected override void OnOperationCancelled()
    {
        HideShapePreview();
    }

    /// <summary>
    /// Create the appropriate command for this shape type.
    /// </summary>
    protected abstract CommandBase CreateShapeCommand(Vector2 startPoint, Vector2 endPoint);

    /// <summary>
    /// Show visual preview of the shape being created.
    /// </summary>
    protected virtual void ShowShapePreview()
    {
        GD.Print($"Showing {Name} preview");
    }

    /// <summary>
    /// Update the visual preview with current shape bounds.
    /// </summary>
    protected virtual void UpdateShapePreview()
    {
        var rect = new Rect2(ShapeStartPoint, ShapeEndPoint - ShapeStartPoint);
        GD.Print($"Updating {Name} preview: {rect}");
    }

    /// <summary>
    /// Hide the visual preview.
    /// </summary>
    protected virtual void HideShapePreview()
    {
        GD.Print($"Hiding {Name} preview");
    }
}

/// <summary>
/// Rectangle tool for creating rectangular shapes.
/// </summary>
public class RectangleTool : ShapeToolBase
{
    public override string Name => "Rectangle";

    protected override CommandBase CreateShapeCommand(Vector2 startPoint, Vector2 endPoint)
    {
        var rect = new Rect2(startPoint, endPoint - startPoint);
        if (rect.Size.LengthSquared() < 1.0f) return null; // Too small
        
        return new CreateRectangleCmd(rect);
    }
}

/// <summary>
/// Ellipse tool for creating elliptical shapes.
/// </summary>
public class EllipseTool : ShapeToolBase
{
    public override string Name => "Ellipse";

    protected override CommandBase CreateShapeCommand(Vector2 startPoint, Vector2 endPoint)
    {
        var rect = new Rect2(startPoint, endPoint - startPoint);
        if (rect.Size.LengthSquared() < 1.0f) return null; // Too small
        
        return new CreateEllipseCmd(rect);
    }
}

// Example shape commands

/// <summary>
/// Command to create a rectangle shape.
/// </summary>
public partial class CreateRectangleCmd : CommandBase
{
    private readonly Rect2 _rect;

    public CreateRectangleCmd(Rect2 rect)
    {
        _rect = rect;
    }

    public override void Do()
    {
        // Create rectangle entity
        var rectEntity = WorkingWorld.Create();
        
        // Add rectangle components (pseudocode - adapt to your data model)
        // rectEntity.Add(new RectangleComponent(_rect));
        // rectEntity.Add(new ToSerializeTag());
        
        DoRefEntities.Add(rectEntity);
        GD.Print($"Created rectangle: {_rect}");
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
        GD.Print("Undid rectangle creation");
    }
}

/// <summary>
/// Command to create an ellipse shape.
/// </summary>
public partial class CreateEllipseCmd : CommandBase
{
    private readonly Rect2 _bounds;

    public CreateEllipseCmd(Rect2 bounds)
    {
        _bounds = bounds;
    }

    public override void Do()
    {
        // Create ellipse entity
        var ellipseEntity = WorkingWorld.Create();
        
        // Add ellipse components (pseudocode - adapt to your data model)
        // ellipseEntity.Add(new EllipseComponent(_bounds));
        // ellipseEntity.Add(new ToSerializeTag());
        
        DoRefEntities.Add(ellipseEntity);
        GD.Print($"Created ellipse: {_bounds}");
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
        GD.Print("Undid ellipse creation");
    }
}