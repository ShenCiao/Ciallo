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
    }

    public override void _Ready()
    {
        IdPressed += OnMenuSelected;
    }

    public void Popup(Entity targetLayer, bool showTimelineLayerActions)
    {
        _targetLayer = targetLayer;
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
        AddItem("Delete Layer".Tr(), (int)MenuItem.DeleteLayer);
        AddItem("Wrap Self into Folder".Tr(), (int)MenuItem.WrapSelfInFolder);
        if (_targetLayer.Has<ShapeLayerSetting>())
            AddItem("Split Stroke and Fill".Tr(), (int)MenuItem.SplitStrokeAndFill);
        if (_targetLayer.Has<FolderLayerSetting>())
        {
            if (_targetLayer.Get<FolderLayerSetting>().IsCelFolder)
                AddItem("Rename Cels by Exposure".Tr(), (int)MenuItem.RenameCelsByExposure);
            AddItem("Wrap Children in Folders".Tr(), (int)MenuItem.WrapChildrenInFolders);
            AddItem("Ungroup Folder".Tr(), (int)MenuItem.UngroupFolder);
        }
    }

    private void OnMenuSelected(long id)
    {
        if (_targetLayer.IsNull || !_targetLayer.IsAlive)
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
                LayerContextActions.DeleteLayer(_targetLayer);
                break;
            case MenuItem.UngroupFolder:
                LayerContextActions.UngroupFolder(_targetLayer);
                break;
            case MenuItem.RenameCelsByExposure:
                LayerContextActions.RenameCelsByExposure(_targetLayer);
                break;
            case MenuItem.WrapChildrenInFolders:
                LayerContextActions.WrapChildrenInFolders(_targetLayer);
                break;
            case MenuItem.WrapSelfInFolder:
                LayerContextActions.WrapSelfInFolder(_targetLayer);
                break;
            case MenuItem.SplitStrokeAndFill:
                LayerContextActions.SplitStrokeAndFill(_targetLayer);
                break;
        }
    }
}
