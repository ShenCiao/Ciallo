using Ciallo.Command;
using Ciallo.Data;
using Frent;
using Godot;

namespace Ciallo.GuiControl;

public partial class CelTrack
{
    private LineEdit _celNameEditor;
    private Entity _renamingCel;
    private int _renamingFrame;

    private readonly record struct CelLabel(Vector2 Baseline, float MaxWidth, Rect2 VisibleText);

    /// <summary>Shared geometry for the painted label, its hit area, and the temporary editor.</summary>
    private bool TryGetCelLabel(int index, out CelLabel label)
    {
        label = default;
        if (_exposures.GetValueAtIndex(index).IsCelFolder) return false;
        label = GetCelLabel(index);
        return label.VisibleText.HasArea();
    }

    private CelLabel GetCelLabel(int index)
    {
        var cel = _exposures.GetValueAtIndex(index);
        float x = FrameToX(_exposures.GetKeyAtIndex(index)) + CelButtonWidth + LabelPad;
        float end = index + 1 < _exposures.Count
            ? FrameToX(_exposures.GetKeyAtIndex(index + 1)) - SpanArrowHeadLength - LabelPad
            : Size.X;
        float maxWidth = Mathf.Max(0f, end - x);

        string name = cel.Get<CommonLayerSetting>().Name.Value;
        float textWidth = LabelFont.GetStringSize(name, HorizontalAlignment.Left, -1, LabelFontSize).X;
        float left = Mathf.Clamp(x, 0f, Size.X);
        float right = Mathf.Clamp(x + Mathf.Min(maxWidth, textWidth), left, Size.X);

        float baseline = Size.Y * 0.5f + LabelFontSize * 0.35f;
        float top = Mathf.Max(0f, baseline - LabelFont.GetAscent(LabelFontSize));
        float bottom = Mathf.Min(Size.Y, baseline + LabelFont.GetDescent(LabelFontSize));
        return new(new(x, baseline), maxWidth, new(left, top, right - left, bottom - top));
    }

    private bool TryBeginCelRename(Vector2 position)
    {
        if (_exposures == null || _pixelsPerFrame <= 0f) return false;
        for (int i = 0; i < _exposures.Count; i++)
        {
            if (!TryGetCelLabel(i, out var label)) continue;
            float left = Mathf.Max(0f, FrameToX(_exposures.GetKeyAtIndex(i)) + CelButtonWidth);
            float right = Mathf.Min(Size.X, label.VisibleText.End.X + LabelPad);
            if (i + 1 < _exposures.Count)
                right = Mathf.Min(right, FrameToX(_exposures.GetKeyAtIndex(i + 1)) - CelButtonWidth);
            if (right <= left || !new Rect2(left, 0f, right - left, Size.Y).HasPoint(position)) continue;

            BeginCelRename(_exposures.GetKeyAtIndex(i));
            return true;
        }
        return false;
    }

    /// <summary>Opens the same inline editor for a name double-click or the cel context menu.</summary>
    internal void BeginCelRename(int frame)
    {
        if (!IsVisibleInTree() || !_exposures.TryGetValue(frame, out var cel) || cel.IsCelFolder) return;
        FinishCelRename(commit: true);
        ResetSpanDrag();
        _pressedFrame = null;
        _isCelButtonDragging = false;
        _celButtonDragSourceFrame = null;
        _celButtonDragTargetFrame = null;
        _renamingCel = cel;
        _renamingFrame = frame;
        var editor = new LineEdit
        {
            Name = "CelNameEditor",
            Text = _renamingCel.Get<CommonLayerSetting>().Name.Value,
            MouseFilter = MouseFilterEnum.Stop,
            SelectAllOnFocus = true,
        };
        _celNameEditor = editor;
        editor.TextSubmitted += _ =>
        {
            if (_celNameEditor == editor) FinishCelRename(commit: true);
        };
        editor.FocusExited += () =>
        {
            if (_celNameEditor == editor) FinishCelRename(commit: true);
        };
        editor.TreeExiting += () =>
        {
            // Tree teardown releases focus while the parent cannot remove children.
            if (_celNameEditor != editor) return;
            _celNameEditor = null;
            _renamingCel = Entity.Null;
            editor.QueueFree();
        };
        editor.GuiInput += input =>
        {
            if (!AppHotkeys.UiCancel.IsPressedBy(input)) return;
            editor.AcceptEvent();
            FinishCelRename(commit: false);
        };
        AddChild(editor);
        UpdateCelNameEditor();
        if (_celNameEditor != editor) return;
        editor.GrabFocus();
        editor.Edit();
        editor.SelectAll();
        QueueRedraw();
    }

    private void UpdateCelNameEditor()
    {
        if (_celNameEditor == null) return;
        if (!_exposures.TryGetValue(_renamingFrame, out var cel) || cel != _renamingCel)
        {
            FinishCelRename(commit: false);
            return;
        }

        var label = GetCelLabel(_exposures.FloorIndex(_renamingFrame));
        float buttonX = FrameToX(_renamingFrame);
        if (!label.VisibleText.HasArea() && (buttonX + CelButtonWidth <= 0f || buttonX >= Size.X))
        {
            FinishCelRename(commit: false);
            return;
        }

        _celNameEditor.AddThemeFontOverride("font", LabelFont);
        _celNameEditor.AddThemeFontSizeOverride("font_size", LabelFontSize);
        var style = _celNameEditor.GetThemeStylebox("normal");
        float desired = Mathf.Max(_celNameEditor.GetMinimumSize().X,
            Mathf.Max(120f, label.VisibleText.Size.X + style.GetMinimumSize().X));
        float width = Mathf.Min(Size.X, desired);
        float left = Mathf.Clamp(label.VisibleText.Position.X - style.GetContentMargin(Side.Left), 0f, Size.X - width);
        float height = Mathf.Max(_celNameEditor.GetMinimumSize().Y, label.VisibleText.Size.Y + style.GetMinimumSize().Y);
        _celNameEditor.Size = new(width, height);
        _celNameEditor.Position = new(left, Mathf.Max(0f, (Size.Y - height) * 0.5f));
    }

    private void FinishCelRename(bool commit)
    {
        if (_celNameEditor == null) return;
        var editor = _celNameEditor;
        var cel = _renamingCel;
        string name = editor.Text;
        // Clear first: hiding a focused editor also emits FocusExited.
        _celNameEditor = null;
        _renamingCel = Entity.Null;
        editor.Hide();
        RemoveChild(editor);
        editor.QueueFree();
        QueueRedraw();

        if (!commit || cel.IsDyingOrDead || _celFolderEntity.IsDyingOrDead
            || !_exposures.TryGetValue(_renamingFrame, out var exposed) || exposed != cel)
            return;
        var property = cel.Get<CommonLayerSetting>().Name;
        if (property.Value != name)
            new CommandBuilder("Rename Cel", cel).SetProperty(property, name).Commit();
    }
}
