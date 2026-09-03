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

// Steam's client-managed session sync runs around application sessions and cannot confirm a
// recovery snapshot while Ciallo is still running. ISteamRemoteStorage write completion only
// confirms a local write. Ciallo uses ICloudService so a recovery snapshot can become a confirmed
// cloud protection point before exit.
internal sealed class SteamCloudRecoveryCoordinator : IAsyncDisposable
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan CatalogRefreshInterval = TimeSpan.FromMinutes(5);
    private const string RecoveryCloudRetentionLimitMessage =
        "The newest recovery snapshot exceeds the configured Steam Cloud recovery storage limit.";

    private readonly SteamCloudOptions _options;
    private readonly SteamCloudRecoveryOutbox _outbox;
    private readonly HttpClient _httpClient;
    private readonly SteamCloudAuthorization _authorization;
    private readonly AuthorizedSteamCloudClient _webApi;
    private readonly SteamCloudRecoveryCatalog _catalogService;
    private readonly SemaphoreSlim _uploadSignal = new(0, 1);
    private readonly SemaphoreSlim _cloudOperationLock = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _uploadLoop;
    private readonly ConcurrentQueue<SteamCloudAuthorizationStatus> _authorizationNotifications = new();
    private readonly ConcurrentQueue<SteamCloudRecoveryCatalogSnapshot> _catalogNotifications = new();
    private DateTimeOffset _nextCatalogRefreshUtc = DateTimeOffset.MinValue;
    private int _disposed;

    public SteamCloudAuthorizationStatus AuthorizationStatus => _authorization.Status;
    public SteamCloudRecoveryCatalogSnapshot Catalog => _catalogService.Catalog;
    public event Action<SteamCloudAuthorizationStatus> AuthorizationStatusChanged;
    public event Action<SteamCloudRecoveryCatalogSnapshot> CatalogChanged;

    public SteamCloudRecoveryCoordinator(
        SteamCloudOptions options,
        ulong currentSteamId,
        DurabilityFileStore files,
        SteamCloudRecoveryOutbox outbox)
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
        _catalogService = new SteamCloudRecoveryCatalog(options, files, outbox, _webApi, protocol);

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
            if (!_outbox.ListPending().Any(record => record.Kind == SteamCloudRecoveryKind.Session))
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

    public async Task<SteamCloudRecoveryCatalogSnapshot> RefreshCatalogAsync(
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

    public async Task<LocalRecoverySnapshotInfo> DownloadSnapshotAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default)
    {
        await _cloudOperationLock.WaitAsync(cancellationToken);
        try
        {
            var previousRefreshUtc = _catalogService.Catalog.RefreshedAtUtc;
            var snapshot = await _catalogService.DownloadSnapshotAsync(revisionId, cancellationToken);
            if (_catalogService.Catalog.RefreshedAtUtc != previousRefreshUtc)
            {
                _nextCatalogRefreshUtc = DateTimeOffset.UtcNow + CatalogRefreshInterval;
                PublishCatalog(_catalogService.Catalog);
            }
            return snapshot;
        }
        finally
        {
            _cloudOperationLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Idempotent: a second close notification (or a manual dispose after one) must not complete
        // the upload loop twice or double-dispose the primitives below.
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _outbox.PendingChanged -= SignalUpload;
        _authorization.StatusChanged -= OnAuthorizationStatusChanged;
        _shutdown.Cancel();
        SignalUpload();
        try
        {
            // Teardown touches no UI, so it must not resume on the captured main-thread context.
            // Without this a sync-over-async caller on the main thread would deadlock waiting for a
            // continuation that only the (blocked) main thread could run.
            await _uploadLoop.ConfigureAwait(false);
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
                        AppDocumentDurability.SetRecoveryCloudStatus(
                            SteamCloudRecoveryState.Ready,
                            retentionLimitExceeded ? RecoveryCloudRetentionLimitMessage : "",
                            retentionLimitExceeded: retentionLimitExceeded);
                        continue;
                    }

                    AppDocumentDurability.SetRecoveryCloudStatus(SteamCloudRecoveryState.Uploading);
                    var remoteFiles = (await _webApi.EnumerateFilesAsync(_shutdown.Token))
                        .ToDictionary(file => file.FileName, StringComparer.Ordinal);
                    foreach (var record in pending)
                    {
                        if (!await UploadRecordAsync(record, remoteFiles, _shutdown.Token))
                            continue;
                        var uploadedAt = DateTimeOffset.UtcNow;
                        _outbox.MarkUploaded(record, uploadedAt);
                        if (record.Kind != SteamCloudRecoveryKind.Session)
                        {
                            AppDocumentDurability.SetRecoveryCloudStatus(
                                SteamCloudRecoveryState.Uploading,
                                protectionPointUtc: uploadedAt);
                        }
                    }
                    var cloudRetentionLimitExceeded =
                        await _catalogService.ReconcileRecoveryRetentionAsync(_shutdown.Token);
                    _nextCatalogRefreshUtc = DateTimeOffset.UtcNow + CatalogRefreshInterval;
                    PublishCatalog(_catalogService.Catalog);
                    AppDocumentDurability.SetRecoveryCloudStatus(
                        SteamCloudRecoveryState.Ready,
                        cloudRetentionLimitExceeded ? RecoveryCloudRetentionLimitMessage : "",
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
                AppDocumentDurability.SetRecoveryCloudStatus(SteamCloudRecoveryState.Offline, exception.Message);
            }
            catch (HttpRequestException exception)
            {
                AppDocumentDurability.SetRecoveryCloudStatus(SteamCloudRecoveryState.Offline, exception.Message);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                AppDocumentDurability.SetRecoveryCloudStatus(SteamCloudRecoveryState.Failed, exception.Message);
            }
        }
    }

    private async Task<bool> UploadRecordAsync(
        SteamCloudRecoveryOutboxRecord record,
        Dictionary<string, SteamCloudFile> remoteFiles,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SteamCloudUploadFile> uploadFiles;
        if (record.Kind == SteamCloudRecoveryKind.Session)
        {
            var bytes = await File.ReadAllBytesAsync(record.SourcePath, cancellationToken);
            var contentSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (contentSha256 != record.ContentSha256)
                return false;
            uploadFiles = [new SteamCloudUploadFile(
                SteamCloudRecoveryPaths.Session(record.DocumentId, record.SessionId!.Value),
                bytes)];
        }
        else
        {
            uploadFiles = SteamCloudRecoveryPackageBuilder.Build(record).UploadFiles;
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
            SteamCloudAuthorizationState.Unconfigured => SteamCloudRecoveryState.Unconfigured,
            SteamCloudAuthorizationState.AuthorizationRequired => SteamCloudRecoveryState.AuthorizationRequired,
            SteamCloudAuthorizationState.Authorizing => SteamCloudRecoveryState.AuthorizationRequired,
            SteamCloudAuthorizationState.Authorized => SteamCloudRecoveryState.Ready,
            SteamCloudAuthorizationState.Failed => SteamCloudRecoveryState.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(status.State), status.State, null),
        };
        AppDocumentDurability.SetRecoveryCloudStatus(cloudState, status.LastExternalError);
    }

    private void PublishCatalog(SteamCloudRecoveryCatalogSnapshot catalog)
        => _catalogNotifications.Enqueue(catalog);

    private void SignalUpload()
    {
        if (_uploadSignal.CurrentCount == 0)
            _uploadSignal.Release();
    }

    internal static IReadOnlyList<SteamCloudRecoveryOutboxRecord> OrderPending(
        IReadOnlyList<SteamCloudRecoveryOutboxRecord> pending)
    {
        return pending
            .OrderBy(record => record.Kind switch
            {
                SteamCloudRecoveryKind.Session => 0,
                SteamCloudRecoveryKind.Recovery => 1,
                _ => throw new ArgumentOutOfRangeException(nameof(record.Kind), record.Kind, null),
            })
            .ThenBy(record => record.Kind == SteamCloudRecoveryKind.Recovery
                ? -record.CapturedAtUtc.ToUnixTimeMilliseconds()
                : record.CapturedAtUtc.ToUnixTimeMilliseconds())
            .ToArray();
    }
}
