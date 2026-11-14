using System;
using System.Linq;
using Ciallo.Data;
using Ciallo.Tool;
using Frent;
using Godot;
using ObservableCollections;
using R3;

namespace Ciallo.GuiControl;

public partial class SelectionBrushPanel : VBoxContainer
{
    public SelectionBrushPanel() { }

    public SelectionBrushPanel(Entity document, ShapeBrushKind kind, Action selectionChanged)
    {
        Name = kind == ShapeBrushKind.Stroke ? "StrokeBrushSelection" : "FillBrushSelection";
        var model = new ShapeBrushSelection(document, kind).AddTo(this);
        var heading = new HBoxContainer();
        AddChild(heading);
        heading.AddChild(new Label
        {
            Text = kind == ShapeBrushKind.Stroke ? "Stroke brushes" : "Fill brushes",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        });
        if (kind == ShapeBrushKind.Stroke)
        {
            var manage = new Button
            {
                Name = "ManageBrushes",
                Icon = GD.Load<Texture2D>("res://Icon/plus.svg"),
                ExpandIcon = true,
                CustomMinimumSize = new(30, 30),
                ThemeTypeVariation = "IconToggleButton",
                TooltipText = "Manage brushes",
            };
            heading.AddChild(manage);
            manage.Pressed += () => AppDialogHost.BrushLibrary.PopupCenteredClamped();
        }

        var palette = new BrushPalette(kind)
        {
            Name = "Palette",
            CustomMinimumSize = Vector2.Zero,
            CustomMaximumSize = new(-1, 320),
            SizeFlagsVertical = SizeFlags.Fill,
        };
        // Reserve keeps wrapping independent of scrollbar visibility; grow only up to the height cap.
        palette.Grid.MinimumSizeChanged += () => palette.CustomMinimumSize = new(0,
            Mathf.Min(palette.Grid.GetCombinedMinimumSize().Y, palette.CustomMaximumSize.Y));
        AddChild(palette);
        var change = new Button
        {
            Name = "ChangeBrush",
            Icon = GD.Load<Texture2D>("res://Icon/wrench.svg"),
            ExpandIcon = true,
            CustomMinimumSize = new(30, 30),
            ThemeTypeVariation = "IconToggleButton",
            TooltipText = "Change brush",
        };
        heading.AddChild(change);
        model.UsedBrushes.Subscribe(brushes =>
        {
            palette.SetBrushes(brushes);
            palette.Visible = brushes.Length > 0;
            var layers = document.Get<SelectionManager>().SelectedLayers.Value;
            Visible = brushes.Length > 0 || layers.Any(layer => kind == ShapeBrushKind.Stroke
                ? layer.Has<ShapeLayerSetting>() : layer.Has<VectorFillLayerSetting>());
        }).AddTo(this);
        model.SelectedBrushes.Subscribe(brushes =>
        {
            palette.SelectBrushes(brushes);
            change.Disabled = brushes.Length == 0;
        }).AddTo(this);
        palette.BrushClicked += brush =>
        {
            model.Toggle(brush);
            selectionChanged();
        };
        change.Pressed += () => ShowBrushPicker(document, kind, model);
    }

    private void ShowBrushPicker(Entity document, ShapeBrushKind kind, ShapeBrushSelection model)
    {
        var dialog = new AcceptDialog
        {
            Name = "ChangeBrushDialog",
            Title = kind == ShapeBrushKind.Stroke ? "Change stroke brush" : "Change fill brush",
            Exclusive = true,
            Transient = true,
            Size = new(520, 420),
        };
        var palette = new BrushPalette(kind) { Name = "Palette", NullBrushText = "No brush" };
        dialog.AddChild(palette);
        AddChild(dialog);
        dialog.GetOkButton().Text = "Cancel";
        dialog.VisibilityChanged += () => { if (!dialog.Visible) dialog.QueueFree(); };
        void RefreshChoices()
        {
            palette.SetBrushes(model.DocumentBrushes.Append(Entity.Null));
            palette.SelectBrushes(model.SelectedBrushes.Value);
        }
        RefreshChoices();
        model.DocumentBrushes.ObserveChanged().Subscribe(_ => RefreshChoices()).AddTo(dialog);
        document.Get<SelectionManager>().SelectedLayers.Skip(1).Subscribe(_ => dialog.Hide()).AddTo(dialog);
        palette.BrushClicked += brush =>
        {
            dialog.Hide();
            model.ChangeBrush(brush);
        };
        dialog.PopupCentered();
    }
}
