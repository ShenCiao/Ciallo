using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Ciallo.Data;
using Frent;
using GdUnit4;
using Godot;
using Newtonsoft.Json;
using Steamworks.WebApi;
using static GdUnit4.Assertions;

namespace Ciallo.Tests;

[TestSuite]
public class DurabilityTests
{
    [TestCase]
    public void CloudOutboxPrioritizesSessionBeforeRecoveryUploads()
    {
        var now = DateTimeOffset.UtcNow;
        var olderRecovery = new SteamCloudRecoveryOutboxRecord
        {
            RevisionId = Guid.NewGuid(),
            Kind = SteamCloudRecoveryKind.Recovery,
            CapturedAtUtc = now.AddMinutes(-3),
        };
        var newerRecovery = new SteamCloudRecoveryOutboxRecord
        {
            RevisionId = Guid.NewGuid(),
            Kind = SteamCloudRecoveryKind.Recovery,
            CapturedAtUtc = now.AddMinutes(-1),
        };
        var session = new SteamCloudRecoveryOutboxRecord
        {
            RevisionId = Guid.NewGuid(),
            Kind = SteamCloudRecoveryKind.Session,
            CapturedAtUtc = now,
        };

        var ordered = SteamCloudRecoveryCoordinator.OrderPending(
            [olderRecovery, newerRecovery, session]);

        AssertThat(ordered.Select(record => record.RevisionId).ToArray()).ContainsExactly(
            session.RevisionId,
            newerRecovery.RevisionId,
            olderRecovery.RevisionId);
    }

