using System;

namespace Ciallo.Data;

public enum SteamCloudRecoveryKind
{
    Recovery,
    Session,
}

public sealed record SteamCloudRecoveryChunk
{
    public int Index { get; init; }
    public int ByteLength { get; init; }
    public string Sha256 { get; init; } = "";
}

public sealed record SteamCloudRecoveryManifest
{
    public int SchemaVersion { get; init; } = 1;
    public Guid RevisionId { get; init; }
    public Guid DocumentId { get; init; }
    public SteamCloudRecoveryKind Kind { get; init; }
    public Guid? SessionId { get; init; }
    public Guid DeviceId { get; init; }
    public string DocumentName { get; init; } = "";
    public string OriginalFilePath { get; init; } = "";
    public DateTimeOffset CapturedAtUtc { get; init; }
    public long PersistenceEpoch { get; init; }
    public long ByteLength { get; init; }
    public string ContentSha256 { get; init; } = "";
    public SteamCloudRecoveryChunk[] Chunks { get; init; } = [];
}

public sealed record SteamCloudRecoveryCatalogSnapshot
{
    public DateTimeOffset RefreshedAtUtc { get; init; }
    public SteamCloudRecoveryManifest[] RecoveryCandidates { get; init; } = [];
}

internal sealed record SteamCloudRecoveryOutboxRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid RevisionId { get; init; }
    public Guid DocumentId { get; init; }
    public SteamCloudRecoveryKind Kind { get; init; }
    public Guid? SessionId { get; init; }
    public Guid DeviceId { get; init; }
    public string DocumentName { get; init; } = "";
    public string OriginalFilePath { get; init; } = "";
    public string SourcePath { get; init; } = "";
    public DateTimeOffset CapturedAtUtc { get; init; }
    public long PersistenceEpoch { get; init; }
    public long ByteLength { get; init; }
    public string ContentSha256 { get; init; } = "";
}

internal sealed record SteamCloudRecoveryReceipt
{
    public int SchemaVersion { get; init; } = 1;
    public Guid RevisionId { get; init; }
    public Guid DocumentId { get; init; }
    public SteamCloudRecoveryKind Kind { get; init; }
    public DateTimeOffset UploadedAtUtc { get; init; }
}

internal static class SteamCloudRecoveryPaths
{
    public const string Prefix = "ciallo/v1";
    public const string SavedRevisionPrefix = Prefix + "/revisions/saved/";
    public const string RecoveryRevisionPrefix = Prefix + "/revisions/recovery/";
    public const string SessionPrefix = Prefix + "/sessions/";
    public const string ChunkPrefix = Prefix + "/chunks/";

    public static string Chunk(string sha256) => ChunkPrefix + sha256 + ".bin";

    public static string RevisionManifest(Guid documentId, Guid revisionId)
        => RecoveryRevisionPrefix + documentId.ToString("N") + "/" + revisionId.ToString("N") + ".json";

    public static string Session(Guid documentId, Guid sessionId)
        => SessionPrefix + documentId.ToString("N") + "/" + sessionId.ToString("N") + ".json";
}
