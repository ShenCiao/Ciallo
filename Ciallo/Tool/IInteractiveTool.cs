using Godot;
using Ciallo.NodeControl;

namespace Ciallo.Tool;

/// <summary>
/// Interface for all interactive tools that can handle user input and create commands.
/// Tools receive input events and can maintain state during user interactions.
/// </summary>
public interface IInteractiveTool
{
    /// <summary>
    /// The display name of this tool.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The cursor to display when this tool is active.
    /// </summary>
    Input.CursorShape Cursor { get; }

    /// <summary>
    /// Called when the tool becomes active.
    /// </summary>
    void OnActivated();

    /// <summary>
    /// Called when the tool is deactivated.
    /// </summary>
    void OnDeactivated();

    /// <summary>
    /// Handle mouse motion events.
    /// </summary>
    /// <param name="data">Cursor motion data including screen and world coordinates</param>
    void OnMouseMotion(CursorMotionData data);

    /// <summary>
    /// Handle mouse button events.
    /// </summary>
    /// <param name="button">The mouse button that was pressed/released</param>
    /// <param name="pressed">True if pressed, false if released</param>
    /// <param name="position">World position of the cursor</param>
    void OnMouseButton(MouseButton button, bool pressed, Vector2 position);

    /// <summary>
    /// Handle keyboard input events.
    /// </summary>
    /// <param name="keyEvent">The keyboard input event</param>
    void OnKeyInput(InputEventKey keyEvent);

    /// <summary>
    /// Called every frame while the tool is active to update any ongoing operations.
    /// </summary>
    void Update(double delta);

    /// <summary>
    /// Cancel any ongoing operation for this tool.
    /// Should clean up temporary state and not create any commands.
    /// </summary>
    void Cancel();
}