using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Ciallo.Command;
using Godot;
using Massive;
using ObservableCollections;
using R3;

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
        WorkingWorld.Select(w => w?.Document() ?? Entity.Null).ToReadOnlyReactiveProperty();
    
    private static readonly Sys.Dictionary<World, Entity> DocumentSingletons = [];

    public static World Create([NotNull] DocumentSetting settings)
    {
        // Only one loaded world is supported for current version.
        Clear();
        var world = World.Create();
        world.AddForbiddenComponents();

        // Init empty document
        var document = world.Create();
        DocumentSingletons.Add(world, document);

        // Add managers
        var layerTreeManager = new LayerTreeManager();
        var selectionManager = new SelectionManager();
        var commandManager = new CommandManager();
        var brushManager = new BrushManager();
        document.Add(settings, layerTreeManager, selectionManager, commandManager, brushManager);

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
        DocumentSingletons.Remove(world);
        world.Dispose();
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
        return DocumentSingletons[world];
    }

    public static void AddForbiddenComponents(this World world)
    {
        var throwError = new Action(() => throw new InvalidOperationException("Primitive types cannot be used as components."));
        // ReSharper disable UnusedParameter.Local
        world.SubscribeComponentAdded((in Entity e, ref Entity _)  => throwError()); // The most common mistake
        world.SubscribeComponentAdded((in Entity e, ref Type _) => throwError());
        world.SubscribeComponentAdded((in Entity e, ref int _)=> throwError());
        world.SubscribeComponentAdded((in Entity e, ref float _) => throwError());
        world.SubscribeComponentAdded((in Entity e, ref double _) => throwError());
        world.SubscribeComponentAdded((in Entity e, ref bool _) => throwError());
        world.SubscribeComponentAdded((in Entity e, ref string _) => throwError());
        world.SubscribeComponentAdded((in Entity e, ref char _) => throwError());
        world.SubscribeComponentAdded((in Entity e, ref Vector2 _) => throwError());
        world.SubscribeComponentAdded((in Entity e, ref Vector2I _) => throwError());
        world.SubscribeComponentAdded((in Entity e, ref Transform2D _) => throwError());
        world.SubscribeComponentAdded((in Entity e, ref Rect2 _) => throwError());
        world.SubscribeComponentAdded((in Entity e, ref Rect2I _) => throwError());
        var throwCollectionError = new Action(() => throw new InvalidOperationException("List of primitive types cannot be used as components."));
        world.SubscribeComponentAdded((in Entity e, ref Sys.List<Entity> _)  => throwCollectionError());
        world.SubscribeComponentAdded((in Entity e, ref Sys.List<int> _) => throwCollectionError());
        world.SubscribeComponentAdded((in Entity e, ref Sys.List<float> _) => throwCollectionError());
        world.SubscribeComponentAdded((in Entity e, ref Sys.List<double> _) => throwCollectionError());
        world.SubscribeComponentAdded((in Entity e, ref Sys.List<bool> _) => throwCollectionError());
        world.SubscribeComponentAdded((in Entity e, ref Sys.List<string> _) => throwCollectionError());
        world.SubscribeComponentAdded((in Entity e, ref Sys.List<char> _) => throwCollectionError());
        world.SubscribeComponentAdded((in Entity e, ref Sys.List<Vector2> _) => throwCollectionError());
        world.SubscribeComponentAdded((in Entity e, ref Sys.List<Vector2I> _) => throwCollectionError());
        world.SubscribeComponentAdded((in Entity e, ref Sys.List<Transform2D> _) => throwCollectionError());
        world.SubscribeComponentAdded((in Entity e, ref Sys.List<Rect2> _) => throwCollectionError());
        world.SubscribeComponentAdded((in Entity e, ref Sys.List<Rect2I> _) => throwCollectionError());
    }
    
    public static World GetWorldById(int id) => LoadedWorlds.Single(w => w.Id == id);
}