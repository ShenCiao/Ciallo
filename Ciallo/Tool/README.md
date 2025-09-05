# Interactive Tool Framework Design

This document outlines the design of the interactive tool framework for the Ciallo paint software, providing a flexible and extensible system for handling user input and creating undoable commands.

## Architecture Overview

The framework consists of several key components that work together to provide a robust tool system:

```
User Input → WorldInteractiveEventDispatcher → ToolManager → ActiveTool → Commands
                                            ↓
                                     CommandManager ← CommandBase
```

## Core Components

### 1. IInteractiveTool Interface

The foundation of all interactive tools. Defines the contract that all tools must implement:

- **Input Handling**: Mouse motion, button clicks, keyboard input
- **Lifecycle Management**: Activation, deactivation, cancellation
- **State Management**: Update loop for continuous operations
- **UI Integration**: Cursor shape, display name

### 2. ToolManager

Central coordinator for the tool system, available globally:

- **Global Access**: Single instance accessible via `Global.ToolManager`
- **Tool Registration**: Register and unregister tools dynamically
- **Tool Switching**: Activate different tools based on user selection
- **Input Routing**: Forward input events to the active tool
- **State Management**: Handle tool lifecycle and cleanup
- **Cross-Document**: Tool state persists when switching between documents

### 3. InteractiveToolBase

Abstract base class providing common functionality:

- **Operation State**: Tracks whether tool is actively operating (e.g., dragging)
- **Default Behaviors**: Standard mouse and keyboard handling patterns
- **Template Methods**: Structured hooks for operation lifecycle

### 4. WorldInteractiveEventDispatcher Integration

Enhanced to work with the global tool system:

- **Global Tool Access**: Uses the global `ToolManager` instance
- **Input Forwarding**: Routes appropriate events to tools vs. navigation
- **Tool Shortcuts**: Keyboard shortcuts for quick tool switching
- **Navigation Preservation**: Maintains existing pan/zoom functionality
- **Tool Registration**: Registers default tools on first initialization

## Tool Categories

### 1. Immediate Tools (InteractiveToolBase)

Tools that create commands instantly on interaction:
- **Selection Tool**: Click to select, drag to multi-select
- **Basic operations** that don't require complex state management

**Example**: SelectionTool - creates SelectEntitiesCmd on click/drag completion

### 2. Progressive Tools (InteractiveToolBase)

Tools that build up state during interaction:
- **Paint Tool**: Accumulates stroke points while dragging
- **Operations with continuous feedback** and single command creation

**Example**: PaintTool - collects points during drag, creates PaintStrokeCmd on completion

### 3. Shape Tools (ShapeToolBase)

Specialized for geometric shape creation:
- **Click-and-drag** interaction pattern
- **Real-time preview** of shape being created
- **Parameterized shapes** (rectangles, ellipses, etc.)

**Example**: RectangleTool, EllipseTool - preview shape while dragging

### 4. Multi-Point Tools (MultiPointToolBase)

For complex shapes requiring multiple click points:
- **Sequential point collection** with visual feedback
- **Completion triggers** (right-click, Enter key, minimum points)
- **Point management** (add, remove, preview)

**Example**: PolygonTool - collect vertices with click, complete with right-click

## Command Integration

### Command Pattern Integration

Tools create commands that integrate with the existing CommandBase system:

```csharp
// Tool creates command
var command = new PaintStrokeCmd(points, brushSize, color);

// Command integrates with undo system
command.Commit(); // Executes and adds to undo history
// Note: Memory cleanup is handled by UndoRedo system automatically
```

### Command Lifecycle

1. **Tool Operation**: User interacts with tool
2. **State Accumulation**: Tool builds up operation state
3. **Command Creation**: Tool creates appropriate command
4. **Command Execution**: Command.Commit() executes and adds to history
5. **Cleanup**: Tool resets state, command is freed

## Input Event Flow

### Event Routing Priority

