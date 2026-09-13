using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ciallo.Data;

internal static class DurabilityFiles
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters =
        {
            new CompactGuidJsonConverter(),
            new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower),
        },
    };

    public static void WriteJson<T>(string path, T value, UnixFileMode? unixFileMode = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            if (unixFileMode != null && !OperatingSystem.IsWindows())
                File.SetUnixFileMode(temporaryPath, unixFileMode.Value);
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public static T ReadJson<T>(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return JsonSerializer.Deserialize<T>(bytes, JsonOptions)!;
    }

    public static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
    }
}

internal sealed class CompactGuidJsonConverter : JsonConverter<Guid>
{
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (!Guid.TryParseExact(reader.GetString(), "N", out var value))
            throw new JsonException("Expected a 32-digit Ciallo identifier.");
        return value;
    }

    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("N"));
}

internal sealed class DurabilityFileStore
{
    public string RootPath { get; }
    public string RecoveryRootPath { get; }
    public string SessionRootPath { get; }
    public string OutboxRootPath { get; }
    public string CloudReceiptRootPath { get; }
    public Guid DeviceId { get; }

    public DurabilityFileStore(string userDataPath)
    {
        RootPath = Path.Combine(userDataPath, "DocumentDurability", "v1");
        RecoveryRootPath = Path.Combine(RootPath, "recovery");
        SessionRootPath = Path.Combine(RootPath, "sessions");
        OutboxRootPath = Path.Combine(RootPath, "outbox");
        CloudReceiptRootPath = Path.Combine(RootPath, "cloud-receipts");

        Directory.CreateDirectory(RecoveryRootPath);
        Directory.CreateDirectory(SessionRootPath);
        Directory.CreateDirectory(OutboxRootPath);
        Directory.CreateDirectory(CloudReceiptRootPath);
        DeviceId = LoadOrCreateDeviceId();
    }

    public RecoverySnapshotWriteResult WriteRecoverySnapshot(RecoveryWriteRequest request)
    {
        var documentDirectory = Path.Combine(RecoveryRootPath, request.Snapshot.DocumentId.ToString("N"));
        Directory.CreateDirectory(documentDirectory);

        var timestamp = request.Snapshot.CapturedAtUtc.UtcDateTime.ToString(
            "yyyyMMdd'T'HHmmssfff'Z'",
            CultureInfo.InvariantCulture);
        var fileName = timestamp + "_" + request.RevisionId.ToString("N") + ".ciallo";
        var snapshotPath = Path.Combine(documentDirectory, fileName);
        DuckDbProjectSerializer.Save(request.Snapshot, snapshotPath);

        var fileInfo = new FileInfo(snapshotPath);
        var info = new LocalRecoverySnapshotInfo
        {
            RevisionId = request.RevisionId,
            DocumentId = request.Snapshot.DocumentId,
            SessionId = request.SessionId,
            DeviceId = request.DeviceId,
            DocumentName = request.DocumentName,
            OriginalFilePath = request.OriginalFilePath,
            SnapshotFileName = fileName,
            ContentSha256 = DurabilityFiles.Sha256(snapshotPath),
            ByteLength = fileInfo.Length,
            PersistenceEpoch = request.Snapshot.PersistenceEpoch,
            CapturedAtUtc = request.Snapshot.CapturedAtUtc,
        };

        DurabilityFiles.WriteJson(Path.ChangeExtension(snapshotPath, ".json"), info);
        var retention = ApplyRecoveryRetention(request.RetentionPolicy);
        return new RecoverySnapshotWriteResult(info, retention);
    }

