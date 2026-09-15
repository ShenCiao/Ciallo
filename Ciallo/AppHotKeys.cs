using Godot;

namespace Ciallo;

/// <summary>
/// Static access to the actions defined in godot editor.
/// </summary>
/// <remarks>Using GodotSharp.SourceGenerators library</remarks>
[InputMap(nameof(Hotkey))]
public static partial class AppHotkeys;

public record Hotkey(StringName Name)
{
    public readonly Shortcut Shortcut = new()
    {
        Events =
        [
            new InputEventAction
            {
                Action = Name,
                Pressed = true,
            }
        ],
    };

    public bool IsPressed => Input.IsActionPressed(Name);
    public bool IsJustPressed => Input.IsActionJustPressed(Name, true);
    public bool IsJustReleased => Input.IsActionJustReleased(Name, true);
    public float Strength => Input.GetActionStrength(Name);

    public bool IsPressedBy(InputEvent inputEvent) => Input.IsActionJustPressedByEvent(Name, inputEvent, true);
    public bool IsReleasedBy(InputEvent inputEvent) => Input.IsActionJustReleasedByEvent(Name, inputEvent, true);

    public void Press() => Input.ActionPress(Name);
    public void Release() => Input.ActionRelease(Name);

    public static implicit operator StringName(Hotkey input) => input.Name;
}