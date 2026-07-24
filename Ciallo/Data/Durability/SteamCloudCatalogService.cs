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

internal sealed class SteamCloudCatalogService
{
    private const int DeleteBatchSize = 100;

    private readonly SteamCloudOptions _options;
    private readonly DurabilityFileStore _files;
    private readonly CloudOutboxStore _outbox;
    private readonly AuthorizedSteamCloudClient _webApi;
    private readonly SteamCloudWebApiClient _downloads;
    private Dictionary<string, SteamCloudFile> _remoteFiles = new(StringComparer.Ordinal);
    private Dictionary<Guid, CloudRevisionManifest> _revisions = [];
    private Dictionary<string, (string Sha1, CloudRevisionManifest Manifest)> _revisionCache =
        new(StringComparer.Ordinal);
    private Dictionary<string, (string Sha1, EditingSessionInfo Manifest)> _sessionCache =
        new(StringComparer.Ordinal);

    public SteamCloudCatalogSnapshot Catalog { get; private set; } = new();

    public SteamCloudCatalogService(
        SteamCloudOptions options,
        DurabilityFileStore files,
        CloudOutboxStore outbox,
        AuthorizedSteamCloudClient webApi,
        SteamCloudWebApiClient downloads)
    {
        _options = options;
        _files = files;
        _outbox = outbox;
        _webApi = webApi;
        _downloads = downloads;
    }

