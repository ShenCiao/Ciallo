using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Steamworks.WebApi;

namespace Ciallo.Data;

internal sealed class SteamCloudRecoveryCatalog
{
    private const int DeleteBatchSize = 100;

    private readonly SteamCloudOptions _options;
    private readonly DurabilityFileStore _files;
    private readonly SteamCloudRecoveryOutbox _outbox;
    private readonly AuthorizedSteamCloudClient _webApi;
    private readonly SteamCloudWebApiClient _downloads;
    private Dictionary<string, SteamCloudFile> _remoteFiles = new(StringComparer.Ordinal);
    private Dictionary<Guid, SteamCloudRecoveryManifest> _revisions = [];
    private Dictionary<string, (string Sha1, SteamCloudRecoveryManifest Manifest)> _revisionCache =
        new(StringComparer.Ordinal);
    private Dictionary<string, (string Sha1, EditingSessionInfo Manifest)> _sessionCache =
        new(StringComparer.Ordinal);

    public SteamCloudRecoveryCatalogSnapshot Catalog { get; private set; } = new();

    public SteamCloudRecoveryCatalog(
        SteamCloudOptions options,
        DurabilityFileStore files,
        SteamCloudRecoveryOutbox outbox,
        AuthorizedSteamCloudClient webApi,
        SteamCloudWebApiClient downloads)
    {
        _options = options;
        _files = files;
        _outbox = outbox;
        _webApi = webApi;
        _downloads = downloads;
    }

    public async Task<SteamCloudRecoveryCatalogSnapshot> RefreshAsync(CancellationToken cancellationToken)
    {
        _remoteFiles = (await _webApi.EnumerateFilesAsync(cancellationToken))
            .ToDictionary(file => file.FileName, StringComparer.Ordinal);

        var revisionFiles = _remoteFiles.Values
            .Where(file => file.FileName.StartsWith(SteamCloudRecoveryPaths.RecoveryRevisionPrefix, StringComparison.Ordinal) &&
                           file.FileName.EndsWith(".json", StringComparison.Ordinal))
            .ToArray();
        var revisions = new Dictionary<Guid, SteamCloudRecoveryManifest>();
        var revisionCache = new Dictionary<string, (string, SteamCloudRecoveryManifest)>(StringComparer.Ordinal);
        foreach (var file in revisionFiles)
        {
            var revision = await ReadManifestAsync(
                file,
                _revisionCache,
                ValidateRevision,
                cancellationToken);
            if (!revisions.TryAdd(revision.RevisionId, revision))
                throw new IOException($"Steam Cloud contains duplicate revision {revision.RevisionId}.");
            if (file.Sha1 is { } currentSha1)
                revisionCache[file.FileName] = (currentSha1, revision);
        }
        _revisions = revisions;
        _revisionCache = revisionCache;

        var sessions = new Dictionary<Guid, EditingSessionInfo>();
        var sessionCache = new Dictionary<string, (string, EditingSessionInfo)>(StringComparer.Ordinal);
        foreach (var file in _remoteFiles.Values.Where(file =>
                     file.FileName.StartsWith(SteamCloudRecoveryPaths.SessionPrefix, StringComparison.Ordinal) &&
                     file.FileName.EndsWith(".json", StringComparison.Ordinal)))
        {
            var session = await ReadManifestAsync(
                file,
                _sessionCache,
                ValidateSessionFile,
                cancellationToken);
            if (!sessions.TryAdd(session.SessionId, session))
                throw new IOException($"Steam Cloud contains duplicate editing session {session.SessionId}.");
            if (file.Sha1 is { } currentSha1)
                sessionCache[file.FileName] = (currentSha1, session);
        }
        _sessionCache = sessionCache;

        var now = DateTimeOffset.UtcNow;
        var interruptedSessionIds = sessions.Values
            .Where(session => session.IsInterrupted(now))
            .Select(session => session.SessionId)
            .ToHashSet();
        var recoveryCandidates = revisions.Values
            .Where(revision => interruptedSessionIds.Contains(revision.SessionId!.Value))
            .OrderByDescending(revision => revision.CapturedAtUtc)
            .ToArray();

        Catalog = new SteamCloudRecoveryCatalogSnapshot
        {
            RefreshedAtUtc = now,
            RecoveryCandidates = recoveryCandidates,
        };
        return Catalog;
    }

