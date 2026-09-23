using Godot;

namespace Ciallo.Widget;

/// <summary>
/// Extends <see cref="DynamicGridContainer"/> with single-selection and drag-reorder support.
/// </summary>
/// <remarks>
/// When a child is dragged to a new position, <see cref="Moved"/> fires with
/// (sourceIndex, destinationIndex).
///
/// If being contained by a ScrollContainer, remember to avoid scrollbar oscillation by setting the
/// </remarks>
[GlobalClass, Tool]
public partial class DynamicGridItemList : DynamicGridContainer
{
    [Signal]
    public delegate void MovedEventHandler(int src, int dst);

    [Signal]
    public delegate void ItemClickedEventHandler(int idx);

    public BaseButton.ActionModeEnum ActionMode { get; set; } = BaseButton.ActionModeEnum.Press;

    public Control SelectedControl
    {
        get;
        set
        {
            field = value;
            UpdateSelectionHighlight();
        }
    }

    // ── drag state ─────────────────────────────────────────────────────────
    private Control _dragSource;
    private bool _isDragging;
    private Vector2 _dragStartGlobalPos;
    private int _dropSlot = -1; // insertion slot ∈ [0, childCount], -1 = none

    private const float DragThreshold = 8f;

    // ── visuals ────────────────────────────────────────────────────────────
    private readonly StrokeRect _selectionHighlight = new()
    {
        Color = new(AppPreference.StrokeWireframeColor) { A = 1.0f },
        Width = 5f,
        MouseFilter = MouseFilterEnum.Ignore,
        Visible = false,
    };
    private readonly ColorRect _dropHintLine = new()
    {
        Color = new(1f, 1f, 0.9f),
        MouseFilter = MouseFilterEnum.Ignore,
        Visible = false,
    };

    public DynamicGridItemList()
    {
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(_selectionHighlight, false, InternalMode.Back);
        AddChild(_dropHintLine, false, InternalMode.Back);
    }

    // ── Godot overrides ────────────────────────────────────────────────────
    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what == NotificationSortChildren)
        {
            UpdateSelectionHighlight();
            UpdateDropHintLine();
        }
        else if (what == NotificationChildOrderChanged)
        {
            EnsureChildMouseFilters();
        }
    }

    // ── visuals helpers ────────────────────────────────────────────────────
    private void UpdateSelectionHighlight()
    {
        if (SelectedControl?.GetParent() == this)
        {
            _selectionHighlight.Visible = true;
            FitChildInRect(_selectionHighlight, SelectedControl.GetRect());
        }
        else
        {
            _selectionHighlight.Visible = false;
        }
    }

    private void UpdateDropHintLine()
    {
        if (!_isDragging || _dropSlot < 0 || _dropSlot > LayoutChildCount)
        {
            _dropHintLine.Visible = false;
            return;
        }
        const float lineWidth = 2f;
        var slotCell = SlotToRowCol(_dropSlot);

        if (Cols == 1)
        {
            // Single-column: horizontal line centred in the vertical separator gap
            float yCentre = slotCell.Y * (ItemSize.Y + VSep) - VSep / 2f;
            _dropHintLine.Position = new Vector2(0f, Mathf.Max(0f, yCentre - lineWidth / 2f));
            _dropHintLine.Size = new Vector2(ItemSize.X, lineWidth);
        }
        else
        {
            // Multi-column: vertical line centred in the horizontal separator gap
            float xCentre = slotCell.X * (ItemSize.X + HSep) - HSep / 2f;
            _dropHintLine.Position = new Vector2(Mathf.Max(0f, xCentre - lineWidth / 2f), slotCell.Y * (ItemSize.Y + VSep));
            _dropHintLine.Size = new Vector2(lineWidth, ItemSize.Y);
        }
        _dropHintLine.Visible = true;
    }

    // ── input ──────────────────────────────────────────────────────────────
    private void EnsureChildMouseFilters()
    {
        foreach (Node child in GetChildren())
            if (child is Control c && !c.TopLevel)
                c.MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _GuiInput(InputEvent et)
    {
        switch (et)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb when mb.IsPressed():
            {
                int idx = HitTestIndex(mb.Position);
                if (idx < 0) return;
                _dragSource = GetLayoutChildren()[idx];
                _dragStartGlobalPos = mb.GlobalPosition;
                _isDragging = false;
                if (ActionMode == BaseButton.ActionModeEnum.Press)
                    EmitSignalItemClicked(idx);
                AcceptEvent();
                break;
            }
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb when mb.IsReleased():
                FinishDrag(mb.Position);
                AcceptEvent();
                break;

            case InputEventMouseMotion { ButtonMask: MouseButtonMask.Left } motion when _dragSource != null:
                if (!_isDragging)
                {
                    if (motion.GlobalPosition.DistanceTo(_dragStartGlobalPos) <= DragThreshold) return;
                    _isDragging = true;
                }
                _dropSlot = ComputeDropSlot(motion.Position);
                UpdateDropHintLine();
                AcceptEvent();
                break;
        }
    }

    private int HitTestIndex(Vector2 localPos)
    {
        if (LayoutChildCount == 0) return -1;

        int col = Mathf.FloorToInt(localPos.X / (ItemSize.X + HSep));
        int row = Mathf.FloorToInt(localPos.Y / (ItemSize.Y + VSep));
        if (col < 0 || row < 0 || col >= Cols) return -1;

        float localX = localPos.X - col * (ItemSize.X + HSep);
        float localY = localPos.Y - row * (ItemSize.Y + VSep);
        if (localX > ItemSize.X || localY > ItemSize.Y) return -1;

        int idx = row * Cols + col;
        return idx >= LayoutChildCount ? -1 : idx;
    }

    private void FinishDrag(Vector2 localPos)
    {
        int clickedIndex = -1;
        if (_dragSource != null && IsInstanceValid(_dragSource))
        {
            var children = GetLayoutChildren();
            int srcIdx = children.IndexOf(_dragSource);
            if (_isDragging)
            {
                int slot = ComputeDropSlot(localPos);
                int dstIdx = slot > srcIdx ? slot - 1 : slot;
                if (srcIdx >= 0 && dstIdx != srcIdx)
                    EmitSignalMoved(srcIdx, dstIdx);
            }
            else if (ActionMode == BaseButton.ActionModeEnum.Release && srcIdx >= 0 && HitTestIndex(localPos) == srcIdx)
            {
                clickedIndex = srcIdx;
            }
        }
        _dragSource = null;
        _isDragging = false;
        _dropSlot = -1;
        UpdateDropHintLine();
        if (clickedIndex >= 0)
            EmitSignalItemClicked(clickedIndex);
    }

    private int ComputeDropSlot(Vector2 localPos)
    {
        if (LayoutChildCount == 0) return 0;

        if (Cols == 1)
        {
            // Single-column: drop positions are above/below each element
            return Mathf.Clamp(Mathf.RoundToInt(localPos.Y / (ItemSize.Y + VSep)), 0, LayoutChildCount);
        }

        int rows = Mathf.CeilToInt((float)LayoutChildCount / Cols);
        int row = Mathf.Clamp(Mathf.FloorToInt(localPos.Y / (ItemSize.Y + VSep)), 0, rows - 1);
        int rowStart = row * Cols;
        int rowItemCnt = Mathf.Min(Cols, LayoutChildCount - rowStart);

        int gapInRow = Mathf.Clamp(Mathf.RoundToInt(localPos.X / (ItemSize.X + HSep)), 0, rowItemCnt);
        return Mathf.Min(rowStart + gapInRow, LayoutChildCount);
    }
}
