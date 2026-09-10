using System;
using System.Collections.Generic;
using System.Linq;
using Ciallo.Command;
using Ciallo.Diagnostics;
using R3;

// ReSharper disable once CheckNamespace
namespace Ciallo;

/// <summary>
/// Document-level command history.
/// </summary>
public partial class CommandManager
{
    private readonly List<HistoryAction> _undoStack = [];
    private readonly List<HistoryAction> _redoStack = [];
    private HistoryAction _openSequenceAction;
    private HistoryAction _closedOpenSequenceAction;
    private long _currentVersion;
    private long _savedVersion;
    private long _persistenceEpoch;
    private readonly ReactiveProperty<bool> _documentModified = new(false);

    public ReadOnlyReactiveProperty<bool> DocumentModified => _documentModified;
    public readonly Subject<bool> HistoryNavigated = new(); // true is undo, false is redo

    public bool HasUndo => _undoStack.Count > 0;
    public bool HasRedo => _redoStack.Count > 0;
    public long PersistenceEpoch => _persistenceEpoch;

    /// <summary>
    /// Creates a new undoable action.
    /// </summary>
    public void Commit(
        string actionName,
        List<ICommand> commands,
        bool execute = true)
    {
        if (commands.Count == 0) return;
        var segment = PrepareSegment(actionName, commands, execute);
        if (segment == null) return;
        CloseOpenSequence();
        ClearRedoStack();
        AddSeparateAction(actionName, segment);
        TrimUndoStack();
        _persistenceEpoch++;
    }

    /// <summary>
    /// Creates a new undoable action, unless the latest action can compress its tail segment.
    /// Use this for continuous value edits that should keep only the first undo and latest redo endpoints.
    /// </summary>
    public void CommitSequence(
        string actionName,
        List<ICommand> commands,
        bool execute = true)
    {
        if (commands.Count == 0) return;

        var segment = PrepareSegment(actionName, commands, execute);
        if (segment == null) return;

        CloseOpenSequence();
        ClearRedoStack();

        if (TryResolveReusableLatestAction(out var targetAction) && targetAction.TryEndpointCompressTail(segment))
        {
            BumpVersion(targetAction);
        }
        else
        {
            AddSeparateAction(actionName, segment);
        }

        TrimUndoStack();
        _persistenceEpoch++;
    }

    /// <summary>
    /// Appends a segment to the latest undoable action if one exists.
    /// Use this for commands that doesn't actually change visual contents, like switch primary layer or toggle folder's expanded state.
    /// </summary>
    public void CommitToLatest(string actionName, List<ICommand> commands, bool execute = true)
    {
        if (commands.Count == 0) return;
        var segment = PrepareSegment(actionName, commands, execute);
        if (segment == null) return;
        CloseOpenSequence();
        ClearRedoStack();
        if (TryResolveReusableLatestAction(out var targetAction))
        {
            targetAction.Append(segment);
            BumpVersion(targetAction);
        }
        else
        {
            AddSeparateAction(actionName, segment);
        }
        TrimUndoStack();
        _persistenceEpoch++;
    }

    /// <summary>
    /// Creates a new undoable action on the first call, then keeps appending later segments
    /// to that same action until another history-writing entrypoint starts a different action.
    /// </summary>
    public void CommitOpenSequence(string actionName, List<ICommand> commands, bool execute = true)
    {
        if (commands.Count == 0) return;

        var segment = PrepareSegment(actionName, commands, execute);
        if (segment == null) return;

        ClearRedoStack();

        if (TryResolveOpenSequenceAction(out var targetAction))
        {
            targetAction.Append(segment);
            BumpVersion(targetAction);
        }
        else
        {
            _openSequenceAction = AddSeparateAction(actionName, segment);
        }

        TrimUndoStack();
        _persistenceEpoch++;
    }

    public void Commit(string actionName, ICommand command, bool execute = true) => Commit(actionName, [command], execute);

    public void CommitSequence(
        string actionName,
        ICommand command,
        bool execute = true)
    {
        CommitSequence(actionName, [command], execute);
    }

    public void CommitToLatest(
        string actionName,
        ICommand command,
        bool execute = true)
    {
        CommitToLatest(actionName, [command], execute);
    }

    public void CommitOpenSequence(
        string actionName,
        ICommand command,
        bool execute = true)
    {
        CommitOpenSequence(actionName, [command], execute);
    }

    public void Undo()
    {
        if (!HasUndo) return;

        CloseOpenSequence();
        var action = PopLast(_undoStack);
        AppBugReport.Undo(action.ActionName);
        action.Undo();
        _redoStack.Add(action);

        _currentVersion = action.BeforeVersion;
        UpdateDocumentModified();
        HistoryNavigated.OnNext(true);
        _persistenceEpoch++;
    }

    public void Redo()
    {
        if (!HasRedo) return;

        CloseOpenSequence();
        var action = PopLast(_redoStack);
        AppBugReport.Redo(action.ActionName);
        action.Do();
        _undoStack.Add(action);

        _currentVersion = action.AfterVersion;
        UpdateDocumentModified();
        HistoryNavigated.OnNext(false);
        _persistenceEpoch++;
    }

    public void OnSave()
    {
        _savedVersion = _currentVersion;
        UpdateDocumentModified();
    }

    public void MarkUnsaved()
    {
        _savedVersion = -1;
        UpdateDocumentModified();
    }

    private static HistorySegment PrepareSegment(string actionName, List<ICommand> commands, bool execute)
    {
        var segment = HistorySegment.Create(actionName, commands);

        if (execute)
            segment.Do();

        bool changesHistory = execute || !segment.IsNoOp;
        return changesHistory ? segment : null;
    }

