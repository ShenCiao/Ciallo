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
    private static SteamCloudCoordinator _cloud;

    public static bool IsCloudAvailable => _cloud != null;
    public static SteamCloudAuthorizationStatus CloudAuthorizationStatus => _cloud.AuthorizationStatus;
    public static SteamCloudCatalogSnapshot CloudCatalog => _cloud.Catalog;
    public static event Action<SteamCloudAuthorizationStatus> CloudAuthorizationStatusChanged
    {
        add => _cloud.AuthorizationStatusChanged += value;
        remove => _cloud.AuthorizationStatusChanged -= value;
    }
    public static event Action<SteamCloudCatalogSnapshot> CloudCatalogChanged
    {
        add => _cloud.CatalogChanged += value;
        remove => _cloud.CatalogChanged -= value;
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
            _cloud = new SteamCloudCoordinator(
                options,
                SteamClient.SteamId,
                AppDocumentDurability.FileStore,
                AppDocumentDurability.OutboxStore);
        }
        catch (Exception e)
        {
            GD.Print($"Steam initialization failed: {e.Message}");
            AppDocumentDurability.SetCloudStatus(SteamCloudSyncState.Offline, e.Message);
            return;
        }
    }

    public override void _Process(double delta)
    {
        _cloud?.ProcessNotifications();
    }

    public override void _ExitTree()
    {
        // Only pure synchronous teardown here. The cloud coordinator is disposed asynchronously
        // during the normal close flow (ShutdownCloudAsync) so we never block the main thread on a
        // sync-over-async wait while a captured SynchronizationContext continuation is queued to it.
        if (SteamClient.IsValid)
            SteamClient.Shutdown();
    }

    /// <summary>
    /// Awaitable cloud teardown, driven from the main-thread async close path. Idempotent: the
    /// coordinator's own DisposeAsync guards against a second call, and a null coordinator (Steam
    /// never initialized, or already shut down) is a no-op.
    /// </summary>
    public static async Task ShutdownCloudAsync()
    {
        if (_cloud == null)
            return;
        var cloud = _cloud;
        _cloud = null;
        await cloud.DisposeAsync();
    }

    public static Task AuthorizeCloudAsync(CancellationToken cancellationToken = default)
        => _cloud.AuthorizeAsync(uri => { OS.ShellOpen(uri); }, cancellationToken);

    public static void ClearCloudAuthorization() => _cloud.ClearAuthorization();

    public static Task FlushCloudSessionAsync(TimeSpan timeout)
        => _cloud.FlushSessionStateAsync(timeout);

    public static Task<SteamCloudCatalogSnapshot> RefreshCloudCatalogAsync(
        CancellationToken cancellationToken = default)
        => _cloud.RefreshCatalogAsync(cancellationToken);

    public static Task<ManagedCloudCopyInfo> DownloadCloudRevisionAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default)
        => _cloud.DownloadRevisionAsync(revisionId, cancellationToken);

    public static Task<SteamCloudCatalogSnapshot> ResolveCloudConflictAsync(
        Guid documentId,
        Guid chosenRevisionId,
        CancellationToken cancellationToken = default)
        => _cloud.ResolveConflictAsync(documentId, chosenRevisionId, AppDocumentDurability.DeviceId, cancellationToken);
}
