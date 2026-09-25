using System.Collections.Generic;
using System.Linq;
using Ciallo.Data;
using Ciallo.Widget;
using Godot;
using ObservableCollections;
using R3;

namespace Ciallo.GuiControl;

/// <summary>
/// Popup palette over <see cref="AppMarkerTextureLibrary.Markers"/>: a thumbnail grid plus import/remove.
/// Clicking a marker emits <see cref="Picked"/> and closes. Built-in markers cannot be removed.
/// Driven by <see cref="MarkerPickerButton"/>; not meant to be used standalone.
/// </summary>
public partial class MarkerPickerPopup : PopupPanel
{
    // Dark backdrop so the white marker silhouettes are visible.
    private static readonly Color BackdropColor = new(0.12f, 0.12f, 0.12f);
    private static readonly Color CellColor = new(0.18f, 0.18f, 0.18f);

    public readonly Subject<ImageTexture> Picked = new();

    private readonly DynamicGridItemList _grid;
    private readonly FileDialog _fileDialog;
    private ISynchronizedView<AppMarkerTextureLibrary.MarkerEntry, Control> _syncView;
    private readonly Dictionary<AppMarkerTextureLibrary.MarkerEntry, Control> _previewMap = [];
    private readonly CompositeDisposable _subs = new();

    private AppMarkerTextureLibrary.MarkerEntry _selectedEntry;

    public MarkerPickerPopup()
    {
        TransparentBg = false;

        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
            margin.AddThemeConstantOverride(side, 8);
        AddChild(margin);

        var root = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        margin.AddChild(root);

        var buttonRow = new HBoxContainer();
        root.AddChild(buttonRow);

        var importButton = new Button
        {
            Icon = GD.Load<Texture2D>("res://Icon/plus.svg"),
            ExpandIcon = true,
            CustomMinimumSize = new(30, 30),
            TooltipText = "Import marker",
        };
        buttonRow.AddChild(importButton);

        var removeButton = new Button
        {
            Icon = GD.Load<Texture2D>("res://Icon/minus.svg"),
            ExpandIcon = true,
            CustomMinimumSize = new(30, 30),
            TooltipText = "Remove marker",
        };
        buttonRow.AddChild(removeButton);

        var backdrop = new PanelContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        backdrop.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = BackdropColor });
        root.AddChild(backdrop);

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new(280, 280),
        };
        backdrop.AddChild(scroll);

        _grid = new DynamicGridItemList
        {
            MinRowHeight = 56,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        scroll.AddChild(_grid);

        _fileDialog = new FileDialog
        {
            Title = "Open a File",
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Filters = [".jpg,*.jpeg,*.png,*.webp,*.tga,*.bmp,*.dds,*.ktx,*.exr,*.hdr,*"],
            UseNativeDialog = true,
            InitialPosition = Window.WindowInitialPosition.CenterScreenWithMouseFocus,
        };
        AddChild(_fileDialog);

        importButton.Pressed += () => _fileDialog.Popup();
        removeButton.Pressed += OnRemovePressed;
        _fileDialog.FileSelected += OnFileSelected;

        _syncView = AppMarkerTextureLibrary.Markers.CreateView(GetOrCreatePreview);
        _syncView.AddTo(_subs);
        _grid.ObserveChildren(_syncView.ToNotifyCollectionChanged());

        _grid.SignalAsObservable<int>(DynamicGridItemList.SignalName.ItemClicked)
            .Subscribe(OnItemClicked)
            .AddTo(_subs);

        // Free preview controls of removed markers; ObserveChildren only detaches them.
        AppMarkerTextureLibrary.Markers.ObserveChanged()
            .Subscribe(e =>
            {
                if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Remove)
                    return;
                if (_previewMap.Remove(e.OldItem, out var control))
                    control.QueueFree();
                if (e.OldItem == _selectedEntry)
                {
                    _selectedEntry = null;
                    _grid.SelectedControl = null;
                }
            })
            .AddTo(_subs);
    }

    /// <summary>Highlights the entry whose texture matches <paramref name="current"/>, if any.</summary>
    public void SyncSelection(ImageTexture current)
    {
        _selectedEntry = AppMarkerTextureLibrary.Markers.FirstOrDefault(m => m.Texture == current);
        _grid.SelectedControl = _selectedEntry == null ? null : GetOrCreatePreview(_selectedEntry);
    }

    private void OnItemClicked(int idx)
    {
        var entry = _syncView.Filtered.ElementAt(idx).Value;
        _selectedEntry = entry;
        _grid.SelectedControl = GetOrCreatePreview(entry);
        Picked.OnNext(entry.Texture);
        Hide();
    }

    private void OnRemovePressed()
    {
        if (_selectedEntry is not { IsBuiltIn: false })
            return;
        AppMarkerTextureLibrary.Markers.Remove(_selectedEntry);
    }

    private void OnFileSelected(string path)
    {
        var image = Image.LoadFromFile(path);
        if (image == null || image.IsEmpty())
        {
            AppDialogHost.WarnUser.DialogText = "[Cannot Load Image]";
            AppDialogHost.WarnUser.Popup();
            return;
        }

        var texture = AppMarkerTextureLibrary.Import(image);
        if (texture == null) return;

        _selectedEntry = AppMarkerTextureLibrary.Markers[^1];
        _grid.SelectedControl = GetOrCreatePreview(_selectedEntry);
        Picked.OnNext(texture);
    }

    private Control GetOrCreatePreview(AppMarkerTextureLibrary.MarkerEntry entry)
    {
        if (_previewMap.TryGetValue(entry, out var box))
            return box;
        box = CreatePreview(entry);
        _previewMap.Add(entry, box);
        return box;
    }

    private static Control CreatePreview(AppMarkerTextureLibrary.MarkerEntry entry)
    {
        var box = new PanelContainer();
        box.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = CellColor });

        var markerRect = new TextureRect
        {
            Texture = entry.Texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        box.AddChild(markerRect);
        return box;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            _syncView?.Dispose();
            _subs.Dispose();
        }
    }
}