    [TestCase]
    public async Task RevisionPackageUsesManifestLastAndValidChunkHashes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ciallo-durability-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "source.ciallo");
            var bytes = new byte[SteamCloudOptions.ChunkByteLength + 31];
            System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
            await File.WriteAllBytesAsync(path, bytes);
            var contentSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var record = new SteamCloudRecoveryOutboxRecord
            {
                RevisionId = Guid.NewGuid(),
                DocumentId = Guid.NewGuid(),
                Kind = SteamCloudRecoveryKind.Recovery,
                SessionId = Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                OriginalFilePath = @"C:\work\coast.ciallo",
                SourcePath = path,
                ByteLength = bytes.Length,
                ContentSha256 = contentSha256,
                CapturedAtUtc = DateTimeOffset.UtcNow,
            };

            var package = SteamCloudRecoveryPackageBuilder.Build(record);
            AssertThat(package.Manifest.Chunks.Length).IsEqual(2);
            AssertThat(package.Manifest.Chunks[0].ByteLength).IsEqual(SteamCloudOptions.ChunkByteLength);
            AssertThat(package.Manifest.Chunks[1].ByteLength).IsEqual(31);
            AssertThat(package.Manifest.ContentSha256).IsEqual(contentSha256);
            AssertThat(package.Manifest.OriginalFilePath).IsEqual(record.OriginalFilePath);
            AssertThat(package.UploadFiles[^1].FileName).IsEqual(package.ManifestFileName);
            foreach (var chunk in package.Manifest.Chunks)
            {
                var expected = bytes.AsSpan(
                    chunk.Index * SteamCloudOptions.ChunkByteLength,
                    chunk.ByteLength);
                var expectedSha256 = Convert.ToHexString(SHA256.HashData(expected)).ToLowerInvariant();
                AssertThat(chunk.Sha256).IsEqual(expectedSha256);
            }
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestCase]
    public async Task RecoveryCatalogCachesOnlyManifestsWithSha1()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ciallo-catalog-cache-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var steamId = 76561198000000000UL;
            var options = new SteamCloudOptions { AppId = 4103990, OAuthClientId = "test-client" };
            var files = new DurabilityFileStore(directory);
            DurabilityFiles.WriteJson(
                Path.Combine(files.RootPath, "steam-oauth-token.json"),
                new StoredSteamOAuthToken
                {
                    AccessToken = "test-token",
                    SteamId = steamId,
                });

            var handler = new CatalogManifestHandler();
            using var httpClient = new System.Net.Http.HttpClient(handler);
            var protocol = new SteamCloudWebApiClient(httpClient);
            var authorization = new SteamCloudAuthorization(options, steamId, files.RootPath, httpClient);
            var authorized = new AuthorizedSteamCloudClient(protocol, options, authorization);
            var catalog = new SteamCloudRecoveryCatalog(
                options,
                files,
                new SteamCloudRecoveryOutbox(files),
                authorized,
                protocol);
            var sessionId = Guid.NewGuid();
            var documentId = Guid.NewGuid();
            var deviceId = Guid.NewGuid();
            var manifest = new SteamCloudRecoveryManifest
            {
                RevisionId = Guid.NewGuid(),
                DocumentId = documentId,
                Kind = SteamCloudRecoveryKind.Recovery,
                SessionId = sessionId,
                DeviceId = deviceId,
                DocumentName = "First",
                CapturedAtUtc = DateTimeOffset.UnixEpoch,
                ContentSha256 = Convert.ToHexString(SHA256.HashData(Array.Empty<byte>())).ToLowerInvariant(),
            };
            var session = new EditingSessionInfo
            {
                SessionId = sessionId,
                DocumentId = documentId,
                DeviceId = deviceId,
                DocumentName = "First",
                OpenedAtUtc = DateTimeOffset.UnixEpoch,
                LastHeartbeatUtc = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(20),
            };

            handler.SetCatalog(manifest, session, includeSha1: true);
            var first = await catalog.RefreshAsync(CancellationToken.None);
            AssertThat(first.RecoveryCandidates.Single().DocumentName).IsEqual("First");
            AssertThat(handler.ManifestDownloadCount).IsEqual(1);

            await catalog.RefreshAsync(CancellationToken.None);
            AssertThat(handler.ManifestDownloadCount).IsEqual(1);

            handler.SetCatalog(manifest with { DocumentName = "Changed" }, session, includeSha1: true);
            var changed = await catalog.RefreshAsync(CancellationToken.None);
            AssertThat(changed.RecoveryCandidates.Single().DocumentName).IsEqual("Changed");
            AssertThat(handler.ManifestDownloadCount).IsEqual(2);

            handler.SetCatalog(manifest with { DocumentName = "No hash" }, session, includeSha1: false);
            await catalog.RefreshAsync(CancellationToken.None);
            await catalog.RefreshAsync(CancellationToken.None);
            AssertThat(handler.ManifestDownloadCount).IsEqual(4);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestCase]
    public async Task SteamCloudUploadCompletesCommittedBatch()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ciallo-steam-api-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var steamId = 76561198000000000UL;
            DurabilityFiles.WriteJson(
                Path.Combine(directory, "steam-oauth-token.json"),
                new StoredSteamOAuthToken
                {
                    AccessToken = "test-token",
                    SteamId = steamId,
                });
            var options = new SteamCloudOptions { AppId = 4103990, OAuthClientId = "test-client" };
            var handler = new SteamCloudApiHandler();
            using var httpClient = new System.Net.Http.HttpClient(handler);
            var authorization = new SteamCloudAuthorization(options, steamId, directory, httpClient);
            var client = new AuthorizedSteamCloudClient(
                new SteamCloudWebApiClient(httpClient),
                options,
                authorization);

            await client.ModifyBatchAsync(
                [new SteamCloudUploadFile("ciallo/v1/test.bin", [1, 2, 3])],
                [],
                CancellationToken.None);

            AssertThat(handler.Calls.ToArray()).ContainsExactly(
                "BeginAppUploadBatch",
                "BeginHTTPUpload",
                "PUT",
                "CommitHTTPUpload",
                "CompleteAppUploadBatch");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestCase]
    public async Task SteamCloudRejectedTokenRequiresNewAuthorization()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ciallo-steam-auth-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var steamId = 76561198000000000UL;
            var tokenPath = Path.Combine(directory, "steam-oauth-token.json");
            DurabilityFiles.WriteJson(
                tokenPath,
                new StoredSteamOAuthToken
                {
                    AccessToken = "revoked-token",
                    SteamId = steamId,
                });
            var options = new SteamCloudOptions { AppId = 4103990, OAuthClientId = "test-client" };
            using var httpClient = new System.Net.Http.HttpClient(new RejectedSteamCloudApiHandler());
            var authorization = new SteamCloudAuthorization(options, steamId, directory, httpClient);
            var client = new AuthorizedSteamCloudClient(
                new SteamCloudWebApiClient(httpClient),
                options,
                authorization);

            var authorizationRequired = false;
            try
            {
                await client.EnumerateFilesAsync();
            }
            catch (SteamWebApiAuthorizationException)
            {
                authorizationRequired = true;
            }

            AssertThat(authorizationRequired).IsTrue();
            AssertThat(authorization.Status.State).IsEqual(SteamCloudAuthorizationState.AuthorizationRequired);
            AssertThat(File.Exists(tokenPath)).IsFalse();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [TestCase]
    public void DurabilityJsonKeepsCompactIdentifiers()
    {
        var revisionId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var manifest = new SteamCloudRecoveryManifest
        {
            RevisionId = revisionId,
            DocumentId = documentId,
            Kind = SteamCloudRecoveryKind.Recovery,
            SessionId = sessionId,
            DeviceId = deviceId,
            OriginalFilePath = @"D:\art\doc.ciallo",
        };

        var json = System.Text.Json.JsonSerializer.Serialize(manifest, DurabilityFiles.JsonOptions);
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<SteamCloudRecoveryManifest>(
            json,
            DurabilityFiles.JsonOptions)!;

        AssertThat(json.Contains(revisionId.ToString("N"), StringComparison.Ordinal)).IsTrue();
        AssertThat(json.Contains(documentId.ToString("N"), StringComparison.Ordinal)).IsTrue();
        AssertThat(json.Contains(sessionId.ToString("N"), StringComparison.Ordinal)).IsTrue();
        AssertThat(json.Contains(deviceId.ToString("N"), StringComparison.Ordinal)).IsTrue();
        AssertThat(roundTrip.RevisionId).IsEqual(revisionId);
        AssertThat(roundTrip.DocumentId).IsEqual(documentId);
        AssertThat(roundTrip.SessionId).IsEqual(sessionId);
        AssertThat(roundTrip.DeviceId).IsEqual(deviceId);
        AssertThat(roundTrip.OriginalFilePath).IsEqual(@"D:\art\doc.ciallo");
    }

    [TestCase]
    public void RecoverySnapshotIntervalLoadsTimeSpan()
    {
        var preference = new Preference();
        JsonConvert.PopulateObject(
            """{"RecoverySnapshotInterval":"00:02:30"}""",
            preference,
            Preference.JsonOptions);
        AssertThat(preference.RecoverySnapshotInterval.Value).IsEqual(TimeSpan.FromSeconds(150));
        AssertThat(JsonConvert.SerializeObject(
            preference.RecoverySnapshotInterval,
            Preference.JsonOptions)).IsEqual("\"00:02:30\"");
    }

    [TestCase]
    [RequireGodotRuntime]
    public void RecoveryRetentionKeepsNewestSnapshotThatExceedsByteLimit()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ciallo-retention-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var document = AppDocumentManager.Create(new DocumentSetting { Name = { Value = "Large snapshot" } });
        try
        {
            var snapshot = PersistenceSnapshotCapture.Capture(document, 1);
            var store = new DurabilityFileStore(directory);
            var result = store.WriteRecoverySnapshot(new RecoveryWriteRequest(
                snapshot,
                Guid.NewGuid(),
                Guid.NewGuid(),
                store.DeviceId,
                "Large snapshot",
                "",
                new RecoveryRetentionPolicy(24, 256, 1)));

            AssertThat(result.Retention.LimitExceeded).IsTrue();
            AssertThat(File.Exists(store.GetRecoverySnapshotPath(result.Snapshot))).IsTrue();
            AssertThat(store.ListRecoverySnapshots(snapshot.DocumentId).Count).IsEqual(1);
        }
        finally
        {
            AppDocumentManager.Clear();
            Directory.Delete(directory, true);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task PersistenceSnapshotIsDetachedFromLaterWorldChanges()
    {
        var path = Path.Combine(Path.GetTempPath(), "ciallo-snapshot-test-" + Guid.NewGuid().ToString("N") + ".ciallo");
        var document = AppDocumentManager.Create(new DocumentSetting { Name = { Value = "Before" } });
        var shape = document.World.Create();
        shape.Tag<ToSerializeTag>();
        var originalPositions = ImmutableArray.Create(new Vector2(1, 2), new Vector2(3, 4));
        shape.Add(new SampledPolyline { Positions = { Value = originalPositions } });

        try
        {
            var snapshot = PersistenceSnapshotCapture.Capture(document, 7);
            var documentId = snapshot.DocumentId;
            document.Get<DocumentSetting>().Name.Value = "After";
            shape.Get<SampledPolyline>().Positions.Value = ImmutableArray.Create(new Vector2(9, 9));

            await Task.Run(() => DuckDbProjectSerializer.Save(snapshot, path));
            var loaded = DuckDbProjectSerializer.Load(path);
            AssertThat(loaded.Get<DocumentSetting>().DocumentId.Value).IsEqual(documentId);
            AssertThat(loaded.Get<DocumentSetting>().Name.Value).IsEqual("Before");
            var query = loaded.World.CreateQuery().With<SampledPolyline>().Build();
            var loadedShape = Entity.Null;
            foreach (var entity in query.EnumerateWithEntities())
                loadedShape = entity;
            AssertThat(loadedShape.Get<SampledPolyline>().Positions.Value.Length).IsEqual(2);
            AssertThat(loadedShape.Get<SampledPolyline>().Positions.Value[1]).IsEqual(new Vector2(3, 4));
            DisposeWorld(loaded.World);
        }
        finally
        {
            AppDocumentManager.Clear();
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public async Task SaveRoundTripsAllSampledPolylineColumns()
    {
        // SampledPolyline is a pure numeric-array table, so it loads through NativeComponentReader.
        // Radii/Pressures are FLOAT[] (PrimitiveArray path: AppendPrimitiveList / BuildImmutableThenContainer),
        // Positions/Tilts are STRUCT(x,y)[] (StructArray path: DecomposeArrayInto / ComposeArrayFromLeaves) —
        // two entirely separate code paths. A second, all-empty shape puts an empty list next to a
        // populated one in the same table so the reader's per-row count-0 branch is exercised too.
        var path = Path.Combine(Path.GetTempPath(), "ciallo-polyline-columns-test-" + Guid.NewGuid().ToString("N") + ".ciallo");
        var document = AppDocumentManager.Create(new DocumentSetting { Name = { Value = "Polyline" } });

        var full = document.World.Create();
        full.Tag<ToSerializeTag>();
        full.Add(new SampledPolyline
        {
            Positions = { Value = ImmutableArray.Create(new Vector2(1, 2), new Vector2(3, 4), new Vector2(5, 6)) },
            Radii = { Value = ImmutableArray.Create(0.5f, 1.5f, 2.5f) },
            Pressures = { Value = ImmutableArray.Create(0.1f, 0.9f, 0.4f) },
            Tilts = { Value = ImmutableArray.Create(new Vector2(-1, -2), new Vector2(-3, -4), new Vector2(-5, -6)) },
        });

        var empty = document.World.Create();
        empty.Tag<ToSerializeTag>();
        empty.Add(new SampledPolyline());

        try
        {
            var snapshot = PersistenceSnapshotCapture.Capture(document, 11);
            await Task.Run(() => DuckDbProjectSerializer.Save(snapshot, path));
            var loaded = DuckDbProjectSerializer.Load(path);

            SampledPolyline loadedFull = null;
            SampledPolyline loadedEmpty = null;
            var query = loaded.World.CreateQuery().With<SampledPolyline>().Build();
            foreach (var entity in query.EnumerateWithEntities())
            {
                var polyline = entity.Get<SampledPolyline>();
                if (polyline.Positions.Value.Length == 0)
                    loadedEmpty = polyline;
                else
                    loadedFull = polyline;
            }

            AssertThat(loadedFull).IsNotNull();
            AssertThat(loadedFull.Radii.Value.ToArray()).ContainsExactly(0.5f, 1.5f, 2.5f);
            AssertThat(loadedFull.Pressures.Value.ToArray()).ContainsExactly(0.1f, 0.9f, 0.4f);
            AssertThat(loadedFull.Positions.Value[2]).IsEqual(new Vector2(5, 6));
            AssertThat(loadedFull.Tilts.Value[2]).IsEqual(new Vector2(-5, -6));

            AssertThat(loadedEmpty).IsNotNull();
            AssertThat(loadedEmpty.Radii.Value.Length).IsEqual(0);
            AssertThat(loadedEmpty.Tilts.Value.Length).IsEqual(0);
            DisposeWorld(loaded.World);
        }
        finally
        {
            AppDocumentManager.Clear();
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void ManualSaveRoundTripsEntityReferences()
    {
        var path = Path.Combine(Path.GetTempPath(), "ciallo-manual-save-test-" + Guid.NewGuid().ToString("N") + ".ciallo");
        var document = AppDocumentManager.Create(new DocumentSetting { Name = { Value = "Manual" } });
        var brush = document.World.Create();
        brush.Tag<ToSerializeTag>();
        var polygon = document.World.Create();
        polygon.Tag<ToSerializeTag>();
        polygon.Add(new FilledPolygonSetting { BrushE = { Value = brush } });

        try
        {
            // Save goes through PersistenceSnapshotCapture, which resolves the live BrushE entity
            // reference to a positional id; Load must reconstruct it as a real entity again.
            AppDocumentManager.Save(document, path);
            var loaded = DuckDbProjectSerializer.Load(path);

            var query = loaded.World.CreateQuery().With<FilledPolygonSetting>().Build();
            var loadedPolygon = Entity.Null;
            foreach (var entity in query.EnumerateWithEntities())
                loadedPolygon = entity;
            var referencedBrush = loadedPolygon.Get<FilledPolygonSetting>().BrushE.Value;
            AssertThat(referencedBrush.IsNull).IsFalse();
            AssertThat(referencedBrush.Has<FilledPolygonSetting>()).IsFalse();
            DisposeWorld(loaded.World);
        }
        finally
        {
            AppDocumentManager.Clear();
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SaveRoundTripsBlankExposureSelfReference()
    {
        var path = Path.Combine(Path.GetTempPath(), "ciallo-blank-exposure-test-" + Guid.NewGuid().ToString("N") + ".ciallo");
        var document = AppDocumentManager.Create(new DocumentSetting { Name = { Value = "Blank exposure" } });
        var celFolder = document.World.Create();
        celFolder.Tag<ToSerializeTag>();
        celFolder.Add(new CommonLayerSetting { Name = { Value = "Animation" } });
        celFolder.Add(new LayerTreeNode());
        celFolder.Add(new FolderLayerSetting { IsCelFolder = true });
        document.Get<LayerTreeNode>().AddChild(celFolder);
        celFolder.Get<FolderLayerSetting>().Exposures.Add(12, celFolder);

        try
        {
            AppDocumentManager.Save(document, path);
            var loaded = DuckDbProjectSerializer.Load(path);

            var loadedCelFolder = Entity.Null;
            var query = loaded.World.CreateQuery().With<FolderLayerSetting>().Build();
            foreach (var entity in query.EnumerateWithEntities())
            {
                if (entity.Get<FolderLayerSetting>().IsCelFolder)
                    loadedCelFolder = entity;
            }

            AssertThat(loadedCelFolder.IsNull).IsFalse();
            var loadedExposures = loadedCelFolder.Get<FolderLayerSetting>().Exposures;
            AssertThat(loadedExposures.ContainsKey(12)).IsTrue();
            AssertThat(loadedExposures[12]).IsEqual(loadedCelFolder);
            DisposeWorld(loaded.World);
        }
        finally
        {
            AppDocumentManager.Clear();
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestCase]
    [RequireGodotRuntime]
    public void SaveRoundTripsPrivateAndInheritedTreeFields()
    {
        var path = Path.Combine(Path.GetTempPath(), "ciallo-tree-test-" + Guid.NewGuid().ToString("N") + ".ciallo");
        var document = AppDocumentManager.Create(new DocumentSetting { Name = { Value = "Tree" } });
        var parent = document.World.Create();
        parent.Tag<ToSerializeTag>();
        parent.Add(new LayerTreeNode());
        parent.Get<LayerTreeNode>().Init(parent);
        var child = document.World.Create();
        child.Tag<ToSerializeTag>();
        child.Add(new LayerTreeNode());
        child.Get<LayerTreeNode>().Init(child);
        // Fills private _children (readonly EntityArray) on parent and private _parent
        // (reactive EntityRef) on child — the DynamicMethod accessor paths.
        parent.Get<LayerTreeNode>().AddChild(child);

        try
        {
            AppDocumentManager.Save(document, path);
            var loaded = DuckDbProjectSerializer.Load(path);

            // Find the node whose _children (readonly EntityArray) round-tripped our AddChild, and
            // confirm the child's _parent (reactive EntityRef) points back — both private fields.
            var parentEntity = Entity.Null;
            var query = loaded.World.CreateQuery().With<LayerTreeNode>().Build();
            foreach (var entity in query.EnumerateWithEntities())
                if (entity.Get<LayerTreeNode>().Children.Count == 1)
                    parentEntity = entity;

            AssertThat(parentEntity.IsNull).IsFalse();
            var loadedChild = parentEntity.Get<LayerTreeNode>().Children[0];
            AssertThat(loadedChild.Get<LayerTreeNode>().ParentValue).IsEqual(parentEntity);
            DisposeWorld(loaded.World);
        }
        finally
        {
            AppDocumentManager.Clear();
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static void DisposeWorld(World world)
    {
        var query = world.CreateQuery().Build();
        foreach (var entity in query.EnumerateWithEntities())
            entity.Delete();
        world.Dispose();
    }

    private sealed class SteamCloudApiHandler : HttpMessageHandler
    {
        public readonly System.Collections.Generic.List<string> Calls = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Put)
            {
                Calls.Add("PUT");
                var bytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
                AssertThat(bytes).ContainsExactly(1, 2, 3);
                return Response("{}");
            }

            var method = request.RequestUri!.Segments[^2].TrimEnd('/');
            Calls.Add(method);
            return method switch
            {
                "BeginAppUploadBatch" => Response("""{"response":{"batch_id":"42","app_change_number":"9"}}"""),
                "BeginHTTPUpload" => Response("""
                    {"response":{"ugcid":"7","timestamp":"8","url_host":"storage.test","url_path":"/upload","use_https":true,"request_headers":[]}}
                    """),
                "CommitHTTPUpload" => Response("""{"response":{"file_committed":true}}"""),
                "CompleteAppUploadBatch" => Response(""),
                _ => throw new InvalidOperationException("Unexpected Steam WebAPI method " + method),
            };
        }

        private static HttpResponseMessage Response(string json)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            response.Headers.Add("x-eresult", "1");
            return response;
        }
    }

    private sealed class CatalogManifestHandler : HttpMessageHandler
    {
        private byte[] _manifestBytes;
        private byte[] _sessionBytes;
        private string _manifestFileName;
        private string _sessionFileName;
        private bool _includeSha1;

        public int ManifestDownloadCount { get; private set; }

        public void SetCatalog(SteamCloudRecoveryManifest manifest, EditingSessionInfo session, bool includeSha1)
        {
            _manifestBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                manifest,
                DurabilityFiles.JsonOptions);
            _sessionBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
                session,
                DurabilityFiles.JsonOptions);
            _manifestFileName = SteamCloudRecoveryPaths.RevisionManifest(manifest.DocumentId, manifest.RevisionId);
            _sessionFileName = SteamCloudRecoveryPaths.Session(session.DocumentId, session.SessionId);
            _includeSha1 = includeSha1;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host == "download.test")
            {
                var target = request.RequestUri.AbsolutePath.Trim('/');
                if (target == "session")
                {
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(_sessionBytes),
                    });
                }

                ManifestDownloadCount++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(_manifestBytes),
                });
            }

            if (!request.RequestUri.AbsolutePath.Contains("/ICloudService/EnumerateUserFiles/", StringComparison.Ordinal))
                throw new InvalidOperationException("Unexpected Steam Cloud request " + request.RequestUri);

            var files = new[]
            {
                EnumeratedFile(_manifestFileName, _manifestBytes, "https://download.test/manifest", _includeSha1),
                EnumeratedFile(_sessionFileName, _sessionBytes, "https://download.test/session", includeSha1: true),
            };
            var json = System.Text.Json.JsonSerializer.Serialize(new
            {
                response = new
                {
                    files,
                    total_files = files.Length,
                },
            });
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            response.Headers.Add("x-eresult", "1");
            return Task.FromResult(response);
        }

        private static Dictionary<string, object> EnumeratedFile(
            string fileName,
            byte[] bytes,
            string url,
            bool includeSha1)
        {
            var file = new Dictionary<string, object>
            {
                ["appid"] = 4103990,
                ["ugcid"] = "11",
                ["filename"] = fileName,
                ["timestamp"] = "1710000000",
                ["file_size"] = bytes.Length,
                ["url"] = url,
                ["steamid_creator"] = "76561198000000000",
                ["flags"] = 0,
                ["platforms_to_sync"] = new[] { SteamCloudPlatforms.All },
            };
            if (includeSha1)
                file["file_sha"] = Convert.ToHexString(SHA1.HashData(bytes)).ToLowerInvariant();
            return file;
        }
    }

    private sealed class RejectedSteamCloudApiHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
        }
    }
}
