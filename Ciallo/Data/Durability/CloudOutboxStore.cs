using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Frent;

namespace Ciallo.Data;

internal sealed class CloudOutboxStore
{
    private readonly DurabilityFileStore _files;
    public event Action PendingChanged;

    public CloudOutboxStore(DurabilityFileStore files)
    {
        _files = files;
        ReconcileRecoverySnapshots();
    }

    public CloudOutboxRecord EnqueueRecovery(LocalRecoverySnapshotInfo snapshot, string sourcePath)
    {
        var cloudState = ReadDocumentState(snapshot.DocumentId);
        var record = new CloudOutboxRecord
        {
            RevisionId = snapshot.RevisionId,
            DocumentId = snapshot.DocumentId,
            Kind = CloudRevisionKind.Recovery,
            ParentRevisionId = ResolveParentRevision(snapshot.DocumentId, cloudState),
            SupersededRevisionIds = cloudState.PreservedConflictRevisionIds,
            SessionId = snapshot.SessionId,
            DeviceId = snapshot.DeviceId,
            DocumentName = snapshot.DocumentName,
            SourcePath = sourcePath,
            CapturedAtUtc = snapshot.CapturedAtUtc,
            PersistenceEpoch = snapshot.PersistenceEpoch,
            ByteLength = snapshot.ByteLength,
            ContentSha256 = snapshot.ContentSha256,
        };
        Write(record);
        return record;
    }

    public CloudOutboxRecord EnqueueManualSave(Entity document, string savedFilePath)
    {
        var settings = document.Get<DocumentSetting>();
        var revisionId = Guid.NewGuid();
        var immutableSourcePath = Path.Combine(_files.ManualSourceRootPath, revisionId.ToString("N") + ".ciallo");
        CopyDurably(savedFilePath, immutableSourcePath);

        var cloudState = ReadDocumentState(settings.DocumentId.Value);
        var parentRevisionId = ResolveParentRevision(settings.DocumentId.Value, cloudState);
        var sourceInfo = new FileInfo(immutableSourcePath);
        var record = new CloudOutboxRecord
        {
            RevisionId = revisionId,
            DocumentId = settings.DocumentId.Value,
            Kind = CloudRevisionKind.Saved,
            ParentRevisionId = parentRevisionId,
            SupersededRevisionIds = cloudState.PreservedConflictRevisionIds,
            DeviceId = _files.DeviceId,
            DocumentName = settings.Name.Value,
            SourcePath = immutableSourcePath,
            DeleteSourceAfterUpload = true,
            CapturedAtUtc = DateTimeOffset.UtcNow,
            PersistenceEpoch = document.Get<CommandManager>().PersistenceEpoch,
            ByteLength = sourceInfo.Length,
            ContentSha256 = DurabilityFiles.Sha256(immutableSourcePath),
        };
        Write(record);
        WriteDocumentState(cloudState with { LastLocalSavedRevisionId = revisionId });
        return record;
    }

    public CloudOutboxRecord EnqueueSession(EditingSessionInfo session)
    {
        var record = new CloudOutboxRecord
        {
            RevisionId = session.SessionId,
            DocumentId = session.DocumentId,
            Kind = CloudRevisionKind.Session,
            SessionId = session.SessionId,
            DeviceId = session.DeviceId,
            DocumentName = session.DocumentName,
            SourcePath = _files.GetSessionPath(session),
            CapturedAtUtc = session.LastHeartbeatUtc,
            ByteLength = new FileInfo(_files.GetSessionPath(session)).Length,
            ContentSha256 = DurabilityFiles.Sha256(_files.GetSessionPath(session)),
        };
        Write(record);
        return record;
    }

    public IReadOnlyList<CloudOutboxRecord> ListPending()
    {
        var result = new List<CloudOutboxRecord>();
        foreach (var path in Directory.EnumerateFiles(_files.OutboxRootPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var record = DurabilityFiles.ReadJson<CloudOutboxRecord>(path);
                ValidateOutboxRecord(record);
                result.Add(record);
            }
            catch (Exception)
            {
                // A corrupt external outbox record does not hide the other pending uploads.
            }
        }
        return result.OrderBy(record => record.CapturedAtUtc).ToArray();
    }

    public int CountPending()
        => Directory.EnumerateFiles(_files.OutboxRootPath, "*.json", SearchOption.TopDirectoryOnly).Count();

    public void MarkUploaded(CloudOutboxRecord record, DateTimeOffset uploadedAtUtc)
    {
        var receipt = new CloudUploadReceipt
        {
            RevisionId = record.RevisionId,
            DocumentId = record.DocumentId,
            Kind = record.Kind,
            UploadedAtUtc = uploadedAtUtc,
        };
        DurabilityFiles.WriteJson(ReceiptPath(record.RevisionId), receipt);

        var outboxPath = OutboxPath(record);
        if (File.Exists(outboxPath))
        {
            var current = DurabilityFiles.ReadJson<CloudOutboxRecord>(outboxPath);
            if (current.ContentSha256 == record.ContentSha256)
            {
                File.Delete(outboxPath);
                if (record.DeleteSourceAfterUpload && File.Exists(record.SourcePath))
                    File.Delete(record.SourcePath);
            }
        }
        PendingChanged?.Invoke();
    }

