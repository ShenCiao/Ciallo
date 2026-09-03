using Godot;
using Steamworks;
using System;
using System.Threading;
using System.Threading.Tasks;
using Ciallo.Data;

namespace Ciallo;

public partial class SteamManager : Node
{
    public const uint AppId = 4103990;
    private static SteamCloudRecoveryCoordinator _recovery;

    public static bool IsRecoveryCloudAvailable => _recovery != null;
    public static SteamCloudAuthorizationStatus CloudAuthorizationStatus => _recovery.AuthorizationStatus;
    public static SteamCloudRecoveryCatalogSnapshot RecoveryCatalog => _recovery.Catalog;
    public static event Action<SteamCloudAuthorizationStatus> CloudAuthorizationStatusChanged
    {
        add => _recovery.AuthorizationStatusChanged += value;
        remove => _recovery.AuthorizationStatusChanged -= value;
    }
    public static event Action<SteamCloudRecoveryCatalogSnapshot> RecoveryCatalogChanged
    {
        add => _recovery.CatalogChanged += value;
        remove => _recovery.CatalogChanged -= value;
    }

    public override void _EnterTree()
    {
        try
        {
            SteamClient.Init(AppId, true);
            if (SteamClient.IsValid)
                GD.Print("Steam initialized successfully.");
            else
                throw new Exception();

            var options = SteamCloudOptions.Load(AppId);
            _recovery = new SteamCloudRecoveryCoordinator(
                options,
                SteamClient.SteamId,
                AppDocumentDurability.FileStore,
                AppDocumentDurability.OutboxStore);
        }
        catch (Exception e)
        {
            GD.Print($"Steam initialization failed: {e.Message}");
            AppDocumentDurability.SetRecoveryCloudStatus(SteamCloudRecoveryState.Offline, e.Message);
            return;
        }
    }

    public override void _Process(double delta)
    {
        _recovery?.ProcessNotifications();
    }

    public override void _ExitTree()
    {
        // Only pure synchronous teardown here. The cloud coordinator is disposed asynchronously
        // during the normal close flow (ShutdownRecoveryCloudAsync) so we never block the main thread on a
        // sync-over-async wait while a captured SynchronizationContext continuation is queued to it.
        if (SteamClient.IsValid)
            SteamClient.Shutdown();
    }

    /// <summary>
    /// Awaitable cloud teardown, driven from the main-thread async close path. Idempotent: the
    /// coordinator's own DisposeAsync guards against a second call, and a null coordinator (Steam
    /// never initialized, or already shut down) is a no-op.
    /// </summary>
    public static async Task ShutdownRecoveryCloudAsync()
    {
        if (_recovery == null)
            return;
        var recovery = _recovery;
        _recovery = null;
        await recovery.DisposeAsync();
    }

    public static Task AuthorizeCloudAsync(CancellationToken cancellationToken = default)
        => _recovery.AuthorizeAsync(uri => { OS.ShellOpen(uri); }, cancellationToken);

    public static void ClearCloudAuthorization() => _recovery.ClearAuthorization();

    public static Task FlushRecoverySessionAsync(TimeSpan timeout)
        => _recovery.FlushSessionStateAsync(timeout);

    public static Task<SteamCloudRecoveryCatalogSnapshot> RefreshRecoveryCatalogAsync(
        CancellationToken cancellationToken = default)
        => _recovery.RefreshCatalogAsync(cancellationToken);

    public static Task<LocalRecoverySnapshotInfo> DownloadRecoverySnapshotAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default)
        => _recovery.DownloadSnapshotAsync(revisionId, cancellationToken);
}
