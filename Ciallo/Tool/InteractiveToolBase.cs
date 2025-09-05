using Godot;
using Ciallo.NodeControl;

namespace Ciallo.Tool;

/// <summary>
/// Abstract base class for interactive tools that provides common functionality.
/// Implements the IInteractiveTool interface with default behaviors.
/// </summary>
public abstract class InteractiveToolBase : IInteractiveTool
{
    /// <summary>
    /// Whether the tool is currently performing an operation (e.g., dragging, drawing).
    /// </summary>
    protected bool IsOperating { get; private set; }

    /// <summary>
    /// The world position where the current operation started.
    /// </summary>
    protected Vector2 OperationStartPosition { get; private set; }

    /// <summary>
    /// The current world position of the cursor.
    /// </summary>
    protected Vector2 CurrentPosition { get; private set; }

    public abstract string Name { get; }
    public virtual Input.CursorShape Cursor => Input.CursorShape.Arrow;

    public virtual void OnActivated()
    {
        // Override in derived classes if needed
    }

    public virtual void OnDeactivated()
    {
        // Cancel any ongoing operation when tool is deactivated
        Cancel();
    }

    public virtual void OnMouseMotion(CursorMotionData data)
    {
        CurrentPosition = data.WorldPosition;
        
        if (IsOperating)
        {
            OnOperationUpdate(data);
        }
        else
        {
            OnHover(data);
        }
    }

    public virtual void OnMouseButton(MouseButton button, bool pressed, Vector2 position)
    {
        CurrentPosition = position;

        if (button == MouseButton.Left)
        {
            if (pressed && !IsOperating)
            {
                StartOperation(position);
            }
            else if (!pressed && IsOperating)
            {
                CompleteOperation(position);
            }
        }
        else if (button == MouseButton.Right && pressed)
        {
            // Right click cancels operation
            Cancel();
        }
    }

    public virtual void OnKeyInput(InputEventKey keyEvent)
    {
        // Handle escape key to cancel operation
        if (keyEvent.Keycode == Key.Escape && keyEvent.Pressed)
        {
            Cancel();
        }
    }

    public virtual void Update(double delta)
    {
        // Override in derived classes if needed for continuous updates
    }

    public virtual void Cancel()
    {
        if (IsOperating)
        {
            OnOperationCancelled();
            IsOperating = false;
        }
    }

    /// <summary>
    /// Start a new operation at the given position.
    /// </summary>
    protected virtual void StartOperation(Vector2 startPosition)
    {
        if (IsOperating) return;

        IsOperating = true;
        OperationStartPosition = startPosition;
        CurrentPosition = startPosition;
        
        OnOperationStarted(startPosition);
    }

    /// <summary>
    /// Complete the current operation at the given position.
    /// </summary>
    protected virtual void CompleteOperation(Vector2 endPosition)
    {
        if (!IsOperating) return;

        CurrentPosition = endPosition;
        OnOperationCompleted(OperationStartPosition, endPosition);
        IsOperating = false;
    }

    // Abstract/virtual methods for derived classes to override

    /// <summary>
    /// Called when an operation starts (e.g., mouse down).
    /// </summary>
    protected virtual void OnOperationStarted(Vector2 startPosition)
    {
        // Override in derived classes
    }

    /// <summary>
    /// Called continuously while an operation is in progress (e.g., mouse drag).
    /// </summary>
    protected virtual void OnOperationUpdate(CursorMotionData data)
    {
        // Override in derived classes
    }

    /// <summary>
    /// Called when an operation is completed successfully (e.g., mouse up).
    /// </summary>
    protected virtual void OnOperationCompleted(Vector2 startPosition, Vector2 endPosition)
    {
        // Override in derived classes
    }

    /// <summary>
    /// Called when an operation is cancelled (e.g., escape key, right click).
    /// </summary>
    protected virtual void OnOperationCancelled()
    {
        // Override in derived classes
    }

    /// <summary>
    /// Called when the mouse is hovering but no operation is in progress.
    /// </summary>
    protected virtual void OnHover(CursorMotionData data)
    {
        // Override in derived classes
    }
}