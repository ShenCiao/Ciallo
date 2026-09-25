using System.Collections.Generic;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Frent;
using Godot;

namespace Ciallo.GuiControl;

/// <summary>
/// Read-only layer picker for choosing a vector fill layer's <see cref="VectorFillLayerSetting.ReferenceLayers"/>.
/// Mirrors the layer tree visually (name + icon + checkbox), but only <see cref="ShapeLayerSetting"/> layers
/// are selectable. Assumes the data layer does not change while the popup is open: it builds a one-time static
/// snapshot of the tree and edits a local selection set, writing back through a single command on Apply.
/// </summary>
public partial class ReferenceLayerPickerPopup : PopupPanel
{
    private static readonly Texture2D RegularFolderIcon = GD.Load<Texture2D>("res://Icon/folder.svg");
    private static readonly Texture2D CelFolderIcon = GD.Load<Texture2D>("res://Icon/folder-animation.svg");
    private static readonly Texture2D ShapeLayerIcon = GD.Load<Texture2D>("res://Icon/FixedSize/brush-icon-sized.svg");

    private const int IndentWidth = 24;
    private const int CheckBoxSlotWidth = 32;
    private const int IconSize = 24;

    private Entity _document;
    private Entity _vectorFillLayer;

    // Local working selection; only touched by checkbox toggles. Written back on Apply.
    private readonly HashSet<Entity> _selected = [];

    private VBoxContainer _rowList;

    public ReferenceLayerPickerPopup()
    {
        Exclusive = true;
        TransparentBg = false;
    }

    public void Popup(Entity document, Entity vectorFillLayer)
    {
        _document = document;
        _vectorFillLayer = vectorFillLayer;

        _selected.Clear();
        foreach (var e in vectorFillLayer.Get<VectorFillLayerSetting>().ReferenceLayers)
            _selected.Add(e);

        BuildUi();
        PopupCentered(new Vector2I(360, 480));
    }

    private void BuildUi()
    {
        foreach (var child in GetChildren())
            child.QueueFree();

        var margin = new MarginContainer();
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        AddChild(margin);

        var root = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        margin.AddChild(root);

        root.AddChild(new Label { Text = "Select reference shape layers" });

        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(320, 380),
        };
        root.AddChild(scroll);

        _rowList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(_rowList);
        BuildRows(_document, 0);

        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        root.AddChild(buttons);

        var cancel = new Button { Text = "Cancel" };
        cancel.Pressed += Hide;
        buttons.AddChild(cancel);

        var apply = new Button { Text = "Apply" };
        apply.Pressed += Apply;
        buttons.AddChild(apply);
    }

    private void BuildRows(Entity parent, int depth)
    {
        var children = parent.Get<LayerTreeNode>().Children;
        // Reverse to match the layer tree's top-to-bottom display order (last child shown first).
        for (int i = children.Count - 1; i >= 0; i--)
        {
            var child = children[i];
            if (!child.Has<CommonLayerSetting>() || !child.Has<LayerTreeNode>())
                continue;

            _rowList.AddChild(CreateRow(child, depth));

            if (child.Has<FolderLayerSetting>())
                BuildRows(child, depth + 1);
        }
    }

    private Control CreateRow(Entity layer, int depth)
    {
        var row = new HBoxContainer();

        if (depth > 0)
            row.AddChild(new Control { CustomMinimumSize = new Vector2(depth * IndentWidth, 0) });

        bool isShapeLayer = layer.Has<ShapeLayerSetting>();
        if (isShapeLayer)
        {
            var captured = layer;
            var checkBox = new CheckBox
            {
                ButtonPressed = _selected.Contains(layer),
                CustomMinimumSize = new Vector2(CheckBoxSlotWidth, 0),
            };
            checkBox.Toggled += pressed =>
            {
                if (pressed) _selected.Add(captured);
                else _selected.Remove(captured);
            };
            row.AddChild(checkBox);
        }
        else
        {
            // Keep names aligned with selectable rows.
            row.AddChild(new Control { CustomMinimumSize = new Vector2(CheckBoxSlotWidth, 0) });
        }

        var icon = GetIcon(layer);
        if (icon != null)
        {
            row.AddChild(new TextureRect
            {
                Texture = icon,
                CustomMinimumSize = new Vector2(IconSize, IconSize),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            });
        }

        row.AddChild(new Label
        {
            Text = layer.Get<CommonLayerSetting>().Name.Value,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        });

        return row;
    }

    private static Texture2D GetIcon(Entity layer)
    {
        if (layer.TryGet<FolderLayerSetting>() is { } folder)
            return folder.IsCelFolder ? CelFolderIcon : RegularFolderIcon;
        if (layer.Has<ShapeLayerSetting>())
            return ShapeLayerIcon;
        return null;
    }

    private void Apply()
    {
        var current = _vectorFillLayer.Get<VectorFillLayerSetting>().ReferenceLayers;
        var toAdd = _selected.Where(e => !current.Contains(e)).ToArray();
        var toRemove = current.Where(e => !_selected.Contains(e)).ToArray();

        if (toAdd.Length == 0 && toRemove.Length == 0)
        {
            Hide();
            return;
        }

        new CommandBuilder("Edit Reference Layers", _vectorFillLayer)
            .SetObservableCollection(
                e => e.Get<VectorFillLayerSetting>().ReferenceLayers,
                layers =>
                {
                    foreach (var e in toRemove) layers.Remove(e);
                    foreach (var e in toAdd) layers.Add(e);
                })
            .Commit();

        Hide();
    }
}
