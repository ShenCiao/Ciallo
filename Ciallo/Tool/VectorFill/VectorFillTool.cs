using System.Collections.Generic;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Rendering;
using Frent;
using Godot;
using R3;

namespace Ciallo.Tool;

[RegisterTool(ToolButton.VectorFill)]
public class VectorFillTool : ToolBase
{
    public readonly VectorFillHover Hover = new();
    public readonly PaintVectorFillMarkerInteractor Left = new();

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
        return e.Has<VectorFillLayerSetting>();
    }

    public readonly Subject<Unit> DeactivateSignal = new();
    public override void OnActivated()
    {
        if (!WorkingLayer.Has<VectorFillLayerSetting>()) return;
        WorkingLayer.Get<OverlayHolder>().Visible = true;

        var referenceLayers = WorkingLayer.Get<VectorFillLayerSetting>().ReferenceLayers;
        AppPreference.ShowVectorFillReferenceLayerWireframe
            .TakeUntil(DeactivateSignal)
            .Subscribe(visible => SetWireframeVisibility(referenceLayers, visible),
                _ => SetWireframeVisibility(referenceLayers, false));
    }

    public override void OnDeactivated()
    {
        DeactivateSignal.OnNext(Unit.Default);
        if (!WorkingLayer.Has<VectorFillLayerSetting>()) return;
        WorkingLayer.Get<OverlayHolder>().Visible = false;
    }

    public static void SetWireframeVisibility(IEnumerable<Entity> list, bool visible)
    {
        foreach (var e in list)
        {
            foreach (var n in e.Get<OverlayHolder>().GetChildren())
            {
                var node = (PolylineWireframe)n;
                node.Visible = visible;
                node.Dots.Visible = !visible;
            }
        }
    }
}
