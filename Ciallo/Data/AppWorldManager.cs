using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Ciallo.Command;
using Massive;
using ObservableCollections;
using R3;
using System.Diagnostics;

namespace Ciallo.Data;

using Sys = System.Collections.Generic;

/// <summary>
/// World and document has one-to-one relationship.
/// In practice, a document is a special singleton entity of the world.
/// All the "document-level singleton data" should be stored in this "document" entity. (For the program-level singletons we commonly use static class).
/// The "document-level singleton data" is the data one per document, such as the DocumentSetting, LayerTree, etc.
/// </summary>
public static partial class AppWorldManager
{
    public static readonly ObservableList<World> LoadedWorlds = [];
    // Current focused document.
    public static readonly ReactiveProperty<World> WorkingWorld = new(null);
    public static readonly ReadOnlyReactiveProperty<Entity> WorkingDocument =
        WorkingWorld.Select(w => w?.Document() ?? default).ToReadOnlyReactiveProperty();

    public static World Create([NotNull] DocumentSetting settings)
    {
        // Only one loaded world is supported for current version.
        Clear();
        var world = new World();

        // Init empty document
        var i = world.Create();
        Debug.Assert(i == 0);
        var document = world.GetEntity(i);
        document.Add<ToSerializeTag>();

        // Add managers
        document.Set(settings);
        document.Set(new SelectionManager());
        document.Set(new LayerTreeManager());
        document.Set(new CommandManager());
        document.Set(new BrushManager());
        
        // Always init first, then add to list
        LoadedWorlds.Add(world);
        
        return world;
    }

    public static void InitialEmptyWorldForUser(World world)
    {
        AppBrushLibrary.SelectedIndex.Value = 0;

        new NewPolylineLayerCmd { WorkingWorld = world }
            .Combine(new ChangeWorkingLayerCmd(0))
            .DoAllCombination();
    }

    public static void Remove([NotNull] World world)
    {
        if (!LoadedWorlds.Contains(world)) throw new Sys.KeyNotFoundException("The specified world does not exist.");
        
        // Remove working world
        LoadedWorlds.Remove(world);
        if(WorkingWorld.Value == world) WorkingWorld.Value = LoadedWorlds.Count > 0 ? LoadedWorlds[0] : null;

        // Dispose or free managers
        world.Document().Get<CommandManager>().Free();
        
        // Dispose world
        world.Clear();
    }
    
    public static void Clear()
    {
        // Don't use `clear` on LoadedWorlds since it will trigger reset rather than remove event.
        foreach (var world in LoadedWorlds.ToList())
        {
            Remove(world);
        }
    }
    
    public static Entity Document([NotNull] this World world)
    {
        return world.GetEntity(0);
    }
}