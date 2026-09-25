using System.Collections.Immutable;
using System.Linq;
using Ciallo.Data;
using Frent;
using Godot;

namespace Ciallo.GuiControl;

/// <summary>
/// Shared right-click context menu for layer header labels.
/// One node lives in each layer-tree scene and is shown only from LabelLineEdit right-clicks.
/// </summary>
public partial class LayerRightClickMenu : PopupMenu
{
    private Entity _targetLayer;
    private ImmutableArray<Entity> _targetLayers;
    private bool _showTimelineLayerActions;
    private ArchetypeContext _archetypes;

    private enum MenuItem
    {
        NewShapeLayer,
        NewFolderLayer,
        NewCelFolderLayer,
        DeleteLayer,
        UngroupFolder,
        RenameCelsByExposure,
        WrapChildrenInFolders,
        WrapSelfInFolder,
        SplitStrokeAndFill,
        MergeLayers,
    }

    public override void _Ready()
    {
        IdPressed += OnMenuSelected;
    }

    public void Popup(Entity targetLayer, bool showTimelineLayerActions)
    {
        _archetypes = null;
        _targetLayer = targetLayer;
        _targetLayers = LayerSelectionActions.ContextLayers(targetLayer);
        _showTimelineLayerActions = showTimelineLayerActions;

        RebuildMenu();

        Position = DisplayServer.MouseGetPosition();
        base.Popup();
    }

    public void PopupArchetypes(Entity folder, string name)
    {
        _archetypes = ArchetypeContextActions.Capture(folder, name);
        _targetLayers = _archetypes.Layers;
        RebuildMenu();
        Position = DisplayServer.MouseGetPosition();
        base.Popup();
    }

    private void RebuildMenu()
    {
        Clear();

        if (_archetypes != null)
        {
            AddSeparator("All Cels");
            AddItem("New Shape Layer", (int)MenuItem.NewShapeLayer);
            AddItem("New Folder Layer", (int)MenuItem.NewFolderLayer);
            AddSeparator();
            AddItem("Delete Layers", (int)MenuItem.DeleteLayer);
            AddItem("Group Layers", (int)MenuItem.WrapSelfInFolder);
            SetItemDisabled(GetItemIndex((int)MenuItem.WrapSelfInFolder), !ArchetypeContextActions.CanGroup(_archetypes));
            AddItem("Split Stroke and Fill", (int)MenuItem.SplitStrokeAndFill);
            SetItemDisabled(GetItemIndex((int)MenuItem.SplitStrokeAndFill), !ArchetypeContextActions.CanSplit(_archetypes));
            AddItem("Merge Layers", (int)MenuItem.MergeLayers);
            SetItemDisabled(GetItemIndex((int)MenuItem.MergeLayers), !ArchetypeContextActions.CanMerge(_archetypes));
            if (ArchetypeContextActions.AreFolders(_archetypes))
            {
                AddItem("Wrap Children in Folders", (int)MenuItem.WrapChildrenInFolders);
                AddItem("Ungroup Folder", (int)MenuItem.UngroupFolder);
            }
            return;
        }

        bool targetIsCelFolder = _targetLayer.TryGet<FolderLayerSetting>() is { IsCelFolder: true };
        AddItem(targetIsCelFolder ? "Add Shape Layer to All Cels" : "New Shape Layer", (int)MenuItem.NewShapeLayer);
        AddItem("New Folder Layer", (int)MenuItem.NewFolderLayer);
        if (_showTimelineLayerActions)
            AddItem("New Cel Folder Layer", (int)MenuItem.NewCelFolderLayer);

        AddSeparator();
        AddItem(_targetLayers.Length > 1 ? "Delete Layers" : "Delete Layer", (int)MenuItem.DeleteLayer);
        AddItem(_targetLayers.Length > 1 ? "Group Layers" : "Wrap Self into Folder", (int)MenuItem.WrapSelfInFolder);
        SetItemDisabled(GetItemIndex((int)MenuItem.WrapSelfInFolder), !LayerContextActions.CanGroupLayers(_targetLayers));
        if (_targetLayers.All(e => e.Has<ShapeLayerSetting>()))
            AddItem("Split Stroke and Fill", (int)MenuItem.SplitStrokeAndFill);
        if (_targetLayers.Length > 1)
        {
            AddItem("Merge Layers", (int)MenuItem.MergeLayers);
            SetItemDisabled(GetItemIndex((int)MenuItem.MergeLayers), !LayerConversionActions.CanMerge(_targetLayers));
        }
        if (_targetLayers.All(e => e.Has<FolderLayerSetting>()))
        {
            if (_targetLayers.All(e => e.Get<FolderLayerSetting>().IsCelFolder))
                AddItem("Rename Cels by Exposure", (int)MenuItem.RenameCelsByExposure);
            AddItem("Wrap Children in Folders", (int)MenuItem.WrapChildrenInFolders);
            AddItem("Ungroup Folder", (int)MenuItem.UngroupFolder);
        }
    }

