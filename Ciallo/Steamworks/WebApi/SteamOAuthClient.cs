#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Steamworks.WebApi;

public sealed record SteamOAuthTokenDetails(
	ulong SteamId,
	string ClientId,
	TimeSpan? ExpiresIn,
	IReadOnlyList<string> Scopes);

public sealed class SteamOAuthException : Exception
{
	public SteamOAuthException(string message, Exception? innerException = null)
		: base(message, innerException)
	{
	}
}

public sealed class SteamOAuthClient
{
	public static readonly Uri DefaultAuthorizationEndpoint = new("https://steamcommunity.com/oauth/login");

	private readonly HttpClient _httpClient;
	private readonly Uri _webApiBaseUri;
	private readonly Uri _authorizationEndpoint;

	public SteamOAuthClient(
		HttpClient httpClient,
		Uri? webApiBaseUri = null,
		Uri? authorizationEndpoint = null)
	{
		_httpClient = httpClient;
		_webApiBaseUri = webApiBaseUri ?? SteamCloudWebApiClient.DefaultBaseUri;
		_authorizationEndpoint = authorizationEndpoint ?? DefaultAuthorizationEndpoint;
	}

	public static string CreateState()
		=> Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

	public Uri CreateAuthorizationUri(string clientId, string state)
		=> new UriBuilder(_authorizationEndpoint)
		{
			Query = "response_type=token" +
					"&client_id=" + Uri.EscapeDataString(clientId) +
					"&state=" + Uri.EscapeDataString(state),
		}.Uri;

	public async Task<SteamOAuthTokenDetails> GetTokenDetailsAsync(
		string accessToken,
		CancellationToken cancellationToken = default)
	{
		var uri = new Uri(
			_webApiBaseUri,
			"ISteamUserOAuth/GetTokenDetails/v1/?access_token=" + Uri.EscapeDataString(accessToken));
		using var response = await _httpClient.GetAsync(uri, cancellationToken);
		if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
			throw new SteamWebApiAuthorizationException(
				"Steam OAuth token was rejected.",
				statusCode: response.StatusCode);
		response.EnsureSuccessStatusCode();

		await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
		try
		{
			using var json = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
			var payload = json.RootElement.TryGetProperty("response", out var wrapped)
				? wrapped
				: json.RootElement;
			if (payload.TryGetProperty("token", out var token))
				payload = token;

			TimeSpan? expiresIn = payload.TryGetProperty("expires_in", out var expires)
				? TimeSpan.FromSeconds(ReadInt64(expires))
				: null;
			if (expiresIn <= TimeSpan.Zero)
				throw new SteamOAuthException("Steam OAuth returned an expired token.");

			return new SteamOAuthTokenDetails(
				ReadUInt64(payload.GetProperty("steamid")),
				payload.TryGetProperty("client_id", out var clientId) ? ReadString(clientId) : string.Empty,
				expiresIn,
				payload.TryGetProperty("scope", out var scope)
					? ReadScopes(scope)
					: Array.Empty<string>());
		}
		catch (SteamOAuthException)
		{
			throw;
		}
		catch (Exception exception) when (exception is JsonException or
											KeyNotFoundException or
											InvalidOperationException or
											FormatException or
											OverflowException)
		{
			throw new SteamOAuthException("Steam OAuth returned invalid token details.", exception);
		}
	}

	private static IReadOnlyList<string> ReadScopes(JsonElement value)
		=> value.ValueKind == JsonValueKind.Array
			? value.EnumerateArray().Select(ReadString).ToArray()
			: ReadString(value).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

	private static string ReadString(JsonElement value)
		=> value.ValueKind == JsonValueKind.String
			? value.GetString()!
			: value.GetRawText();

	private static long ReadInt64(JsonElement value)
		=> value.ValueKind == JsonValueKind.String
			? long.Parse(value.GetString()!, CultureInfo.InvariantCulture)
			: value.GetInt64();

	private static ulong ReadUInt64(JsonElement value)
		=> value.ValueKind == JsonValueKind.String
			? ulong.Parse(value.GetString()!, CultureInfo.InvariantCulture)
			: value.GetUInt64();
}
