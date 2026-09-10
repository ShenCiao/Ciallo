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
        _targetLayer = targetLayer;
        _targetLayers = LayerSelectionActions.ContextLayers(targetLayer);
        _showTimelineLayerActions = showTimelineLayerActions;

        RebuildMenu();

        Position = DisplayServer.MouseGetPosition();
        base.Popup();
    }

    private void RebuildMenu()
    {
        Clear();

        bool targetIsCelFolder = _targetLayer.TryGet<FolderLayerSetting>() is { IsCelFolder: true };
        AddItem((targetIsCelFolder ? "Add Shape Layer to All Cels" : "New Shape Layer").Tr(), (int)MenuItem.NewShapeLayer);
        AddItem("New Folder Layer".Tr(), (int)MenuItem.NewFolderLayer);
        if (_showTimelineLayerActions)
            AddItem("New Cel Folder Layer".Tr(), (int)MenuItem.NewCelFolderLayer);

        AddSeparator();
        AddItem((_targetLayers.Length > 1 ? "Delete Layers" : "Delete Layer").Tr(), (int)MenuItem.DeleteLayer);
        AddItem((_targetLayers.Length > 1 ? "Group Layers" : "Wrap Self into Folder").Tr(), (int)MenuItem.WrapSelfInFolder);
        SetItemDisabled(GetItemIndex((int)MenuItem.WrapSelfInFolder), !LayerContextActions.CanGroupLayers(_targetLayers));
        if (_targetLayers.All(e => e.Has<ShapeLayerSetting>()))
            AddItem("Split Stroke and Fill".Tr(), (int)MenuItem.SplitStrokeAndFill);
        if (_targetLayers.Length > 1)
        {
            AddItem("Merge Layers".Tr(), (int)MenuItem.MergeLayers);
            SetItemDisabled(GetItemIndex((int)MenuItem.MergeLayers), !LayerConversionActions.CanMerge(_targetLayers));
        }
        if (_targetLayers.All(e => e.Has<FolderLayerSetting>()))
        {
            if (_targetLayers.All(e => e.Get<FolderLayerSetting>().IsCelFolder))
                AddItem("Rename Cels by Exposure".Tr(), (int)MenuItem.RenameCelsByExposure);
            AddItem("Wrap Children in Folders".Tr(), (int)MenuItem.WrapChildrenInFolders);
            AddItem("Ungroup Folder".Tr(), (int)MenuItem.UngroupFolder);
        }
    }

    private void OnMenuSelected(long id)
    {
        if (_targetLayers.Any(e => !e.IsAlive || !e.Tagged<ToSerializeTag>()))
            return;

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
