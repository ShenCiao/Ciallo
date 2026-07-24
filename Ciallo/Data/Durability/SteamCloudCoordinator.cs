using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Steamworks.WebApi;

namespace Ciallo.Data;

internal sealed class SteamCloudCoordinator : IAsyncDisposable
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CatalogRefreshInterval = TimeSpan.FromMinutes(5);
    private const string CloudRetentionLimitMessage =
        "The newest recovery snapshot exceeds the configured Steam Cloud recovery storage limit.";

    private readonly SteamCloudOptions _options;
    private readonly CloudOutboxStore _outbox;
    private readonly HttpClient _httpClient;
    private readonly SteamCloudAuthorization _authorization;
    private readonly AuthorizedSteamCloudClient _webApi;
    private readonly SteamCloudCatalogService _catalogService;
    private readonly SemaphoreSlim _uploadSignal = new(0, 1);
    private readonly SemaphoreSlim _cloudOperationLock = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _uploadLoop;
    private readonly ConcurrentQueue<SteamCloudAuthorizationStatus> _authorizationNotifications = new();
    private readonly ConcurrentQueue<SteamCloudCatalogSnapshot> _catalogNotifications = new();
    private DateTimeOffset _nextCatalogRefreshUtc = DateTimeOffset.MinValue;

    public SteamCloudAuthorizationStatus AuthorizationStatus => _authorization.Status;
    public SteamCloudCatalogSnapshot Catalog => _catalogService.Catalog;
    public event Action<SteamCloudAuthorizationStatus> AuthorizationStatusChanged;
    public event Action<SteamCloudCatalogSnapshot> CatalogChanged;

    public SteamCloudCoordinator(
        SteamCloudOptions options,
        ulong currentSteamId,
        DurabilityFileStore files,
        CloudOutboxStore outbox)
    {
        _options = options;
        _outbox = outbox;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _authorization = new SteamCloudAuthorization(
            options,
            currentSteamId,
            files.RootPath,
            _httpClient);
        var protocol = new SteamCloudWebApiClient(_httpClient);
        _webApi = new AuthorizedSteamCloudClient(
            protocol,
            options,
            _authorization);
        _catalogService = new SteamCloudCatalogService(options, files, outbox, _webApi, protocol);

        _authorization.StatusChanged += OnAuthorizationStatusChanged;
        _outbox.PendingChanged += SignalUpload;
        ApplyAuthorizationStatus(_authorization.Status);
        _uploadLoop = Task.Run(UploadLoopAsync);
        SignalUpload();
    }

    public Task AuthorizeAsync(Action<string> openBrowser, CancellationToken cancellationToken = default)
        => _authorization.AuthorizeAsync(openBrowser, cancellationToken);

    public void ClearAuthorization() => _authorization.Clear();

    public void ProcessNotifications()
    {
        while (_authorizationNotifications.TryDequeue(out var authorization))
            AuthorizationStatusChanged?.Invoke(authorization);
        while (_catalogNotifications.TryDequeue(out var catalog))
            CatalogChanged?.Invoke(catalog);
    }

    public async Task FlushSessionStateAsync(TimeSpan timeout)
    {
        if (_authorization.Status.State != SteamCloudAuthorizationState.Authorized)
            return;

        var drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnPendingChanged()
        {
            if (!_outbox.ListPending().Any(record => record.Kind == CloudRevisionKind.Session))
                drained.TrySetResult();
        }

        _outbox.PendingChanged += OnPendingChanged;
        try
        {
            SignalUpload();
            OnPendingChanged();
            using var timeoutSource = new CancellationTokenSource(timeout);
            await using (timeoutSource.Token.Register(() => drained.TrySetResult()))
                await drained.Task;
        }
        finally
        {
            _outbox.PendingChanged -= OnPendingChanged;
        }
    }

    public async Task<SteamCloudCatalogSnapshot> RefreshCatalogAsync(
        CancellationToken cancellationToken = default)
    {
        await _cloudOperationLock.WaitAsync(cancellationToken);
        try
        {
            var catalog = await _catalogService.RefreshAsync(cancellationToken);
            _nextCatalogRefreshUtc = DateTimeOffset.UtcNow + CatalogRefreshInterval;
            PublishCatalog(catalog);
            return catalog;
        }
        finally
        {
            _cloudOperationLock.Release();
        }
    }

    public async Task<ManagedCloudCopyInfo> DownloadRevisionAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default)
    {
        await _cloudOperationLock.WaitAsync(cancellationToken);
        try
        {
            var previousRefreshUtc = _catalogService.Catalog.RefreshedAtUtc;
            var copy = await _catalogService.DownloadRevisionAsync(revisionId, cancellationToken);
            if (_catalogService.Catalog.RefreshedAtUtc != previousRefreshUtc)
            {
                _nextCatalogRefreshUtc = DateTimeOffset.UtcNow + CatalogRefreshInterval;
                PublishCatalog(_catalogService.Catalog);
            }
            return copy;
        }
        finally
        {
            _cloudOperationLock.Release();
        }
    }

    public async Task<SteamCloudCatalogSnapshot> ResolveConflictAsync(
        Guid documentId,
        Guid chosenRevisionId,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        await _cloudOperationLock.WaitAsync(cancellationToken);
        try
        {
            var catalog = await _catalogService.ResolveConflictAsync(
                documentId,
                chosenRevisionId,
                deviceId,
                cancellationToken);
            _nextCatalogRefreshUtc = DateTimeOffset.UtcNow + CatalogRefreshInterval;
            PublishCatalog(catalog);
            return catalog;
        }
        finally
        {
            _cloudOperationLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _outbox.PendingChanged -= SignalUpload;
        _authorization.StatusChanged -= OnAuthorizationStatusChanged;
        _shutdown.Cancel();
        SignalUpload();
        try
        {
            await _uploadLoop;
        }
        catch (OperationCanceledException)
        {
        }
        _shutdown.Dispose();
        _uploadSignal.Dispose();
        _cloudOperationLock.Dispose();
        _httpClient.Dispose();
    }

    private async Task UploadLoopAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            await WaitForSignalOrRetryAsync(_shutdown.Token);
            if (_authorization.Status.State != SteamCloudAuthorizationState.Authorized)
                continue;

            try
            {
                await _cloudOperationLock.WaitAsync(_shutdown.Token);
                try
                {
                var pending = OrderPending(_outbox.ListPending());
                if (pending.Count == 0)
                {
                    if (DateTimeOffset.UtcNow < _nextCatalogRefreshUtc)
                        continue;

                    var retentionLimitExceeded =
                        await _catalogService.ReconcileRecoveryRetentionAsync(_shutdown.Token);
                    _nextCatalogRefreshUtc = DateTimeOffset.UtcNow + CatalogRefreshInterval;
                    PublishCatalog(_catalogService.Catalog);
                    AppDocumentDurability.SetCloudStatus(
                        SteamCloudSyncState.Ready,
                        retentionLimitExceeded ? CloudRetentionLimitMessage : "",
                        retentionLimitExceeded: retentionLimitExceeded);
                    continue;
                }

                AppDocumentDurability.SetCloudStatus(SteamCloudSyncState.Uploading);
                var remoteFiles = (await _webApi.EnumerateFilesAsync(_shutdown.Token))
                    .ToDictionary(file => file.FileName, StringComparer.Ordinal);
                foreach (var record in pending)
                {
                    if (!await UploadRecordAsync(record, remoteFiles, _shutdown.Token))
                        continue;
                    var uploadedAt = DateTimeOffset.UtcNow;
                    _outbox.MarkUploaded(record, uploadedAt);
                    if (record.Kind != CloudRevisionKind.Session)
                    {
                        AppDocumentDurability.SetCloudStatus(
                            SteamCloudSyncState.Uploading,
                            protectionPointUtc: uploadedAt);
                    }
                }
                var cloudRetentionLimitExceeded =
                    await _catalogService.ReconcileRecoveryRetentionAsync(_shutdown.Token);
                _nextCatalogRefreshUtc = DateTimeOffset.UtcNow + CatalogRefreshInterval;
                PublishCatalog(_catalogService.Catalog);
                AppDocumentDurability.SetCloudStatus(
                    SteamCloudSyncState.Ready,
                    cloudRetentionLimitExceeded ? CloudRetentionLimitMessage : "",
                    retentionLimitExceeded: cloudRetentionLimitExceeded);
                }
                finally
                {
                    _cloudOperationLock.Release();
                }
            }
            catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
            {
                throw;
            }
            catch (SteamWebApiAuthorizationException)
            {
            }
            catch (OperationCanceledException exception)
            {
                AppDocumentDurability.SetCloudStatus(SteamCloudSyncState.Offline, exception.Message);
            }
            catch (HttpRequestException exception)
            {
                AppDocumentDurability.SetCloudStatus(SteamCloudSyncState.Offline, exception.Message);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                AppDocumentDurability.SetCloudStatus(SteamCloudSyncState.Failed, exception.Message);
            }
        }
    }

    private async Task<bool> UploadRecordAsync(
        CloudOutboxRecord record,
        Dictionary<string, SteamCloudFile> remoteFiles,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SteamCloudUploadFile> uploadFiles;
        if (record.Kind == CloudRevisionKind.Session)
        {
            var bytes = await File.ReadAllBytesAsync(record.SourcePath, cancellationToken);
            var contentSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (contentSha256 != record.ContentSha256)
                return false;
            uploadFiles = [new SteamCloudUploadFile(
                SteamCloudPaths.Session(record.DocumentId, record.SessionId!.Value),
                bytes)];
        }
        else
        {
            uploadFiles = CloudRevisionPackageBuilder.Build(record).UploadFiles;
        }

        var requiredUploads = uploadFiles
            .Where(upload => !remoteFiles.TryGetValue(upload.FileName, out var remote) ||
                             remote.Sha1 != upload.Sha1)
            .ToArray();
        await _webApi.ModifyBatchAsync(requiredUploads, [], cancellationToken);
        foreach (var upload in uploadFiles)
        {
            remoteFiles[upload.FileName] = new SteamCloudFile(
                _options.AppId,
                0,
                upload.FileName,
                checked((ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                upload.ByteLength,
                null,
                0,
                0,
                upload.PlatformsToSync,
                upload.Sha1);
        }
        return true;
    }

    private async Task WaitForSignalOrRetryAsync(CancellationToken cancellationToken)
    {
        await _uploadSignal.WaitAsync(RetryInterval, cancellationToken);
    }

    private void OnAuthorizationStatusChanged(SteamCloudAuthorizationStatus status)
    {
        ApplyAuthorizationStatus(status);
        _authorizationNotifications.Enqueue(status);
        SignalUpload();
    }

    private static void ApplyAuthorizationStatus(SteamCloudAuthorizationStatus status)
    {
        var cloudState = status.State switch
        {
            SteamCloudAuthorizationState.Unconfigured => SteamCloudSyncState.Unconfigured,
            SteamCloudAuthorizationState.AuthorizationRequired => SteamCloudSyncState.AuthorizationRequired,
            SteamCloudAuthorizationState.Authorizing => SteamCloudSyncState.AuthorizationRequired,
            SteamCloudAuthorizationState.Authorized => SteamCloudSyncState.Ready,
            SteamCloudAuthorizationState.Failed => SteamCloudSyncState.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(status.State), status.State, null),
        };
        AppDocumentDurability.SetCloudStatus(cloudState, status.LastExternalError);
    }

    private void PublishCatalog(SteamCloudCatalogSnapshot catalog)
        => _catalogNotifications.Enqueue(catalog);

    private void SignalUpload()
    {
        if (_uploadSignal.CurrentCount == 0)
            _uploadSignal.Release();
    }

    internal static IReadOnlyList<CloudOutboxRecord> OrderPending(
        IReadOnlyList<CloudOutboxRecord> pending)
    {
        return pending
            .OrderBy(record => record.Kind switch
            {
                CloudRevisionKind.Session => 0,
                CloudRevisionKind.Saved => 1,
                CloudRevisionKind.Recovery => 2,
                _ => throw new ArgumentOutOfRangeException(nameof(record.Kind), record.Kind, null),
            })
            .ThenBy(record => record.Kind == CloudRevisionKind.Recovery
                ? -record.CapturedAtUtc.ToUnixTimeMilliseconds()
                : record.CapturedAtUtc.ToUnixTimeMilliseconds())
            .ToArray();
    }
}
