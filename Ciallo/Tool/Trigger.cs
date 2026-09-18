using System;
using System.Collections.Generic;
using Godot;

namespace Ciallo.Tool;

// A trigger identifies only what happened. The exiting Interaction decides whether to
// commit (End) or rollback (Cancel) through CancelsOn(transition). The same input can mean
// different things to different interactions without configuration conflict.
//
// Events that don't cause a transition are routed to the interaction's OnKey/OnMouseButton handlers.
public class Trigger
{
    public string Name { get; }
    public bool IsInput { get; }
    internal bool IsActionInput { get; }
    private readonly Func<InputEvent, bool> _matchesInput = _ => false;

    public Trigger(string name)
    {
        Name = name;
    }

    private Trigger(string name, Func<InputEvent, bool> matchesInput, bool isActionInput = false) : this(name)
    {
        _matchesInput = matchesInput;
        IsInput = true;
        IsActionInput = isActionInput;
    }

    // Read InputMap at dispatch time so rebinding does not invalidate the state graph.
    // Echo still matches: the router consumes it without firing the action again.
    public bool Matches(InputEvent input) => _matchesInput(input);

    // "The data this state read is stale; read it again." Published on undo/redo, selection changes.
    // Only states that explicitly permit reentry react; others ignore it.
    public static readonly Trigger Refresh = new("Refresh");

    // Canonical instances, so reference equality is a complete trigger comparison.
    private static readonly Dictionary<MouseButton, Trigger> MouseButtonPress = new();
    private static readonly Dictionary<MouseButton, Trigger> MouseButtonRelease = new();
    private static readonly Dictionary<Key, Trigger> KeyPress = new();
    private static readonly Dictionary<Key, Trigger> KeyRelease = new();
    private static readonly Dictionary<Hotkey, Trigger> HotkeyPress = new();
    private static readonly Dictionary<Hotkey, Trigger> HotkeyRelease = new();

    public static Trigger Get(MouseButton button, bool isPress)
    {
        var dict = isPress ? MouseButtonPress : MouseButtonRelease;
        if (!dict.TryGetValue(button, out var trigger))
        {
            trigger = new Trigger($"{(isPress ? "Press" : "Release")}({button})",
                input => input is InputEventMouseButton mouse && mouse.ButtonIndex == button && mouse.Pressed == isPress);
            dict[button] = trigger;
        }
        return trigger;
    }

    public static Trigger Get(Key key, bool isPress)
    {
        var dict = isPress ? KeyPress : KeyRelease;
        if (!dict.TryGetValue(key, out var trigger))
        {
            trigger = new Trigger($"{(isPress ? "Press" : "Release")}({key})",
                input => input is InputEventKey keyboard && keyboard.Keycode == key && keyboard.Pressed == isPress);
            dict[key] = trigger;
        }
        return trigger;
    }

    public static Trigger Get(Hotkey hotkey, bool isPress)
    {
        var dict = isPress ? HotkeyPress : HotkeyRelease;
        if (!dict.TryGetValue(hotkey, out var trigger))
        {
            trigger = new Trigger($"{(isPress ? "Press" : "Release")}({hotkey.Name})",
                input => isPress
                    ? input.IsActionPressed(hotkey.Name, allowEcho: true, exactMatch: true)
                    : input.IsActionReleased(hotkey.Name, exactMatch: true),
                isActionInput: true);
            dict[hotkey] = trigger;
        }
        return trigger;
    }

    public static Trigger Press(MouseButton button) => Get(button, true);
    public static Trigger Release(MouseButton button) => Get(button, false);
    public static Trigger Press(Key key) => Get(key, true);
    public static Trigger Release(Key key) => Get(key, false);
    public static Trigger Press(Hotkey hotkey) => Get(hotkey, true);
    public static Trigger Release(Hotkey hotkey) => Get(hotkey, false);
}
