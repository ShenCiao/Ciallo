using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ciallo.Data;
using Frent;
using Godot;
using ObservableCollections;
using R3;
using Environment = System.Environment;

namespace Ciallo.Geometry;

// Builds topology from strokes in the observed shape layers; filled polygons are not boundaries.
public class ArrangementManager : IDisposable
{
    // The currently queryable Arrangement, or null if not ready (e.g. mid-rebuild on a worker thread).
    // Subscribers must handle the null case. Same reference is re-emitted on every mutation.
    private readonly ReactiveProperty<Arrangement> _arrReadyProperty;
    public ReadOnlyReactiveProperty<Arrangement> ArrReady => _arrReadyProperty;

    private static readonly SemaphoreSlim NativeConcurrency = new(Math.Max(1, Environment.ProcessorCount - 3));

    private readonly Arrangement _arrangement = new();
    private readonly HashSet<Entity> _sourceShapes = [];

    private IReadOnlyList<ChildShapePolylineLookup> _observedLookups;
    private bool _isSyncingModifications;

    private readonly Lock _pendingChangesLock = new();
    private readonly Subject<Unit> _flushRequests = new();
    // Default ImmutableArray mean remove; non-default ImmutableArray mean upsert with that polyline.
    private readonly Dictionary<Entity, ImmutableArray<Vector2>> _pendingChanges = [];
    private bool _isDrainRunning;
    private bool _hasPendingClear;

    private readonly CancellationTokenSource _disposeTokenSource = new();
    private readonly IDisposable _flushSubscription;
    private CompositeDisposable _modificationSubscriptions;
    private bool _arrangementDisposed;

    public ArrangementManager()
    {
        // Empty arrangement is queryable from the start.
        _arrReadyProperty = new ReactiveProperty<Arrangement>(_arrangement);

        _flushSubscription = _flushRequests
            .DebounceFrame(1, GodotFrameProvider.Process)
            .Subscribe(_ => FlushPendingChanges());
    }

    public IReadOnlySet<Entity> SourceShapes => _sourceShapes;

    /// <summary>
    /// Observe the given shape-layer polyline lookups and synchronize the arrangement with them.
    /// Would be burst called 100+ times on project load.
    /// </summary>
    /// <remarks>
    /// Would be called multiple times, clean up previous subscriptions and start observing the new set of lookups.
    /// </remarks>
    public void Observe(params ChildShapePolylineLookup[] lookups)
    {
        _observedLookups = lookups;
        RebuildAsync();
        if (_isSyncingModifications)
            RebuildModificationSubscriptions();
    }

    public void SyncModification()
    {
        _isSyncingModifications = true;
        if (_observedLookups == null)
            return;
        RebuildModificationSubscriptions();
    }

    private void RebuildModificationSubscriptions()
    {
        _modificationSubscriptions?.Dispose();
        var subs = _modificationSubscriptions = new CompositeDisposable();

        foreach (var lookup in _observedLookups)
        {
            lookup.Polylines.ObserveDictionaryAdd().Subscribe(et =>
            {
                SyncShape(et.Key, et.Value);
            }).AddTo(subs);

            lookup.Polylines.ObserveDictionaryRemove().Subscribe(et =>
            {
                if (_sourceShapes.Remove(et.Key))
                    RemoveShape(et.Key);
            }).AddTo(subs);

            lookup.Polylines.ObserveDictionaryReplace().Subscribe(et =>
            {
                SyncShape(et.Key, et.NewValue);
            }).AddTo(subs);

            lookup.Polylines.ObserveClear().Subscribe(_ =>
            {
                RebuildAsync();
            }).AddTo(subs);
        }
    }

    public void DesyncModification()
    {
        _isSyncingModifications = false;
        _modificationSubscriptions?.Dispose();
        _modificationSubscriptions = null;
    }

    public void Dispose()
    {
        DesyncModification();
        NotifyNotReady();

        bool disposeNow;
        lock (_pendingChangesLock)
        {
            _hasPendingClear = false;
            _pendingChanges.Clear();
            disposeNow = !_isDrainRunning && !_arrangementDisposed;
            if (disposeNow)
                _arrangementDisposed = true;
        }

        _flushSubscription.Dispose();
        _flushRequests.Dispose();
        _disposeTokenSource.Cancel();

        if (disposeNow)
            DisposeArrangement();
    }

    private void RebuildAsync()
    {
        _sourceShapes.Clear();
        Clear();
        foreach (var lookup in _observedLookups)
        foreach (var (shapeE, polyline) in lookup.Polylines)
            SyncShape(shapeE, polyline);
    }

    // Bypass ReactiveProperty's equality dedup by going through OnNext directly:
    // _arrangement is the same reference each time, but downstream consumers must still be re-triggered.
    private void NotifyReady() => _arrReadyProperty.OnNext(_arrangement);
    private void NotifyNotReady() => _arrReadyProperty.OnNext(null);

