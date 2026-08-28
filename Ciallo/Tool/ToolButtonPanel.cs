using System.Collections.Generic;
using Godot;
using R3;

namespace Ciallo.Tool;

[Tool]
public partial class ToolButtonPanel : Container
{
    public ButtonGroup ToolButtonGroup { get; } = new();
    private readonly Dictionary<ToolButton.Type, Button> _buttons = new();

    [OnInstantiate]
    private void Initialise()
    {
        while (GetChildCount() > 0)
        {
            var child = GetChild(0);
            RemoveChild(child);
            child.QueueFree();
        }

        foreach (var definition in ToolButton.Definitions)
        {
            var button = new Button
            {
                CustomMinimumSize = new(40, 40),
                TooltipText = definition.Tooltip,
                ThemeTypeVariation = "IconToggleButton",
                ToggleMode = true,
                ActionMode = BaseButton.ActionModeEnum.Press,
                Icon = GD.Load<Texture2D>(definition.IconPath),
                ExpandIcon = true,
                ButtonGroup = ToolButtonGroup,
            };
            if (definition.Shortcut is { } hotkey)
                button.Shortcut = hotkey.Shortcut;
            _buttons[definition.Type] = button;
            AddChild(button);
        }
    }

    public ToolButtonPanel Bind()
    {
        ToolButton.ActiveToolButton.Subscribe(toolButton =>
        {
            if (toolButton is null)
                UnpressActiveButton();
            else
                PressButton(toolButton.Value);
        }).AddTo(this);

        ToolButtonGroup.SignalAsObservable<BaseButton>(ButtonGroup.SignalName.Pressed)
            .DistinctUntilChanged()
            .Subscribe(button =>
            {
                foreach (var (type, mapped) in _buttons)
                {
                    if (!ReferenceEquals(mapped, button)) continue;
                    InteractionManager.RequestTool(type);
                    return;
                }
            }).AddTo(this);

        return this;
    }

    public void PressButton(ToolButton.Type toolButton)
    {
        if (_buttons.TryGetValue(toolButton, out var button))
            button.ButtonPressed = true;
    }

    public void UnpressActiveButton()
    {
        ToolButtonGroup.GetPressedButton()?.SetPressedNoSignal(false);
    }
}
