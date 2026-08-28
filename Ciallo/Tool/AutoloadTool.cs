using System.Collections.Immutable;
using Ciallo.Command;
using Ciallo.Data;
using Frent;
using Godot;
using R3;

namespace Ciallo.Tool;

public partial class AutoloadTool : Node
{
    public override void _Ready()
    {
        InteractionManager.Initialize();
        if (AppPreference.PressedToolButton.Value is { } saved)
            InteractionManager.RequestTool(saved);

        AppDocumentManager.WorkingDocument.Subscribe(document =>
        {
            if (document.IsNull) return;

            InteractionManager.OpenDocument(document, ToLayers(document.Get<SelectionManager>().WorkingLayer.Value));

            document.Get<SelectionManager>().WorkingLayer
                .Skip(1)
                .Subscribe(layer => InteractionManager.ChangeWorkingLayers(ToLayers(layer)))
                .AddTo(document);

            document.Get<TimelineSetting>().IsRollingFrame
                .DistinctUntilChanged()
                .Subscribe(InteractionManager.SetTimelineRolling)
                .AddTo(document);

            document.Get<CommandManager>().HistoryNavigated
                .Subscribe(_ => InteractionManager.NotifyRefresh())
                .AddTo(document);
        });
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut || what == NotificationWMWindowFocusOut)
            InteractionManager.CancelForInputCaptureLoss();
    }

    private static ImmutableArray<Entity> ToLayers(Entity layer) =>
        layer.IsNull ? ImmutableArray<Entity>.Empty : [layer];
}