    private void SyncShape(Entity shapeE, IndexedPolyline polyline)
    {
        if (!shapeE.Has<StrokeSetting>() || !CanCreateArrangementCurve(polyline.Positions))
        {
            if (_sourceShapes.Remove(shapeE))
                RemoveShape(shapeE);
            return;
        }

        _sourceShapes.Add(shapeE);
        UpsertShape(shapeE, polyline);
    }

    private static bool CanCreateArrangementCurve(ImmutableArray<Vector2> positions)
    {
        if (positions.Length < 2)
            return false;

        var previous = positions[0];
        for (int i = 1; i < positions.Length; i++)
        {
            if (!positions[i].IsEqualApprox(previous))
                return true;
            previous = positions[i];
        }
        return false;
    }

    private void UpsertShape(Entity shapeE, IndexedPolyline polyline)
    {
        NotifyNotReady();
        lock (_pendingChangesLock)
        {
            if (IsDisposed) return;
            _pendingChanges[shapeE] = polyline.Positions;
        }
        _flushRequests.OnNext(Unit.Default);
    }

    private void RemoveShape(Entity shapeE)
    {
        NotifyNotReady();
        lock (_pendingChangesLock)
        {
            if (IsDisposed) return;
            _pendingChanges[shapeE] = default;
        }
        _flushRequests.OnNext(Unit.Default);
    }

    private void Clear()
    {
        NotifyNotReady();
        lock (_pendingChangesLock)
        {
            if (IsDisposed) return;
            _hasPendingClear = true;
            _pendingChanges.Clear();
        }
        _flushRequests.OnNext(Unit.Default);
    }

    private void FlushPendingChanges()
    {
        bool startDrain;
        lock (_pendingChangesLock)
        {
            if (IsDisposed || (!_hasPendingClear && _pendingChanges.Count == 0))
                return;

            startDrain = !_isDrainRunning;
            if (startDrain)
                _isDrainRunning = true;
        }

        if (startDrain)
            _ = RunDrainBatchesAsync();
    }

    private async Task RunDrainBatchesAsync()
    {
        Exception fault = null;
        try
        {
            await DrainBatchesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            fault = ex;
        }

        bool restart;
        lock (_pendingChangesLock)
        {
            _isDrainRunning = false;
            restart = !IsDisposed && (_hasPendingClear || _pendingChanges.Count > 0);
            if (restart)
                _isDrainRunning = true;
        }

        if (restart)
            _ = RunDrainBatchesAsync();

        FrameProviderDispatcher.Post(() =>
        {
            UpdateMainThreadState();
            if (fault != null)
                GD.PushError($"ArrangementManager drain faulted: {fault}");
        });
    }

    private async Task DrainBatchesAsync()
    {
        while (true)
        {
            bool clear;
            List<(long Id, ImmutableArray<Vector2> Positions)> changes;
            lock (_pendingChangesLock)
            {
                if (IsDisposed) return;
                if (!_hasPendingClear && _pendingChanges.Count == 0) return;

                clear = _hasPendingClear;
                changes = [.. _pendingChanges.Select(p => (p.Key.PackedValue, p.Value))];
                _hasPendingClear = false;
                _pendingChanges.Clear();
            }

            try
            {
                await NativeConcurrency.WaitAsync(_disposeTokenSource.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await Task.Run(() => ApplyBatch(clear, changes)).ConfigureAwait(false);
            }
            finally
            {
                NativeConcurrency.Release();
            }
        }
    }

    private void ApplyBatch(bool clear, IReadOnlyList<(long Id, ImmutableArray<Vector2> Positions)> changes)
    {
        if (clear)
        {
            if (IsDisposed)
                return;
            _arrangement.Clear();
        }

        foreach (var change in changes)
        {
            if (IsDisposed)
                return;

            if (change.Positions.IsDefault)
                _arrangement.RemovePolyline(change.Id);
            else
                _arrangement.SetPolyline(change.Id, change.Positions);
        }
    }

    private void UpdateMainThreadState()
    {
        bool shouldDispose;
        bool shouldNotifyReady;

        lock (_pendingChangesLock)
        {
            shouldDispose = IsDisposed && !_isDrainRunning && !_arrangementDisposed;
            if (shouldDispose)
                _arrangementDisposed = true;
            shouldNotifyReady = !IsDisposed
                                && !_isDrainRunning
                                && !_hasPendingClear
                                && _pendingChanges.Count == 0;
        }

        if (shouldDispose)
            DisposeArrangement();
        else if (shouldNotifyReady)
            NotifyReady();
    }

    private void DisposeArrangement()
    {
        _arrangement.Dispose();
        _disposeTokenSource.Dispose();
        _arrReadyProperty.Dispose();
    }

    private bool IsDisposed => _disposeTokenSource.IsCancellationRequested;
}
