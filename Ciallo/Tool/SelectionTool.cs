using System.Collections.Generic;
using Godot;
using Ciallo.Command;
using Ciallo.NodeControl;
using Ciallo.Data;
using Arch.Core;

namespace Ciallo.Tool;

/// <summary>
/// Selection tool for selecting objects/layers in the canvas.
/// Supports both click selection and drag selection (selection rectangle).
/// </summary>
public class SelectionTool : InteractiveToolBase
{
    public override string Name => "Select";
    public override Input.CursorShape Cursor => Input.CursorShape.Arrow;

    private bool _isDragSelecting;
    private Rect2 _selectionRect;

    protected override void OnMouseButton(MouseButton button, bool pressed, Vector2 position)
    {
        if (button == MouseButton.Left)
        {
            if (pressed)
            {
                var hitEntity = GetEntityAtPosition(position);
                
                if (hitEntity != Entity.Null)
                {
                    // Click selection - select the clicked entity
                    HandleClickSelection(hitEntity, Input.IsKeyPressed(Key.Ctrl));
                }
                else
                {
                    // Start drag selection
                    StartDragSelection(position);
                }
            }
            else if (_isDragSelecting)
            {
                // Complete drag selection
                CompleteDragSelection(position);
            }
        }
        else
        {
            base.OnMouseButton(button, pressed, position);
        }
    }

    protected override void OnOperationUpdate(CursorMotionData data)
    {
        if (_isDragSelecting)
        {
            UpdateSelectionRect(data.WorldPosition);
        }
    }

    private void StartDragSelection(Vector2 startPosition)
    {
        _isDragSelecting = true;
        _selectionRect = new Rect2(startPosition, Vector2.Zero);
        ShowSelectionRect();
    }

    private void UpdateSelectionRect(Vector2 currentPosition)
    {
        var start = _selectionRect.Position;
        var size = currentPosition - start;
        
        // Normalize the rectangle to handle dragging in any direction
        if (size.X < 0)
        {
            _selectionRect.Position = new Vector2(currentPosition.X, start.Y);
            _selectionRect.Size = new Vector2(-size.X, size.Y);
        }
        else
        {
            _selectionRect.Size = new Vector2(size.X, _selectionRect.Size.Y);
        }
        
        if (size.Y < 0)
        {
            _selectionRect.Position = new Vector2(_selectionRect.Position.X, currentPosition.Y);
            _selectionRect.Size = new Vector2(_selectionRect.Size.X, -size.Y);
        }
        else
        {
            _selectionRect.Size = new Vector2(_selectionRect.Size.X, size.Y);
        }
        
        UpdateSelectionRectVisual();
    }

    private void CompleteDragSelection(Vector2 endPosition)
    {
        UpdateSelectionRect(endPosition);
        
        var entitiesInRect = GetEntitiesInRect(_selectionRect);
        var addToSelection = Input.IsKeyPressed(Key.Ctrl);
        
        if (entitiesInRect.Count > 0)
        {
            var cmd = new SelectEntitiesCmd(entitiesInRect, addToSelection);
            cmd.Commit();
            cmd.Free();
        }
        
        _isDragSelecting = false;
        HideSelectionRect();
    }

    private void HandleClickSelection(Entity entity, bool addToSelection)
    {
        var cmd = new SelectEntitiesCmd(new List<Entity> { entity }, addToSelection);
        cmd.Commit();
        cmd.Free();
    }

    protected override void OnOperationCancelled()
    {
        if (_isDragSelecting)
        {
            _isDragSelecting = false;
            HideSelectionRect();
        }
    }

    private Entity GetEntityAtPosition(Vector2 worldPosition)
    {
        // This would query your spatial indexing system or iterate through entities
        // to find what's at the given position
        // Implementation depends on your rendering/collision system
        
        // Placeholder implementation
        GD.Print($"Checking for entity at position {worldPosition}");
        return Entity.Null;
    }

    private List<Entity> GetEntitiesInRect(Rect2 rect)
    {
        // This would query entities that intersect with the selection rectangle
        // Implementation depends on your spatial indexing system
        
        var entities = new List<Entity>();
        GD.Print($"Finding entities in rect {rect}");
        return entities;
    }

    private void ShowSelectionRect()
    {
        // Show visual feedback for the selection rectangle
        GD.Print("Showing selection rectangle");
    }

    private void UpdateSelectionRectVisual()
    {
        // Update the visual representation of the selection rectangle
        GD.Print($"Updating selection rect: {_selectionRect}");
    }

    private void HideSelectionRect()
    {
        // Hide the selection rectangle visual
        GD.Print("Hiding selection rectangle");
    }
}

/// <summary>
/// Command for selecting entities. Integrates with the SelectionManager.
/// </summary>
public partial class SelectEntitiesCmd : CommandBase
{
    private readonly List<Entity> _entitiesToSelect;
    private readonly bool _addToSelection;
    private List<Entity> _previousSelection;

    public SelectEntitiesCmd(List<Entity> entities, bool addToSelection = false)
    {
        _entitiesToSelect = new List<Entity>(entities);
        _addToSelection = addToSelection;
    }

    public override void Do()
    {
        var selectionManager = Document.Get<SelectionManager>();
        
        // Store previous selection for undo
        if (_previousSelection == null)
        {
            _previousSelection = new List<Entity>(selectionManager.SelectedLayers);
        }
        
        if (_addToSelection)
        {
            // Add to existing selection
            foreach (var entity in _entitiesToSelect)
            {
                if (!selectionManager.SelectedLayers.Contains(entity))
                {
                    selectionManager.SelectedLayers.Add(entity);
                }
            }
        }
        else
        {
            // Replace selection
            selectionManager.SelectedLayers.Clear();
            foreach (var entity in _entitiesToSelect)
            {
                selectionManager.SelectedLayers.Add(entity);
            }
        }
        
        // Update working layer if only one entity is selected
        if (selectionManager.SelectedLayers.Count == 1)
        {
            selectionManager.WorkingLayer = selectionManager.SelectedLayers[0];
        }
        
        GD.Print($"Selected {_entitiesToSelect.Count} entities (add: {_addToSelection})");
    }

    public override void Undo()
    {
        var selectionManager = Document.Get<SelectionManager>();
        
        // Restore previous selection
        selectionManager.SelectedLayers.Clear();
        if (_previousSelection != null)
        {
            foreach (var entity in _previousSelection)
            {
                selectionManager.SelectedLayers.Add(entity);
            }
            
            // Restore working layer
            if (_previousSelection.Count == 1)
            {
                selectionManager.WorkingLayer = _previousSelection[0];
            }
            else if (_previousSelection.Count == 0)
            {
                selectionManager.WorkingLayer = Entity.Null;
            }
        }
        
        GD.Print("Restored previous selection");
    }
}