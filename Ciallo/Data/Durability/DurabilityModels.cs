using System;

namespace Ciallo.Data;

public enum LocalRecoveryState
{
    Idle,
    Capturing,
    Writing,
    Failed,
}

public enum SteamCloudRecoveryState
{
    Unconfigured,
    AuthorizationRequired,
    Ready,
    Uploading,
    Offline,
    Failed,
}

public sealed record DocumentDurabilityStatus
{
    public Guid DocumentId { get; init; }
    public LocalRecoveryState LocalState { get; init; } = LocalRecoveryState.Idle;
    public SteamCloudRecoveryState RecoveryCloudState { get; init; } = SteamCloudRecoveryState.Unconfigured;
    public int PendingUploadCount { get; init; }
    public DateTimeOffset? LatestLocalSnapshotUtc { get; init; }
    public DateTimeOffset? LatestRecoveryProtectionUtc { get; init; }
    public bool LocalRetentionLimitExceeded { get; init; }
    public bool RecoveryCloudRetentionLimitExceeded { get; init; }
    public string LastLocalError { get; init; } = "";
    public string LastRecoveryCloudError { get; init; } = "";
    public string LastExternalError => LastLocalError.Length > 0 ? LastLocalError : LastRecoveryCloudError;
}

public sealed record LocalRecoverySnapshotInfo
{
    public int SchemaVersion { get; init; } = 1;
    public Guid RevisionId { get; init; }
    public Guid DocumentId { get; init; }
    public Guid SessionId { get; init; }
    public Guid DeviceId { get; init; }
    public string DocumentName { get; init; } = "";
    public string OriginalFilePath { get; init; } = "";
    public string SnapshotFileName { get; init; } = "";
    public string ContentSha256 { get; init; } = "";
    public long ByteLength { get; init; }
    public long PersistenceEpoch { get; init; }
    public DateTimeOffset CapturedAtUtc { get; init; }
}

public sealed record EditingSessionInfo
{
    /// <summary>
    /// A session with no close marker whose heartbeat is at least this stale is treated as interrupted,
    /// which makes its recovery snapshots eligible as recovery candidates. Shared by local and cloud
    /// candidate detection so both sides use one threshold.
    /// </summary>
    public static readonly TimeSpan InterruptedTimeout = TimeSpan.FromMinutes(15);

    public int SchemaVersion { get; init; } = 1;
    public Guid SessionId { get; init; }
    public Guid DocumentId { get; init; }
    public Guid DeviceId { get; init; }
    public string DocumentName { get; init; } = "";
    public DateTimeOffset OpenedAtUtc { get; init; }
    public DateTimeOffset LastHeartbeatUtc { get; init; }
    public DateTimeOffset? ClosedAtUtc { get; init; }

    public bool IsInterrupted(DateTimeOffset nowUtc)
        => ClosedAtUtc == null && nowUtc - LastHeartbeatUtc >= InterruptedTimeout;
}

internal sealed record RecoveryWriteRequest(
    PersistenceSnapshot Snapshot,
    Guid RevisionId,
    Guid SessionId,
    Guid DeviceId,
    string DocumentName,
    string OriginalFilePath,
    RecoveryRetentionPolicy RetentionPolicy);

internal sealed record RecoveryRetentionPolicy(
    int PerDocumentLimit,
    int AccountFileLimit,
    long AccountByteLimit);

internal sealed record RecoveryRetentionResult(
    bool LimitExceeded,
    string Error);

internal sealed record RecoverySnapshotWriteResult(
    LocalRecoverySnapshotInfo Snapshot,
    RecoveryRetentionResult Retention);
