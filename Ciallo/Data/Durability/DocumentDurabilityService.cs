using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Frent;

namespace Ciallo.Data;

internal sealed class DocumentDurabilityService
{
    private static readonly TimeSpan SessionHeartbeatInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MinimumRecoverySnapshotInterval = TimeSpan.FromMinutes(1);
    private const string LocalRetentionLimitMessage =
        "The newest recovery snapshot exceeds the configured local recovery storage limit.";

    private readonly DurabilityFileStore _fileStore;
    private readonly CloudOutboxStore _outboxStore;
    private readonly Channel<RecoveryWriteRequest> _recoveryWrites;
    private readonly Task _recoveryWriterTask;
    private readonly ConcurrentQueue<DocumentDurabilityStatus> _statusNotifications = new();
    private readonly object _statusLock = new();
    private readonly object _sessionLock = new();

    private Entity _trackedDocument = Entity.Null;
    private EditingSessionInfo _editingSession;
    private Guid _trackedSessionId;
    private long _lastWrittenEpoch = -1;
    private long _queuedEpoch = -1;
    private TimeSpan _snapshotElapsed;
    private TimeSpan _heartbeatElapsed;
    private DocumentDurabilityStatus _status = new();
    private bool _isShutdown;

    public event Action<DocumentDurabilityStatus> StatusChanged;

    public DocumentDurabilityStatus Status
    {
        get
        {
            lock (_statusLock)
                return _status;
        }
    }

    public Guid DeviceId => _fileStore.DeviceId;
    public DurabilityFileStore FileStore => _fileStore;
    public CloudOutboxStore OutboxStore => _outboxStore;

