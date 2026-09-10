using System.Collections.Generic;
using Ciallo.Command;
using Ciallo.Data;
using Frent;
using Godot;

namespace Ciallo.GuiControl;

[Tool]
public partial class TimelineRulerRightClickMenu : PopupMenu
{
    private const int FrameCount = 1;

    private const int IdInsertFrame = 0;
    private const int IdDeleteFrame = 1;
    private const int IdSetPlaybackStart = 2;
    private const int IdSetPlaybackEnd = 3;

    private Entity _document;
    private SelectionManager _selectionManager;
    private TimelineSetting _timelineSetting;
    private int _rightClickedFrame;

    public override void _Ready()
    {
        IdPressed += OnMenuSelected;
    }

    public void InitDocument(Entity document)
    {
        _document = document;
        _selectionManager = document.Get<SelectionManager>();
        _timelineSetting = document.Get<TimelineSetting>();
    }

    public void Popup(int frame)
    {
        _rightClickedFrame = frame;
        RebuildMenu();

        Position = DisplayServer.MouseGetPosition();
        base.Popup();
    }

    private void RebuildMenu()
    {
        Clear();

        AddItem("Insert Frame".Tr(), IdInsertFrame);
        AddItem("Delete Frame".Tr(), IdDeleteFrame);
        SetItemDisabled(ItemCount - 1, !CanDeleteFrame());

        AddSeparator();

        AddItem("Set Playback Start".Tr(), IdSetPlaybackStart);
        SetItemDisabled(ItemCount - 1, !CanSetPlaybackStart());

        AddItem("Set Playback End".Tr(), IdSetPlaybackEnd);
        SetItemDisabled(ItemCount - 1, !CanSetPlaybackEnd());
    }

    private void OnMenuSelected(long id)
    {
        switch ((int)id)
        {
            case IdInsertFrame:
                ActionInsertFrame();
                break;
            case IdDeleteFrame:
                ActionDeleteFrame();
                break;
            case IdSetPlaybackStart:
                ActionSetPlaybackStart();
                break;
            case IdSetPlaybackEnd:
                ActionSetPlaybackEnd();
                break;
        }
    }

    private void ActionInsertFrame()
    {
        if (_document.IsNull || _timelineSetting == null || _selectionManager == null) return;

        var cmd = new CommandBuilder("Insert Frame", _document);
        AddInsertFrameCommands(cmd, _rightClickedFrame, FrameCount);
        cmd.Commit();
    }

    private void ActionDeleteFrame()
    {
        if (_document.IsNull || _timelineSetting == null || _selectionManager == null) return;
        if (!CanDeleteFrame()) return;

        var cmd = new CommandBuilder("Delete Frame", _document);
        AddDeleteFrameCommands(cmd, _rightClickedFrame, FrameCount);
        cmd.Commit();
    }

    private void ActionSetPlaybackStart()
    {
        if (_document.IsNull || _timelineSetting == null || _selectionManager == null) return;
        if (!CanSetPlaybackStart()) return;

        int oldStart = _timelineSetting.PlaybackStart.Value;
        int newStart = _rightClickedFrame;
        if (oldStart == newStart) return;

        var cmd = new CommandBuilder("Set Playback Start", _document)
            .SetProperty(_timelineSetting.PlaybackStart, oldStart, newStart);

        int oldFrame = _selectionManager.CurrentFrame.Value;
        int newFrame = oldFrame < newStart ? newStart : oldFrame;
        AddCurrentFrameChange(cmd, oldFrame, newFrame);

        cmd.Commit();
    }

    private void ActionSetPlaybackEnd()
    {
        if (_document.IsNull || _timelineSetting == null || _selectionManager == null) return;
        if (!CanSetPlaybackEnd()) return;

        int oldEnd = _timelineSetting.PlaybackEnd.Value;
        int newEnd = _rightClickedFrame;
        if (oldEnd == newEnd) return;

        var cmd = new CommandBuilder("Set Playback End", _document)
            .SetProperty(_timelineSetting.PlaybackEnd, oldEnd, newEnd);

        int oldFrame = _selectionManager.CurrentFrame.Value;
        int newFrame = oldFrame >= newEnd ? newEnd - 1 : oldFrame;
        AddCurrentFrameChange(cmd, oldFrame, newFrame);

        cmd.Commit();
    }

