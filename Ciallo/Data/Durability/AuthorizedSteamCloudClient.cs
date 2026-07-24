using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Steamworks.WebApi;

namespace Ciallo.Data;

internal sealed class AuthorizedSteamCloudClient
{
    private readonly SteamCloudWebApiClient _client;
    private readonly SteamCloudOptions _options;
    private readonly SteamCloudAuthorization _authorization;

    public AuthorizedSteamCloudClient(
        SteamCloudWebApiClient client,
        SteamCloudOptions options,
        SteamCloudAuthorization authorization)
    {
        _client = client;
        _options = options;
        _authorization = authorization;
    }

    public async Task<IReadOnlyList<SteamCloudFile>> EnumerateFilesAsync(
        CancellationToken cancellationToken = default)
    {
        var token = _authorization.GetAccessToken();
        try
        {
            return await _client.EnumerateAllUserFilesAsync(
                token,
                _options.AppId,
                cancellationToken: cancellationToken);
        }
        catch (SteamWebApiAuthorizationException exception)
        {
            _authorization.Reject(exception.Message);
            throw;
        }
    }

    public async Task ModifyBatchAsync(
        IReadOnlyList<SteamCloudUploadFile> uploads,
        IReadOnlyList<string> deletes,
        CancellationToken cancellationToken = default)
    {
        var token = _authorization.GetAccessToken();
        try
        {
            await _client.ModifyBatchAsync(
                token,
                _options.AppId,
                Environment.MachineName,
                uploads,
                deletes,
                cancellationToken);
        }
        catch (SteamWebApiAuthorizationException exception)
        {
            _authorization.Reject(exception.Message);
            throw;
        }
    }
}
