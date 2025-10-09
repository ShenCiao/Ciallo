using System.Collections.Generic;
using System.Linq;
using Ciallo.Data;
using Godot;
using Godot.Collections;
using Humanizer;
using Massive;

namespace Ciallo.Command;

/// <remarks>
/// Shen: Several attempts have been made to allow `using var cmd = new Cmd()`, but all failed.
/// This class cannot be `RefCounted` otherwise cannot get notification of Predelete.
/// Override the Dispose method get unknown/undefined behavior.
/// Already solved by making CommandBase not inherent GodotObject. 
/// </remarks>>
/// <summary>
/// `using var cmd = new Cmd()` cause memory leak. Must manually call `cmd.Free()`.
/// </summary>
public abstract class CommandBase
{
    /// <summary>
    /// The world in which this command operates.
    /// </summary>
    public World WorkingWorld { get; set; } = AppWorldManager.WorkingWorld.Value;
    public Entity Document => WorkingWorld.Document();
    public virtual string Name => GetType().Name.Humanize();
    public SceneTree SceneTree => (SceneTree)Engine.GetMainLoop();

    public Array<Node> GetNodesInGroup(StringName group) => SceneTree.GetNodesInGroup(group);

    /// <summary>
    /// `DoRefEntities` are the entities will be destroyed when this command is ready to redo and deleted.
    /// e.g. User undo the most recent command, then clear the whole history. So the most recent command satisfies the above statement.
    /// Entity version of `add_do_reference`.
    /// </summary>
    public virtual IEnumerable<Entity> DoRefEntities => null;
    public virtual IEnumerable<Entity> UndoRefEntities => null;

    public virtual IEnumerable<GodotObject> DoRefObjects => null;
    public virtual IEnumerable<GodotObject> UndoRefObjects => null;

    public abstract void Do();
    public abstract void Undo();

    
    public void Commit(bool execute = true)
    {
        if (WorkingWorld == null)
        {
            GD.PushWarning("WorkingWorld is null");
            return;
        }
        var cm = WorkingWorld.Document().Get<CommandManager>();
        
        // Add Do/Undo Reference method order matters:
        var commands = GetDepthFirstCommands().ToArray();
        var objects = commands.Select(c => new CommandWrapperObject(c)).ToArray();
        
        cm.CreateAction(Name);
        foreach (var obj in objects) cm.AddDo(obj);
        foreach (var obj in objects.Reverse()) cm.AddUndo(obj);
        cm.CommitAction(execute);
    }
    
    public void DoAllCombination(bool useRootWorld = true)
    {
        foreach (var cmd in GetDepthFirstCommands())
        {
            if(useRootWorld) cmd.WorkingWorld = WorkingWorld;
            cmd.Do();
        }
    }

    private readonly List<CommandBase> _combinations = [];
    public CommandBase Combine(CommandBase other)
    {
        other.WorkingWorld = WorkingWorld;
        _combinations.Add(other);
        return this;
    }

    // Recursively yields this command and all combined commands in depth-first order
    private IEnumerable<CommandBase> GetDepthFirstCommands()
    {
        yield return this;
        foreach (var cmd in _combinations)
            foreach (var subCmd in cmd.GetDepthFirstCommands())
                yield return subCmd;
    }

    public override string ToString()
    {
        return $"{Name}";
    }
    
    public static IEnumerable<Entity> ToEnumerable(Entity value)
    {
        if (value.IsNotNull()) yield return value;
    }
}