    public void SetCloudBaseRevision(
        Guid documentId,
        Guid revisionId,
        IReadOnlyCollection<Guid> preservedConflictRevisionIds)
    {
        var state = ReadDocumentState(documentId);
        WriteDocumentState(state with
        {
            BaseCloudRevisionId = revisionId,
            LastLocalSavedRevisionId = revisionId,
            PreservedConflictRevisionIds = preservedConflictRevisionIds.ToArray(),
        });
    }

    private void ReconcileRecoverySnapshots()
    {
        foreach (var documentDirectory in Directory.EnumerateDirectories(_files.RecoveryRootPath))
        {
            if (!Guid.TryParseExact(Path.GetFileName(documentDirectory), "N", out var documentId))
                continue;
            foreach (var snapshot in _files.ListRecoverySnapshots(documentId))
            {
                if (File.Exists(ReceiptPath(snapshot.RevisionId)) ||
                    File.Exists(Path.Combine(
                        _files.OutboxRootPath,
                        snapshot.RevisionId.ToString("N") + ".json")))
                    continue;
                EnqueueRecovery(snapshot, _files.GetRecoverySnapshotPath(snapshot));
            }
        }
    }

    private Guid? ResolveParentRevision(Guid documentId, DocumentCloudState state)
    {
        if (state.LastLocalSavedRevisionId.HasValue)
            return state.LastLocalSavedRevisionId;

        var latestPending = ListPending()
            .Where(record => record.Kind == CloudRevisionKind.Saved && record.DocumentId == documentId)
            .OrderByDescending(record => record.CapturedAtUtc)
            .FirstOrDefault();
        return latestPending?.RevisionId ?? state.BaseCloudRevisionId;
    }

    private DocumentCloudState ReadDocumentState(Guid documentId)
    {
        var path = DocumentStatePath(documentId);
        var state = File.Exists(path)
            ? DurabilityFiles.ReadJson<DocumentCloudState>(path)
            : new DocumentCloudState { DocumentId = documentId };
        if (state.SchemaVersion != 1 ||
            state.DocumentId != documentId ||
            state.PreservedConflictRevisionIds == null ||
            state.BaseCloudRevisionId == Guid.Empty ||
            state.LastLocalSavedRevisionId == Guid.Empty ||
            state.PreservedConflictRevisionIds.Any(revisionId => revisionId == Guid.Empty))
            throw new IOException($"Document cloud state {path} is invalid.");
        return state;
    }

    private void WriteDocumentState(DocumentCloudState state)
        => DurabilityFiles.WriteJson(DocumentStatePath(state.DocumentId), state);

    private void Write(CloudOutboxRecord record)
    {
        DurabilityFiles.WriteJson(OutboxPath(record), record);
        PendingChanged?.Invoke();
    }

    private string OutboxPath(CloudOutboxRecord record)
        => Path.Combine(_files.OutboxRootPath, record.RevisionId.ToString("N") + ".json");

    private string ReceiptPath(Guid revisionId)
        => Path.Combine(_files.CloudReceiptRootPath, revisionId.ToString("N") + ".json");

    private string DocumentStatePath(Guid documentId)
        => Path.Combine(_files.DocumentCloudStateRootPath, documentId.ToString("N") + ".json");

    private void ValidateOutboxRecord(CloudOutboxRecord record)
    {
        if (record == null ||
            record.SchemaVersion != 1 ||
            record.RevisionId == Guid.Empty ||
            record.DocumentId == Guid.Empty ||
            record.DeviceId == Guid.Empty ||
            record.SupersededRevisionIds == null ||
            record.DocumentName == null ||
            record.SourcePath == null ||
            record.ContentSha256 == null ||
            record.ByteLength < 0 ||
            record.ContentSha256.Length != 64 ||
            !record.ContentSha256.All(Uri.IsHexDigit) ||
            record.ContentSha256 != record.ContentSha256.ToLowerInvariant() ||
            record.Kind is not (CloudRevisionKind.Saved or CloudRevisionKind.Recovery or CloudRevisionKind.Session))
            throw new IOException("A Steam Cloud outbox record is invalid.");
        if (record.ParentRevisionId == Guid.Empty)
            throw new IOException("A Steam Cloud outbox parent revision is invalid.");
        if (record.SupersededRevisionIds.Any(revisionId => revisionId == Guid.Empty) ||
            record.SupersededRevisionIds.Distinct().Count() !=
            record.SupersededRevisionIds.Length)
            throw new IOException("A Steam Cloud outbox conflict candidate is invalid.");
        if ((record.Kind is CloudRevisionKind.Session or CloudRevisionKind.Recovery) &&
            (!record.SessionId.HasValue || record.SessionId == Guid.Empty))
            throw new IOException("A Steam Cloud session outbox record is invalid.");

        var root = Path.GetFullPath(_files.RootPath) + Path.DirectorySeparatorChar;
        var source = Path.GetFullPath(record.SourcePath);
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!source.StartsWith(root, comparison))
            throw new IOException("A Steam Cloud outbox source path is outside Ciallo durability storage.");
    }

    private static void CopyDurably(string sourcePath, string targetPath)
    {
        var temporaryPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.Copy(sourcePath, temporaryPath, false);
            using (var stream = new FileStream(temporaryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                stream.Flush(true);
            File.Move(temporaryPath, targetPath, false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