    private void AddInsertFrameCommands(CommandBuilder cmd, int frame, int frameCount)
    {
        foreach (var celFolder in EnumerateCelFolders(_document))
        {
            var exposures = celFolder.Get<FolderLayerSetting>().Exposures;
            if (TimelineFrameRetiming.InsertFramesWouldChange(exposures, frame, frameCount))
                cmd.SetObservableCollection(exposures,
                    exp => TimelineFrameRetiming.InsertFrames(exp, frame, frameCount));
        }

        int oldStart = _timelineSetting.PlaybackStart.Value;
        int newStart = TimelineFrameRetiming.MapInsert(oldStart, frame, frameCount);
        if (oldStart != newStart)
            cmd.SetProperty(_timelineSetting.PlaybackStart, oldStart, newStart);

        int oldEnd = _timelineSetting.PlaybackEnd.Value;
        int newEnd = TimelineFrameRetiming.MapInsert(oldEnd, frame, frameCount);
        if (oldEnd != newEnd)
            cmd.SetProperty(_timelineSetting.PlaybackEnd, oldEnd, newEnd);

        int oldCurrentFrame = _selectionManager.CurrentFrame.Value;
        int newCurrentFrame = TimelineFrameRetiming.MapInsert(oldCurrentFrame, frame, frameCount);
        if (oldCurrentFrame != newCurrentFrame)
            cmd.SetProperty(_selectionManager.CurrentFrame, oldCurrentFrame, newCurrentFrame);
    }

    private void AddDeleteFrameCommands(CommandBuilder cmd, int frame, int frameCount)
    {
        foreach (var celFolder in EnumerateCelFolders(_document))
        {
            var exposures = celFolder.Get<FolderLayerSetting>().Exposures;
            if (TimelineFrameRetiming.DeleteFramesWouldChange(exposures, frame, frameCount))
                cmd.SetObservableCollection(exposures,
                    exp => TimelineFrameRetiming.DeleteFrames(exp, frame, frameCount));
        }

        int oldStart = _timelineSetting.PlaybackStart.Value;
        int newStart = TimelineFrameRetiming.MapDelete(oldStart, frame, frameCount);
        if (oldStart != newStart)
            cmd.SetProperty(_timelineSetting.PlaybackStart, oldStart, newStart);

        int oldEnd = _timelineSetting.PlaybackEnd.Value;
        int newEnd = TimelineFrameRetiming.MapDelete(oldEnd, frame, frameCount);
        if (oldEnd != newEnd)
            cmd.SetProperty(_timelineSetting.PlaybackEnd, oldEnd, newEnd);

        int oldCurrentFrame = _selectionManager.CurrentFrame.Value;
        int newCurrentFrame = TimelineFrameRetiming.MapDelete(oldCurrentFrame, frame, frameCount);
        if (oldCurrentFrame != newCurrentFrame)
            cmd.SetProperty(_selectionManager.CurrentFrame, oldCurrentFrame, newCurrentFrame);
    }

    private void AddCurrentFrameChange(CommandBuilder cmd, int oldFrame, int newFrame)
    {
        if (oldFrame == newFrame) return;

        cmd.SetProperty(_selectionManager.CurrentFrame, oldFrame, newFrame);
        var newPrimaryLayer = _selectionManager.ResolvePrimaryLayerForTimelineFrameSelection(newFrame);
        if (_selectionManager.NeedsTimelineSelectionCommit(newPrimaryLayer))
            cmd.SetTarget(newPrimaryLayer).SetLayerSelection();
    }

    private bool CanDeleteFrame()
    {
        if (_timelineSetting == null) return false;

        int newStart = TimelineFrameRetiming.MapDelete(
            _timelineSetting.PlaybackStart.Value,
            _rightClickedFrame,
            FrameCount);
        int newEnd = TimelineFrameRetiming.MapDelete(
            _timelineSetting.PlaybackEnd.Value,
            _rightClickedFrame,
            FrameCount);
        return newEnd > newStart;
    }

    private bool CanSetPlaybackStart() =>
        _timelineSetting != null && _rightClickedFrame < _timelineSetting.PlaybackEnd.Value;

    private bool CanSetPlaybackEnd() =>
        _timelineSetting != null && _rightClickedFrame > _timelineSetting.PlaybackStart.Value;

    private static IEnumerable<Entity> EnumerateCelFolders(Entity document)
    {
        if (document.IsNull || !document.IsAlive)
            yield break;

        foreach (var celFolder in EnumerateCelFoldersRecursive(document))
            yield return celFolder;
    }

    private static IEnumerable<Entity> EnumerateCelFoldersRecursive(Entity entity)
    {
        if (entity.TryGet<FolderLayerSetting>() is { IsCelFolder: true })
            yield return entity;

        if (!entity.Has<LayerTreeNode>())
            yield break;

        foreach (var child in entity.Get<LayerTreeNode>().Children)
        {
            if (child.IsNull || !child.IsAlive)
                continue;

            foreach (var celFolder in EnumerateCelFoldersRecursive(child))
                yield return celFolder;
        }
    }
}
