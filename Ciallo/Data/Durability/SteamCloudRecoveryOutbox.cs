using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Ciallo.Data;

internal sealed class SteamCloudRecoveryOutbox
{
    private readonly DurabilityFileStore _files;
    public event Action PendingChanged;

    public SteamCloudRecoveryOutbox(DurabilityFileStore files)
    {
        _files = files;
        DiscardAbandonedSavedRecords();
        ReconcileRecoverySnapshots();
    }

    public SteamCloudRecoveryOutboxRecord EnqueueRecovery(LocalRecoverySnapshotInfo snapshot, string sourcePath)
    {
        var record = new SteamCloudRecoveryOutboxRecord
        {
            RevisionId = snapshot.RevisionId,
            DocumentId = snapshot.DocumentId,
            Kind = SteamCloudRecoveryKind.Recovery,
            SessionId = snapshot.SessionId,
            DeviceId = snapshot.DeviceId,
            DocumentName = snapshot.DocumentName,
            OriginalFilePath = snapshot.OriginalFilePath,
            SourcePath = sourcePath,
            CapturedAtUtc = snapshot.CapturedAtUtc,
            PersistenceEpoch = snapshot.PersistenceEpoch,
            ByteLength = snapshot.ByteLength,
            ContentSha256 = snapshot.ContentSha256,
        };
        Write(record);
        return record;
    }

    public SteamCloudRecoveryOutboxRecord EnqueueSession(EditingSessionInfo session)
    {
        var record = new SteamCloudRecoveryOutboxRecord
        {
            RevisionId = session.SessionId,
            DocumentId = session.DocumentId,
            Kind = SteamCloudRecoveryKind.Session,
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

    public IReadOnlyList<SteamCloudRecoveryOutboxRecord> ListPending()
    {
        var result = new List<SteamCloudRecoveryOutboxRecord>();
        foreach (var path in Directory.EnumerateFiles(_files.OutboxRootPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var record = DurabilityFiles.ReadJson<SteamCloudRecoveryOutboxRecord>(path);
                ValidateOutboxRecord(record);
                result.Add(record);
            }
            catch (Exception)
            {
                // A corrupt or abandoned outbox record does not hide the other pending uploads.
            }
        }
        return result.OrderBy(record => record.CapturedAtUtc).ToArray();
    }

    public int CountPending() => ListPending().Count;

    public void MarkUploaded(SteamCloudRecoveryOutboxRecord record, DateTimeOffset uploadedAtUtc)
    {
        WriteReceipt(record.RevisionId, record.DocumentId, record.Kind, uploadedAtUtc);

        var outboxPath = OutboxPath(record);
        if (File.Exists(outboxPath))
        {
            var current = DurabilityFiles.ReadJson<SteamCloudRecoveryOutboxRecord>(outboxPath);
            if (current.ContentSha256 == record.ContentSha256)
                File.Delete(outboxPath);
        }
        PendingChanged?.Invoke();
    }

    public void RecordReceipt(Guid revisionId, Guid documentId, SteamCloudRecoveryKind kind, DateTimeOffset at)
        => WriteReceipt(revisionId, documentId, kind, at);

    private void DiscardAbandonedSavedRecords()
    {
        foreach (var path in Directory.EnumerateFiles(_files.OutboxRootPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllBytes(path));
                if (document.RootElement.TryGetProperty("kind", out var kind) &&
                    kind.ValueKind == JsonValueKind.String &&
                    string.Equals(kind.GetString(), "saved", StringComparison.Ordinal))
                    File.Delete(path);
            }
            catch (Exception)
            {
                // Leave unreadable records for ListPending to skip.
            }
        }
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

    private void WriteReceipt(
        Guid revisionId,
        Guid documentId,
        SteamCloudRecoveryKind kind,
        DateTimeOffset uploadedAtUtc)
    {
        DurabilityFiles.WriteJson(ReceiptPath(revisionId), new SteamCloudRecoveryReceipt
        {
            RevisionId = revisionId,
            DocumentId = documentId,
            Kind = kind,
            UploadedAtUtc = uploadedAtUtc,
        });
    }

    private void Write(SteamCloudRecoveryOutboxRecord record)
    {
        DurabilityFiles.WriteJson(OutboxPath(record), record);
        PendingChanged?.Invoke();
    }

    private string OutboxPath(SteamCloudRecoveryOutboxRecord record)
        => Path.Combine(_files.OutboxRootPath, record.RevisionId.ToString("N") + ".json");

    private string ReceiptPath(Guid revisionId)
        => Path.Combine(_files.CloudReceiptRootPath, revisionId.ToString("N") + ".json");

    private void ValidateOutboxRecord(SteamCloudRecoveryOutboxRecord record)
    {
        if (record == null ||
            record.SchemaVersion != 1 ||
            record.RevisionId == Guid.Empty ||
            record.DocumentId == Guid.Empty ||
            record.DeviceId == Guid.Empty ||
            record.DocumentName == null ||
            record.OriginalFilePath == null ||
            record.SourcePath == null ||
            record.ContentSha256 == null ||
            record.ByteLength < 0 ||
            record.ContentSha256.Length != 64 ||
            !record.ContentSha256.All(Uri.IsHexDigit) ||
            record.ContentSha256 != record.ContentSha256.ToLowerInvariant() ||
            record.Kind is not (SteamCloudRecoveryKind.Recovery or SteamCloudRecoveryKind.Session))
            throw new IOException("A Steam Cloud outbox record is invalid.");
        if ((record.Kind is SteamCloudRecoveryKind.Session or SteamCloudRecoveryKind.Recovery) &&
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
}
