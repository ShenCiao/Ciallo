using System;
using Godot;

namespace Ciallo.Data;

internal sealed record SteamCloudOptions
{
    public const int ChunkByteLength = 8 * 1024 * 1024;
    public static readonly Uri OAuthRedirectUri = new("http://127.0.0.1:41039/steam-oauth/");

    public uint AppId { get; init; }
    public string OAuthClientId { get; init; } = "";
    public int RecoveryPerDocumentLimit { get; init; }
    public int RecoveryAccountFileLimit { get; init; }
    public long RecoveryAccountByteLimit { get; init; }
    public bool IsConfigured => OAuthClientId.Length > 0;

    public static SteamCloudOptions Load(uint appId)
    {
        var clientId = System.Environment.GetEnvironmentVariable("CIALLO_STEAM_OAUTH_CLIENT_ID") ??
                       ProjectSettings.GetSetting("steam/cloud/oauth_client_id", "").AsString();
        return new SteamCloudOptions
        {
            AppId = appId,
            OAuthClientId = clientId.Trim(),
            RecoveryPerDocumentLimit = AppPreference.RecoverySnapshotLimitPerDocument.Value,
            RecoveryAccountFileLimit = AppPreference.RecoverySnapshotAccountFileLimit.Value,
            RecoveryAccountByteLimit = AppPreference.RecoverySnapshotAccountByteLimit.Value,
        };
    }
}
