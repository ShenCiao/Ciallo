using System;
using Ciallo.Data;
using Ciallo.Rendering;
using Frent;
using Godot;
using ObservableCollections;

namespace Ciallo.Tool;

[RegisterState]
public class PolylineRectSelectInteractor : CapturingInteraction
{
    private StrokeView _boxSelectionDash;
    private Rect2 _boxSelectionRect;
    private Entity[] _baseSelection;
    private ObservableList<Entity> _selectedShapes;
    private Entity _initialHoveredShape;

    private void ToggleSelectionWireframe(bool visible)
    {
        foreach (var e in _selectedShapes)
            e.Get<PolylineWireframe>().Visible = visible;
    }

    public override void BeforeSourceExit(Interaction src)
    {
        if (src is PolylineNoSelectionHover hover)
        {
            _initialHoveredShape = hover.CurrHoveredShape;
        }
    }

    public override void Start(CursorButtonData data)
    {
        _boxSelectionDash = new StrokeView();
        _boxSelectionDash.Material = AutoloadRendering.DashWireframeMaterial;
        Document.Get<WorldOverlay>().AddChild(_boxSelectionDash);
        _boxSelectionRect.Position = data.WorldPosition;
        _boxSelectionRect.Size = Vector2.Zero;
        _selectedShapes = Document.Get<SelectionManager>().SelectedShapes;
        _baseSelection = [.. _selectedShapes];
        if (!Input.IsKeyPressed(Key.Shift))
            _selectedShapes.Clear();
        if (!_initialHoveredShape.IsNull && !_selectedShapes.Remove(_initialHoveredShape))
            _selectedShapes.Add(_initialHoveredShape);
        ToggleSelectionWireframe(true);
    }

    public override void Moving(CursorMotionData data)
    {
        _boxSelectionRect.Size = data.WorldPosition - _boxSelectionRect.Position;
        var points = _boxSelectionRect.GetCorners();
        _boxSelectionDash.SetGeometry([.. points, points[0]], AppPreference.StrokeWireframeRadius);

        // Selection
        var es = Document.Get<WorldBody>().RectQuery(_boxSelectionRect);

        ToggleSelectionWireframe(false);

        if (Input.IsKeyPressed(Key.Shift))
        {
            // XOR: base ∆ rectResults
            _selectedShapes.Clear();
            foreach (var e in _baseSelection)
            {
                if (!es.Contains(e))
                    _selectedShapes.Add(e);
            }
            foreach (var e in es)
            {
                if (Array.IndexOf(_baseSelection, e) < 0)
                    _selectedShapes.Add(e);
            }
        }
        else
        {
            _selectedShapes.Clear();
            _selectedShapes.AddRange(es);
        }

        ToggleSelectionWireframe(true);
    }

    public override void End(CursorButtonData data) => Clear();

    public override void Cancel()
    {
        _selectedShapes.Clear();
        _selectedShapes.AddRange(_baseSelection);

        Clear();
    }

    public void Clear()
    {
        _initialHoveredShape = Entity.Null;
        ToggleSelectionWireframe(false);
        _boxSelectionDash.QueueFree();
        _boxSelectionDash = null;
        _baseSelection = null;
        _selectedShapes = null;
    }
}
