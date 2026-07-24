using System;

namespace Ciallo.Data;

public enum CloudRevisionKind
{
    Saved,
    Recovery,
    Session,
}

public sealed record CloudChunkDescriptor
{
    public int Index { get; init; }
    public int ByteLength { get; init; }
    public string Sha256 { get; init; } = "";
}

public sealed record CloudRevisionManifest
{
    public int SchemaVersion { get; init; } = 1;
    public Guid RevisionId { get; init; }
    public Guid DocumentId { get; init; }
    public CloudRevisionKind Kind { get; init; }
    public Guid? ParentRevisionId { get; init; }
    public Guid[] SupersededRevisionIds { get; init; } = [];
    public Guid? SessionId { get; init; }
    public Guid DeviceId { get; init; }
    public string DocumentName { get; init; } = "";
    public DateTimeOffset CapturedAtUtc { get; init; }
    public long PersistenceEpoch { get; init; }
    public long ByteLength { get; init; }
    public string ContentSha256 { get; init; } = "";
    public CloudChunkDescriptor[] Chunks { get; init; } = [];
}

public sealed record CloudDocumentInfo
{
    public Guid DocumentId { get; init; }
    public string DocumentName { get; init; } = "";
    public CloudRevisionManifest[] SavedHeads { get; init; } = [];
    public CloudRevisionManifest[] PreservedConflictCandidates { get; init; } = [];
    public bool HasConflict => SavedHeads.Length > 1;
}

public sealed record SteamCloudCatalogSnapshot
{
    public DateTimeOffset RefreshedAtUtc { get; init; }
    public CloudDocumentInfo[] Documents { get; init; } = [];
    public CloudRevisionManifest[] RecoveryCandidates { get; init; } = [];
}

public sealed record ManagedCloudCopyInfo
{
    public string LocalFilePath { get; init; } = "";
    public CloudRevisionManifest Revision { get; init; } = new();
}

internal sealed record CloudOutboxRecord
{
    public int SchemaVersion { get; init; } = 1;
    public Guid RevisionId { get; init; }
    public Guid DocumentId { get; init; }
    public CloudRevisionKind Kind { get; init; }
    public Guid? ParentRevisionId { get; init; }
    public Guid[] SupersededRevisionIds { get; init; } = [];
    public Guid? SessionId { get; init; }
    public Guid DeviceId { get; init; }
    public string DocumentName { get; init; } = "";
    public string SourcePath { get; init; } = "";
    public bool DeleteSourceAfterUpload { get; init; }
    public DateTimeOffset CapturedAtUtc { get; init; }
    public long PersistenceEpoch { get; init; }
    public long ByteLength { get; init; }
    public string ContentSha256 { get; init; } = "";
}

internal sealed record CloudUploadReceipt
{
    public int SchemaVersion { get; init; } = 1;
    public Guid RevisionId { get; init; }
    public Guid DocumentId { get; init; }
    public CloudRevisionKind Kind { get; init; }
    public DateTimeOffset UploadedAtUtc { get; init; }
}

internal sealed record DocumentCloudState
{
    public int SchemaVersion { get; init; } = 1;
    public Guid DocumentId { get; init; }
    public Guid? BaseCloudRevisionId { get; init; }
    public Guid? LastLocalSavedRevisionId { get; init; }
    public Guid[] PreservedConflictRevisionIds { get; init; } = [];
}

internal static class SteamCloudPaths
{
    public const string Prefix = "ciallo/v1";

    public static string Chunk(string sha256) => Prefix + "/chunks/" + sha256 + ".bin";

    public static string RevisionManifest(CloudRevisionKind kind, Guid documentId, Guid revisionId)
        => Prefix + "/revisions/" + KindSegment(kind) + "/" + documentId.ToString("N") + "/" + revisionId.ToString("N") + ".json";

    public static string Session(Guid documentId, Guid sessionId)
        => Prefix + "/sessions/" + documentId.ToString("N") + "/" + sessionId.ToString("N") + ".json";

    public static string KindSegment(CloudRevisionKind kind) => kind switch
    {
        CloudRevisionKind.Saved => "saved",
        CloudRevisionKind.Recovery => "recovery",
        CloudRevisionKind.Session => "session",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
