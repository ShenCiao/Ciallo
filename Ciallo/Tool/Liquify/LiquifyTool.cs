using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Widget;
using Frent;
using Godot;
using R3;

namespace Ciallo.Tool;

[RegisterTool(ToolButton.Liquify)]
public class LiquifyTool : ToolBase
{
    public readonly ReactiveProperty<LiquifyMode> Mode = new(LiquifyMode.Push);
    public readonly ReactiveProperty<float> Radius = new(64f);
    public readonly ReactiveProperty<float> Strength = new(0.5f);

    public readonly LiquifyHover Hover = new();
    public readonly LiquifyInteractor Left = new();

    protected override void ConfigureStateMachine()
    {
        ConfigureInitial(Hover)
            .Permit(Press(MouseButton.Left), Left);

        Configure(Left)
            .Permit(Release(MouseButton.Left), Hover)
            .Permit(Press(AppHotkeys.Global.InteractionCancel), Hover)
            .Permit(Press(AppHotkeys.Global.InteractionConfirm), Hover);
    }

    public override bool CanHandleLayer(params Entity[] layerEs)
    {
        if (layerEs.Length != 1) return false;
        var e = layerEs.Single();
        return !e.IsDyingOrDead && e.Has<ShapeLayerSetting>();
    }

    public override void DrawProperty(PropertyContainer container)
    {
        container.AddProperty("Mode",
            new OptionButton()
            {
                CustomMinimumSize = new(128, 32),
                FitToLongestItem = false,
            }.BindEnum(Mode));

        container.AddProperty("Radius",
            new SpinSlider
            {
                MinValue = 1f,
                MaxValue = 512f,
                Step = 1f,
                ExpEdit = true,
            }.BindNumber(Radius));

        container.AddProperty("Strength",
            new SpinSlider
            {
                MinValue = 0.01f,
                MaxValue = 1f,
                Step = 0.01f,
                AllowGreater = true,
            }.BindNumber(Strength));

        base.DrawProperty(container);
    }
}
