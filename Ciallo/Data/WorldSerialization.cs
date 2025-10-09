using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using Ciallo.Command;
using Ciallo.Misc;
using Godot;
using Massive;
using MessagePack;

namespace Ciallo.Data;

public static partial class AppWorldManager
{
    public static readonly HashSet<Type> ToSerializeTypes = [..GetToSerializeTypes()];
    public static readonly HashSet<Type> ToSerializeTags = ToSerializeTypes.Where(t => t.IsTag()).ToHashSet();

    public static void CopyWorldByData(Entity dataDocument)
    {
        // Load brushes
        var resultWorld = Create(dataDocument.Get<DocumentSetting>());
        WorkingWorld.Value = resultWorld;
        Dictionary<Entity, Entity> brushMap = [];
        foreach (var brushDataE in dataDocument.Get<BrushManager>().Brushes)
        {
            var setting = brushDataE.Get<BrushSetting>();
            var cmd = new NewBrushCmd(setting);
            cmd.Do();
            brushMap.Add(brushDataE, cmd.BrushE);
        }
        
        // Load layers and strokes
        var dataTreeRoot = dataDocument.Get<LayerTreeManager>().Root;
        Dictionary<Entity, Entity> layerMap = [];
        foreach (var layerDataE in dataTreeRoot.Children)
        {
            if (layerDataE.Has<PolylineLayerSetting>())
            {
                var setting = layerDataE.Get<PolylineLayerSetting>();
                var newPolylineLayerCmd = new NewPolylineLayerCmd(setting);
                newPolylineLayerCmd.Do();
                var layerE = newPolylineLayerCmd.LayerE;
                layerMap.Add(layerDataE, layerE);

                foreach (var polylineDataE in layerDataE.Get<LayerTreeNode>().Children)
                {
                    var geometry = polylineDataE.Get<StrokeGeometry>();
                    var newStrokeCmd = new NewStrokeCmd(layerE);
                    newStrokeCmd.Do();
                    var polylineE = newStrokeCmd.StrokeE;
                    new SetStrokeGeometryCmd(polylineE, geometry).Do();
                    
                    var strokeBrush = polylineDataE.Get<StrokeBrush>();
                    new ChangeStrokeBrushCmd(polylineE, brushMap[strokeBrush.Value]).Do();
                }
            }
        }
        
        // Load selection
        var dataSm = dataDocument.Get<SelectionManager>();
        new ChangeWorkingLayerCmd(layerMap[dataSm.WorkingLayer.CurrentValue]).Do();
        var idx = dataDocument.Get<BrushManager>().Brushes.IndexOf(dataSm.WorkingBrush.CurrentValue);
        if (idx != -1) new ChangeWorkingBrushCmd(idx).Do();
    }

    public static void SaveWorkingWorld()
    {
        if (WorkingDocument.CurrentValue.IsNull()) return;
        var settings = WorkingDocument.CurrentValue.Get<DocumentSetting>();
        if (CanSaveFile(settings.FilePath.Value))
            Save(WorkingWorld.Value, settings.FilePath.Value);
    }

    public static void ReloadWorkingWorld() // for debug
    {
        if(WorkingDocument.CurrentValue.IsNull()) return;
        var settings = WorkingDocument.CurrentValue.Get<DocumentSetting>();
        if (!File.Exists(settings.FilePath.Value)) return;
        var world = Load(settings.FilePath.Value, out var document);
        CopyWorldByData(document);
    }

    public static void Save(World world, string filePath)
    {
        var bins = Serialize(world);
        var writer = new ZipPacker();
        var err = writer.Open(filePath);
        if (err != Error.Ok) throw new InvalidOperationException($"Cannot open file {filePath} for writing.");
        writer.StartFile("EntityComponent.bin");
        writer.WriteFile(bins[0]);
        writer.CloseFile();
        writer.StartFile("ComponentData.bin");
        writer.WriteFile(bins[1]);
        writer.CloseFile();
        writer.Close();
    }
    
    public static World Load(string filePath, out Entity document)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException($"File {filePath} not found.");
        var reader = new ZipReader();
        var err = reader.Open(filePath);
        var ecBin = reader.ReadFile("EntityComponent.bin");
        var componentBin = reader.ReadFile("ComponentData.bin");
        reader.Close();

