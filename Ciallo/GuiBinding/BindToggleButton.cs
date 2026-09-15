using System;
using Godot;
using R3;

namespace Ciallo;

/// <summary>
/// Two-way bindings for buttons whose pressed state represents a value.
/// </summary>
public static class BindToggleButton
{
    public static TButton BindBool<TButton>(
        this TButton button,
        ReactiveProperty<bool> property,
        CompositeDisposable subs)
        where TButton : BaseButton
    {
        RequireToggleMode(button);
        property.Subscribe(button.SetPressedNoSignal).AddTo(subs);
        button.OnToggledAsObservable()
            .Subscribe(value => property.Value = value)
            .AddTo(subs);
        return button;
    }

    public static TButton BindBool<TButton>(this TButton button, ReactiveProperty<bool> property)
        where TButton : BaseButton
    {
        var subs = new CompositeDisposable();
        BindBool(button, property, subs);
        subs.AddTo(button);
        return button;
    }

    public static TButton BindFlag<TButton, TEnum>(
        this TButton button,
        ReactiveProperty<TEnum> property,
        TEnum mask,
        CompositeDisposable subs)
        where TButton : BaseButton
        where TEnum : Enum
    {
        RequireToggleMode(button);
        property.Subscribe(value => button.SetPressedNoSignal(value.HasFlag(mask))).AddTo(subs);
        button.OnToggledAsObservable()
            .Subscribe(pressed =>
            {
                property.Value = pressed
                    ? (TEnum)((dynamic)property.Value | (dynamic)mask)
                    : (TEnum)((dynamic)property.Value & ~(dynamic)mask);
            })
            .AddTo(subs);
        return button;
    }

    public static TButton BindFlag<TButton, TEnum>(
        this TButton button,
        ReactiveProperty<TEnum> property,
        TEnum mask)
        where TButton : BaseButton
        where TEnum : Enum
    {
        var subs = new CompositeDisposable();
        BindFlag(button, property, mask, subs);
        subs.AddTo(button);
        return button;
    }

    private static void RequireToggleMode(BaseButton button)
    {
        if (!button.ToggleMode)
            throw new ArgumentException("Button must be in toggle mode", nameof(button));
    }
}
