using Godot;
using System;
using Ciallo.Widget;
using Ciallo.Tool;

namespace Ciallo.NodeControl;

/// <summary>
/// Responsible for collecting and dispatching canvas gui input events.
/// Current version also handles canvas navigation with mouse wheel. May change in the future.
/// </summary>
public partial class WorldInteractiveEventDispatcher : SubViewportContainer
{
    private Camera2D _camera;
    
    private bool _isHovering = false;
    private bool _isPanning = false;
    private Vector2 _prevScreenPos;
    private Vector2 _prevWorldPos;
    
    /// <summary>
    /// The tool manager that handles tool switching and input routing.
    /// </summary>
    public ToolManager ToolManager { get; private set; }
    
    public override void _Ready()
    {
        _camera = GetNode<Camera2D>("%Camera2D");
        
        // Initialize tool manager and register default tools
        ToolManager = new ToolManager();
        RegisterDefaultTools();
    }
    
    /// <summary>
    /// Register the default tools available in the application.
    /// </summary>
    private void RegisterDefaultTools()
    {
        ToolManager.RegisterTool(new SelectionTool());
        ToolManager.RegisterTool(new PaintTool());
        ToolManager.RegisterTool(new RectangleTool());
        ToolManager.RegisterTool(new EllipseTool());
        ToolManager.RegisterTool(new PolygonTool());
        
        // Set selection tool as default
        ToolManager.SetActiveTool("Select");
    }
    
    public override void _Process(double delta)
    {
        // Update the active tool every frame
        ToolManager?.UpdateActiveTool(delta);
    }
    
    public void OnGuiInput(InputEvent e)
    {
        var panel = (PaintPanel)Owner;
        
        // Handle keyboard input for tools
        if (e is InputEventKey keyEvent)
        {
            // Tool switching shortcuts
            if (keyEvent.Pressed)
            {
                switch (keyEvent.Keycode)
                {
                    case Key.Key1:
                        ToolManager?.SetActiveTool("Select");
                        return;
                    case Key.Key2:
                        ToolManager?.SetActiveTool("Paint");
                        return;
                    case Key.Key3:
                        ToolManager?.SetActiveTool("Rectangle");
                        return;
                    case Key.Key4:
                        ToolManager?.SetActiveTool("Ellipse");
                        return;
                    case Key.Key5:
                        ToolManager?.SetActiveTool("Polygon");
                        return;
                }
            }
            
            // Forward to active tool
            ToolManager?.DispatchKeyInput(keyEvent);
            return;
        }
        
        if (e is InputEventMouseMotion motion)
        {
            var worldPos = _camera.GetViewportTransform().AffineInverse() * motion.Position;
            var screenPos = motion.Position;
            var prevWorldPosWithCurrentCamera = _camera.GetViewportTransform().AffineInverse() * _prevScreenPos;
            var screenDelta = screenPos - _prevScreenPos;
            var worldDelta = worldPos - prevWorldPosWithCurrentCamera;
            var data = new CursorMotionData
            {
                ScreenPosition = screenPos,
                ScreenDelta = screenDelta,
                WorldPosition = worldPos,
                WorldDelta = worldDelta,
                RawData = motion
            };
            
            // Dispatch to tool system first
            Dispatch(data);
            
            _prevScreenPos = screenPos;
            _prevWorldPos = worldPos;
            
            if (_isPanning)
            {
                panel.Offset.Value -= worldDelta;
            }
        }

        // Handle mouse button events
        if (e is InputEventMouseButton buttonEvent)
        {
            var worldPos = _camera.GetViewportTransform().AffineInverse() * buttonEvent.Position;
            
            // Handle navigation with middle mouse button
            if (buttonEvent.ButtonIndex == MouseButton.Middle)
            {
                if (buttonEvent.Pressed && _isHovering)
                {
                    _isPanning = true;
                }
                else if (!buttonEvent.Pressed)
                {
                    _isPanning = false;
                }
                
                // Double click to reset camera position
                if (buttonEvent.DoubleClick)
                {
                    panel.Offset.Value = Vector2.Zero;
                }
                return; // Don't forward navigation events to tools
            }
            
            // Handle zoom with mouse wheel
            if (buttonEvent.ButtonIndex == MouseButton.WheelUp || buttonEvent.ButtonIndex == MouseButton.WheelDown)
            {
                if (_isHovering)
                {
                    var zoomFactor = Preferences.MouseWheelZoomFactor.Value;
                    if (buttonEvent.ButtonIndex == MouseButton.WheelUp)
                    {
                        panel.Zoom.Value *= 1.0f + zoomFactor;
                    }
                    else
                    {
                        panel.Zoom.Value *= 1.0f - zoomFactor;
                    }
                }
                return; // Don't forward zoom events to tools
            }
            
            // Forward other mouse button events to tools
            ToolManager?.DispatchMouseButton(buttonEvent.ButtonIndex, buttonEvent.Pressed, worldPos);
        }
    }
    
    public void OnMouseEnter()
    {
        // Pitfall: Godot Bug 4.4.1, Dragging the vsplit/hsplit bar around the container can trigger mouse enter.
        // So use OnGuiInput together to decide whether handle world input.
        _isHovering = true;
    }
    
    public void OnMouseExit()
    {
        _isHovering = false;
    }

    public void Dispatch(CursorMotionData data)
    {
        // Forward mouse motion events to the active tool
        ToolManager?.DispatchMouseMotion(data);
    }
}
