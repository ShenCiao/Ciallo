using Godot;
using Ciallo.Tool;

namespace Ciallo.NodeControl;

/// <summary>
/// Example of how to integrate the tool framework into your application.
/// This shows how to create a UI for tool selection and how to respond to tool changes.
/// </summary>
public partial class ToolPanelExample : PanelContainer
{
    private ToolManager _toolManager;
    private OptionButton _toolSelector;
    private Label _activeToolLabel;
    private VBoxContainer _toolConfigContainer;

    public override void _Ready()
    {
        SetupUI();
        
        // Get tool manager from WorldInteractiveEventDispatcher
        var dispatcher = GetNode<WorldInteractiveEventDispatcher>("%WorldInteractiveEventDispatcher");
        _toolManager = dispatcher.ToolManager;
        
        // Subscribe to tool changes
        _toolManager.ActiveToolChanged.Subscribe(OnActiveToolChanged);
        
        // Populate tool selector
        PopulateToolSelector();
    }

    private void SetupUI()
    {
        var vbox = new VBoxContainer();
        AddChild(vbox);
        
        // Tool selector
        var selectorLabel = new Label { Text = "Active Tool:" };
        vbox.AddChild(selectorLabel);
        
        _toolSelector = new OptionButton();
        _toolSelector.ItemSelected += OnToolSelected;
        vbox.AddChild(_toolSelector);
        
        // Active tool display
        _activeToolLabel = new Label { Text = "No tool selected" };
        vbox.AddChild(_activeToolLabel);
        
        // Tool configuration area
        var configLabel = new Label { Text = "Tool Configuration:" };
        vbox.AddChild(configLabel);
        
        _toolConfigContainer = new VBoxContainer();
        vbox.AddChild(_toolConfigContainer);
    }

    private void PopulateToolSelector()
    {
        _toolSelector.Clear();
        
        foreach (var toolName in _toolManager.GetToolNames())
        {
            _toolSelector.AddItem(toolName);
        }
        
        // Select current active tool
        if (_toolManager.ActiveTool != null)
        {
            var index = 0;
            foreach (var toolName in _toolManager.GetToolNames())
            {
                if (toolName == _toolManager.ActiveTool.Name)
                {
                    _toolSelector.Selected = index;
                    break;
                }
                index++;
            }
        }
    }

    private void OnToolSelected(long index)
    {
        var toolNames = new List<string>(_toolManager.GetToolNames());
        if (index >= 0 && index < toolNames.Count)
        {
            var toolName = toolNames[(int)index];
            _toolManager.SetActiveTool(toolName);
        }
    }

    private void OnActiveToolChanged(IInteractiveTool tool)
    {
        if (tool != null)
        {
            _activeToolLabel.Text = $"Active: {tool.Name}";
            
            // Update cursor (this would typically be done by the viewport)
            Input.SetDefaultCursorShape(tool.Cursor);
            
            // Clear and setup tool-specific configuration UI
            SetupToolConfiguration(tool);
        }
        else
        {
            _activeToolLabel.Text = "No tool selected";
            Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
            ClearToolConfiguration();
        }
    }

    private void SetupToolConfiguration(IInteractiveTool tool)
    {
        ClearToolConfiguration();
        
        // Add tool-specific configuration controls
        switch (tool)
        {
            case PaintTool paintTool:
                SetupPaintToolConfig(paintTool);
                break;
            case SelectionTool selectionTool:
                SetupSelectionToolConfig(selectionTool);
                break;
            // Add other tool types as needed
        }
    }

    private void SetupPaintToolConfig(PaintTool paintTool)
    {
        // Brush size control
        var sizeLabel = new Label { Text = "Brush Size:" };
        _toolConfigContainer.AddChild(sizeLabel);
        
        var sizeSpinBox = new SpinBox
        {
            MinValue = 1.0,
            MaxValue = 100.0,
            Value = paintTool.BrushSize,
            Step = 1.0
        };
        sizeSpinBox.ValueChanged += (double value) => paintTool.BrushSize = (float)value;
        _toolConfigContainer.AddChild(sizeSpinBox);
        
        // Brush color control
        var colorLabel = new Label { Text = "Brush Color:" };
        _toolConfigContainer.AddChild(colorLabel);
        
        var colorPicker = new ColorPickerButton
        {
            Color = paintTool.BrushColor
        };
        colorPicker.ColorChanged += (Color color) => paintTool.BrushColor = color;
        _toolConfigContainer.AddChild(colorPicker);
    }

    private void SetupSelectionToolConfig(SelectionTool selectionTool)
    {
        var infoLabel = new Label
        {
            Text = "Left click: Select\nDrag: Multi-select\nCtrl+Click: Add to selection",
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        _toolConfigContainer.AddChild(infoLabel);
    }

    private void ClearToolConfiguration()
    {
        foreach (Node child in _toolConfigContainer.GetChildren())
        {
            child.QueueFree();
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            // Clean up subscription
            _toolManager?.ActiveToolChanged?.Dispose();
        }
    }
}

/// <summary>
/// Example of extending a tool with additional functionality.
/// Shows how to create tool variants or add features to existing tools.
/// </summary>
public class AdvancedPaintTool : PaintTool
{
    public override string Name => "Advanced Paint";
    
    // Additional properties
    public float Opacity { get; set; } = 1.0f;
    public BlendMode BlendMode { get; set; } = BlendMode.Mix;
    public bool PressureSensitive { get; set; } = false;

    protected override void OnOperationUpdate(CursorMotionData data)
    {
        // Add pressure sensitivity if available
        if (PressureSensitive && data.RawData.Pressure > 0)
        {
            var pressureSize = BrushSize * data.RawData.Pressure;
            // Use pressure-adjusted brush size for this point
        }
        
        base.OnOperationUpdate(data);
    }

    protected override void OnOperationCompleted(Vector2 startPosition, Vector2 endPosition)
    {
        if (_currentStrokeCommand != null && _currentStrokePoints.Count > 1)
        {
            // Create advanced stroke command with additional properties
            var advancedCmd = new AdvancedPaintStrokeCmd(
                _currentStrokePoints, 
                BrushSize, 
                BrushColor, 
                Opacity, 
                BlendMode
            );
            
            advancedCmd.Commit();
            advancedCmd.Free();
            _currentStrokeCommand = null;
        }
        
        ClearStrokePreview();
        _currentStrokePoints.Clear();
    }
}

/// <summary>
/// Example of an advanced command with more parameters.
/// </summary>
public partial class AdvancedPaintStrokeCmd : CommandBase
{
    private readonly List<Vector2> _points;
    private readonly float _brushSize;
    private readonly Color _brushColor;
    private readonly float _opacity;
    private readonly BlendMode _blendMode;

    public AdvancedPaintStrokeCmd(
        List<Vector2> points, 
        float brushSize, 
        Color brushColor, 
        float opacity, 
        BlendMode blendMode)
    {
        _points = new List<Vector2>(points);
        _brushSize = brushSize;
        _brushColor = brushColor;
        _opacity = opacity;
        _blendMode = blendMode;
    }

    public override void Do()
    {
        // Create advanced stroke with additional properties
        var strokeEntity = WorkingWorld.Create();
        
        // Add components with advanced properties
        // strokeEntity.Add(new AdvancedStrokeComponent(
        //     _points, _brushSize, _brushColor, _opacity, _blendMode));
        
        DoRefEntities.Add(strokeEntity);
        GD.Print($"Created advanced stroke: size={_brushSize}, opacity={_opacity}, blend={_blendMode}");
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
        GD.Print("Undid advanced stroke");
    }
}