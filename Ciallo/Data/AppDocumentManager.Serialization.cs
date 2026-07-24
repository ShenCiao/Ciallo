using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Ciallo.Command;
using Ciallo.Data;
using Frent;
using Frent.Core;
using Godot;

namespace Ciallo.Data;

public static partial class AppDocumentManager
{
    // Pitfall: If serialize an empty class without any [DataMember], then add a new [DataMember] later version and deserialize it back.
    // MessagePack will throw error without any useful information.
    public static readonly HashSet<Type> ToSerializeTypes = [.. GetToSerializeTypes()];
    public static readonly HashSet<Type> ToSerializeTags = ToSerializeTypes.Where(t => t.IsTag()).ToHashSet();
    public static readonly HashSet<Type> ToSerializeComponents = ToSerializeTypes.Except(ToSerializeTags).ToHashSet();

    static AppDocumentManager()
    {
        var registerMethod =
            typeof(Component).GetMethod("RegisterComponent", BindingFlags.Public | BindingFlags.Static);
        foreach (var t in ToSerializeComponents)
        {
            var genericMethod = registerMethod!.MakeGenericMethod(t);
            genericMethod.Invoke(null, null);
        }
    }

    // Slop design here
    public static void CopyWorldByData(Entity dataDocument)
    {
        Dictionary<Entity, Entity> entityMap = new() { { Entity.Null, Entity.Null } };

        var resultDocument = Create(dataDocument.Get<DocumentSetting>());
        entityMap.Add(dataDocument, resultDocument);
        WorkingDocument.Value = resultDocument;
        // Pre-create all result entities upfront (mirrors Serialize's Tagged<ToSerializeTag> query).
        var dataWorld = dataDocument.World;
        var resultWorld = resultDocument.World;
        var normalEntityQuery = dataWorld.CreateQuery().Tagged<ToSerializeTag>().Build();
        foreach (var dataE in normalEntityQuery.EnumerateWithEntities())
        {
            if (dataE == dataDocument) continue;
            entityMap.Add(dataE, resultWorld.Create());
        }

        // Load brushes
        var loadBrushCmd = new CommandBuilder("Load Brushes");
        foreach (var strokeBrushDataE in dataDocument.Get<BrushManager>().StrokeBrushes)
            loadBrushCmd.SetTarget(entityMap[strokeBrushDataE]).NewStrokeBrush(strokeBrushDataE);
        foreach (var vectorFillBrushDataE in dataDocument.Get<BrushManager>().VectorFillBrushes)
            loadBrushCmd.SetTarget(entityMap[vectorFillBrushDataE]).NewVectorFillBrush(vectorFillBrushDataE);
        loadBrushCmd.Do();

        // Load layers and strokes
        LoadChildrenLayer(dataDocument, resultDocument);

        void LoadChildrenLayer(Entity dataParentE, Entity resultParentE)
        {
            foreach (var layerDataE in dataParentE.Get<LayerTreeNode>().Children)
                LoadLayer(layerDataE, resultParentE);
        }

        void LoadLayer(Entity layerDataE, Entity resultParentE)
        {
            var layerResultE = entityMap[layerDataE];
            var cachedExposures = new SortedList<int, Entity>();
            if (layerDataE.Has<FolderLayerSetting>())
            {
                if (layerDataE.Get<FolderLayerSetting>().IsCelFolder)
                {
                    var dataExposures = layerDataE.Get<FolderLayerSetting>().Exposures;
                    foreach (var (frame, exposedE) in dataExposures.ToArray())
                    {
                        cachedExposures[frame] = entityMap[exposedE];
                    }
                    dataExposures.Clear();
                }

                new CommandBuilder("Load Folder Layer", layerResultE)
                    .NewFolderLayer(layerDataE)
                    .AddToLayerTree(resultParentE)
                    .Do();
                LoadChildrenLayer(layerDataE, layerResultE);
                // Post set exposures to avoid unready entity components added to timeline.
                foreach (var (frame, exposedE) in cachedExposures)
                {
                    layerResultE.Get<FolderLayerSetting>().Exposures[frame] = exposedE;
                }
            }
            else if (layerDataE.Has<ImageLayerSetting>())
            {
                new CommandBuilder("Load Image Layer", layerResultE)
                    .NewImageLayer(layerDataE)
                    .AddToLayerTree(resultParentE)
                    .Do();
            }
            else if (layerDataE.Has<ShapeLayerSetting>())
            {
                new CommandBuilder("Load Shape Layer", layerResultE)
                    .NewShapeLayer(layerDataE)
                    .AddToLayerTree(resultParentE)
                    .Do();

                foreach (var shapeDataE in layerDataE.Get<LayerTreeNode>().Children)
                {
                    if (shapeDataE.Has<StrokeSetting>())
                    {
                        new CommandBuilder("Load Stroke", entityMap[shapeDataE])
                            .NewStroke(shapeDataE, entityMap)
                            .AddToLayerTree(layerResultE)
                            .Do();
                    }
                    else if (shapeDataE.Has<FilledPolygonSetting>())
                    {
                        new CommandBuilder("Load Filled Polygon", entityMap[shapeDataE])
                            .NewFilledPolygon(shapeDataE, entityMap)
                            .AddToLayerTree(layerResultE)
                            .Do();
                    }
                }
            }
            else if (layerDataE.Has<VectorFillLayerSetting>())
            {
                new CommandBuilder("Load Vector Fill Layer", layerResultE)
                    .NewVectorFillLayer(layerDataE)
                    .AddToLayerTree(resultParentE)
                    .Do();

                foreach (var markerDataE in layerDataE.Get<LayerTreeNode>().Children)
                {
                    new CommandBuilder("Load Vector Fill Marker", entityMap[markerDataE])
                        .NewVectorFillMarker(markerDataE, entityMap)
                        .AddToLayerTree(layerResultE)
                        .Do();
                }
            }
        }

        // Vector fil reference layers remap
        var vectorFillLayerQuery = dataDocument.World.CreateQuery()
            .With<VectorFillLayerSetting>()
            .Tagged<ToSerializeTag>()
            .Build();
        foreach (var dataE in vectorFillLayerQuery.EnumerateWithEntities())
        {
            var resultE = entityMap[dataE];
            var newEs = dataE.Get<VectorFillLayerSetting>().ReferenceLayers.Select(e => entityMap[e]);
            resultE.Get<VectorFillLayerSetting>().ReferenceLayers.AddRange(newEs);
        }

        // Load selection
        var loadSelectionCmd = new CommandBuilder("Load Selection");
        var dataSm = dataDocument.Get<SelectionManager>();
        loadSelectionCmd.SetTarget(entityMap[dataSm.WorkingLayer.CurrentValue])
            .SetWorkingLayer(true);

        var dataStrokeBrushE = dataSm.WorkingStrokeBrush.Value;
        if (!dataStrokeBrushE.IsNull)
            loadSelectionCmd.SetTarget(entityMap[dataStrokeBrushE]).SetWorkingStrokeBrush().Do();
        var dataVectorFillBrushE = dataSm.WorkingVectorFillBrush.Value;
        if (!dataVectorFillBrushE.IsNull)
            loadSelectionCmd.SetTarget(resultDocument)
                .SetProperty(e => e.Get<SelectionManager>().WorkingVectorFillBrush, entityMap[dataVectorFillBrushE]);
        loadSelectionCmd.SetTarget(resultDocument)
            .SetProperty(e => e.Get<SelectionManager>().CurrentFrame, dataSm.CurrentFrame.Value);

        loadSelectionCmd.Do();

        // Load timeline setting
        resultDocument.Get<TimelineSetting>().CopyFrom(dataDocument.Get<TimelineSetting>());
    }