    public LocalRecoverySnapshotInfo InstallDownloadedSnapshot(
        SteamCloudRecoveryManifest revision,
        string assembledPath,
        RecoveryRetentionPolicy retentionPolicy)
    {
        var documentDirectory = Path.Combine(RecoveryRootPath, revision.DocumentId.ToString("N"));
        Directory.CreateDirectory(documentDirectory);

        var timestamp = revision.CapturedAtUtc.UtcDateTime.ToString(
            "yyyyMMdd'T'HHmmssfff'Z'",
            CultureInfo.InvariantCulture);
        var fileName = timestamp + "_" + revision.RevisionId.ToString("N") + ".ciallo";
        var snapshotPath = Path.Combine(documentDirectory, fileName);
        var temporaryPath = snapshotPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.Copy(assembledPath, temporaryPath, false);
            using (var stream = new FileStream(temporaryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                stream.Flush(true);
            File.Move(temporaryPath, snapshotPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        var info = new LocalRecoverySnapshotInfo
        {
            RevisionId = revision.RevisionId,
            DocumentId = revision.DocumentId,
            SessionId = revision.SessionId!.Value,
            DeviceId = revision.DeviceId,
            DocumentName = revision.DocumentName,
            OriginalFilePath = revision.OriginalFilePath,
            SnapshotFileName = fileName,
            ContentSha256 = revision.ContentSha256,
            ByteLength = revision.ByteLength,
            PersistenceEpoch = revision.PersistenceEpoch,
            CapturedAtUtc = revision.CapturedAtUtc,
        };
        DurabilityFiles.WriteJson(Path.ChangeExtension(snapshotPath, ".json"), info);
        ApplyRecoveryRetention(retentionPolicy);
        return info;
    }

    public LocalRecoverySnapshotInfo GetRecoverySnapshot(Guid revisionId)
        => ReadRecoverySnapshots().Single(snapshot => snapshot.RevisionId == revisionId);

    public IReadOnlyList<LocalRecoverySnapshotInfo> ListRecoverySnapshots(Guid documentId)
    {
        return ReadRecoverySnapshots()
            .Where(snapshot => snapshot.DocumentId == documentId)
            .OrderByDescending(snapshot => snapshot.CapturedAtUtc)
            .ToArray();
    }

    public IReadOnlyList<LocalRecoverySnapshotInfo> ListRecoveryCandidates(DateTimeOffset nowUtc)
    {
        var interruptedSessionIds = ReadSessions()
            .Where(session => session.IsInterrupted(nowUtc))
            .Select(session => session.SessionId)
            .ToHashSet();

        return ReadRecoverySnapshots()
            .Where(snapshot => interruptedSessionIds.Contains(snapshot.SessionId))
            .OrderByDescending(snapshot => snapshot.CapturedAtUtc)
            .ToArray();
    }

    public string GetRecoverySnapshotPath(LocalRecoverySnapshotInfo info)
    {
        return Path.Combine(RecoveryRootPath, info.DocumentId.ToString("N"), info.SnapshotFileName);
    }

    public string GetSessionPath(EditingSessionInfo session)
    {
        return Path.Combine(SessionRootPath, session.DocumentId.ToString("N"), session.SessionId.ToString("N") + ".json");
    }

    public EditingSessionInfo CreateSession(Guid documentId, string documentName, DateTimeOffset nowUtc)
    {
        return new EditingSessionInfo
        {
            SessionId = Guid.NewGuid(),
            DocumentId = documentId,
            DeviceId = DeviceId,
            DocumentName = documentName,
            OpenedAtUtc = nowUtc,
            LastHeartbeatUtc = nowUtc,
        };
    }

    public EditingSessionInfo HeartbeatSession(EditingSessionInfo session, DateTimeOffset nowUtc)
    {
        var updated = session with { LastHeartbeatUtc = nowUtc };
        WriteSession(updated);
        return updated;
    }

    public EditingSessionInfo CloseSession(EditingSessionInfo session, DateTimeOffset nowUtc)
    {
        var updated = session with
        {
            LastHeartbeatUtc = nowUtc,
            ClosedAtUtc = nowUtc,
        };
        WriteSession(updated);
        return updated;
    }

    public void WriteSession(EditingSessionInfo session)
    {
        var path = Path.Combine(SessionRootPath, session.DocumentId.ToString("N"), session.SessionId.ToString("N") + ".json");
        DurabilityFiles.WriteJson(path, session);
    }

    private IReadOnlyList<EditingSessionInfo> ReadSessions()
    {
        if (!Directory.Exists(SessionRootPath))
            return [];

        var result = new List<EditingSessionInfo>();
        foreach (var path in Directory.EnumerateFiles(SessionRootPath, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                result.Add(DurabilityFiles.ReadJson<EditingSessionInfo>(path));
            }
            catch (Exception)
            {
                // A corrupt external sidecar does not invalidate other recovery sessions.
            }
        }
        return result;
    }

    private IReadOnlyList<LocalRecoverySnapshotInfo> ReadRecoverySnapshots()
    {
        if (!Directory.Exists(RecoveryRootPath))
            return [];

        var result = new List<LocalRecoverySnapshotInfo>();
        foreach (var path in Directory.EnumerateFiles(RecoveryRootPath, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                result.Add(DurabilityFiles.ReadJson<LocalRecoverySnapshotInfo>(path));
            }
            catch (Exception)
            {
                // A corrupt external sidecar does not invalidate other recovery snapshots.
            }
        }
        return result;
    }

    private RecoveryRetentionResult ApplyRecoveryRetention(RecoveryRetentionPolicy policy)
    {
        var plan = RecoveryRetentionPlanner.Plan(
            ReadRecoverySnapshots(),
            snapshot => snapshot.RevisionId,
            snapshot => snapshot.DocumentId,
            snapshot => snapshot.CapturedAtUtc,
            snapshot => snapshot.ByteLength,
            new RecoveryRetentionLimits(
                policy.PerDocumentLimit,
                policy.AccountFileLimit,
                policy.AccountByteLimit));

        var errors = new List<string>();
        foreach (var snapshot in plan.Evicted)
        {
            try
            {
                DeleteRecoverySnapshot(snapshot);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                errors.Add(exception.Message);
            }
        }

        var limitExceeded = plan.LimitExceeded || errors.Count > 0;
        return new RecoveryRetentionResult(limitExceeded, string.Join(Environment.NewLine, errors));
    }

    private void DeleteRecoverySnapshot(LocalRecoverySnapshotInfo snapshot)
    {
        var snapshotPath = GetRecoverySnapshotPath(snapshot);
        if (File.Exists(snapshotPath))
            File.Delete(snapshotPath);
        var metadataPath = Path.ChangeExtension(snapshotPath, ".json");
        if (File.Exists(metadataPath))
            File.Delete(metadataPath);
        var outboxPath = Path.Combine(OutboxRootPath, snapshot.RevisionId.ToString("N") + ".json");
        if (File.Exists(outboxPath))
            File.Delete(outboxPath);
        var receiptPath = Path.Combine(CloudReceiptRootPath, snapshot.RevisionId.ToString("N") + ".json");
        if (File.Exists(receiptPath))
            File.Delete(receiptPath);
    }

    private Guid LoadOrCreateDeviceId()
    {
        var path = Path.Combine(RootPath, "device.json");
        if (File.Exists(path))
            return DurabilityFiles.ReadJson<DeviceIdentity>(path).DeviceId;

        var identity = new DeviceIdentity { DeviceId = Guid.NewGuid() };
        DurabilityFiles.WriteJson(path, identity);
        return identity.DeviceId;
    }

    private sealed record DeviceIdentity
    {
        public int SchemaVersion { get; init; } = 1;
        public Guid DeviceId { get; init; }
    }
}
