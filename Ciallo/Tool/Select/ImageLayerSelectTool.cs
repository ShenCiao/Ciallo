using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Frent;
using Godot;

namespace Ciallo.Tool;

[RegisterTool(ToolButton.Select)]
public class ImageLayerSelectTool : ToolBase
{
    public readonly ImageLayerSelectHover Hover = new();
    public readonly ImageTransformInteractor Left = new();

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
        return layerEs.Length == 1 && layerEs.Single().Has<ImageLayerSetting>();
    }
}
