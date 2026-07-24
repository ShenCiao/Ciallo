using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Steamworks.WebApi;

namespace Ciallo.Data;

internal sealed record CloudRevisionPackage(
    CloudRevisionManifest Manifest,
    string ManifestFileName,
    IReadOnlyList<SteamCloudUploadFile> UploadFiles);

internal static class CloudRevisionPackageBuilder
{
    public static CloudRevisionPackage Build(CloudOutboxRecord record)
    {
        if (record.Kind is not (CloudRevisionKind.Saved or CloudRevisionKind.Recovery))
            throw new InvalidOperationException($"Cannot package cloud revision kind {record.Kind}.");

        var chunks = new List<CloudChunkDescriptor>();
        var uploadsByName = new Dictionary<string, SteamCloudUploadFile>(StringComparer.Ordinal);
        using var fullHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using (var stream = File.OpenRead(record.SourcePath))
        {
            long offset = 0;
            int index = 0;
            var buffer = new byte[SteamCloudOptions.ChunkByteLength];
            while (offset < stream.Length)
            {
                int byteLength = (int)Math.Min(buffer.Length, stream.Length - offset);
                stream.ReadExactly(buffer.AsSpan(0, byteLength));
                var chunkBytes = buffer.AsSpan(0, byteLength);
                fullHash.AppendData(chunkBytes);
                var sha256 = Convert.ToHexString(SHA256.HashData(chunkBytes)).ToLowerInvariant();
                var sha1 = Convert.ToHexString(SHA1.HashData(chunkBytes)).ToLowerInvariant();
                var remoteName = SteamCloudPaths.Chunk(sha256);
                var capturedOffset = offset;
                if (!uploadsByName.ContainsKey(remoteName))
                {
                    uploadsByName.Add(
                        remoteName,
                        new SteamCloudUploadFile(
                            remoteName,
                            byteLength,
                            sha1,
                            () => ReadSegment(record.SourcePath, capturedOffset, byteLength)));
                }
                chunks.Add(new CloudChunkDescriptor
                {
                    Index = index,
                    ByteLength = byteLength,
                    Sha256 = sha256,
                });
                offset += byteLength;
                index++;
            }

            if (stream.Length != record.ByteLength)
                throw new IOException($"Cloud outbox source {record.SourcePath} changed size.");
        }

        var contentSha256 = Convert.ToHexString(fullHash.GetHashAndReset()).ToLowerInvariant();
        if (contentSha256 != record.ContentSha256)
            throw new IOException($"Cloud outbox source {record.SourcePath} changed content.");

        var manifest = new CloudRevisionManifest
        {
            RevisionId = record.RevisionId,
            DocumentId = record.DocumentId,
            Kind = record.Kind,
            ParentRevisionId = record.ParentRevisionId,
            SupersededRevisionIds = record.SupersededRevisionIds,
            SessionId = record.SessionId,
            DeviceId = record.DeviceId,
            DocumentName = record.DocumentName,
            CapturedAtUtc = record.CapturedAtUtc,
            PersistenceEpoch = record.PersistenceEpoch,
            ByteLength = record.ByteLength,
            ContentSha256 = record.ContentSha256,
            Chunks = chunks.ToArray(),
        };
        var manifestFileName = SteamCloudPaths.RevisionManifest(
            record.Kind,
            record.DocumentId,
            record.RevisionId);
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, DurabilityFiles.JsonOptions);
        var uploads = uploadsByName.Values.ToList();
        uploads.Add(new SteamCloudUploadFile(manifestFileName, manifestBytes));
        return new CloudRevisionPackage(manifest, manifestFileName, uploads);
    }

    private static byte[] ReadSegment(string path, long offset, int byteLength)
    {
        using var stream = File.OpenRead(path);
        stream.Position = offset;
        var bytes = new byte[byteLength];
        stream.ReadExactly(bytes);
        return bytes;
    }
}
