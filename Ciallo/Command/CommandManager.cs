using System.Collections.Generic;
using System.Linq;
using Massive;
using Godot;

namespace Ciallo.Command;

/// <summary>
/// Inherits from UndoRedo with extra methods to manage commands.
/// </summary>
public partial class CommandManager : UndoRedo
{
    public CommandManager()
    {
        SetMaxSteps(3); // fast invoke bugs
    }

    public void AddDo(CommandWrapperObject cmdWrapper)
    {
        cmdWrapper.DoWrapper = new WrapperObject(cmdWrapper.Command.DoRefEntities, cmdWrapper.Command.DoRefObjects);
        AddDoMethod(new(cmdWrapper, CommandWrapperObject.MethodName.Do));
        AddDoReference(cmdWrapper.DoWrapper);
        AddDoReference(cmdWrapper);
    }

    public void AddUndo(CommandWrapperObject cmdWrapper)
    {
        cmdWrapper.UndoWrapper = new WrapperObject(cmdWrapper.Command.UndoRefEntities, cmdWrapper.Command.UndoRefObjects);
        AddUndoMethod(new(cmdWrapper, CommandWrapperObject.MethodName.Undo));
        AddUndoReference(cmdWrapper.UndoWrapper);
        AddUndoReference(cmdWrapper);
    }
}

public partial class CommandWrapperObject(CommandBase command) : GodotObject
{
    public CommandBase Command { get; } = command;
    public WrapperObject DoWrapper;
    public WrapperObject UndoWrapper;

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            if (IsInstanceValid(DoWrapper))
                DoWrapper.FreeWithoutDestroying();
            if (IsInstanceValid(UndoWrapper))
                UndoWrapper.FreeWithoutDestroying();
        }
    }

    public void Do() => Command.Do();
    public void Undo() => Command.Undo();
}

/// <summary>
/// Wrapper for the IEnumerable to be used in CommandBase.
/// Automatically destroy entities and objects when the command is deleted, unless FreeWithoutDestroying is called.
/// </summary>
public partial class WrapperObject(IEnumerable<Entity> entities, IEnumerable<GodotObject> objects) : GodotObject
{
    private bool _destroy = true;

    public override void _Notification(int what)
    {
        if (what != NotificationPredelete || !_destroy) return;
        
        if (entities?.Any() == true)
        {
            foreach (var e in entities)
                e.Destroy();
        }

        if (objects != null)
        {
            foreach (var obj in objects)
                obj.Free();
        }
    }

    public void FreeWithoutDestroying()
    {
        _destroy = false;
        Free();
    }
}