        var world = Deserialize([ecBin, componentBin], out document);
        document.Get<DocumentSetting>().FilePath.Value = filePath;
        return world;
    }

    /// <remarks>
    /// Serialize all entities with <see cref="ToSerializeTag"/> and their components marked with <see cref="ToSerializeAttribute"/>.
    /// The result is two binary blobs:
    /// 1. Entity-Component structure data: List(List(Type))`, each inner list corresponds to an entity and contains its component types.
    /// 2. Component data: `Dictionary(Type, List(byte[]))`, each list contains the data of that component type for all entities in order.
    /// </remarks>
    public static byte[][] Serialize([NotNull] World world)
    {
        SortedDictionary<int, List<Type>> sortedEcData = [];
        foreach (var t in ToSerializeTypes)
        {
            var filter = (Filter)typeof(DynamicFilter)
                .GetMethod("Include")!
                .MakeGenericMethod(t)
                .Invoke(new DynamicFilter(world).Include<ToSerializeTag>(), null);
            world.Filter(filter).ForEach(id =>
            {
                if(!sortedEcData.ContainsKey(id))
                    sortedEcData[id] = [];
                sortedEcData[id].Add(t);
            });
        }
        
        var ecBin = MessagePackSerializer.Serialize(sortedEcData.Values.ToList());
        
        EntityToIndexFormatter.Instance.EntityList = sortedEcData.Keys.Select(world.GetEntity).ToList();
        
        // Note, directly using List<object> will cause losing type information in deserialization.
        Dictionary<Type, List<byte[]>> componentData = [];
        Dictionary<Type, MethodInfo> worldGetFunctions = [];
        var getMethodDef = typeof(WorldIdExtensions).GetMethods().Single(info=>info.Name == "Get");
        foreach (var t in ToSerializeTypes.Where(t => !t.IsTag()))
            worldGetFunctions[t] = getMethodDef!.MakeGenericMethod(t);
        
        foreach (var (id, types) in sortedEcData)
        {
            foreach (var t in types)
            {
                if (ToSerializeTags.Contains(t)) continue;
                
                var obj = worldGetFunctions[t].Invoke(null, [world, id]);
                if (!componentData.ContainsKey(t)) componentData[t] = [];
                var bytes = MessagePackSerializer.Serialize(t, obj);
                componentData[t].Add(bytes);
            }
        }
        
        var componentBin = MessagePackSerializer.Serialize(componentData);
        
        return [ecBin, componentBin];
    }

    public static World Deserialize(byte[][] bins, out Entity document)
    {
        var world = new World();
        
        var ecBin = bins[0];
        var ecData = MessagePackSerializer.Deserialize<List<List<Type>>>(ecBin);
        var entities = new List<Entity>(ecData.Count);
        foreach (var types in ecData)
        {
            var e = world.CreateEntity();
            entities.Add(e);
        }
        document = entities[0];
        
        EntityToIndexFormatter.Instance.EntityList = entities;
        
        var componentBin = bins[1];
        var componentData = MessagePackSerializer.Deserialize<Dictionary<Type, Queue<byte[]>>>(componentBin);
        Dictionary<Type, MethodInfo> entityAddFunctions = [];
        Dictionary<Type, MethodInfo> entitySetFunctions = [];
        var addMethodDef = typeof(Entity).GetMethod("Add");
        var setMethodDef = typeof(Entity).GetMethod("Set");
        foreach (var t in ToSerializeTypes.Where(t => !t.IsTag()))
        {
            entityAddFunctions[t] = addMethodDef!.MakeGenericMethod(t);
            entitySetFunctions[t] = setMethodDef!.MakeGenericMethod(t);
        }
        foreach (var (idx, ts) in ecData.Index())
        {
            var e = entities[idx];
            foreach (var t in ts)
            {
                if (t.IsTag()) entityAddFunctions[t].Invoke(e, null);
                else
                {
                    if (!componentData.TryGetValue(t, out var dataQueue)) continue;
                    var bytes = dataQueue.Dequeue();
                    var component = MessagePackSerializer.Deserialize(t, bytes);
                    entitySetFunctions[t].Invoke(e, [component]);
                }
            }
        }
        
        return world;
    }

    public static IEnumerable<Type> GetToSerializeTypes()
    {
        var allTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a =>
        {
            try
            {
                return a.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.Where(t => t != null);
            }
        }).Where(t => t is { IsAbstract: false });

        return allTypes.Where(t => t!.GetCustomAttributes(typeof(ToSerializeAttribute), false).Length > 0);
    }

    public static bool IsTag(this Type type)
    {
        if (!type.IsValueType || type.IsEnum || type.IsPrimitive)
            return false;

        var fields = type.GetFields(
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly
        );

        return fields.Length == 0;
    }

    public static bool CanSaveFile(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory)) return false;
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        try
        {
            // has write permission.
            using var x = File.Create(filePath, 1, FileOptions.DeleteOnClose);
            return true;
        }
        catch
        {
            return false;
        }
    }
}