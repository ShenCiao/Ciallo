using System.Collections.Generic;
using Godot;

namespace Ciallo.Widget;

/// <summary>
/// A pure layout container: like GridContainer but with dynamic row height and fixed aspect-ratio scaling.
/// </summary>
/// <remarks>
/// Assumes all children controls have the same natural aspect ratio.
///
/// When resizing, children scale uniformly. Once they would fall below MinRowHeight the column
/// count decreases (fewer columns → larger items).
/// Once the container width exceed MinRowHeight*current column count, column count increases.
///
/// Grid height depends on width; auto scrollbars can toggle forever at the overflow threshold.
/// When hosted in a <see cref="ScrollContainer"/>, set
/// <see cref="ScrollContainer.VerticalScrollMode"/> to <see cref="ScrollContainer.ScrollMode.Reserve"/>
/// so showing or hiding the scrollbar does not change the grid width.
/// </remarks>
[GlobalClass, Tool]
public partial class DynamicGridContainer : Container
{
    [Export] public int MinRowHeight
    {
        get;
        set
        {
            field = value;
            QueueSort();
        }
    } = 48;

    /// <summary>Actual max row height = MaxRowHeightRatio * MinRowHeight. Set to 0 to disable.</summary>
    [Export] public float MaxRowHeightRatio
    {
        get;
        set
        {
            field = value;
            QueueSort();
        }
    } = 2.0f;

    [Export] public float AspectRatio
    {
        get;
        set
        {
            field = value;
            QueueSort();
        }
    } = 1.0f;

    // ── cached layout state (set in Resort) ────────────────────────────────
    protected int Cols;
    protected Vector2 ItemSize;
    protected int LayoutChildCount;
    protected int HSep;
    protected int VSep;

    protected int HSeparation => GetThemeConstant("h_separation", "GridContainer");
    protected int VSeparation => GetThemeConstant("v_separation", "GridContainer");

    // ── Godot overrides ────────────────────────────────────────────────────
    public override void _Notification(int what)
    {
        if (what == NotificationSortChildren)
            Resort();
    }

    public override Vector2 _GetMinimumSize()
    {
        if (LayoutChildCount == 0 || Cols == 0)
            return Vector2.Zero;
        int rows = Mathf.CeilToInt((float)LayoutChildCount / Cols);
        return new(0f, Mathf.CeilToInt(rows * ItemSize.Y + (rows - 1) * VSep));
    }

    // ── layout ─────────────────────────────────────────────────────────────
    protected List<Control> GetLayoutChildren()
    {
        var result = new List<Control>();
        foreach (Node child in GetChildren())
            if (child is Control { TopLevel: false, Visible: true } c)
                result.Add(c);
        return result;
    }

    protected (int Cols, Vector2 ItemSize) CalcLayout(List<Control> children)
    {
        float w = Size.X;
        if (w <= 0f || children.Count == 0)
            return (1, new Vector2(Mathf.Max(w, 1f), MinRowHeight));

        float ar = AspectRatio > 0f ? AspectRatio : 1f;

        int colsMax = Mathf.FloorToInt((w + HSep) / (MinRowHeight * ar + HSep));
        int cols = Mathf.Clamp(colsMax, 1, children.Count);

        float iw = (w - (cols - 1) * HSep) / cols;
        float ih = iw / ar;

        // Clamp item height to MaxRowHeightRatio * MinRowHeight; shrink width proportionally.
        float maxRowHeight = MaxRowHeightRatio * MinRowHeight;
        if (MaxRowHeightRatio > 0f && ih > maxRowHeight)
        {
            ih = maxRowHeight;
            iw = ih * ar;
        }

        return (cols, new(iw, ih));
    }

    protected virtual void Resort()
    {
        HSep = HSeparation;
        VSep = VSeparation;
        var children = GetLayoutChildren();
        if (children.Count == 0)
        {
            Cols = 0;
            ItemSize = Vector2.Zero;
            LayoutChildCount = 0;
            CustomMinimumSize = Vector2.Zero;
            return;
        }

        (Cols, ItemSize) = CalcLayout(children);
        LayoutChildCount = children.Count;

        for (int i = 0; i < children.Count; i++)
        {
            int col = i % Cols;
            int row = i / Cols;
            FitChildInRect(children[i], new Rect2(
                col * (ItemSize.X + HSep),
                row * (ItemSize.Y + VSep),
                ItemSize.X, ItemSize.Y));
        }

        int rows = Mathf.CeilToInt((float)LayoutChildCount / Cols);
        CustomMinimumSize = new Vector2(0f, rows * ItemSize.Y + (rows - 1) * VSep);
    }

    // ── helpers ─────────────────────────────────────────────────────────────
    protected Rect2 ItemRect(int slotIdx)
    {
        var cell = SlotToRowCol(slotIdx);
        return new Rect2(cell.X * (ItemSize.X + HSep), cell.Y * (ItemSize.Y + VSep), ItemSize.X, ItemSize.Y);
    }

    /// <summary>Maps an insertion slot index to a (col, row) cell coordinate for drawing.</summary>
    protected Vector2I SlotToRowCol(int slot)
    {
        if (slot >= LayoutChildCount)
        {
            int last = LayoutChildCount - 1;
            int r = last / Cols;
            int c = last % Cols + 1;
            if (c >= Cols)
            {
                c = 0;
                r++;
            }
            return new(c, r);
        }
        return new(slot % Cols, slot / Cols);
    }
}
