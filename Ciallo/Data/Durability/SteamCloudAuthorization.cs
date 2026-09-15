using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Steamworks.WebApi;

namespace Ciallo.Data;

public enum SteamCloudAuthorizationState
{
    Unconfigured,
    AuthorizationRequired,
    Authorizing,
    Authorized,
    Failed,
}

public sealed record SteamCloudAuthorizationStatus
{
    public SteamCloudAuthorizationState State { get; init; }
    public ulong SteamId { get; init; }
    public string LastExternalError { get; init; } = "";
}

internal sealed record StoredSteamOAuthToken
{
    public int SchemaVersion { get; init; } = 1;
    public string AccessToken { get; init; } = "";
    public ulong SteamId { get; init; }
}

internal sealed class SteamCloudAuthorization
{
    private readonly SteamCloudOptions _options;
    private readonly ulong _currentSteamId;
    private readonly OAuthTokenStore _tokenStore;
    private readonly SteamOAuthClient _oauthClient;
    private StoredSteamOAuthToken _token;

    public SteamCloudAuthorizationStatus Status { get; private set; }
    public event Action<SteamCloudAuthorizationStatus> StatusChanged;

    public SteamCloudAuthorization(
        SteamCloudOptions options,
        ulong currentSteamId,
        string durabilityRootPath,
        HttpClient httpClient)
    {
        _options = options;
        _currentSteamId = currentSteamId;
        _tokenStore = new OAuthTokenStore(Path.Combine(durabilityRootPath, "steam-oauth-token.json"));
        _oauthClient = new SteamOAuthClient(httpClient);

        if (!options.IsConfigured)
        {
            Status = new SteamCloudAuthorizationStatus { State = SteamCloudAuthorizationState.Unconfigured };
            return;
        }

        _token = _tokenStore.Load();
        Status = ResolveStoredTokenStatus();
    }

    public string GetAccessToken()
    {
        if (_token == null)
            throw new SteamWebApiAuthorizationException("Steam Cloud authorization is required.");
        return _token.AccessToken;
    }

    public void Reject(string externalError)
    {
        externalError = DiscardToken(externalError);
        SetStatus(new SteamCloudAuthorizationStatus
        {
            State = SteamCloudAuthorizationState.AuthorizationRequired,
            SteamId = _currentSteamId,
            LastExternalError = externalError,
        });
    }

    public async Task AuthorizeAsync(Action<string> openBrowser, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
            throw new InvalidOperationException("Steam Cloud OAuth Client ID is not configured.");

        SetStatus(Status with
        {
            State = SteamCloudAuthorizationState.Authorizing,
            LastExternalError = "",
        });

        try
        {
            var state = SteamOAuthClient.CreateState();
            await using var callback = new SteamOAuthLoopbackReceiver(
                SteamCloudOptions.OAuthRedirectUri,
                state,
                "Ciallo");
            callback.Start();
            openBrowser(_oauthClient.CreateAuthorizationUri(_options.OAuthClientId, state).AbsoluteUri);

            var accessToken = await callback.WaitForTokenAsync(cancellationToken);
            var details = await _oauthClient.GetTokenDetailsAsync(accessToken, cancellationToken);
            if (details.SteamId != _currentSteamId)
                throw new InvalidOperationException("Steam OAuth account does not match the Steam account running Ciallo.");
            if (details.ClientId.Length > 0 && details.ClientId != _options.OAuthClientId)
                throw new InvalidOperationException("Steam OAuth token belongs to a different OAuth client.");
            if (details.Scopes.Count > 0 &&
                (!details.Scopes.Contains("read_cloud", StringComparer.Ordinal) ||
                 !details.Scopes.Contains("write_cloud", StringComparer.Ordinal)))
                throw new InvalidOperationException("Steam OAuth token does not grant read_cloud and write_cloud.");

            _token = new StoredSteamOAuthToken
            {
                AccessToken = accessToken,
                SteamId = details.SteamId,
            };
            _tokenStore.Save(_token);
            SetStatus(new SteamCloudAuthorizationStatus
            {
                State = SteamCloudAuthorizationState.Authorized,
                SteamId = _token.SteamId,
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetStatus(Status with
            {
                State = SteamCloudAuthorizationState.AuthorizationRequired,
                LastExternalError = "",
            });
        }
        catch (Exception exception)
        {
            SetStatus(Status with
            {
                State = SteamCloudAuthorizationState.Failed,
                LastExternalError = exception.Message,
            });
        }
    }

    public void Clear()
    {
        var externalError = DiscardToken("");
        SetStatus(new SteamCloudAuthorizationStatus
        {
            State = _options.IsConfigured
                ? SteamCloudAuthorizationState.AuthorizationRequired
                : SteamCloudAuthorizationState.Unconfigured,
            SteamId = _currentSteamId,
            LastExternalError = externalError,
        });
    }

    private SteamCloudAuthorizationStatus ResolveStoredTokenStatus()
    {
        if (_token == null)
        {
            return new SteamCloudAuthorizationStatus
            {
                State = SteamCloudAuthorizationState.AuthorizationRequired,
                SteamId = _currentSteamId,
            };
        }

        if (_token.SteamId != _currentSteamId)
        {
            var externalError = DiscardToken("");
            return new SteamCloudAuthorizationStatus
            {
                State = SteamCloudAuthorizationState.AuthorizationRequired,
                SteamId = _currentSteamId,
                LastExternalError = externalError,
            };
        }

        return new SteamCloudAuthorizationStatus
        {
            State = SteamCloudAuthorizationState.Authorized,
            SteamId = _token.SteamId,
        };
    }

    private void SetStatus(SteamCloudAuthorizationStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(status);
    }

    private string DiscardToken(string externalError)
    {
        _token = null;
        try
        {
            _tokenStore.Clear();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return externalError.Length == 0
                ? exception.Message
                : externalError + Environment.NewLine + exception.Message;
        }
        return externalError;
    }
}

internal sealed class OAuthTokenStore
{
    private readonly string _path;

    public OAuthTokenStore(string path)
    {
        _path = path;
    }

    public StoredSteamOAuthToken Load()
    {
        if (!File.Exists(_path))
            return null;

        try
        {
            var token = DurabilityFiles.ReadJson<StoredSteamOAuthToken>(_path);
            if (token != null &&
                token.SchemaVersion == 1 &&
                token.AccessToken != null &&
                token.AccessToken.Length > 0 &&
                token.SteamId != 0)
                return token;
        }
        catch (JsonException)
        {
        }

        Clear();
        return null;
    }

    public void Save(StoredSteamOAuthToken token)
    {
        DurabilityFiles.WriteJson(
            _path,
            token,
            UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    public void Clear()
    {
        if (File.Exists(_path))
            File.Delete(_path);
    }
}
