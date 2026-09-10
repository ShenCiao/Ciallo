using Ciallo.Data;
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

            InteractionManager.OpenDocument(document, document.Get<SelectionManager>().WorkingLayers.Value);

            document.Get<SelectionManager>().WorkingLayers
                .Skip(1)
                .Subscribe(InteractionManager.ChangeWorkingLayers)
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
}
