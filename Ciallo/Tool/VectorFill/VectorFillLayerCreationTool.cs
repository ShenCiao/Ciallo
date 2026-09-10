using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Ciallo.Command;
using Ciallo.Data;
using Ciallo.Rendering;
using Ciallo.Widget;
using Frent;
using Godot;
using ObservableCollections;
using R3;
using Stateless;

namespace Ciallo.Tool;

using StateMachine = StateMachine<InteractionState, Trigger>;

[RegisterState]
public class VectorFillLayerCreationTool : InteractionScope, IPropertyProvider, ILayerDependent
{
    public enum CreationStrategy
    {
        WithinCurrentCel,
        WithinAllCels, // Put new vector fill layers into the same cel.
        // If exposed cels are already regular folders, put new layers into corresponding regular folders.
        // If not, wrap a new layer and their reference layers into a new regular folder, named by the exposed cel.
        NewCelFolder, // Create a cel folder and put new vector fill layers into it
    }

    public readonly ReactiveProperty<CreationStrategy> Strategy = new(CreationStrategy.WithinAllCels);
    [Substate]
    internal VectorFillLayerCreationHover Hover;

    public override void ConfigureStateMachine(StateMachine sm)
    {
        sm.Configure(this)
            .InitialTransition(Hover);
        sm.Configure(Hover)
            .InternalTransition(Trigger.Press(MouseButton.Left), OnCreate)
            .PermitReentry(Trigger.Refresh);
    }

    public static bool CanHandleLayers(ImmutableArray<Entity> layers) =>
        layers[0].Has<ShapeLayerSetting>();

