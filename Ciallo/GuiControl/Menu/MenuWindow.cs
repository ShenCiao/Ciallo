using System;
using Godot;

namespace Ciallo.GuiControl;

public partial class MenuWindow : PopupMenu
{
    private enum Command
    {
        ToolPanel = 100,
        ToolPropertyPanel = 101,
        LayerPanel = 102,
        TimelinePanel = 103,
        ResetLayout = 200,
    }

    private MainDockableContainer _dockableContainer;

    public override void _Ready()
    {
        _dockableContainer = GetTree().CurrentScene.GetNode<MainDockableContainer>(MainDockableContainer.ScenePath);

        AddCheckItem("Tool", (int)Command.ToolPanel);
        AddCheckItem("Tool Properties", (int)Command.ToolPropertyPanel);
        AddCheckItem("Layers", (int)Command.LayerPanel);
        AddCheckItem("Timeline", (int)Command.TimelinePanel);
        AddSeparator();
        AddItem("Reset Layout", (int)Command.ResetLayout);

        AboutToPopup += SynchronizePanelChecks;
        IdPressed += id => OnIdPressed((Command)id);
    }

    private void OnIdPressed(Command command)
    {
        switch (command)
        {
            case Command.ToolPanel:
                TogglePanel(Command.ToolPanel, MainDockableContainer.ToolPanelName);
                break;
            case Command.ToolPropertyPanel:
                TogglePanel(Command.ToolPropertyPanel, MainDockableContainer.ToolPropertyPanelName);
                break;
            case Command.LayerPanel:
                TogglePanel(Command.LayerPanel, MainDockableContainer.LayerPanelName);
                break;
            case Command.TimelinePanel:
                TogglePanel(Command.TimelinePanel, MainDockableContainer.TimelinePanelName);
                break;
            case Command.ResetLayout:
                _dockableContainer.ResetLayout();
                SynchronizePanelChecks();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, "Unhandled Window menu command");
        }
    }

    private void TogglePanel(Command command, string panelName)
    {
        bool visible = !_dockableContainer.IsAuxiliaryPanelVisible(panelName);
        _dockableContainer.SetAuxiliaryPanelVisible(panelName, visible);
        SetItemChecked(GetItemIndex((int)command), visible);
    }

    private void SynchronizePanelChecks()
    {
        SetPanelChecked(Command.ToolPanel, MainDockableContainer.ToolPanelName);
        SetPanelChecked(Command.ToolPropertyPanel, MainDockableContainer.ToolPropertyPanelName);
        SetPanelChecked(Command.LayerPanel, MainDockableContainer.LayerPanelName);
        SetPanelChecked(Command.TimelinePanel, MainDockableContainer.TimelinePanelName);
    }

    private void SetPanelChecked(Command command, string panelName) =>
        SetItemChecked(GetItemIndex((int)command), _dockableContainer.IsAuxiliaryPanelVisible(panelName));

}
