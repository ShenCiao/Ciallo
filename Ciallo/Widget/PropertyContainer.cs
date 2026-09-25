using System.Diagnostics.CodeAnalysis;
using Godot;

namespace Ciallo.Widget;

[GlobalClass, Icon("res://Icon/tune.svg")]
public partial class PropertyContainer : VBoxContainer
{
    public override void _EnterTree()
    {
        AddThemeConstantOverride("separation", 20);
    }

    public Container AddProperty(string name, [NotNull] Control control)
    {
        var box = CreatePropertyBox(name, control);
        AddChild(box);
        return box;
    }

    public Container RemoveProperty(string name)
    {
        var child = GetNode<Container>(name);
        RemoveChild(child);
        return child;
    }

    public BoxContainer CreateBox()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 20);
        return box;
    }

    public Container CreatePropertyBox(string name, [NotNull] Control control)
    {
        // Pitfall: If a control's CustomMinimumSize is zero, it will never be wrapped in FlowContainer.
        var box = CreateHContainer();
        box.AddChild(new Label
        {
            Text = name,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            CustomMinimumSize = new(32, 0),
        });
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        box.AddChild(control);

        if (control is not CheckBox)
        {
            var controlMinSize = control.CustomMinimumSize.Max(new Vector2(32, 32));
            control.CustomMinimumSize = controlMinSize;
        }
        return box;
    }

    public Container CreateHContainer()
    {
        var box = new HFlowContainer();
        box.AddThemeConstantOverride("h_separation", 15);
        return box;
    }

    /// <summary>
    /// Control is visible if checkBox is pressed.
    /// </summary>
    public Container CreateCheckBoxCombo(string name, CheckBox checkBox, Control control)
    {
        control.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        control.Visible = checkBox.IsPressed();

        checkBox.Pressed += () => control.Visible = checkBox.IsPressed();
        checkBox.Text = name;
        // checkBox.IconAlignment = HorizontalAlignment.Right; // This not work. Bug? 
        checkBox.SizeFlagsVertical = SizeFlags.ShrinkBegin;

        var container = CreateHContainer();
        container.AddChild(checkBox);
        container.AddChild(control);
        return container;
    }

    public Button CreateButton(string text)
    {
        var button = new Button()
        {
            Name = text,
            Text = text,
            Alignment = HorizontalAlignment.Left,
            CustomMinimumSize = new(0, 32),
            SizeFlagsHorizontal = SizeFlags.Fill,
        };
        return button;
    }
}
