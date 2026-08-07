using Godot;

namespace Ciallo.Widget;

/// <summary>
/// This spin slider aims to be same as Godot's EditorSpinSlider. Current version is visually different.
/// </summary>
[GlobalClass, Tool]
public partial class SpinSlider : HBoxContainer
{
    [Signal]
    public delegate void ValueChangedEventHandler(double oldValue, double newValue);

    public HSlider Slider { get; private set; }
    public SpinBox SpinBox { get; private set; }

    #region Export

    [Export]
    public double MinValue
    {
        get => field;
        set
        {
            field = value;
            Slider?.MinValue = value;
        }
    }

    [Export]
    public double MaxValue
    {
        get => field;
        set
        {
            field = value;
            Slider?.MaxValue = value;
        }
    }

    private double _value;
    [Export]
    public double Value
    {
        get => _value;
        set
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (_value == value) return;
            var oldValue = _value;
            _value = value;
            Slider?.SetValueNoSignal(value);
            EmitSignalValueChanged(oldValue, value);
        }
    }

    [Export]
    public double Step
    {
        get => field;
        set
        {
            field = value;
            Slider?.Step = value;
        }
    }

    [Export]
    public bool ExpEdit
    {
        get => field;
        set
        {
            field = value;
            Slider?.ExpEdit = value;
        }
    }

    [Export]
    public bool AllowLesser
    {
        get => field;
        set
        {
            field = value;
            Slider?.AllowLesser = value;
        }
    }

    [Export]
    public bool AllowGreater
    {
        get => field;
        set
        {
            field = value;
            Slider?.AllowGreater = value;
        }
    }

    [Export]
    public bool Rounded
    {
        get => field;
        set
        {
            field = value;
            Slider?.Rounded = value;
        }
    }

    [Export]
    public bool Editable
    {
        get;
        set
        {
            field = value;
            Slider?.Editable = value;
        }
    } = true;

    #endregion

    public override void _Ready()
    {
        Slider = new()
        {
            MinValue = MinValue,
            MaxValue = MaxValue,
            Step = Step,
            ExpEdit = ExpEdit,
            AllowLesser = AllowLesser,
            AllowGreater = AllowGreater,
            Rounded = Rounded,
            Value = Value,
            CustomMinimumSize = new(64, 0),
            Scrollable = false,
            SizeFlagsVertical = SizeFlags.ShrinkCenter | SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        SpinBox = new()
        {
            MinValue = MinValue,
            MaxValue = MaxValue,
            Step = Step,
            ExpEdit = ExpEdit,
            AllowLesser = AllowLesser,
            AllowGreater = AllowGreater,
            Rounded = Rounded,
            Value = Value,
        };

        Slider.Share(SpinBox);
        AddChild(Slider, false, InternalMode.Back);
        AddChild(SpinBox, false, InternalMode.Back);

        Slider.ValueChanged += v => Value = v;

        // Pitfall: LineEdit has no way to show rounded number without modifying the number itself.
        // Have to do this manually.
        // // This not work
        // SpinBox.GetLineEdit().TextChanged += text =>
        // {
        //     if (text.Length > 6)
        //         SpinBox.GetLineEdit().Text = text[..6];
        // };
        SpinBox.GetLineEdit().TextDirection = TextDirection.Ltr;
        SpinBox.GetLineEdit().SubmitOnFocusExit();
    }

    public override void _ExitTree()
    {
        if (Engine.IsEditorHint())
            this.QueueFreeChildren();
    }

    public void SetValueNoSignal(double value)
    {
        _value = value;
        Slider?.SetValueNoSignal(value);
    }

}