    private bool TryResolveLatestAction(out HistoryAction action)
    {
        action = null;
        if (_undoStack.Count == 0)
            return false;

        action = _undoStack[^1];
        return true;
    }

    private bool TryResolveReusableLatestAction(out HistoryAction action)
    {
        if (TryResolveLatestAction(out action) && !ReferenceEquals(action, _closedOpenSequenceAction))
            return true;

        action = null;
        return false;
    }

    private bool TryResolveOpenSequenceAction(out HistoryAction action)
    {
        if (_openSequenceAction != null && TryResolveLatestAction(out action) && ReferenceEquals(action, _openSequenceAction))
            return true;

        _openSequenceAction = null;
        action = null;
        return false;
    }

    private HistoryAction AddSeparateAction(string actionName, HistorySegment segment)
    {
        var action = new HistoryAction(actionName, segment, _currentVersion, NextVersion());
        _undoStack.Add(action);
        _closedOpenSequenceAction = null;
        UpdateDocumentModified();
        return action;
    }

    private void BumpVersion(HistoryAction action)
    {
        action.AfterVersion = NextVersion();
        UpdateDocumentModified();
    }

    private long NextVersion() => ++_currentVersion;

    private void ClearRedoStack()
    {
        if (_redoStack.Contains(_openSequenceAction))
            _openSequenceAction = null;
        if (_redoStack.Contains(_closedOpenSequenceAction))
            _closedOpenSequenceAction = null;
        foreach (var action in _redoStack)
            action.OnDeletedAsDo();
        _redoStack.Clear();
    }

    private void TrimUndoStack()
    {
        int maxSteps = Math.Max(0, AppPreference.CommandHistoryLimit.Value);
        while (_undoStack.Count > maxSteps)
        {
            var action = _undoStack[0];
            _undoStack.RemoveAt(0);
            if (ReferenceEquals(action, _openSequenceAction))
                _openSequenceAction = null;
            if (ReferenceEquals(action, _closedOpenSequenceAction))
                _closedOpenSequenceAction = null;
            action.OnDeletedAsUndo();
        }
    }

    private void CloseOpenSequence()
    {
        if (_openSequenceAction != null)
            _closedOpenSequenceAction = _openSequenceAction;
        _openSequenceAction = null;
    }

    private void UpdateDocumentModified()
    {
        _documentModified.Value = _savedVersion != _currentVersion;
    }

    private static T PopLast<T>(List<T> list)
    {
        var item = list[^1];
        list.RemoveAt(list.Count - 1);
        return item;
    }
}

internal sealed class HistoryAction
{
    private readonly List<HistorySegment> _segments = [];

    public string ActionName { get; }
    public long BeforeVersion { get; }
    public long AfterVersion { get; set; }

    public HistoryAction(string actionName, HistorySegment segment, long beforeVersion, long afterVersion)
    {
        ActionName = actionName;
        _segments.Add(segment);
        BeforeVersion = beforeVersion;
        AfterVersion = afterVersion;
    }

    public void Do()
    {
        foreach (var segment in _segments)
            segment.Do();
    }

    public void Undo()
    {
        foreach (var segment in _segments.AsEnumerable().Reverse())
            segment.Undo();
    }

    public void Append(HistorySegment segment)
    {
        _segments.Add(segment);
    }

    public void OnDeletedAsDo()
    {
        foreach (var segment in _segments)
            segment.OnDeletedAsDo();
    }

    public void OnDeletedAsUndo()
    {
        foreach (var segment in _segments)
            segment.OnDeletedAsUndo();
    }

    public bool TryEndpointCompressTail(HistorySegment segment)
    {
        if (_segments.Count == 0) return false;
        return _segments[^1].TryEndpointCompress(segment);
    }
}

internal sealed class HistorySegment
{
    private IReadOnlyList<ICommand> _doCommands;
    private readonly IReadOnlyList<ICommand> _undoCommands;

    public string ActionName { get; }
    public string SegmentKey { get; }
    public bool IsNoOp => _doCommands.Count == 0 && _undoCommands.Count == 0;

    private HistorySegment(
        string actionName,
        string segmentKey,
        IReadOnlyList<ICommand> doCommands,
        IReadOnlyList<ICommand> undoCommands)
    {
        ActionName = actionName;
        SegmentKey = segmentKey;
        _doCommands = doCommands;
        _undoCommands = undoCommands;
    }

    public static HistorySegment Create(string actionName, IReadOnlyList<ICommand> commands)
    {
        var commandArray = commands.ToArray();
        // Segment keys are derived internally from the user-facing action and command shape.
        // Matching adjacent keys are endpoint-compressible within one undoable action.
        return new HistorySegment(actionName, CreateSegmentKey(actionName, commandArray), commandArray, commandArray);
    }

    public void Do()
    {
        foreach (var command in _doCommands)
            command.Do();
    }

    public void Undo()
    {
        foreach (var command in _undoCommands.Reverse())
            command.Undo();
    }

    public bool TryEndpointCompress(HistorySegment next)
    {
        // Endpoint compression preserves this segment's original undo endpoint and replaces
        // only the do endpoint. This is only attempted for adjacent segments in one action.
        if (SegmentKey == null || SegmentKey != next.SegmentKey)
            return false;

        OnDeletedAsDo();
        next.OnDeletedAsUndo();
        _doCommands = next._doCommands;
        return true;
    }

    public void OnDeletedAsDo()
    {
        foreach (var command in _doCommands)
            command.OnDeletedAsDo();
    }

    public void OnDeletedAsUndo()
    {
        foreach (var command in _undoCommands)
            command.OnDeletedAsUndo();
    }

    private static string CreateSegmentKey(string actionName, IReadOnlyList<ICommand> commands)
    {
        return actionName + "|" + string.Join("|", commands.Select(command => command.GetType().FullName));
    }
}
