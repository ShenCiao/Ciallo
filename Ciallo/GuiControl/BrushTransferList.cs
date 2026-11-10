using System;
using System.Collections.Generic;
using System.Linq;
using Ciallo.Data;
using Ciallo.Widget;
using Frent;
using Godot;

namespace Ciallo.GuiControl;

internal sealed record BrushTransferItem(StrokeBrushSetting Setting, Entity DocumentBrush = default);

// Native Godot dragging supplies the threshold, pointer capture, drag preview and Escape cancellation.
internal partial class BrushDragData : RefCounted
{
    public BrushTransferList Source;
    public BrushTransferItem Key;
}

internal partial class BrushTransferList : ScrollContainer
{
    public event Action<BrushTransferItem> Selected;
    public event Action<BrushTransferItem> Activated;
    public event Action<BrushTransferList, BrushTransferItem, int, bool> Dropped;
    public event Action<string> HintChanged;
    public readonly BrushTransferGrid Grid;
    private readonly Dictionary<BrushTransferItem, Control> _previews = [];
    private BrushTransferItem[] _keys = [];
    private BrushTransferItem _selection;
    public BrushTransferItem Selection => _selection;
    public int Count => _keys.Length;
    public bool AcceptTransfers = true;
    private readonly Label _empty;
    private readonly Control _content;
    private readonly Color _hintColor = new(1f, 0.8f, 0.35f);
    private int _slot = -1;
    private bool _replace;
    private bool _hovering;

    public BrushTransferList()
    {
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        HorizontalScrollMode = ScrollMode.Disabled;
        VerticalScrollMode = ScrollMode.Reserve;
        CustomMinimumSize = new(240, 240);
        FocusMode = FocusModeEnum.All;
        // Newly appended previews become scrollable after the container updates its scroll range.
        GetVScrollBar().Changed += () => Callable.From(ScrollToSelection).CallDeferred();
        _content = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        AddChild(_content);
        Grid = new BrushTransferGrid
        {
            OwnerList = this, MouseFilter = MouseFilterEnum.Stop,
            MinRowHeight = 80, MaxRowHeightRatio = 2, AspectRatio = BrushPreview.StrokeAspectRatio,
        };
        _content.AddChild(Grid);
        Grid.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        Grid.MinimumSizeChanged += () => _content.CustomMinimumSize = new(0, Grid.GetCombinedMinimumSize().Y);
        _empty = new Label
        {
            Text = "[Empty Brush List]", AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _content.AddChild(_empty);
        _empty.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        SetDragForwarding(Callable.From<Vector2, Variant>(GetDrag),
            Callable.From<Vector2, Variant, bool>(CanDrop), Callable.From<Vector2, Variant>(Drop));
        _content.SetDragForwarding(Callable.From<Vector2, Variant>(p => GetDrag(p + _content.Position)),
            Callable.From<Vector2, Variant, bool>((p, d) => CanDrop(p + _content.Position, d)),
            Callable.From<Vector2, Variant>((p, d) => Drop(p + _content.Position, d)));
    }

    public void SetItems(IEnumerable<BrushTransferItem> keys)
    {
        int oldIndex = Array.IndexOf(_keys, _selection);
        _keys = keys.ToArray();
        foreach (var key in _previews.Keys.Except(_keys).ToArray())
        {
            var preview = _previews[key];
            Grid.RemoveChild(preview);
            preview.QueueFree();
            _previews.Remove(key);
        }
        for (int i = 0; i < _keys.Length; i++)
        {
            var key = _keys[i];
            if (!_previews.TryGetValue(key, out var preview))
            {
                preview = key.DocumentBrush.IsNull ? BrushPreview.Stroke(key.Setting) : BrushPreview.Stroke(key.DocumentBrush);
                preview.MouseBehaviorRecursive = MouseBehaviorRecursiveEnum.Disabled;
                _previews.Add(key, preview);
                Grid.AddChild(preview);
            }
            Grid.MoveChild(preview, i);
        }
        _empty.Visible = _keys.Length == 0;
        Select(_selection != null && _previews.ContainsKey(_selection) ? _selection
            : oldIndex >= 0 && _keys.Length > 0 ? _keys[Math.Min(oldIndex, _keys.Length - 1)] : null);
        ClearHint();
    }

    public void Select(BrushTransferItem key)
    {
        _selection = key;
        Grid.QueueRedraw();
        Selected?.Invoke(key);
        Callable.From(ScrollToSelection).CallDeferred();
    }

    private void ScrollToSelection()
    {
        if (_selection != null) EnsureControlVisible(_previews[_selection]);
    }

    public BrushTransferItem ItemAt(int index) => _keys[index];
    public int IndexOf(BrushTransferItem item) => Array.IndexOf(_keys, item);

    internal void Click(Vector2 gridPosition, bool doubleClick)
    {
        GrabFocus();
        int index = Hit(gridPosition);
        if (index < 0) return;
        var key = _keys[index];
        Select(key);
        if (doubleClick) Activated?.Invoke(key);
    }

    private int Hit(Vector2 point)
    {
        for (int i = 0; i < _keys.Length; i++)
            if (_previews[_keys[i]].GetRect().HasPoint(point)) return i;
        return -1;
    }

    internal Variant GetDrag(Vector2 localPosition)
    {
        var point = localPosition + GlobalPosition - Grid.GlobalPosition;
        int index = Hit(point);
        if (index < 0) return default;
        GrabFocus();
        Select(_keys[index]);
        var preview = new Label { Text = _keys[index].Setting.Name.Value, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        preview.AddThemeColorOverride("font_color", _hintColor);
        SetDragPreview(preview);
        return new BrushDragData { Source = this, Key = _keys[index] };
    }

    internal bool CanDrop(Vector2 position, Variant data)
    {
        if (!AcceptTransfers || data.VariantType != Variant.Type.Object || data.AsGodotObject() is not BrushDragData drag ||
            !drag.Source._previews.ContainsKey(drag.Key) || !new Rect2(Vector2.Zero, Size).HasPoint(position))
        {
            ClearHint();
            return false;
        }
        Vector2 point = position + GlobalPosition - Grid.GlobalPosition;
        int hit = Hit(point);
        _replace = false;
        _slot = Grid.InsertionSlot(point);
        if (hit >= 0)
        {
            var rect = _previews[_keys[hit]].GetRect();
            // The middle half is replacement; a generous outer band always permits insertion.
            var center = new Rect2(rect.Position + rect.Size * new Vector2(0.15f, 0.25f), rect.Size * new Vector2(0.7f, 0.5f));
            _replace = drag.Source != this && center.HasPoint(point);
            if (_replace) _slot = hit;
        }
        _hovering = true;
        HintChanged?.Invoke(_replace ? "Replace brush" : drag.Source == this ? "Reorder brushes" : "Copy brush here");
        Grid.QueueRedraw();
        return true;
    }

    internal void Drop(Vector2 position, Variant data)
    {
        if (!CanDrop(position, data)) return;
        var drag = (BrushDragData)data.AsGodotObject();
        int slot = _slot;
        bool replace = _replace;
        ClearHint();
        Dropped?.Invoke(drag.Source, drag.Key, slot, replace);
    }

    public void ClearHint()
    {
        _slot = -1;
        _hovering = false;
        Grid.QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!_hovering) return;
        var point = GetLocalMousePosition();
        if (!GetViewport().GuiIsDragging() || !new Rect2(Vector2.Zero, Size).HasPoint(point))
        {
            ClearHint();
            return;
        }
        float edge = 40;
        float speed = point.Y < edge ? -(edge - point.Y) / edge
            : point.Y > Size.Y - edge ? (point.Y - Size.Y + edge) / edge : 0;
        ScrollVertical += (int)(speed * 700 * delta);
        CanDrop(point, GetViewport().GuiGetDragData());
    }

    internal void DrawHints(CanvasItem canvas)
    {
        if (_selection != null && _previews.TryGetValue(_selection, out var selected))
            canvas.DrawRect(selected.GetRect().Grow(-3), new Color(AppPreference.StrokeWireframeColor) { A = 1 }, false, 3);
        if (_slot < 0) return;
        if (_replace)
            canvas.DrawRect(_previews[_keys[_slot]].GetRect().Grow(-5), _hintColor, false, 5);
        else Grid.DrawInsertionHint(canvas, _slot, _hintColor);
    }
}

internal partial class BrushTransferGrid : DynamicGridContainer
{
    public BrushTransferList OwnerList;
    private Node2D _overlay;

