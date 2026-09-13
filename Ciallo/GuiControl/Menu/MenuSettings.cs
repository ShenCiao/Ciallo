using System;
using Godot;

namespace Ciallo.GuiControl;

public partial class MenuSettings : PopupMenu
{
    private enum Command
    {
        BrushLibrary = 100,
        GlobalPenPressure = 101,
        Shortcuts = 102,
    }

    public override void _Ready()
    {
        AddItem($"{Tr("Brush Library")}...", (int)Command.BrushLibrary);
        AddItem($"{Tr("Global Pen Pressure")}...", (int)Command.GlobalPenPressure);
        AddItem($"{Tr("Shortcuts")}...", (int)Command.Shortcuts);

        IdPressed += id => OnIdPressed((Command)id);
    }

    public override void _Notification(int what)
    {
        base._Notification(what);
        if (what != NotificationTranslationChanged || !IsNodeReady()) return;

        SetCommandText(Command.BrushLibrary, "Brush Library");
        SetCommandText(Command.GlobalPenPressure, "Global Pen Pressure");
        SetCommandText(Command.Shortcuts, "Shortcuts");
    }

    private static void OnIdPressed(Command command)
    {
        switch (command)
        {
            case Command.BrushLibrary:
                AppDialogHost.BrushLibrary.Popup();
                break;
            case Command.GlobalPenPressure:
                AppDialogHost.ConfigureGlobalPenPressure.Popup();
                break;
            case Command.Shortcuts:
                AppDialogHost.ShortcutSettings.Popup();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, "Unhandled Settings menu command");
        }
    }

    private void SetCommandText(Command command, string text) =>
        SetItemText(GetItemIndex((int)command), $"{Tr(text)}...");
}
