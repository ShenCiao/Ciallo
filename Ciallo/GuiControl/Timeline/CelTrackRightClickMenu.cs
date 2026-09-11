using System.Collections.Generic;
using Ciallo.Command;
using Ciallo.Data;
using Frent;
using Godot;

namespace Ciallo.GuiControl;

/// <summary>
/// Shared right-click context menu for all <see cref="CelTrack"/> instances.
/// Inherits <see cref="PopupMenu"/> directly — one node in <c>TimelinePanel.tscn</c>.
///
/// Usage: Call <see cref="Popup"/> from a <see cref="CelTrack"/> on right-click.
///
/// Whether the click is "on a cel" is determined internally from the exposure map.
/// </summary>
public partial class CelTrackRightClickMenu : PopupMenu
{
    // ── Context captured at Show() ────────────────────────────────────────────
    private Entity _celFolderEntity;
    private int _rightClickedFrame;
    private bool _onCel;

    // Ordered list of entities shown as cel-list items
    private readonly List<Entity> _celListEntities = new();

    // ── Menu item IDs ─────────────────────────────────────────────────────────
    private const int IdNewAnimationCel = 0;
    private const int IdDeleteCel = 1;
    private const int CelListIdBase = 100;

    // ── Init ─────────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        IdPressed += OnMenuSelected;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Populates and displays the context menu.
    /// Whether the clicked frame has an existing cel is resolved from the exposure map.
    /// </summary>
    public void Popup(Entity celFolderEntity, int frame)
    {
        _celFolderEntity = celFolderEntity;
        _rightClickedFrame = frame;
        var exposures = celFolderEntity.Get<FolderLayerSetting>().Exposures;
        _onCel = exposures.ContainsKey(frame);

        RebuildMenu();

        Position = DisplayServer.MouseGetPosition();
        base.Popup();
    }

    // ── Menu building ─────────────────────────────────────────────────────────

    private void RebuildMenu()
    {
        Clear();
        _celListEntities.Clear();

        _celListEntities.Add(_celFolderEntity);
        var children = _celFolderEntity.Get<LayerTreeNode>().Children;
        foreach (var celEntity in children)
        {
            if (celEntity.IsNull || !celEntity.IsAlive || !celEntity.Tagged<CelTag>() || !celEntity.Has<CommonLayerSetting>())
                continue;
            _celListEntities.Add(celEntity);
        }

        AddItem("New Animation Cel".Tr(), IdNewAnimationCel);

        AddSeparator();

        string celListLabel = _onCel ? "Replace Cel:".Tr() : "Insert Cel:".Tr();
        AddItem(celListLabel, -1);
        SetItemDisabled(ItemCount - 1, true);

        if (_celListEntities.Count == 0)
        {
            AddItem("  " + "(no cels)".Tr(), -2);
            SetItemDisabled(ItemCount - 1, true);
        }
        else
        {
            for (int i = 0; i < _celListEntities.Count; i++)
            {
                if (_celListEntities[i].IsCelFolder)
                {
                    AddItem("  " + "Blank".Tr(), CelListIdBase + i);
                    continue;
                }

                string name = _celListEntities[i].Get<CommonLayerSetting>().Name.Value;
                AddItem("  " + (string.IsNullOrEmpty(name) ? "(unnamed)".Tr() : name), CelListIdBase + i);
            }
        }

        if (_onCel)
        {
            AddSeparator();
            string deleteLabel = _celFolderEntity.Get<FolderLayerSetting>().Exposures[_rightClickedFrame].IsCelFolder
                ? "Delete Blank"
                : "Delete Cel";
            AddItem(deleteLabel.Tr(), IdDeleteCel);
        }
    }

    // ── Event handler ─────────────────────────────────────────────────────────

    private void OnMenuSelected(long id)
    {
        int intId = (int)id;
        switch (intId)
        {
            case IdNewAnimationCel:
                ActionNewAnimationCel();
                break;
            case IdDeleteCel:
                ActionDeleteCel();
                break;
            default:
                if (intId >= CelListIdBase)
                {
                    int idx = intId - CelListIdBase;
                    if (idx < _celListEntities.Count)
                        ActionInsertOrReplaceCel(_celListEntities[idx]);
                }
                break;
        }
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private void ActionNewAnimationCel()
    {
        var exposures = _celFolderEntity.Get<FolderLayerSetting>().Exposures;
        int targetFrame;
        string name;

        if (_onCel && exposures[_rightClickedFrame].IsCelFolder)
        {
            targetFrame = _rightClickedFrame;
            var usedNames = TimelineAction.GetUsedCelNames(_celFolderEntity);
            name = TimelineAction.GetNewAnimationCelName(exposures, targetFrame, usedNames);
        }
        else if (_onCel)
        {
            (targetFrame, name) = TimelineAction.GetNewAnimationCelFrameName(
                _celFolderEntity, _rightClickedFrame);
        }
        else
        {
            targetFrame = exposures.ContainsKey(_rightClickedFrame)
                ? TimelineAction.FindNearestUnoccupiedFrame(exposures, _rightClickedFrame)
                : _rightClickedFrame;
            var usedNames = TimelineAction.GetUsedCelNames(_celFolderEntity);
            name = TimelineAction.GetNewAnimationCelName(exposures, targetFrame, usedNames);
        }

        TimelineAction.NewCelFromArchetype(_celFolderEntity, targetFrame, name);
    }

    private void ActionInsertOrReplaceCel(Entity celEntity)
    {
        var exposures = _celFolderEntity.Get<FolderLayerSetting>().Exposures;
        int frame = _rightClickedFrame;
        bool onCel = _onCel;
        string label = onCel ? "Replace Cel" : "Insert Cel";

        new CommandBuilder(label, _celFolderEntity)
            .SetObservableCollection(exposures, exp =>
            {
                if (onCel) exp.Remove(frame);
                exp.Add(frame, celEntity);
            })
            .Commit();
    }

    private void ActionDeleteCel()
    {
        var exposures = _celFolderEntity.Get<FolderLayerSetting>().Exposures;
        int frame = _rightClickedFrame;
        if (!exposures.ContainsKey(frame)) return;

        string label = exposures[frame].IsCelFolder ? "Delete Blank" : "Delete Cel";
        new CommandBuilder(label)
            .SetObservableCollection(exposures, exp => exp.Remove(frame))
            .Commit();
    }
}