    public async Task<SteamCloudCatalogSnapshot> RefreshAsync(CancellationToken cancellationToken)
    {
        _remoteFiles = (await _webApi.EnumerateFilesAsync(cancellationToken))
            .ToDictionary(file => file.FileName, StringComparer.Ordinal);

        var revisionFiles = _remoteFiles.Values
            .Where(file => file.FileName.StartsWith(SteamCloudPaths.Prefix + "/revisions/", StringComparison.Ordinal) &&
                           file.FileName.EndsWith(".json", StringComparison.Ordinal))
            .ToArray();
        var revisions = new Dictionary<Guid, CloudRevisionManifest>();
        var revisionCache = new Dictionary<string, (string, CloudRevisionManifest)>(StringComparer.Ordinal);
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
                     file.FileName.StartsWith(SteamCloudPaths.Prefix + "/sessions/", StringComparison.Ordinal) &&
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

        var saved = revisions.Values.Where(revision => revision.Kind == CloudRevisionKind.Saved).ToArray();
        var documents = saved
            .GroupBy(revision => revision.DocumentId)
            .Select(group =>
            {
                var heads = FindHeads(group).OrderByDescending(head => head.CapturedAtUtc).ToArray();
                var preservedIds = group.SelectMany(revision => revision.SupersededRevisionIds)
                    .ToHashSet();
                return new CloudDocumentInfo
                {
                    DocumentId = group.Key,
                    DocumentName = heads.FirstOrDefault()?.DocumentName ?? group.First().DocumentName,
                    SavedHeads = heads,
                    PreservedConflictCandidates = group
                        .Where(revision => preservedIds.Contains(revision.RevisionId))
                        .OrderByDescending(revision => revision.CapturedAtUtc)
                        .ToArray(),
                };
            })
            .OrderBy(document => document.DocumentName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var now = DateTimeOffset.UtcNow;
        var interruptedSessionIds = sessions.Values
            .Where(session => session.IsInterrupted(now))
            .Select(session => session.SessionId)
            .ToHashSet();
        var recoveryCandidates = revisions.Values
            .Where(revision => revision.Kind == CloudRevisionKind.Recovery &&
                               interruptedSessionIds.Contains(revision.SessionId!.Value))
            .OrderByDescending(revision => revision.CapturedAtUtc)
            .ToArray();

        Catalog = new SteamCloudCatalogSnapshot
        {
            RefreshedAtUtc = now,
            Documents = documents,
            RecoveryCandidates = recoveryCandidates,
        };
        return Catalog;
    }

    public async Task<ManagedCloudCopyInfo> DownloadRevisionAsync(
        Guid revisionId,
        CancellationToken cancellationToken)
    {
        if (!_revisions.TryGetValue(revisionId, out var revision))
        {
            await RefreshAsync(cancellationToken);
            revision = _revisions[revisionId];
        }

        var documentDirectory = Path.Combine(_files.ManagedCloudRootPath, revision.DocumentId.ToString("N"));
        Directory.CreateDirectory(documentDirectory);
        var targetFileName = revision.Kind == CloudRevisionKind.Saved
            ? "document.ciallo"
            : "recovery-" + revision.RevisionId.ToString("N") + ".ciallo";
        var targetPath = Path.Combine(documentDirectory, targetFileName);
        var temporaryPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

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
                    var remoteName = SteamCloudPaths.Chunk(chunk.Sha256);
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
            File.Move(temporaryPath, targetPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        return new ManagedCloudCopyInfo { LocalFilePath = targetPath, Revision = revision };
    }

    public async Task<SteamCloudCatalogSnapshot> ResolveConflictAsync(
        Guid documentId,
        Guid chosenRevisionId,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken);
        var document = Catalog.Documents.Single(item => item.DocumentId == documentId);
        if (!document.HasConflict)
            throw new InvalidOperationException($"Document {documentId} has no cloud conflict.");
        var chosen = document.SavedHeads.Single(head => head.RevisionId == chosenRevisionId);

        var resolution = chosen with
        {
            RevisionId = Guid.NewGuid(),
            ParentRevisionId = chosen.RevisionId,
            SupersededRevisionIds = document.PreservedConflictCandidates
                .Select(candidate => candidate.RevisionId)
                .Concat(document.SavedHeads
                    .Where(head => head.RevisionId != chosen.RevisionId)
                    .Select(head => head.RevisionId))
                .Distinct()
                .ToArray(),
            DeviceId = deviceId,
            CapturedAtUtc = DateTimeOffset.UtcNow,
        };
        var remoteName = SteamCloudPaths.RevisionManifest(
            CloudRevisionKind.Saved,
            documentId,
            resolution.RevisionId);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(resolution, DurabilityFiles.JsonOptions);
        await _webApi.ModifyBatchAsync([new SteamCloudUploadFile(remoteName, bytes)], [], cancellationToken);
        _outbox.SetCloudBaseRevision(
            documentId,
            resolution.RevisionId,
            resolution.SupersededRevisionIds);
        return await RefreshAsync(cancellationToken);
    }

    public async Task<bool> ReconcileRecoveryRetentionAsync(CancellationToken cancellationToken)
    {
        await RefreshAsync(cancellationToken);
        var plan = RecoveryRetentionPlanner.Plan(
            _revisions.Values.Where(revision => revision.Kind == CloudRevisionKind.Recovery),
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

        var saved = _revisions.Values.Where(revision => revision.Kind == CloudRevisionKind.Saved).ToArray();
        var savedContentIds = saved
            .GroupBy(revision => revision.DocumentId)
            .SelectMany(FindHeads)
            .Select(revision => revision.RevisionId)
            .Concat(saved.SelectMany(revision => revision.SupersededRevisionIds))
            .ToHashSet();
        var contentManifests = saved.Where(revision => savedContentIds.Contains(revision.RevisionId))
            .Concat(retainedRecovery)
            .ToArray();
        var referencedChunks = contentManifests
            .SelectMany(revision => revision.Chunks)
            .Select(chunk => SteamCloudPaths.Chunk(chunk.Sha256))
            .ToHashSet(StringComparer.Ordinal);

        var deletes = plan.Evicted
            .Select(revision => SteamCloudPaths.RevisionManifest(
                CloudRevisionKind.Recovery,
                revision.DocumentId,
                revision.RevisionId))
            .Concat(saved
                .Where(revision => !savedContentIds.Contains(revision.RevisionId))
                .Select(revision => SteamCloudPaths.RevisionManifest(
                    CloudRevisionKind.Saved,
                    revision.DocumentId,
                    revision.RevisionId)))
            .Concat(_remoteFiles.Keys.Where(fileName =>
                fileName.StartsWith(SteamCloudPaths.Prefix + "/chunks/", StringComparison.Ordinal) &&
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

    internal static IEnumerable<CloudRevisionManifest> FindHeads(IEnumerable<CloudRevisionManifest> revisions)
    {
        var all = revisions.ToArray();
        var superseded = all
            .SelectMany(revision => revision.SupersededRevisionIds)
            .ToHashSet();
        foreach (var revision in all)
        {
            if (revision.ParentRevisionId.HasValue)
                superseded.Add(revision.ParentRevisionId.Value);
        }
        return all.Where(revision => !superseded.Contains(revision.RevisionId));
    }

    private static void ValidateRevision(CloudRevisionManifest revision, string remoteFileName)
    {
        if (revision.SchemaVersion != 1 ||
            revision.RevisionId == Guid.Empty ||
            revision.DocumentId == Guid.Empty ||
            revision.DeviceId == Guid.Empty ||
            revision.DocumentName == null ||
            revision.ContentSha256 == null ||
            revision.SupersededRevisionIds == null ||
            revision.Chunks == null)
            throw new IOException($"Steam Cloud revision {revision.RevisionId} contains null metadata.");
        if (revision.Chunks.Any(chunk => chunk == null))
            throw new IOException($"Steam Cloud revision {revision.RevisionId} contains a null chunk.");
        if (revision.SessionId == Guid.Empty ||
            revision.ParentRevisionId == Guid.Empty ||
            revision.SupersededRevisionIds.Any(revisionId => revisionId == Guid.Empty))
            throw new IOException($"Steam Cloud revision {revision.RevisionId} contains an empty identifier.");
        if (revision.ParentRevisionId == revision.RevisionId ||
            revision.SupersededRevisionIds.Contains(revision.RevisionId) ||
            revision.SupersededRevisionIds.Distinct().Count() !=
            revision.SupersededRevisionIds.Length)
            throw new IOException($"Steam Cloud revision {revision.RevisionId} has invalid ancestry metadata.");
        if (revision.Kind is not (CloudRevisionKind.Saved or CloudRevisionKind.Recovery))
            throw new IOException($"Steam Cloud revision {revision.RevisionId} has invalid kind {revision.Kind}.");
        if (revision.Kind == CloudRevisionKind.Recovery && !revision.SessionId.HasValue)
            throw new IOException($"Steam Cloud recovery revision {revision.RevisionId} has no editing session.");
        if (revision.Kind == CloudRevisionKind.Saved && revision.SessionId.HasValue)
            throw new IOException($"Steam Cloud saved revision {revision.RevisionId} has an editing session.");
        if (remoteFileName != SteamCloudPaths.RevisionManifest(
                revision.Kind,
                revision.DocumentId,
                revision.RevisionId))
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
        if (remoteFileName != SteamCloudPaths.Session(session.DocumentId, session.SessionId))
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
