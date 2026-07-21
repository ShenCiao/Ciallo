using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Frent;
using Godot;

namespace Ciallo.Tool;

[RegisterTool(ToolButton.PaintFill)]
public class PaintFillTool : ToolBase
{
    public readonly PaintFillHover Hover = new();
    public readonly PaintFillInteractor Left = new();

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
}