    public override void _Ready()
    {
        _overlay = new Node2D { ZIndex = 3 };
        AddChild(_overlay, false, InternalMode.Back);
        _overlay.Draw += () => OwnerList.DrawHints(_overlay);
        Draw += () => _overlay.QueueRedraw();
    }

    public override void _GuiInput(InputEvent input)
    {
        if (input is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mouse)
            OwnerList.Click(mouse.Position, mouse.DoubleClick);
    }

    private Vector2 ToList(Vector2 point) => point + GlobalPosition - OwnerList.GlobalPosition;

    protected override void Resort()
    {
        base.Resort();
        QueueRedraw();
    }

    public int InsertionSlot(Vector2 point) => ComputeDropSlot(point);

    public void DrawInsertionHint(CanvasItem canvas, int slot, Color color)
    {
        var rect = LayoutChildCount == 0 ? new Rect2(0, 0, Size.X, 8)
            : ItemRect(Math.Min(slot, LayoutChildCount - 1));
        bool afterLast = slot == LayoutChildCount && LayoutChildCount > 0;
        Vector2 from, to;
        if (Cols <= 1)
        {
            float y = afterLast ? rect.End.Y - 2 : Mathf.Max(2, rect.Position.Y - VSep / 2f);
            from = new(rect.Position.X + 2, y);
            to = new(rect.End.X - 2, y);
        }
        else
        {
            float x = afterLast ? rect.End.X - 2 : Mathf.Max(2, rect.Position.X - HSep / 2f);
            from = new(x, rect.Position.Y + 2);
            to = new(x, rect.End.Y - 2);
        }
        canvas.DrawLine(from, to, color, 3);
        canvas.DrawCircle(from, 4, color);
    }

    public override Variant _GetDragData(Vector2 position) => OwnerList.GetDrag(ToList(position));
    public override bool _CanDropData(Vector2 position, Variant data) => OwnerList.CanDrop(ToList(position), data);
    public override void _DropData(Vector2 position, Variant data) => OwnerList.Drop(ToList(position), data);
}
