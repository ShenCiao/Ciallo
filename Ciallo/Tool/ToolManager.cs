using System;
using System.Collections.Generic;
using Godot;
using Ciallo.NodeControl;
using R3;

namespace Ciallo.Tool;

/// <summary>
/// Manages the active tool and dispatches input events to it.
/// Integrates with WorldInteractiveEventDispatcher to handle tool switching and input routing.
/// </summary>
public partial class ToolManager : GodotObject
{
    private readonly Dictionary<string, IInteractiveTool> _tools = new();
    private IInteractiveTool _activeTool;

    /// <summary>
    /// Observable for when the active tool changes.
    /// </summary>
    public readonly Subject<IInteractiveTool> ActiveToolChanged = new();

    /// <summary>
    /// The currently active tool, or null if no tool is active.
    /// </summary>
    public IInteractiveTool ActiveTool
    {
        get => _activeTool;
        private set
        {
            if (_activeTool == value) return;
            
            _activeTool?.OnDeactivated();
            _activeTool = value;
            _activeTool?.OnActivated();
            
            ActiveToolChanged.OnNext(_activeTool);
        }
    }

    /// <summary>
    /// Register a tool with the manager.
    /// </summary>
    /// <param name="tool">The tool to register</param>
    public void RegisterTool(IInteractiveTool tool)
    {
        if (tool == null)
            throw new ArgumentNullException(nameof(tool));

        _tools[tool.Name] = tool;
        
        // Set as active tool if no tool is currently active
        if (_activeTool == null)
        {
            SetActiveTool(tool.Name);
        }
    }

    /// <summary>
    /// Unregister a tool from the manager.
    /// </summary>
    /// <param name="toolName">The name of the tool to unregister</param>
    public void UnregisterTool(string toolName)
    {
        if (_tools.TryGetValue(toolName, out var tool))
        {
            if (_activeTool == tool)
            {
                _activeTool?.Cancel();
                ActiveTool = null;
            }
            _tools.Remove(toolName);
        }
    }

    /// <summary>
    /// Set the active tool by name.
    /// </summary>
    /// <param name="toolName">The name of the tool to activate</param>
    /// <returns>True if the tool was found and activated, false otherwise</returns>
    public bool SetActiveTool(string toolName)
    {
        if (_tools.TryGetValue(toolName, out var tool))
        {
            // Cancel any ongoing operation with the current tool
            _activeTool?.Cancel();
            
            ActiveTool = tool;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Get all registered tool names.
    /// </summary>
    /// <returns>Collection of tool names</returns>
    public IEnumerable<string> GetToolNames()
    {
        return _tools.Keys;
    }

    /// <summary>
    /// Get a tool by name.
    /// </summary>
    /// <param name="toolName">The name of the tool</param>
    /// <returns>The tool, or null if not found</returns>
    public IInteractiveTool GetTool(string toolName)
    {
        _tools.TryGetValue(toolName, out var tool);
        return tool;
    }

    // Input event dispatching methods
    
    /// <summary>
    /// Dispatch mouse motion to the active tool.
    /// </summary>
    public void DispatchMouseMotion(CursorMotionData data)
    {
        _activeTool?.OnMouseMotion(data);
    }

    /// <summary>
    /// Dispatch mouse button event to the active tool.
    /// </summary>
    public void DispatchMouseButton(MouseButton button, bool pressed, Vector2 worldPosition)
    {
        _activeTool?.OnMouseButton(button, pressed, worldPosition);
    }

    /// <summary>
    /// Dispatch keyboard input to the active tool.
    /// </summary>
    public void DispatchKeyInput(InputEventKey keyEvent)
    {
        _activeTool?.OnKeyInput(keyEvent);
    }

    /// <summary>
    /// Update the active tool. Should be called from _Process or similar.
    /// </summary>
    public void UpdateActiveTool(double delta)
    {
        _activeTool?.Update(delta);
    }

    /// <summary>
    /// Cancel the current tool operation.
    /// </summary>
    public void CancelCurrentOperation()
    {
        _activeTool?.Cancel();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            ActiveToolChanged?.Dispose();
        }
    }
}