    public async Task<LocalRecoverySnapshotInfo> DownloadSnapshotAsync(
        Guid revisionId,
        CancellationToken cancellationToken)
    {
        if (!_revisions.TryGetValue(revisionId, out var revision))
        {
            await RefreshAsync(cancellationToken);
            revision = _revisions[revisionId];
        }

        var temporaryPath = Path.Combine(
            _files.RecoveryRootPath,
            "." + revision.RevisionId.ToString("N") + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using var fullHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long byteLength = 0;
            using (var output = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       81920,
                       FileOptions.WriteThrough))
            {
                foreach (var chunk in revision.Chunks.OrderBy(chunk => chunk.Index))
                {
                    var remoteName = SteamCloudRecoveryPaths.Chunk(chunk.Sha256);
                    if (!_remoteFiles.TryGetValue(remoteName, out var remoteFile))
                        throw new IOException($"Steam Cloud revision {revision.RevisionId} is missing chunk {remoteName}.");
                    var bytes = await _downloads.DownloadFileAsync(remoteFile, cancellationToken);
                    var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                    if (sha256 != chunk.Sha256 || bytes.Length != chunk.ByteLength)
                        throw new IOException($"Steam Cloud chunk {remoteName} failed validation.");
                    await output.WriteAsync(bytes, cancellationToken);
                    fullHash.AppendData(bytes);
                    byteLength += bytes.Length;
                }
                output.Flush(true);
            }

            var contentSha256 = Convert.ToHexString(fullHash.GetHashAndReset()).ToLowerInvariant();
            if (contentSha256 != revision.ContentSha256 || byteLength != revision.ByteLength)
                throw new IOException($"Steam Cloud revision {revision.RevisionId} failed validation.");

            var installed = _files.InstallDownloadedSnapshot(
                revision,
                temporaryPath,
                new RecoveryRetentionPolicy(
                    _options.RecoveryPerDocumentLimit,
                    _options.RecoveryAccountFileLimit,
                    _options.RecoveryAccountByteLimit));
            _outbox.RecordReceipt(
                revision.RevisionId,
                revision.DocumentId,
                SteamCloudRecoveryKind.Recovery,
                DateTimeOffset.UtcNow);
            return installed;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    public async Task<bool> ReconcileRecoveryRetentionAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken);
        var plan = RecoveryRetentionPlanner.Plan(
            _revisions.Values,
            revision => revision.RevisionId,
            revision => revision.DocumentId,
            revision => revision.CapturedAtUtc,
            revision => revision.ByteLength,
            new RecoveryRetentionLimits(
                _options.RecoveryPerDocumentLimit,
                _options.RecoveryAccountFileLimit,
                _options.RecoveryAccountByteLimit));
        var retainedRecovery = plan.Retained;
        var limitExceeded = plan.LimitExceeded;

        var referencedChunks = retainedRecovery
            .SelectMany(revision => revision.Chunks)
            .Select(chunk => SteamCloudRecoveryPaths.Chunk(chunk.Sha256))
            .ToHashSet(StringComparer.Ordinal);