    public void DrawPropertyAfterSubstates(PropertyContainer container)
    {
        container.AddChild(new Label
        {
            Text = "[Vector Fill On Shape Layer Hint]".Tr(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        });

        var list = new ItemList()
        {
            AutoHeight = true,
            AutoWidth = true,
        }.BindEnum(Strategy);
        container.AddProperty("Cel creation method", list)
            .VisibleIf(Document.Get<SelectionManager>().WorkingCelFolder, e => !e.IsNull);
    }

    public void OnCreate()
    {
        var celFolder = Document.Get<SelectionManager>().WorkingCelFolder.CurrentValue;
        if (celFolder.IsNull || Strategy.Value == CreationStrategy.WithinCurrentCel)
        {
            CreateSingleVectorFillLayer();
            return;
        }

        switch (Strategy.Value)
        {
            case CreationStrategy.WithinAllCels:
                CreateVectorFillLayersWithinCelFolder(celFolder);
                break;
            case CreationStrategy.NewCelFolder:
                CreateVectorFillLayersInNewCelFolder(celFolder);
                break;
        }
    }

    private void CreateSingleVectorFillLayer()
    {
        var vectorFillLayer = Document.World.Create();
        var referenceLayers = GetVisibleShapeLayers(Document.World);

        var cmd = new CommandBuilder("Create Vector Fill Layer", vectorFillLayer)
            .NewVectorFillLayer();
        AddReferenceLayers(cmd, referenceLayers)
            .AddToLayerTree(Document, 0)
            .SetLayerSelection()
            .Commit();
    }

    private void CreateVectorFillLayersWithinCelFolder(Entity celFolder)
    {
        SortedList<int, Entity> sourceExposures = new(celFolder.Get<FolderLayerSetting>().Exposures);
        var plans = BuildCelVectorFillPlans(sourceExposures);
        if (plans.Count == 0) return;

        var focusSourceCel = ResolveFocusSourceCel(celFolder);
        Entity focusVectorFillLayer = Entity.Null;

        var cmd = new CommandBuilder("Create Vector Fill Layers", Document);
        foreach (var plan in plans)
        {
            var parent = plan.SourceCel;
            if (!IsRegularFolder(parent))
                parent = WrapCelInRegularFolder(cmd, celFolder, plan.SourceCel);

            var vectorFillLayer = Document.World.Create();
            cmd.SetTarget(vectorFillLayer)
                .NewVectorFillLayer();
            AddReferenceLayers(cmd, plan.ReferenceLayers)
                .AddToLayerTree(parent, 0);

            if (focusVectorFillLayer.IsNull || plan.SourceCel == focusSourceCel)
                focusVectorFillLayer = vectorFillLayer;
        }

        if (!focusVectorFillLayer.IsNull)
            cmd.SetTarget(focusVectorFillLayer)
                .SetLayerSelection(recordCelSelectionPreference: true);

        cmd.Commit();
    }

    private void CreateVectorFillLayersInNewCelFolder(Entity celFolder)
    {
        SortedList<int, Entity> sourceExposures = new(celFolder.Get<FolderLayerSetting>().Exposures);
        var plans = BuildCelVectorFillPlans(sourceExposures);
        if (plans.Count == 0) return;

        var newCelFolder = Document.World.Create();
        var sourceNode = celFolder.Get<LayerTreeNode>();
        var focusSourceCel = ResolveFocusSourceCel(celFolder);
        var fillLayersBySourceCel = new Dictionary<Entity, Entity>();
        Entity focusVectorFillLayer = Entity.Null;

        var cmd = new CommandBuilder("Create Vector Fill Cel Folder", newCelFolder)
            .NewCelFolder()
            .SetProperty(e => e.Get<CommonLayerSetting>().Name, GetVectorFillCelFolderName(celFolder))
            .AddToLayerTree(sourceNode.ParentValue, sourceNode.Index);

        foreach (var plan in plans)
        {
            var vectorFillLayer = Document.World.Create();
            fillLayersBySourceCel[plan.SourceCel] = vectorFillLayer;

            cmd.SetTarget(vectorFillLayer)
                .NewVectorFillLayer()
                .SetProperty(e => e.Get<CommonLayerSetting>().Name, plan.SourceCel.Get<CommonLayerSetting>().Name.Value);
            AddReferenceLayers(cmd, plan.ReferenceLayers)
                .AddToLayerTree(newCelFolder);

            if (focusVectorFillLayer.IsNull || plan.SourceCel == focusSourceCel)
                focusVectorFillLayer = vectorFillLayer;
        }

        cmd.SetTarget(newCelFolder)
            .SetObservableCollection(
                e => e.Get<FolderLayerSetting>().Exposures,
                exposures => AddCorrespondingExposures(
                    exposures,
                    sourceExposures,
                    fillLayersBySourceCel,
                    newCelFolder));

        if (!focusVectorFillLayer.IsNull)
            cmd.SetTarget(focusVectorFillLayer)
                .SetLayerSelection(recordCelSelectionPreference: true);

        cmd.Commit();
    }

    private Entity WrapCelInRegularFolder(CommandBuilder cmd, Entity celFolder, Entity sourceCel)
    {
        var wrapper = Document.World.Create();
        var sourceNode = sourceCel.Get<LayerTreeNode>();
        var name = sourceCel.Get<CommonLayerSetting>().Name.Value;

        cmd.SetTarget(wrapper)
            .NewFolderLayer()
            .SetProperty(e => e.Get<CommonLayerSetting>().Name, name)
            .AddToLayerTree(celFolder, sourceNode.Index);

        cmd.SetTarget(celFolder)
            .SetObservableCollection(
                e => e.Get<FolderLayerSetting>().Exposures,
                exposures => ReplaceExposureValue(exposures, sourceCel, wrapper));

        cmd.SetTarget(Document)
            .MoveLayer(sourceCel, wrapper, 0);

        return wrapper;
    }

    private static List<CelVectorFillPlan> BuildCelVectorFillPlans(
        SortedList<int, Entity> sourceExposures)
    {
        var seen = new HashSet<Entity>();
        var plans = new List<CelVectorFillPlan>();
        foreach (var sourceCel in sourceExposures.Values)
        {
            if (!sourceCel.IsCelFolder && seen.Add(sourceCel))
                plans.Add(new(sourceCel, GetReferenceShapeLayersForCel(sourceCel)));
        }

        return plans;
    }

    /// <summary>
    /// Picks the source cel whose newly-created vector fill layer should become the primary layer.
    /// Prefers the cel exposed at the current frame, then falls back to the current primary layer's cel.
    /// </summary>
    private Entity ResolveFocusSourceCel(Entity celFolder)
    {
        var exposures = celFolder.Get<FolderLayerSetting>().Exposures;
        if (exposures is { Count: > 0 })
        {
            var currentFrame = Document.Get<SelectionManager>().CurrentFrame.Value;
            int index = exposures.FloorIndex(currentFrame);
            if (index >= 0)
                return exposures.GetValueAtIndex(index);
        }

        return FindCelUnderCelFolder(PrimaryLayer, celFolder);
    }

    private static Entity FindCelUnderCelFolder(Entity layer, Entity celFolder)
    {
        var cursor = layer;
        while (!cursor.IsNull && !cursor.IsDocument && cursor.Has<LayerTreeNode>())
        {
            if (cursor.Tagged<CelTag>() && cursor.Get<LayerTreeNode>().ParentValue == celFolder)
                return cursor;

            cursor = cursor.Get<LayerTreeNode>().ParentValue;
        }

        return Entity.Null;
    }

    private static void AddCorrespondingExposures(
        ObservableSortedList<int, Entity> targetExposures,
        SortedList<int, Entity> sourceExposures,
        IReadOnlyDictionary<Entity, Entity> fillLayersBySourceCel,
        Entity targetCelFolder)
    {
        foreach (var (frame, sourceCel) in sourceExposures)
        {
            if (sourceCel.IsCelFolder)
                targetExposures.Add(frame, targetCelFolder);
            else if (fillLayersBySourceCel.TryGetValue(sourceCel, out var fillLayer))
                targetExposures.Add(frame, fillLayer);
        }
    }

    private static void ReplaceExposureValue(
        ObservableSortedList<int, Entity> exposures,
        Entity oldCel,
        Entity newCel)
    {
        foreach (var pair in exposures.ToArray())
        {
            if (pair.Value == oldCel)
                exposures[pair.Key] = newCel;
        }
    }

    private static CommandBuilder AddReferenceLayers(
        CommandBuilder cmd,
        IReadOnlyCollection<Entity> referenceLayers)
    {
        if (referenceLayers.Count == 0)
            return cmd;

        return cmd.SetObservableCollection(
            e => e.Get<VectorFillLayerSetting>().ReferenceLayers,
            layers => layers.AddRange(referenceLayers));
    }

    private static List<Entity> GetVisibleShapeLayers(World world)
    {
        var result = new List<Entity>();
        var query = world.CreateQuery().With<ShapeLayerSetting>().Tagged<ToSerializeTag>().Build();
        foreach (var layer in query.EnumerateWithEntities())
        {
            if (IsVisibleShapeLayer(layer))
                result.Add(layer);
        }

        return result;
    }

    /// <summary>
    /// Collects visible shape layers inside a source cel to use as the new vector fill layer's references.
    /// A shape-layer cel references itself; a regular-folder cel references visible shape descendants.
    /// </summary>
    private static List<Entity> GetReferenceShapeLayersForCel(Entity cel)
    {
        var result = new List<Entity>();
        CollectReferenceShapeLayers(cel, result);
        return result;
    }

    private static void CollectReferenceShapeLayers(Entity layer, List<Entity> result)
    {
        // 🔧 Change ReferenceShapeLayers rules here when vector fill reference eligibility changes.
        if (!IsValidLayer(layer) || !layer.Tagged<ToSerializeTag>())
            return;

        if (layer.Has<ShapeLayerSetting>())
        {
            if (IsVisibleShapeLayer(layer))
                result.Add(layer);
            return;
        }

        if (!IsRegularFolder(layer))
            return;

        foreach (var child in layer.Get<LayerTreeNode>().Children)
            CollectReferenceShapeLayers(child, result);
    }

    private static bool IsVisibleShapeLayer(Entity layer) =>
        IsValidLayer(layer)
        && layer.Tagged<ToSerializeTag>()
        && layer.Has<ShapeLayerSetting>()
        && layer.Get<CommonLayerSetting>().IsVisible.Value;

    private static bool IsRegularFolder(Entity layer) =>
        layer.TryGet<FolderLayerSetting>() is { IsCelFolder: false };

    private static bool IsValidLayer(Entity layer) =>
        !layer.IsNull
        && layer.IsAlive
        && layer.Has<LayerTreeNode>()
        && layer.Has<CommonLayerSetting>();

    private static string GetVectorFillCelFolderName(Entity sourceCelFolder)
    {
        string sourceName = sourceCelFolder.Get<CommonLayerSetting>().Name.Value;
        return string.IsNullOrWhiteSpace(sourceName) ? "Vector fill" : $"{sourceName} Fill";
    }

    private readonly record struct CelVectorFillPlan(Entity SourceCel, List<Entity> ReferenceLayers);
}

[RegisterState]
public class VectorFillLayerCreationHover : Interaction, IPropertyProvider
{
    public void DrawPropertyBeforeSubstates(PropertyContainer container) =>
        AppPreference.BucketFill.DrawModeProperty(container);

    public override void Start(CursorButtonData data)
    {
        Document.Get<WorldBody>().DefaultCursorShape = Control.CursorShape.PointingHand;
    }

    public override void Moving(CursorMotionData data) { }

    public override void End(CursorButtonData data) => Cancel();
    public override void Cancel()
    {
        Document.Get<WorldBody>().DefaultCursorShape = default;
    }

    public override bool OnKey(InputEventKey key, CursorButtonData data) => false;
}