    private void OnMenuSelected(long id)
    {
        if (_targetLayers.Any(e => !e.IsAlive || !e.Tagged<ToSerializeTag>()))
            return;

        if (_archetypes != null)
        {
            if (!_archetypes.Folder.IsAlive || !_archetypes.Folder.Tagged<ToSerializeTag>()) return;
            switch ((MenuItem)id)
            {
                case MenuItem.NewShapeLayer:
                    ArchetypeContextActions.NewLayer(_archetypes, folder: false);
                    break;
                case MenuItem.NewFolderLayer:
                    ArchetypeContextActions.NewLayer(_archetypes, folder: true);
                    break;
                case MenuItem.DeleteLayer:
                    ArchetypeContextActions.Delete(_archetypes);
                    break;
                case MenuItem.WrapSelfInFolder when ArchetypeContextActions.CanGroup(_archetypes):
                    ArchetypeContextActions.Group(_archetypes);
                    break;
                case MenuItem.SplitStrokeAndFill when ArchetypeContextActions.CanSplit(_archetypes):
                    ArchetypeContextActions.Split(_archetypes);
                    break;
                case MenuItem.MergeLayers when ArchetypeContextActions.CanMerge(_archetypes):
                    ArchetypeContextActions.Merge(_archetypes);
                    break;
                case MenuItem.UngroupFolder when ArchetypeContextActions.AreFolders(_archetypes):
                    ArchetypeContextActions.Ungroup(_archetypes);
                    break;
                case MenuItem.WrapChildrenInFolders when ArchetypeContextActions.AreFolders(_archetypes):
                    ArchetypeContextActions.WrapChildren(_archetypes);
                    break;
            }
            return;
        }

        switch ((MenuItem)id)
        {
            case MenuItem.NewShapeLayer:
                LayerContextActions.NewShapeLayer(_targetLayer);
                break;
            case MenuItem.NewFolderLayer:
                LayerContextActions.NewFolderLayer(_targetLayer);
                break;
            case MenuItem.NewCelFolderLayer:
                LayerContextActions.NewCelFolderLayer(_targetLayer);
                break;
            case MenuItem.DeleteLayer:
                LayerContextActions.DeleteLayers(_targetLayers);
                break;
            case MenuItem.UngroupFolder:
                LayerContextActions.UngroupFolders(_targetLayers);
                break;
            case MenuItem.RenameCelsByExposure:
                LayerContextActions.RenameCelsByExposure(_targetLayers);
                break;
            case MenuItem.WrapChildrenInFolders:
                LayerContextActions.WrapChildrenInFolders(_targetLayers);
                break;
            case MenuItem.WrapSelfInFolder:
                if (LayerContextActions.CanGroupLayers(_targetLayers))
                    LayerContextActions.GroupLayers(_targetLayers);
                break;
            case MenuItem.SplitStrokeAndFill:
                LayerContextActions.SplitStrokeAndFill(_targetLayers);
                break;
            case MenuItem.MergeLayers:
                if (LayerConversionActions.CanMerge(_targetLayers))
                    LayerConversionActions.Merge(_targetLayers);
                break;
        }
    }
}
