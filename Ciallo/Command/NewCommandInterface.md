# NewXXXCmd Interface Design Documentation

## Overview

The `NewXXXCmd` classes (e.g., `NewBrushCmd`, `NewStrokeCmd`, `NewPolylineLayerCmd`) have been refactored to provide a better interface that handles two important scenarios:

1. **Creating a new Entity**: When the command creates a brand new entity from scratch
2. **Working with an existing Entity**: When the command needs to add View/Overlay components to an already-created entity (e.g., during deserialization)

## Problem Statement

Previously, the `Do()` method would always try to add Data layer components (like `ToSerializeTag`) to entities, even if they already existed. This was problematic during deserialization, where entities are loaded with their Data components already present, but still need View and Overlay components to be attached.

## Solution Design

### Constructor Overloads

Each `NewXXXCmd` class now provides two constructors:

#### Pattern 1: Create from Settings
```csharp
// Creates a new command that will create a new entity
public NewBrushCmd(BrushSetting setting = null)
public NewPolylineLayerCmd(PolylineLayerSetting setting = null)
```

#### Pattern 2: Accept Existing Entity
```csharp
// Creates a command for an existing entity
public NewBrushCmd(Entity brushE)
public NewPolylineLayerCmd(Entity layerE)
public NewStrokeCmd(Entity layerE, Entity strokeE)
```

### Idempotent Do() Method

The `Do()` method now checks if components already exist before adding them:

```csharp
public override void Do()
{
    InitEntity();
    
    // Data - only add if not present
    if (!BrushE.Has<ToSerializeTag>())
    {
        BrushE.Add(new ToSerializeTag());
        // ... add to managers, etc.
    }
    
    // Material - only add if not present
    if (!BrushE.Has<BrushMaterial>())
    {
        var material = new BrushMaterial();
        // ... configure and add
    }
    
    // UI - check if already registered
    // ... conditional UI setup
}
```

### InitEntity() Method

The `InitEntity()` method is idempotent - it either creates a new entity or returns the existing one:

```csharp
public Entity InitEntity()
{
    if (BrushE == Entity.Null)
    {
        BrushE = WorkingWorld.Create();
        BrushE.Add(_setting);
    }
    return BrushE;
}
```

## Usage Examples

### Example 1: Creating a New Brush (Original Pattern)

```csharp
// User creates a new brush in the UI
var setting = new BrushSetting();
var cmd = new NewBrushCmd(setting);
cmd.Do();
// cmd.BrushE now contains the newly created entity with all components
```

### Example 2: Deserialization with Existing Entity (New Pattern)

```csharp
// During deserialization, entity already has data components
var existingBrushE = deserializedWorld.Create();
existingBrushE.Add(deserializedSetting);
existingBrushE.Add(new ToSerializeTag());

// Create command with existing entity
var cmd = new NewBrushCmd(existingBrushE);
cmd.Do();
// cmd.BrushE contains the existing entity, now with View/Overlay components added
```

### Example 3: Cloning from Existing Setting (Backward Compatible)

```csharp
// Clone a brush from an existing one
var existingSetting = someEntity.Get<BrushSetting>();
var cmd = new NewBrushCmd(existingSetting);
cmd.Do();
// Creates a new entity with cloned settings
```

## Benefits

1. **Separation of Concerns**: Data layer components vs View/Overlay components are handled separately
2. **Idempotent Operations**: Calling `Do()` multiple times won't cause errors or duplicate components
3. **Serialization Support**: Deserialization can now properly reconstruct entities with all their components
4. **Backward Compatible**: Existing code using the old pattern continues to work
5. **Type Safety**: The compiler ensures correct usage patterns

## Component Layers

The commands now clearly distinguish between three layers:

### Data Layer
- Persisted to disk
- Examples: `ToSerializeTag`, `BrushSetting`, `StrokeGeometry`, `LayerTreeNode`
- Added conditionally only if not present

### View Layer
- Runtime rendering components
- Examples: `BrushMaterial`, `StrokeView`, `PolylineLayerView`
- Created fresh when needed

### Overlay Layer
- Runtime interaction components
- Examples: `StrokeOverlay`, `PolylineLayerOverlay`
- Created fresh when needed

## Testing

Unit tests have been added in `TestNewCommands.cs` to validate:
- Creating new entities from scratch
- Working with pre-existing entities
- Idempotent behavior of `Do()` method
- Correct component attachment for all scenarios

## Migration Guide

### For New Code

When deserializing or working with existing entities:
```csharp
// OLD (workaround approach)
var setting = existingEntity.Get<BrushSetting>();
var cmd = new NewBrushCmd(setting);
cmd.Do();

// NEW (preferred approach)
var cmd = new NewBrushCmd(existingEntity);
cmd.Do();
```

### For Existing Code

No changes required! The old pattern continues to work as before:
```csharp
var cmd = new NewBrushCmd(optionalSetting);
cmd.Do();
// Works exactly as before
```

## Future Improvements

Potential enhancements for consideration:
1. Extract common patterns into a base class or interface
2. Add validation to ensure entities have required components before `Do()`
3. Consider builder pattern for more complex entity construction
4. Add events or callbacks for component addition