1. **Navigation Events**: Middle mouse, wheel → handled by dispatcher
2. **Tool Shortcuts**: Keyboard shortcuts → handled by dispatcher
3. **Tool Events**: Left/right mouse, motion → forwarded to active tool
4. **Tool Keyboard**: Other keys → forwarded to active tool

### Mouse Event Handling

```csharp
// In WorldInteractiveEventDispatcher
if (buttonEvent.ButtonIndex == MouseButton.Middle)
{
    // Handle navigation (pan/zoom)
    HandleNavigation(buttonEvent);
}
else
{
    // Forward to active tool
    ToolManager?.DispatchMouseButton(button, pressed, worldPos);
}
```

## Usage Examples

### Basic Tool Usage

```csharp
// Access the global tool manager
var toolManager = Global.ToolManager;

// Switch tools
toolManager.SetActiveTool("Paint");

// Tool automatically receives input events
// and creates commands as needed
```

### Creating Custom Tools

```csharp
public class CustomTool : InteractiveToolBase
{
    public override string Name => "Custom";
    
    protected override void OnOperationCompleted(Vector2 start, Vector2 end)
    {
        var cmd = new CustomCommand(start, end);
        cmd.Commit();
        // Note: Memory cleanup is handled by UndoRedo system automatically
    }
}
```

### Tool Registration

```csharp
// Tools are registered globally during application startup
// In WorldInteractiveEventDispatcher._Ready() or application initialization
private void RegisterDefaultToolsIfNeeded()
{
    var toolManager = Global.ToolManager;
    
    // Only register if not already registered
    if (!toolManager.GetToolNames().Any())
    {
        toolManager.RegisterTool(new SelectionTool());
        toolManager.RegisterTool(new PaintTool());
        toolManager.RegisterTool(new RectangleTool());
        toolManager.RegisterTool(new PolygonTool());
        
        toolManager.SetActiveTool("Select"); // Default tool
    }
}
}
```

## Extension Points

### 1. Custom Tool Types

Extend the base classes for specific interaction patterns:

```csharp
// For tools with special state management
public abstract class StatefulToolBase : InteractiveToolBase
{
    protected ToolState CurrentState { get; set; }
    // Add state machine logic
}
```

### 2. Tool Parameters

Add configurable parameters to tools:

```csharp
public class PaintTool : InteractiveToolBase
{
    public float BrushSize { get; set; } = 5.0f;
    public Color BrushColor { get; set; } = Colors.Black;
    public BlendMode BlendMode { get; set; } = BlendMode.Normal;
}
```

### 3. Tool UI Integration

Connect tools with UI panels:

```csharp
// Tools can expose properties for UI binding
public interface IConfigurableTool : IInteractiveTool
{
    Control CreateConfigurationPanel();
    void ApplyConfiguration(Dictionary<string, object> settings);
}
```

## Benefits of This Design

### 1. **Separation of Concerns**
- Tools focus on interaction logic
- Commands focus on data manipulation
- Dispatcher handles input routing

### 2. **Extensibility**
- Easy to add new tool types
- Consistent interaction patterns
- Reusable base classes

### 3. **Integration**
- Works with existing command system
- Maintains undo/redo functionality
- Preserves navigation behavior

### 4. **Flexibility**
- Multiple interaction patterns supported
- Tools can be complex or simple
- Easy tool switching and management

### 5. **Maintainability**
- Clear responsibilities
- Consistent patterns
- Well-defined interfaces

## Implementation Notes

### Memory Management

Following the existing Godot object management pattern:
- Tools are not GodotObjects (avoid memory issues)
- Commands inherit from CommandBase (GodotObject)
- Manual Free() calls required for commands

### Performance Considerations

- Tools should be lightweight for frequent input events
- Heavy operations should be deferred to command execution
- Preview rendering should be optimized for real-time feedback

### Error Handling

- Tools should gracefully handle invalid input
- Commands should validate data before execution
- Cancellation should properly clean up state

This framework provides a solid foundation for building interactive tools while maintaining compatibility with the existing Ciallo architecture.