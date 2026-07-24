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
        if (_cloud != null)
        {
            _cloud.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _cloud = null;
        }
        SteamClient.Shutdown();
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
