using System.Linq;
using Ciallo.Data;
using Frent;
using Godot;
using ObservableCollections;
using R3;

namespace Ciallo.GuiControl;

public partial class BrushPanel : AcceptDialog
{
    private Entity _document;
    private BrushTransferList _left, _right;
    private Control _documentPane, _transferButtons;
    private Button _toDocument, _toLibrary;
    private Button _leftCopy, _leftDelete, _leftEdit, _rightCopy, _rightDelete, _rightEdit;
    private Label _hint;
    private CommandManager _history;
    private StrokeBrushEditor _editor;
    private readonly SerialDisposable _documentSubscription = new();

    public override void _Ready()
    {
        Title = "Manage brushes";
        GetOkButton().Text = "Close";
        _editor = StrokeBrushEditor.New(Entity.Null);
        _editor.Name = "BrushSettings";
        AddChild(_editor);
        var layout = new VBoxContainer();
        AddChild(layout);
        var panes = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        panes.AddThemeConstantOverride("separation", 16);
        layout.AddChild(panes);
        _left = new BrushTransferList { Name = "DocumentBrushes" };
        _documentPane = CreatePane(panes, "Document brushes", "[Document Brushes Scope]", _left,
            out _, out _leftCopy, out _leftDelete, out _leftEdit);
        _documentPane.Name = "DocumentPane";
        var middle = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        _transferButtons = middle;
        middle.Name = "TransferButtons";
        panes.AddChild(middle);
        _toDocument = new Button { Name = "AddToDocument", Text = "Add to document", Icon = GD.Load<Texture2D>("res://Icon/arrow-left.svg") };
        _toLibrary = new Button { Name = "SaveToLibrary", Text = "Save to library", Icon = GD.Load<Texture2D>("res://Icon/arrow-right.svg"), IconAlignment = HorizontalAlignment.Right };
        _toDocument.AddThemeConstantOverride("icon_max_width", 24);
        _toLibrary.AddThemeConstantOverride("icon_max_width", 24);
        middle.AddChild(_toDocument);
        middle.AddChild(_toLibrary);
        _right = new BrushTransferList { Name = "LibraryBrushes" };
        CreatePane(panes, "Application brush library", "[Library Brushes Scope]", _right,
            out var libraryButtons, out _rightCopy, out _rightDelete, out _rightEdit);
        var add = IconButton(libraryButtons, "plus", "New brush");
        add.Name = "NewBrush";
        libraryButtons.MoveChild(add, 0);
        add.Pressed += () => _right.Select(new(StrokeBrushActions.AddToLibrary(
            new StrokeBrushSetting { Name = { Value = "New brush".Tr() } }, _right.Count, _history)));
        var restore = IconButton(libraryButtons, "restore", "Restore built-in brushes");
        restore.Name = "RestoreBuiltIns";
        restore.Pressed += () => StrokeBrushActions.RestoreBuiltIns(_history);
        _hint = new Label { Text = "[Brush Transfer Hint]", Size = new(1000, 48), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        layout.AddChild(_hint);
        _left.Selected += _ => UpdateButtons();
        _right.Selected += _ => UpdateButtons();
        _left.Activated += key => CopyTo(_right, key, _right.Count);
        _right.Activated += key =>
        {
            if (!_document.IsNull) CopyTo(_left, key, _left.Count);
        };
        _left.HintChanged += text => _hint.Text = text;
        _right.HintChanged += text => _hint.Text = text;
        _left.Dropped += (source, key, slot, replace) => Drop(_left, source, key, slot, replace);
        _right.Dropped += (source, key, slot, replace) => Drop(_right, source, key, slot, replace);
        _toDocument.Pressed += () => CopyTo(_left, _right.Selection, _left.Count);
        _toLibrary.Pressed += () => CopyTo(_right, _left.Selection, _right.Count);
        _leftCopy.Pressed += () => CopyTo(_left, _left.Selection, _left.Count);
        _rightCopy.Pressed += () => CopyTo(_right, _right.Selection, _right.Count);
        _leftDelete.Pressed += () => StrokeBrushActions.DeleteDocumentBrush(_document, _left.Selection.DocumentBrush);
        _rightDelete.Pressed += () => StrokeBrushActions.DeleteLibraryBrush(_right.Selection.Setting, _history);
        _leftEdit.Pressed += () => Edit(false);
        _rightEdit.Pressed += () => Edit(true);
        AppStrokeBrushLibrary.BrushSettings.ObserveChanged().Subscribe(_ => RefreshLibrary()).AddTo(this);
        AppDocumentManager.WorkingDocument.Subscribe(BindDocument).AddTo(this);
        _documentSubscription.AddTo(this);
        RefreshLibrary();
        AboutToPopup += () =>
        {
            Size = new(_document.IsNull ? 520 : 1040, 680);
            _hint.Text = "[Brush Transfer Hint]";
            if (!_document.IsNull)
            {
                var working = _document.Get<SelectionManager>().WorkingStrokeBrush.Value;
                if (!working.IsNull) _left.Select(new(working.Get<StrokeBrushSetting>(), working));
            }
        };
    }

    private static VBoxContainer CreatePane(Container parent, string title, string scope, BrushTransferList list,
        out VBoxContainer buttons, out Button copy, out Button remove, out Button edit)
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        parent.AddChild(box);
        var heading = new Label { Text = title };
        heading.AddThemeFontSizeOverride("font_size", 22);
        box.AddChild(heading);
        var description = new Label { Text = scope, Size = new(300, 66), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new(0, 66) };
        description.AddThemeFontSizeOverride("font_size", 17);
        box.AddChild(description);
        var content = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        box.AddChild(content);
        buttons = new VBoxContainer { Name = "Actions" };
        content.AddChild(buttons);
        copy = IconButton(buttons, "content-copy", "Duplicate brush");
        remove = IconButton(buttons, "minus", "Delete brush");
        edit = IconButton(buttons, "cog", "Brush settings");
        edit.Name = "BrushSettings";
        content.AddChild(list);
        return box;
    }

