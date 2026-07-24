using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Frent;

namespace Ciallo.Data;

public static class AppDocumentDurability
{
    private static DocumentDurabilityService _service;

    public static DocumentDurabilityStatus Status => _service.Status;
    public static Guid DeviceId => _service.DeviceId;

    public static event Action<DocumentDurabilityStatus> StatusChanged
    {
        add => _service.StatusChanged += value;
        remove => _service.StatusChanged -= value;
    }

    internal static DurabilityFileStore FileStore => _service.FileStore;
    internal static CloudOutboxStore OutboxStore => _service.OutboxStore;

    internal static void Initialize(string userDataPath)
    {
        _service = new DocumentDurabilityService(userDataPath);
    }

    internal static void Process(double delta) => _service.Process(delta);

    internal static void OnDocumentClosing(Entity document) => _service.OnDocumentClosing(document);

    internal static Task ShutdownAsync() => _service.ShutdownAsync();

    internal static void EnqueueManualSave(Entity document, string filePath)
        => _service.EnqueueManualSave(document, filePath);

    internal static void SetCloudStatus(
        SteamCloudSyncState state,
        string externalError = "",
        DateTimeOffset? protectionPointUtc = null,
        bool? retentionLimitExceeded = null)
        => _service.SetCloudStatus(state, externalError, protectionPointUtc, retentionLimitExceeded);

    public static IReadOnlyList<LocalRecoverySnapshotInfo> ListLocalSnapshots(Guid documentId)
        => _service.ListRecoverySnapshots(documentId);

    public static IReadOnlyList<LocalRecoverySnapshotInfo> ListRecoveryCandidates()
        => _service.ListRecoveryCandidates();

    public static Entity OpenRecoverySnapshot(Guid revisionId)
        => _service.OpenRecoverySnapshot(revisionId);

    public static Entity OpenManagedCloudCopy(ManagedCloudCopyInfo copy)
        => _service.OpenManagedCloudCopy(copy);
}
