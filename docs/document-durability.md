# Document durability

## Supported behavior

Ciallo maintains local recovery snapshots and mirrors those snapshots, plus editing-session records, to Steam Cloud while the application is running.

- Every five minutes, the working document's current committed persistence epoch is captured when that epoch is not already written or queued.
- Capture runs on the Godot main thread and detaches persistence data from the live Frent world. Entity references and mutable collections are copied, while immutable geometry arrays are shared.
- DuckDB serialization, hashing, recovery retention, and recovery outbox creation run on one background writer.
- The background queue retains one pending automatic capture. A newer capture replaces an older pending capture while a write is in progress.
- A failed write leaves the persistence epoch unprotected so a later interval retries it.
- Every successful recovery snapshot write enqueues that same snapshot file for Steam Cloud.
- Editing-session open, heartbeat, and close records are mirrored to Steam Cloud so another device can detect an interrupted session.
- Manual save writes only the user's local document file. It does not create a cloud object.
- The outbox is retried while Ciallo is running and survives application restarts.
- Steam Cloud is refreshed and reconciled every five minutes while authorized, including when there are no pending uploads.
- Leftover `ciallo/v1/revisions/saved/` files from the withdrawn saved-revision path are deleted during reconcile.

The persistence world must contain only committed interaction state. In-progress image transforms, timeline drags, and similar interactions use preview buffers and publish one persistence epoch only when the interaction commits.

## Local files

The durability root is `user://DocumentDurability/v1`. The [`--user-data-dir` startup option](command-line.md) overrides Godot's `user://` root, including its recovery outbox and Steam OAuth credentials.

| Path | Purpose |
| --- | --- |
| `recovery/<document-id>/` | Atomic `.ciallo` recovery snapshots and JSON metadata |
| `sessions/<document-id>/` | Editing-session heartbeat and closed markers |
| `outbox/` | Persistent pending Steam Cloud operations |
| `cloud-receipts/` | Completed upload receipts |
| `steam-oauth-token.json` | User OAuth token and authorized Steam account |

Recovery snapshot and metadata writes use sibling temporary files followed by an atomic replacement. A recovery snapshot contains document content but no command history.

A cloud recovery download writes into the same local recovery directory, then opens through `OpenRecoverySnapshot`.

## Scheduling and retention

The preferences are:

| Preference | Default | Meaning |
| --- | ---: | --- |
| `RecoverySnapshotInterval` | `00:05:00` | Automatic capture interval; durations below one minute are treated as one minute |
| `RecoverySnapshotLimitPerDocument` | 24 | Maximum retained recovery snapshots for one document |
| `RecoverySnapshotAccountFileLimit` | 256 | Maximum retained recovery snapshots across documents |
| `RecoverySnapshotAccountByteLimit` | 2 GiB | Maximum logical recovery bytes across documents |

Local and Steam Cloud recovery retention remove the oldest recovery snapshots first. At least the newest recovery snapshot is preserved even when that one file exceeds an account limit. `DocumentDurabilityStatus.LocalRetentionLimitExceeded` or `RecoveryCloudRetentionLimitExceeded` remains available for the GUI to show that condition.

Unreferenced chunks are deleted only after they have been orphaned for at least one day.

## Recovery sessions

Opening a working document creates an editing session. Ciallo writes a heartbeat every five minutes and writes a closed marker during normal document close.

A recovery snapshot becomes a recovery candidate when its session has no closed marker and its last heartbeat is at least 15 minutes old. Opening a recovery candidate restores its original file path and marks the working document unsaved. Returning to the last manual save uses the normal document-open workflow for that original path.

## Steam Cloud authorization

Steam Cloud uses AppID `4103990` and the Steam `ICloudService` WebAPI. The Valve OAuth client registration requires:

- permissions `read_cloud` and `write_cloud`;
- token lifetime of 30 days;
- redirect URI `http://127.0.0.1:41039/steam-oauth/`;
- the Ciallo Steam AppID.

