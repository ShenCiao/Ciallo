#nullable enable

using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Steamworks.WebApi;

public sealed class SteamOAuthLoopbackReceiver : IAsyncDisposable
{
	private const int MaximumRequestBytes = 16 * 1024;

	private readonly string _expectedState;
	private readonly string _callbackPath;
	private readonly string _tokenPath;
	private readonly string _callbackPage;
	private readonly string _successPage;
	private readonly TcpListener _listener;

	public SteamOAuthLoopbackReceiver(Uri redirectUri, string expectedState, string applicationName)
	{
		_expectedState = expectedState;
		_callbackPath = redirectUri.AbsolutePath;
		_tokenPath = _callbackPath.TrimEnd('/') + "/token";
		_listener = new TcpListener(IPAddress.Loopback, redirectUri.Port);

		var title = WebUtility.HtmlEncode(applicationName);
		var tokenPath = JsonSerializer.Serialize(_tokenPath);
		_callbackPage = "<!doctype html><meta charset=\"utf-8\"><title>" + title + "</title>" +
			"<p>Completing Steam authorization...</p><script>" +
			"const values=new URLSearchParams(location.hash.substring(1));" +
			"fetch(" + tokenPath + ",{method:'POST',headers:{'Content-Type':'application/json'}," +
			"body:JSON.stringify({accessToken:values.get('access_token')||'',state:values.get('state')||'',error:values.get('error')||''})})" +
			".then(()=>document.body.textContent='Steam authorization completed. You can close this tab.')" +
			".catch(()=>document.body.textContent='Steam authorization failed. Return to the application and try again.');" +
			"</script>";
		_successPage = "<!doctype html><meta charset=\"utf-8\"><title>" + title +
			"</title><p>Steam authorization completed. You can close this tab.</p>";
	}

	public void Start() => _listener.Start(4);

	public async Task<string> WaitForTokenAsync(CancellationToken cancellationToken = default)
	{
		while (true)
		{
			using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
			var request = await ReadRequestAsync(client.GetStream(), cancellationToken);
			if (request.Method == "GET" && request.Path == _callbackPath)
			{
				await WriteResponseAsync(
					client.GetStream(),
					"200 OK",
					"text/html; charset=utf-8",
					_callbackPage,
					cancellationToken);
				continue;
			}

			if (request.Method == "POST" && request.Path == _tokenPath)
			{
				var callback = JsonSerializer.Deserialize<OAuthCallbackPayload>(
					request.Body,
					new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
				if (callback.State != _expectedState)
					throw new SteamOAuthException("Steam OAuth state did not match the authorization request.");
				if (callback.Error.Length > 0)
					throw new SteamOAuthException("Steam OAuth authorization was denied.");
				if (callback.AccessToken.Length == 0)
					throw new SteamOAuthException("Steam OAuth callback did not contain an access token.");

				await WriteResponseAsync(
					client.GetStream(),
					"200 OK",
					"text/html; charset=utf-8",
					_successPage,
					cancellationToken);
				return callback.AccessToken;
			}

			await WriteResponseAsync(
				client.GetStream(),
				"404 Not Found",
				"text/plain; charset=utf-8",
				"Not found",
				cancellationToken);
		}
	}

	public ValueTask DisposeAsync()
	{
		_listener.Stop();
		return ValueTask.CompletedTask;
	}

	private static async Task<HttpRequestData> ReadRequestAsync(
		NetworkStream stream,
		CancellationToken cancellationToken)
	{
		var rented = ArrayPool<byte>.Shared.Rent(MaximumRequestBytes);
		try
		{
			var length = 0;
			var headerEnd = -1;
			while (headerEnd < 0)
			{
				var read = await stream.ReadAsync(rented.AsMemory(length, rented.Length - length), cancellationToken);
				if (read == 0)
					throw new SteamOAuthException("OAuth callback closed before sending an HTTP request.");
				length += read;
				if (length == rented.Length)
					throw new SteamOAuthException("OAuth callback request was too large.");
				headerEnd = FindHeaderEnd(rented.AsSpan(0, length));
			}

			var headerText = Encoding.ASCII.GetString(rented, 0, headerEnd);
			var headerLines = headerText.Split("\r\n", StringSplitOptions.None);
			var requestLine = headerLines[0].Split(' ', 3);
			if (requestLine.Length != 3)
				throw new SteamOAuthException("OAuth callback sent an invalid HTTP request line.");

			var contentLength = 0;
			foreach (var line in headerLines)
			{
				if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
					contentLength = int.Parse(line[15..].Trim(), CultureInfo.InvariantCulture);
			}
			if (contentLength < 0 || contentLength > MaximumRequestBytes - headerEnd - 4)
				throw new SteamOAuthException("OAuth callback body was too large.");

			var bodyOffset = headerEnd + 4;
			while (length - bodyOffset < contentLength)
			{
				var read = await stream.ReadAsync(rented.AsMemory(length, rented.Length - length), cancellationToken);
				if (read == 0)
					throw new SteamOAuthException("OAuth callback closed before sending its body.");
				length += read;
			}
			return new HttpRequestData(
				requestLine[0],
				requestLine[1],
				Encoding.UTF8.GetString(rented, bodyOffset, contentLength));
		}
		finally
		{
			ArrayPool<byte>.Shared.Return(rented);
		}
	}

	private static int FindHeaderEnd(ReadOnlySpan<byte> bytes)
	{
		for (var i = 0; i <= bytes.Length - 4; i++)
		{
			if (bytes[i] == '\r' && bytes[i + 1] == '\n' && bytes[i + 2] == '\r' && bytes[i + 3] == '\n')
				return i;
		}
		return -1;
	}

	private static async Task WriteResponseAsync(
		NetworkStream stream,
		string status,
		string contentType,
		string content,
		CancellationToken cancellationToken)
	{
		var body = Encoding.UTF8.GetBytes(content);
		var headers = Encoding.ASCII.GetBytes(
			"HTTP/1.1 " + status + "\r\n" +
			"Content-Type: " + contentType + "\r\n" +
			"Content-Length: " + body.Length + "\r\n" +
			"Cache-Control: no-store\r\n" +
			"Content-Security-Policy: default-src 'none'; script-src 'unsafe-inline'; connect-src 'self'; base-uri 'none'; form-action 'none'\r\n" +
			"Referrer-Policy: no-referrer\r\n" +
			"X-Content-Type-Options: nosniff\r\n" +
			"Connection: close\r\n\r\n");
		await stream.WriteAsync(headers, cancellationToken);
		await stream.WriteAsync(body, cancellationToken);
	}

	private sealed record HttpRequestData(string Method, string Path, string Body);

	private sealed record OAuthCallbackPayload
	{
		public string AccessToken { get; init; } = string.Empty;
		public string State { get; init; } = string.Empty;
		public string Error { get; init; } = string.Empty;
	}
}
