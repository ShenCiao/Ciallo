using System.Collections.Generic;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Godot;

namespace Ciallo.GuiControl;

public partial class MenuEdit : PopupMenu
{
    public static readonly OrderedDictionary<string, Hotkey> MenuItems = new()
    {
        { "Undo", AppHotkeys.Global.EditUndo },
        { "Redo", AppHotkeys.Global.EditRedo },
    };

    public override void _Ready()
    {
        foreach (var (i, item) in MenuItems.Index())
        {
            if (item.Key.StartsWith('-'))
            {
                AddSeparator();
                continue;
            }
            AddItem(Tr(item.Key));
            if (item.Value != null) SetItemShortcut(i, item.Value.Shortcut);
        }

        IndexPressed += id => OnMenuButtonPressed((int)id);
    }

    public static void OnMenuButtonPressed(int id)
    {
        if (AppDocumentManager.WorkingDocument.Value.IsNull) return;
        var cmdM = AppDocumentManager.WorkingDocument.CurrentValue.Get<CommandManager>();
        switch (id)
        {
            case 0: cmdM.Undo(); break;
            case 1: cmdM.Redo(); break;
            default: GD.PrintErr($"Unhandled menu item index: {id}"); break;
        }
    }
}