Configure the issued OAuth Client ID through either:

- environment variable `CIALLO_STEAM_OAUTH_CLIENT_ID`; or
- Godot project setting `steam/cloud/oauth_client_id`.

`Ciallo/Steamworks/WebApi` provides the documented `ICloudService` and OAuth
protocol implementation, alongside the [Facepunch.Steamworks submodule](../.github/CONTRIBUTING.md#facepunchsteamworks-dependency).
Ciallo owns token
persistence, account and scope validation, snapshot packaging, and retention.

Ciallo binds the callback only to `127.0.0.1`, verifies a random OAuth state, and verifies that the authorized SteamID matches the Steam account running Ciallo. The token is stored outside preferences. Unix token permissions are `0600`; Windows uses the user-profile ACL inherited by the Godot user directory.

Authorization persists across launches, and Steam enforces the configured token lifetime. Ciallo does not calculate token expiry. HTTP 401/403, Steam `EResult=15`, or a Steam account mismatch clears the authorization and requires the user to authorize again.

## Cloud format and commit point

Recovery snapshots are immutable packages:

- document, revision, session, and device identities use `System.Guid` throughout the application;
- JSON, local paths, and Steam Remote Storage names encode those identities as lowercase 32-digit `N` strings only at their storage boundary;
- document content is split into 8 MiB content-addressed chunks;
- each chunk records SHA-256 content identity and uses SHA-1 for Steam transport validation;
- the revision manifest records identity, session, device, original file path, byte length, hashes, and ordered chunk descriptors;
- chunks are uploaded before the manifest.

Each modification uses the Steam batch sequence `BeginAppUploadBatch`, `BeginHTTPUpload`, HTTP `PUT`, `CommitHTTPUpload`, optional `Delete`, and `CompleteAppUploadBatch`. A snapshot becomes a cloud protection point only after every required file is committed and the batch completes. Batch completion has an independent ten-second shutdown bound.

Steam Remote Storage names use the prefix `ciallo/v1/`: recovery manifests under `revisions/recovery/`, session records under `sessions/`, and chunks under `chunks/`.

## Runtime API

`AppDocumentDurability` exposes:

- `Status` and main-thread `StatusChanged` notifications;
- `ListLocalSnapshots(documentId)`;
- `ListRecoveryCandidates()`;
- `OpenRecoverySnapshot(revisionId)`.

`DocumentDurabilityStatus` separates `LastLocalError` and `LastRecoveryCloudError`; `LastExternalError` returns the local error first for compatibility. It also reports pending upload count, latest local snapshot, latest confirmed cloud protection point, local/cloud state, and both retention-limit flags.

`SteamManager` exposes:

- `IsRecoveryCloudAvailable`, `CloudAuthorizationStatus`, and `RecoveryCatalog`;
- main-thread `CloudAuthorizationStatusChanged` and `RecoveryCatalogChanged` notifications;
- `AuthorizeCloudAsync()` and `ClearCloudAuthorization()`;
- `RefreshRecoveryCatalogAsync()`;
- `DownloadRecoverySnapshotAsync(revisionId)`, which installs the snapshot into local recovery storage;
- `FlushRecoverySessionAsync(timeout)` for bounded normal-exit flushing.

`SteamCloudRecoveryCatalogSnapshot.RecoveryCandidates` lists interrupted-session snapshots only. No durability GUI is currently provided. UI code consumes these status, catalog, and action APIs.

## Verification

Build the project and run the durability suite with:

```powershell
dotnet build Ciallo/Ciallo.csproj --no-restore
dotnet test Ciallo/Ciallo.csproj --no-build --filter FullyQualifiedName~DurabilityTests
```

The suite covers upload priority, manifest/chunk construction, catalog SHA-1 caching, detached snapshot serialization, over-limit newest-snapshot retention, the Steam upload batch sequence, and revoked-token handling. A live Steam Cloud integration test additionally requires the issued OAuth Client ID and a Steam account licensed for AppID `4103990`.