    public DocumentDurabilityService(string userDataPath)
    {
        _fileStore = new DurabilityFileStore(userDataPath);
        _outboxStore = new CloudOutboxStore(_fileStore);
        _outboxStore.PendingChanged += UpdatePendingUploadCount;
        _recoveryWrites = Channel.CreateBounded<RecoveryWriteRequest>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.DropOldest,
        });
        _recoveryWriterTask = Task.Run(ProcessRecoveryWritesAsync);
        UpdatePendingUploadCount();
    }

    public void Process(double delta)
    {
        DrainStatusNotifications();
        TrackWorkingDocument(AppDocumentManager.WorkingDocument.CurrentValue);
        if (_trackedDocument.IsNull)
            return;

        var elapsed = TimeSpan.FromSeconds(delta);
        _snapshotElapsed += elapsed;
        _heartbeatElapsed += elapsed;

        if (_heartbeatElapsed >= SessionHeartbeatInterval)
        {
            _heartbeatElapsed = Remainder(_heartbeatElapsed, SessionHeartbeatInterval);
            try
            {
                _editingSession = _fileStore.HeartbeatSession(_editingSession, DateTimeOffset.UtcNow);
                _outboxStore.EnqueueSession(_editingSession);
            }
            catch (Exception exception)
            {
                UpdateStatus(status => status with { LastLocalError = exception.Message });
            }
        }

        var snapshotInterval = AppPreference.RecoverySnapshotInterval.Value;
        if (snapshotInterval < MinimumRecoverySnapshotInterval)
            snapshotInterval = MinimumRecoverySnapshotInterval;
        if (_snapshotElapsed < snapshotInterval)
            return;

        _snapshotElapsed = Remainder(_snapshotElapsed, snapshotInterval);
        var commandManager = _trackedDocument.Get<CommandManager>();
        var persistenceEpoch = commandManager.PersistenceEpoch;
        if (persistenceEpoch == Volatile.Read(ref _lastWrittenEpoch) ||
            persistenceEpoch == Volatile.Read(ref _queuedEpoch))
            return;

        CaptureRecoverySnapshot(persistenceEpoch);
    }

    public void OnDocumentClosing(Entity document)
    {
        if (_trackedDocument.IsNull || _trackedDocument != document)
            return;
        CloseTrackedSession();
        _trackedDocument = Entity.Null;
        SetTrackedSessionId(Guid.Empty);
    }

    public IReadOnlyList<LocalRecoverySnapshotInfo> ListRecoverySnapshots(Guid documentId)
        => _fileStore.ListRecoverySnapshots(documentId);

    public IReadOnlyList<LocalRecoverySnapshotInfo> ListRecoveryCandidates()
        => _fileStore.ListRecoveryCandidates(DateTimeOffset.UtcNow);

    public Entity OpenRecoverySnapshot(Guid revisionId)
    {
        var info = _fileStore.ListRecoveryCandidates(DateTimeOffset.UtcNow)
            .Single(snapshot => snapshot.RevisionId == revisionId);
        var path = _fileStore.GetRecoverySnapshotPath(info);
        var dataDocument = AppDocumentManager.Load(path);
        dataDocument.Get<DocumentSetting>().FilePath.Value = info.OriginalFilePath;
        AppDocumentManager.CopyWorldByData(dataDocument);
        var workingDocument = AppDocumentManager.WorkingDocument.CurrentValue;
        workingDocument.Get<CommandManager>().MarkUnsaved();
        return workingDocument;
    }

    public Entity OpenManagedCloudCopy(ManagedCloudCopyInfo copy)
    {
        if (copy.Revision.Kind == CloudRevisionKind.Session)
            throw new InvalidOperationException("An editing-session record is not a document revision.");

        var dataDocument = AppDocumentManager.Load(copy.LocalFilePath);
        var documentDirectory = Path.GetDirectoryName(copy.LocalFilePath)!;
        dataDocument.Get<DocumentSetting>().FilePath.Value = copy.Revision.Kind == CloudRevisionKind.Saved
            ? copy.LocalFilePath
            : Path.Combine(documentDirectory, "document.ciallo");
        AppDocumentManager.CopyWorldByData(dataDocument);
        var workingDocument = AppDocumentManager.WorkingDocument.CurrentValue;

        Guid? cloudBase = copy.Revision.Kind == CloudRevisionKind.Saved
            ? copy.Revision.RevisionId
            : copy.Revision.ParentRevisionId;
        if (cloudBase.HasValue)
            _outboxStore.SetCloudBaseRevision(
                copy.Revision.DocumentId,
                cloudBase.Value,
                copy.Revision.SupersededRevisionIds);
        if (copy.Revision.Kind == CloudRevisionKind.Recovery)
            workingDocument.Get<CommandManager>().MarkUnsaved();
        return workingDocument;
    }

    public void EnqueueManualSave(Entity document, string filePath)
    {
        try
        {
            _outboxStore.EnqueueManualSave(document, filePath);
        }
        catch (Exception exception)
        {
            UpdateStatus(status => status with { LastLocalError = exception.Message });
        }
    }

    public void SetCloudStatus(
        SteamCloudSyncState state,
        string externalError = "",
        DateTimeOffset? protectionPointUtc = null,
        bool? retentionLimitExceeded = null)
    {
        UpdateStatus(status => status with
        {
            CloudState = state,
            LastCloudError = externalError,
            LatestCloudProtectionUtc = protectionPointUtc ?? status.LatestCloudProtectionUtc,
            CloudRetentionLimitExceeded = retentionLimitExceeded ?? status.CloudRetentionLimitExceeded,
        });
    }

    public async Task ShutdownAsync()
    {
        if (_isShutdown)
            return;
        _isShutdown = true;

        if (!_trackedDocument.IsNull)
        {
            CloseTrackedSession();
            _trackedDocument = Entity.Null;
            SetTrackedSessionId(Guid.Empty);
        }

        _recoveryWrites.Writer.TryComplete();
        await _recoveryWriterTask;
        DrainStatusNotifications();
    }

    private void TrackWorkingDocument(Entity workingDocument)
    {
        if (workingDocument == _trackedDocument &&
            (workingDocument.IsNull ||
             workingDocument.Get<DocumentSetting>().DocumentId.Value == _editingSession.DocumentId))
            return;

        if (!_trackedDocument.IsNull)
        {
            CloseTrackedSession();
            SetTrackedSessionId(Guid.Empty);
        }

        _trackedDocument = workingDocument;
        Interlocked.Exchange(ref _lastWrittenEpoch, -1);
        Interlocked.Exchange(ref _queuedEpoch, -1);
        _snapshotElapsed = TimeSpan.Zero;
        _heartbeatElapsed = TimeSpan.Zero;

        if (workingDocument.IsNull)
        {
            SetTrackedSessionId(Guid.Empty);
            UpdateStatus(status => status with { DocumentId = Guid.Empty });
            return;
        }

        var settings = workingDocument.Get<DocumentSetting>();
        _editingSession = _fileStore.CreateSession(
            settings.DocumentId.Value,
            settings.Name.Value,
            DateTimeOffset.UtcNow);
        SetTrackedSessionId(_editingSession.SessionId);
        UpdateStatus(status => status with
        {
            DocumentId = settings.DocumentId.Value,
            LocalState = LocalRecoveryState.Idle,
            LocalRetentionLimitExceeded = false,
            LastLocalError = "",
        });
        try
        {
            _fileStore.WriteSession(_editingSession);
            _outboxStore.EnqueueSession(_editingSession);
        }
        catch (Exception exception)
        {
            UpdateStatus(status => status with { LastLocalError = exception.Message });
        }
    }

    private void CaptureRecoverySnapshot(long persistenceEpoch)
    {
        UpdateStatus(status => status with { LocalState = LocalRecoveryState.Capturing });
        var settings = _trackedDocument.Get<DocumentSetting>();
        var snapshot = PersistenceSnapshotCapture.Capture(_trackedDocument, persistenceEpoch);
        var request = new RecoveryWriteRequest(
            snapshot,
            Guid.NewGuid(),
            _editingSession.SessionId,
            _fileStore.DeviceId,
            settings.Name.Value,
            settings.FilePath.Value,
            new RecoveryRetentionPolicy(
                AppPreference.RecoverySnapshotLimitPerDocument.Value,
                AppPreference.RecoverySnapshotAccountFileLimit.Value,
                AppPreference.RecoverySnapshotAccountByteLimit.Value));

        Interlocked.Exchange(ref _queuedEpoch, persistenceEpoch);
        _recoveryWrites.Writer.TryWrite(request);
    }

    private async Task ProcessRecoveryWritesAsync()
    {
        await foreach (var request in _recoveryWrites.Reader.ReadAllAsync())
        {
            if (IsCurrentSession(request))
                UpdateStatus(status => status with { LocalState = LocalRecoveryState.Writing });
            try
            {
                var result = _fileStore.WriteRecoverySnapshot(request);
                var info = result.Snapshot;
                _outboxStore.EnqueueRecovery(info, _fileStore.GetRecoverySnapshotPath(info));
                if (!IsCurrentSession(request))
                    continue;

                Interlocked.Exchange(ref _lastWrittenEpoch, request.Snapshot.PersistenceEpoch);
                Interlocked.CompareExchange(ref _queuedEpoch, -1, request.Snapshot.PersistenceEpoch);
                var retentionError = result.Retention.Error;
                if (result.Retention.LimitExceeded && retentionError.Length == 0)
                    retentionError = LocalRetentionLimitMessage;
                UpdateStatus(status => status with
                {
                    LocalState = LocalRecoveryState.Idle,
                    LatestLocalSnapshotUtc = info.CapturedAtUtc,
                    LocalRetentionLimitExceeded = result.Retention.LimitExceeded,
                    LastLocalError = retentionError,
                });
            }
            catch (Exception exception)
            {
                if (!IsCurrentSession(request))
                    continue;

                Interlocked.CompareExchange(ref _queuedEpoch, -1, request.Snapshot.PersistenceEpoch);
                UpdateStatus(status => status with
                {
                    LocalState = LocalRecoveryState.Failed,
                    LastLocalError = exception.Message,
                });
            }
        }
    }

    private void CloseTrackedSession()
    {
        try
        {
            _editingSession = _fileStore.CloseSession(_editingSession, DateTimeOffset.UtcNow);
            _outboxStore.EnqueueSession(_editingSession);
        }
        catch (Exception exception)
        {
            UpdateStatus(status => status with { LastLocalError = exception.Message });
        }
    }

    private void UpdateStatus(Func<DocumentDurabilityStatus, DocumentDurabilityStatus> update)
    {
        DocumentDurabilityStatus updated;
        lock (_statusLock)
        {
            _status = update(_status);
            updated = _status;
        }
        _statusNotifications.Enqueue(updated);
    }

    private void DrainStatusNotifications()
    {
        while (_statusNotifications.TryDequeue(out var status))
            StatusChanged?.Invoke(status);
    }

    private void UpdatePendingUploadCount()
    {
        var pendingCount = _outboxStore.CountPending();
        UpdateStatus(status => status with { PendingUploadCount = pendingCount });
    }

    private bool IsCurrentSession(RecoveryWriteRequest request)
    {
        lock (_sessionLock)
            return request.SessionId == _trackedSessionId;
    }

    private void SetTrackedSessionId(Guid sessionId)
    {
        lock (_sessionLock)
            _trackedSessionId = sessionId;
    }

    private static TimeSpan Remainder(TimeSpan elapsed, TimeSpan interval)
        => TimeSpan.FromTicks(elapsed.Ticks % interval.Ticks);
}