    private static Button IconButton(Container parent, string icon, string tooltip)
    {
        var button = new Button
        {
            Icon = GD.Load<Texture2D>($"res://Icon/{icon}.svg"), ExpandIcon = true,
            CustomMinimumSize = new(32, 32), TooltipText = tooltip, ThemeTypeVariation = "IconToggleButton",
        };
        parent.AddChild(button);
        return button;
    }

    private void BindDocument(Entity document)
    {
        _editor.Hide();
        _documentSubscription.Disposable = null;
        _document = document;
        _history = document.IsNull ? new CommandManager() : document.Get<CommandManager>();
        _documentPane.Visible = _transferButtons.Visible = _hint.Visible = !document.IsNull;
        MinSize = new(document.IsNull ? 420 : 800, 480);
        Size = new(document.IsNull ? 520 : 1040, 680);
        _left.AcceptTransfers = !document.IsNull;
        if (document.IsNull) _left.SetItems([]);
        else
        {
            RefreshDocument();
            _documentSubscription.Disposable = document.Get<BrushManager>().StrokeBrushes.ObserveChanged().Subscribe(_ => RefreshDocument());
        }
        UpdateButtons();
    }

    private void RefreshDocument() => _left.SetItems(_document.Get<BrushManager>().StrokeBrushes
        .Select(e => new BrushTransferItem(e.Get<StrokeBrushSetting>(), e)));
    private void RefreshLibrary() => _right.SetItems(AppStrokeBrushLibrary.BrushSettings.Select(s => new BrushTransferItem(s)));

    private void CopyTo(BrushTransferList target, BrushTransferItem item, int index)
    {
        if (target == _left)
        {
            var brush = StrokeBrushActions.AddToDocument(_document, item.Setting, index);
            target.Select(new(brush.Get<StrokeBrushSetting>(), brush));
        }
        else target.Select(new(StrokeBrushActions.AddToLibrary(item.Setting, index, _history)));
    }

    private void Drop(BrushTransferList target, BrushTransferList source, BrushTransferItem item, int slot, bool replace)
    {
        if (source == target)
        {
            int from = target.IndexOf(item);
            int to = slot > from ? slot - 1 : slot;
            if (target == _left) StrokeBrushActions.MoveDocumentBrush(_document, from, to);
            else StrokeBrushActions.MoveLibraryBrush(from, to, _history);
        }
        else if (replace)
        {
            var replaced = target.ItemAt(slot);
            if (target == _left) StrokeBrushActions.ReplaceDocumentBrush(replaced.DocumentBrush, item.Setting);
            else StrokeBrushActions.ReplaceLibraryBrush(replaced.Setting, item.Setting, _history);
            target.Select(replaced);
        }
        else CopyTo(target, item, slot);
        _hint.Text = "[Brush Transfer Hint]";
    }

    private void Edit(bool library)
    {
        _editor.InitSetting((library ? _right : _left).Selection.Setting,
            _document, _history, library);
        _editor.CallDeferred(Window.MethodName.PopupCenteredClamped);
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        bool dragging = GuiIsDragging();
        DialogCloseOnEscape = !dragging;
        if (!dragging) _hint.Text = "[Brush Transfer Hint]";
    }

    public override void _UnhandledKeyInput(InputEvent input)
    {
        bool undo = AppHotkeys.Global.EditUndo.IsPressedBy(input);
        bool redo = AppHotkeys.Global.EditRedo.IsPressedBy(input);
        if (!undo && !redo) return;
        if (redo) _history.Redo(); else _history.Undo();
        SetInputAsHandled();
    }

    private void UpdateButtons()
    {
        bool left = _left.Selection != null && !_document.IsNull;
        bool right = _right.Selection != null;
        _leftCopy.Disabled = _leftDelete.Disabled = _leftEdit.Disabled = !left;
        _rightCopy.Disabled = _rightDelete.Disabled = _rightEdit.Disabled = !right;
        _toDocument.Disabled = !right || _document.IsNull;
        _toLibrary.Disabled = !left;
    }
}
