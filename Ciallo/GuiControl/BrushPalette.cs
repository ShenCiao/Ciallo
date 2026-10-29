using System;
using System.Collections.Generic;
using System.Linq;
using Ciallo.Tool;
using Ciallo.Widget;
using Frent;
using Godot;

namespace Ciallo.GuiControl;

public partial class BrushPalette : ScrollContainer
{
    public event Action<Entity> BrushClicked;
    public readonly DynamicGridItemList Grid;
    public string NullBrushText { get; set; } = "Missing brush";
    private readonly ShapeBrushKind _kind;
    private readonly Dictionary<Entity, Control> _previews = [];
    private Entity[] _brushes = [];
    private Entity[] _selected = [];

    public BrushPalette() : this(ShapeBrushKind.Stroke) { }

    public BrushPalette(ShapeBrushKind kind)
    {
        _kind = kind;
        HorizontalScrollMode = ScrollMode.Disabled;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        CustomMinimumSize = new(0, 200);
        Grid = new DynamicGridItemList
        {
            Name = "Brushes",
            MinRowHeight = kind == ShapeBrushKind.Stroke ? 80 : 60,
            AspectRatio = kind == ShapeBrushKind.Stroke ? 2.667f : 1f,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ReorderEnabled = false,
            ActionMode = BaseButton.ActionModeEnum.Release,
        };
        AddChild(Grid);
        Grid.ItemClicked += index => BrushClicked?.Invoke(_brushes[index]);
    }

    public void SetBrushes(IEnumerable<Entity> brushes)
    {
        _brushes = brushes.ToArray();
        var included = _brushes.ToHashSet();
        Grid.SetSelectedControls([]);
        foreach (var brush in _previews.Keys.Where(brush => !included.Contains(brush)).ToArray())
        {
            var preview = _previews[brush];
            Grid.RemoveChild(preview);
            preview.QueueFree();
            _previews.Remove(brush);
        }
        for (int i = 0; i < _brushes.Length; i++)
        {
            var brush = _brushes[i];
            if (!_previews.TryGetValue(brush, out var preview))
            {
                preview = brush.IsNull ? BrushPreview.Missing(NullBrushText)
                    : _kind == ShapeBrushKind.Stroke ? BrushPreview.Stroke(brush) : BrushPreview.Fill(brush);
                _previews.Add(brush, preview);
                Grid.AddChild(preview);
            }
            Grid.MoveChild(preview, i);
        }
        SelectBrushes(_selected);
    }

    public void SelectBrushes(IEnumerable<Entity> brushes)
    {
        _selected = brushes.ToArray();
        Grid.SetSelectedControls(_selected.Where(_previews.ContainsKey).Select(brush => _previews[brush]));
    }
}
