using Godot;
using System.Collections.Generic;
using System.Linq;
using Massive;
using Ciallo.Command;
using Ciallo.Data;

namespace Ciallo.NodeControl;

public partial class MenuEdit : PopupMenu
{
    public static readonly OrderedDictionary<string, AppAction> MenuItems = new()
    {
        { "Undo", AppActions.Undo },
        { "Redo", AppActions.Redo },
        { "-1", null },
        { "Cut", AppActions.Cut },
        { "Copy", AppActions.Copy },
        { "Paste", AppActions.Paste },
        { "Delete", AppActions.Delete },
    };
    
    public override void _Ready()
    {
        foreach(var (i, item) in MenuItems.Index())
        {
            if(item.Key.StartsWith('-'))
            {
                AddSeparator();
                continue;
            }
            AddItem(Tr(item.Key));
            if (item.Value != null) SetItemShortcut(i, item.Value.Shortcut);
        }
        
        IndexPressed += id => OnIndexPressed((int)id);
    }
    
    public static void OnIndexPressed(int id)
    {
        if(AppWorldManager.WorkingWorld.Value == null) return;
        var cmdM = AppWorldManager.WorkingDocument.CurrentValue.Get<CommandManager>();
        switch (id)
        {
            case 0: cmdM.Undo(); break;
            case 1: cmdM.Redo(); break;
            default: GD.PrintErr($"Unhandled menu item index: {id}"); break;
        }
    }
}
