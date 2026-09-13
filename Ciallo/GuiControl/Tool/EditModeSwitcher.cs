using Godot;
using System;
using Ciallo.Tool;
using R3;

namespace Ciallo;

[Instantiable, SceneTree]
public partial class EditModeSwitcher : Container
{
    [Export] public ButtonGroup Group;

    public EditModeSwitcher Bind(ReactiveProperty<PolylineSelectTool.EditMode> mode)
    {
        var subs = new CompositeDisposable();
        subs.AddTo(this);
        return Bind(mode, subs);
    }

    public EditModeSwitcher Bind(ReactiveProperty<PolylineSelectTool.EditMode> mode, CompositeDisposable subs)
    {
        Group.Pressed += (button) =>
        {
            if (button == null)
                return;
            else if (button == RectTransform)
                mode.Value = PolylineSelectTool.EditMode.RectTransform;
            else if (button == BezierDeform)
                mode.Value = PolylineSelectTool.EditMode.BezierDeform;
            else
                throw new Exception("Unknown button");
        };
        mode.Subscribe(value =>
        {
            switch (value)
            {
                case PolylineSelectTool.EditMode.RectTransform:
                    RectTransform.SetPressedNoSignal(true);
                    break;
                case PolylineSelectTool.EditMode.BezierDeform:
                    BezierDeform.SetPressedNoSignal(true);
                    break;
                default:
                    throw new Exception("Unknown edit mode");
            }
        }).AddTo(subs);
        return this;
    }
}