using System;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Geometry;
using Frent;
using Godot;
using R3;

namespace Ciallo.Tool;

/// <summary>
/// Trim tool aims to be "visually/feelingly topologically robust"
/// Not truly topologically correct, as long as users feel it correct.
/// Ciallo is a drawing app, not a CAD topology editor: prefer visually
/// correct 95% behavior over preserving every tiny real stroke or junction.
/// </summary>
[RegisterTool(ToolButton.Trim)]
public class TrimTool : ToolBase
{
    public readonly TrimHover Hover = new();
    public readonly TrimInteractor Trim = new();

    // Layer-owned ArrangementManager, shared with vector-fill and future topology tools.
    public ArrangementManager Arrangement { get; private set; }

    private IDisposable _arrReadySub;

    protected override void ConfigureStateMachine()
    {
        ConfigureInitial(Hover)
            .PermitIf(Press(MouseButton.Left), Trim, () => Arrangement?.ArrReady.CurrentValue != null);

        Configure(Trim)
            .Permit(Release(MouseButton.Left), Hover)
            .Permit(Press(AppHotkeys.Global.InteractionCancel), Hover);
    }

    public override bool CanHandleLayer(params Entity[] layerEs)
    {
        if (layerEs.Length != 1) return false;
        var layerE = layerEs[0];
        return layerE.Has<ShapeLayerSetting>() || layerE.Has<VectorFillLayerSetting>();
    }

    public override void OnActivated()
    {
        Arrangement = WorkingLayer.Get<ArrangementManager>();

        _arrReadySub = Arrangement.ArrReady.Subscribe(_ =>
        {
            if (Machine.State is TrimHover hover)
                hover.RefreshCursor();
        });
    }

    public override void OnDeactivated()
    {
        _arrReadySub?.Dispose();
        _arrReadySub = null;
        Arrangement = null;
    }
}
