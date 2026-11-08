using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Frent;
using Godot;
using ObservableCollections;
using R3;

namespace Ciallo.GuiControl;

public partial class SelectionColorPanel : VBoxContainer
{
    public SelectionColorPanel() { }

    public SelectionColorPanel(Entity document)
    {
        Name = "SelectionColor";
        var selected = document.Get<SelectionManager>().SelectedShapes;
        AddChild(new Label { Text = "Selected color" });
        var button = new ColorPickerButton
        {
            Name = "Color",
            CustomMinimumSize = new(0, 32),
            TooltipText = "[Selected Color Tooltip]",
        };
        AddChild(button);
        var mixed = new Label { Text = "Mixed colors" };
        AddChild(mixed);
        DocumentColorControls.BindSwatches(button, document);
        var colors = new SerialDisposable().AddTo(this);
        selected.ObserveChanged().Select(_ => Unit.Default).Prepend(Unit.Default).Subscribe(_ =>
        {
            CompositeDisposable subscriptions = new();
            colors.Disposable = subscriptions;
            foreach (var shape in selected)
                shape.Get<ShapeSetting>().Color.Skip(1).Subscribe(_ => Refresh()).AddTo(subscriptions);
            Refresh();
        }).AddTo(this);
        button.ColorChanged += color =>
        {
            var command = new CommandBuilder("Set Selected Shape Color", document);
            foreach (var shape in selected)
                if (shape.Get<ShapeSetting>().Color.Value != color)
                    command.SetTarget(shape).SetProperty(e => e.Get<ShapeSetting>().Color, color);
            command.CommitSequence();
        };

        void Refresh()
        {
            Visible = selected.Count > 0;
            if (selected.Count == 0) return;
            var color = selected[0].Get<ShapeSetting>().Color.Value;
            button.Color = color;
            mixed.Visible = selected.Any(shape => shape.Get<ShapeSetting>().Color.Value != color);
        }
    }
}