        var deletes = plan.Evicted
            .Select(revision => SteamCloudRecoveryPaths.RevisionManifest(revision.DocumentId, revision.RevisionId))
            .Concat(_remoteFiles.Keys.Where(fileName =>
                fileName.StartsWith(SteamCloudRecoveryPaths.SavedRevisionPrefix, StringComparison.Ordinal)))
            .Concat(_remoteFiles.Keys.Where(fileName =>
                fileName.StartsWith(SteamCloudRecoveryPaths.ChunkPrefix, StringComparison.Ordinal) &&
                !referencedChunks.Contains(fileName) &&
                DateTimeOffset.FromUnixTimeSeconds(checked((long)_remoteFiles[fileName].Timestamp)) <
                DateTimeOffset.UtcNow - TimeSpan.FromDays(1)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        foreach (var batch in deletes.Chunk(DeleteBatchSize))
            await _webApi.ModifyBatchAsync([], batch, cancellationToken);
        if (deletes.Length > 0)
            await RefreshAsync(cancellationToken);
        return limitExceeded;
    }

    private static void ValidateRevision(SteamCloudRecoveryManifest revision, string remoteFileName)
    {
        if (revision.SchemaVersion != 1 ||
            revision.RevisionId == Guid.Empty ||
            revision.DocumentId == Guid.Empty ||
            revision.DeviceId == Guid.Empty ||
            revision.DocumentName == null ||
            revision.OriginalFilePath == null ||
            revision.ContentSha256 == null ||
            revision.Chunks == null)
            throw new IOException($"Steam Cloud revision {revision.RevisionId} contains null metadata.");
        if (revision.Chunks.Any(chunk => chunk == null))
            throw new IOException($"Steam Cloud revision {revision.RevisionId} contains a null chunk.");
        if (revision.SessionId == Guid.Empty)
            throw new IOException($"Steam Cloud revision {revision.RevisionId} contains an empty identifier.");
        if (revision.Kind != SteamCloudRecoveryKind.Recovery)
            throw new IOException($"Steam Cloud revision {revision.RevisionId} has invalid kind {revision.Kind}.");
        if (!revision.SessionId.HasValue)
            throw new IOException($"Steam Cloud recovery revision {revision.RevisionId} has no editing session.");
        if (remoteFileName != SteamCloudRecoveryPaths.RevisionManifest(revision.DocumentId, revision.RevisionId))
            throw new IOException($"Steam Cloud revision path {remoteFileName} does not match its content.");

        if (revision.ByteLength < 0)
            throw new IOException($"Steam Cloud revision {revision.RevisionId} has a negative byte length.");

        long byteLength = 0;
        var expectedIndex = 0;
        foreach (var chunk in revision.Chunks.OrderBy(chunk => chunk.Index))
        {
            if (chunk.Index != expectedIndex ||
                chunk.ByteLength <= 0 ||
                chunk.ByteLength > SteamCloudOptions.ChunkByteLength ||
                chunk.Sha256 == null ||
                chunk.Sha256.Length != 64 ||
                !chunk.Sha256.All(Uri.IsHexDigit) ||
                chunk.Sha256 != chunk.Sha256.ToLowerInvariant())
                throw new IOException($"Steam Cloud revision {revision.RevisionId} has invalid chunk metadata.");
            try
            {
                byteLength = checked(byteLength + chunk.ByteLength);
            }
            catch (OverflowException exception)
            {
                throw new IOException($"Steam Cloud revision {revision.RevisionId} byte length overflowed.", exception);
            }
            expectedIndex++;
        }
        if (byteLength != revision.ByteLength ||
            revision.ContentSha256.Length != 64 ||
            !revision.ContentSha256.All(Uri.IsHexDigit) ||
            revision.ContentSha256 != revision.ContentSha256.ToLowerInvariant())
            throw new IOException($"Steam Cloud revision {revision.RevisionId} has invalid content metadata.");
    }

    private static void ValidateSession(EditingSessionInfo session)
    {
        if (session.SchemaVersion != 1 ||
            session.SessionId == Guid.Empty ||
            session.DocumentId == Guid.Empty ||
            session.DeviceId == Guid.Empty ||
            session.DocumentName == null ||
            session.LastHeartbeatUtc < session.OpenedAtUtc ||
            session.ClosedAtUtc < session.LastHeartbeatUtc)
            throw new IOException($"Steam Cloud editing session {session.SessionId} has invalid metadata.");
    }

    private static void ValidateSessionFile(EditingSessionInfo session, string remoteFileName)
    {
        ValidateSession(session);
        if (remoteFileName != SteamCloudRecoveryPaths.Session(session.DocumentId, session.SessionId))
            throw new IOException($"Steam Cloud session path {remoteFileName} does not match its content.");
    }

    private async Task<T> ReadManifestAsync<T>(
        SteamCloudFile file,
        IReadOnlyDictionary<string, (string Sha1, T Manifest)> cache,
        Action<T, string> validate,
        CancellationToken cancellationToken)
    {
        if (file.Sha1 is { } sha1 &&
            cache.TryGetValue(file.FileName, out var cached) &&
            cached.Sha1 == sha1)
            return cached.Manifest;

        var bytes = await _downloads.DownloadFileAsync(file, cancellationToken);
        var manifest = DeserializeCloudFile<T>(bytes, file.FileName);
        validate(manifest, file.FileName);
        return manifest;
    }

    private static T DeserializeCloudFile<T>(byte[] bytes, string remoteFileName)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(bytes, DurabilityFiles.JsonOptions)
                   ?? throw new JsonException("The JSON value was null.");
        }
        catch (JsonException exception)
        {
            throw new IOException($"Steam Cloud file {remoteFileName} contains invalid JSON.", exception);
        }
    }
}