    public static bool SaveWorkingDocument()
    {
        if (WorkingDocument.CurrentValue.IsNull) return false;
        var settings = WorkingDocument.CurrentValue.Get<DocumentSetting>();
        try
        {
            EnsureSaveDirectory(settings.FilePath.Value);
            Save(WorkingDocument.Value, settings.FilePath.Value);
            WorkingDocument.CurrentValue.Get<CommandManager>().OnSave();
            AppDocumentDurability.EnqueueManualSave(WorkingDocument.CurrentValue, settings.FilePath.Value);
            return true;
        }
        catch (Exception exception)
        {
            WarnSaveFailed(exception);
            return false;
        }
    }

    public static bool SaveWorkingDocumentAs(string filePath)
    {
        if (WorkingDocument.CurrentValue.IsNull) return false;
        var settings = WorkingDocument.CurrentValue.Get<DocumentSetting>();
        var previousDocumentId = settings.DocumentId.Value;
        var previousName = settings.Name.Value;
        var previousFilePath = settings.FilePath.Value;
        try
        {
            EnsureSaveDirectory(filePath);
            settings.DocumentId.Value = Guid.NewGuid();
            settings.Name.Value = filePath.GetFile().GetBaseName();
            settings.FilePath.Value = filePath;
            Save(WorkingDocument.Value, filePath);
            WorkingDocument.CurrentValue.Get<CommandManager>().OnSave();
            AppDocumentDurability.EnqueueManualSave(WorkingDocument.CurrentValue, filePath);
            return true;
        }
        catch (Exception exception)
        {
            settings.DocumentId.Value = previousDocumentId;
            settings.Name.Value = previousName;
            settings.FilePath.Value = previousFilePath;
            WarnSaveFailed(exception);
            return false;
        }
    }

    public static void ReloadWorkingDocument() // for debug
    {
        if (WorkingDocument.CurrentValue.IsNull) return;
        var settings = WorkingDocument.CurrentValue.Get<DocumentSetting>();
        if (!File.Exists(settings.FilePath.Value)) return;
        var document = Load(settings.FilePath.Value);
        CopyWorldByData(document);
    }

    public static void Save(Entity document, string filePath)
    {
        DuckDbProjectSerializer.Save(document, filePath);
    }

    public static Entity Load(string filePath)
    {
        return DuckDbProjectSerializer.Load(filePath);
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

    private static void EnsureSaveDirectory(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(directory))
            throw new InvalidOperationException("Save path has no directory.");
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }

    private static void WarnSaveFailed(Exception exception)
    {
        GD.PrintErr(exception);
        AppDialogHost.WarnUser.DialogText = "Cannot save document.".Tr() + " " + exception.Message;
        AppDialogHost.WarnUser.Popup();
    }
}
