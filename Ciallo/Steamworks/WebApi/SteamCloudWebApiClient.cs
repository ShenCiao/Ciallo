#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Steamworks.WebApi;

public sealed class SteamCloudWebApiClient
{
	private const uint DefaultPageSize = 500;
	private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(10);

	public static readonly Uri DefaultBaseUri = new("https://api.steampowered.com/");

	private readonly HttpClient _httpClient;
	private readonly Uri _baseUri;

	public SteamCloudWebApiClient(HttpClient httpClient, Uri? baseUri = null)
	{
		_httpClient = httpClient;
		_baseUri = baseUri ?? DefaultBaseUri;
	}

	public async Task<SteamCloudFilePage> EnumerateUserFilesAsync(
		string accessToken,
		uint appId,
		bool extendedDetails = false,
		uint count = DefaultPageSize,
		uint startIndex = 0,
		CancellationToken cancellationToken = default)
	{
		var uri = new Uri(
			_baseUri,
			"ICloudService/EnumerateUserFiles/v1/?access_token=" + Uri.EscapeDataString(accessToken) +
			"&appid=" + appId.ToString(CultureInfo.InvariantCulture) +
			"&extended_details=" + (extendedDetails ? "1" : "0") +
			"&count=" + count.ToString(CultureInfo.InvariantCulture) +
			"&start_index=" + startIndex.ToString(CultureInfo.InvariantCulture));
		using var response = await _httpClient.GetAsync(uri, cancellationToken);
		EnsureSuccess(response);
		using var json = await ParseResponseAsync(response, cancellationToken);
		return ReadProtocolValue("EnumerateUserFiles", () =>
		{
			var payload = Unwrap(json.RootElement);
			var files = payload.GetProperty("files")
				.EnumerateArray()
				.Select(ReadCloudFile)
				.ToArray();
			return new SteamCloudFilePage(
				files,
				ReadUInt32(payload.GetProperty("total_files")));
		});
	}

	public async Task<IReadOnlyList<SteamCloudFile>> EnumerateAllUserFilesAsync(
		string accessToken,
		uint appId,
		bool extendedDetails = true,
		uint pageSize = DefaultPageSize,
		CancellationToken cancellationToken = default)
	{
		var files = new List<SteamCloudFile>();
		var names = new HashSet<string>(StringComparer.Ordinal);
		uint totalFiles;
		do
		{
			var page = await EnumerateUserFilesAsync(
				accessToken,
				appId,
				extendedDetails,
				pageSize,
				checked((uint)files.Count),
				cancellationToken);
			totalFiles = page.TotalFiles;
			foreach (var file in page.Files)
			{
				if (!names.Add(file.FileName))
					throw new SteamWebApiProtocolException($"Steam Cloud returned duplicate file {file.FileName}.");
			}
			if (page.Files.Count == 0 && files.Count < totalFiles)
				throw new SteamWebApiProtocolException("Steam Cloud file enumeration did not advance.");
			files.AddRange(page.Files);
		} while (files.Count < totalFiles);

		return files;
	}

	public Task<byte[]> DownloadFileAsync(
		SteamCloudFile file,
		CancellationToken cancellationToken = default)
	{
		if (file.DownloadUri is null)
			throw new InvalidOperationException("The Steam Cloud file has no download URL. Enumerate it with extended details.");
		return DownloadFileAsync(file.DownloadUri, file.Sha1, cancellationToken);
	}

	public async Task<byte[]> DownloadFileAsync(
		Uri downloadUri,
		string? expectedSha1 = null,
		CancellationToken cancellationToken = default)
	{
		using var response = await _httpClient.GetAsync(downloadUri, cancellationToken);
		response.EnsureSuccessStatusCode();
		var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
		if (expectedSha1 is not null)
		{
			var actualSha1 = Convert.ToHexString(SHA1.HashData(bytes)).ToLowerInvariant();
			if (!actualSha1.Equals(expectedSha1, StringComparison.OrdinalIgnoreCase))
				throw new SteamWebApiProtocolException("The Steam Cloud download failed its SHA-1 check.");
		}
		return bytes;
	}

	public async Task<SteamCloudUploadBatch> BeginAppUploadBatchAsync(
		string accessToken,
		uint appId,
		string machineName,
		IReadOnlyList<string> filesToUpload,
		IReadOnlyList<string> filesToDelete,
		CancellationToken cancellationToken = default)
	{
		using var response = await PostAsync(
			"ICloudService/BeginAppUploadBatch/v1/",
			accessToken,
			new
			{
				appid = appId,
				machine_name = machineName,
				files_to_upload = filesToUpload,
				files_to_delete = filesToDelete,
			},
			cancellationToken);
		using var json = await ParseResponseAsync(response, cancellationToken);
		return ReadProtocolValue("BeginAppUploadBatch", () =>
		{
			var payload = Unwrap(json.RootElement);
			return new SteamCloudUploadBatch(
				ReadUInt64(payload.GetProperty("batch_id")),
				ReadUInt64(payload.GetProperty("app_change_number")));
		});
	}

	public async Task CompleteAppUploadBatchAsync(
		string accessToken,
		uint appId,
		ulong batchId,
		SteamCloudBatchResult batchResult,
		CancellationToken cancellationToken = default)
	{
		using var response = await PostAsync(
			"ICloudService/CompleteAppUploadBatch/v1/",
			accessToken,
			new
			{
				appid = appId,
				batch_id = batchId.ToString(CultureInfo.InvariantCulture),
				batch_eresult = (uint)batchResult,
			},
			cancellationToken);
	}

	public async Task<SteamCloudHttpUploadTarget> BeginHttpUploadAsync(
		string accessToken,
		uint appId,
		uint fileSize,
		string fileName,
		string sha1,
		bool isPublic,
		IReadOnlyList<string> platformsToSync,
		ulong uploadBatchId,
		CancellationToken cancellationToken = default)
	{
		using var response = await PostAsync(
			"ICloudService/BeginHTTPUpload/v1/",
			accessToken,
			new
			{
				appid = appId,
				file_size = fileSize,
				filename = fileName,
				file_sha = sha1,
				is_public = isPublic,
				platforms_to_sync = platformsToSync,
				upload_batch_id = uploadBatchId.ToString(CultureInfo.InvariantCulture),
			},
			cancellationToken);
		using var json = await ParseResponseAsync(response, cancellationToken);
		return ReadProtocolValue("BeginHTTPUpload", () =>
		{
			var payload = Unwrap(json.RootElement);
			var headers = payload.GetProperty("request_headers")
				.EnumerateArray()
				.Select(header => new SteamCloudHttpHeader(
					header.GetProperty("name").GetString()!,
					header.GetProperty("value").GetString()!))
				.ToArray();
			return new SteamCloudHttpUploadTarget(
				ReadUInt64(payload.GetProperty("ugcid")),
				ReadUInt64(payload.GetProperty("timestamp")),
				payload.GetProperty("url_host").GetString()!,
				payload.GetProperty("url_path").GetString()!,
				ReadBoolean(payload.GetProperty("use_https")),
				headers);
		});
	}

	public async Task<bool> PutHttpUploadAsync(
		SteamCloudHttpUploadTarget target,
		HttpContent content,
		CancellationToken cancellationToken = default)
	{
		using var request = new HttpRequestMessage(HttpMethod.Put, target.UploadUri) { Content = content };
		foreach (var header in target.RequestHeaders)
		{
			if (!request.Headers.TryAddWithoutValidation(header.Name, header.Value) &&
				!content.Headers.TryAddWithoutValidation(header.Name, header.Value))
				throw new SteamWebApiProtocolException($"Steam Cloud returned invalid upload header {header.Name}.");
		}
		using var response = await _httpClient.SendAsync(request, cancellationToken);
		return response.IsSuccessStatusCode;
	}

	public async Task<bool> CommitHttpUploadAsync(
		string accessToken,
		uint appId,
		bool transferSucceeded,
		string fileName,
		string sha1,
		CancellationToken cancellationToken = default)
	{
		using var response = await PostAsync(
			"ICloudService/CommitHTTPUpload/v1/",
			accessToken,
			new
			{
				appid = appId,
				transfer_succeeded = transferSucceeded,
				filename = fileName,
				file_sha = sha1,
			},
			cancellationToken);
		using var json = await ParseResponseAsync(response, cancellationToken);
		return ReadProtocolValue(
			"CommitHTTPUpload",
			() => ReadBoolean(Unwrap(json.RootElement).GetProperty("file_committed")));
	}

	public async Task DeleteAsync(
		string accessToken,
		uint appId,
		string fileName,
		CancellationToken cancellationToken = default)
	{
		using var response = await PostAsync(
			"ICloudService/Delete/v1/",
			accessToken,
			new { appid = appId, filename = fileName },
			cancellationToken);
	}

	public async Task ModifyBatchAsync(
		string accessToken,
		uint appId,
		string machineName,
		IReadOnlyList<SteamCloudUploadFile> uploads,
		IReadOnlyList<string> deletes,
		CancellationToken cancellationToken = default)
	{
		if (uploads.Count == 0 && deletes.Count == 0)
			return;

		var batch = await BeginAppUploadBatchAsync(
			accessToken,
			appId,
			machineName,
			uploads.Select(upload => upload.FileName).ToArray(),
			deletes,
			cancellationToken);
		var succeeded = false;
		try
		{
			foreach (var upload in uploads)
				await UploadFileAsync(accessToken, appId, batch.BatchId, upload, cancellationToken);
			foreach (var fileName in deletes)
				await DeleteAsync(accessToken, appId, fileName, cancellationToken);
			succeeded = true;
		}
		finally
		{
			using var cleanup = new CancellationTokenSource(CleanupTimeout);
			await CompleteAppUploadBatchAsync(
				accessToken,
				appId,
				batch.BatchId,
				succeeded ? SteamCloudBatchResult.Success : SteamCloudBatchResult.Failure,
				cleanup.Token);
		}
	}

	private async Task UploadFileAsync(
		string accessToken,
		uint appId,
		ulong batchId,
		SteamCloudUploadFile upload,
		CancellationToken cancellationToken)
	{
		var target = await BeginHttpUploadAsync(
			accessToken,
			appId,
			upload.ByteLength,
			upload.FileName,
			upload.Sha1,
			upload.IsPublic,
			upload.PlatformsToSync,
			batchId,
			cancellationToken);

		var transferSucceeded = false;
		Exception? transferException = null;
		try
		{
			using var content = upload.CreateContent();
			transferSucceeded = await PutHttpUploadAsync(target, content, cancellationToken);
		}
		catch (Exception exception) when (exception is HttpRequestException or
											IOException or
											UnauthorizedAccessException or
											OperationCanceledException)
		{
			transferException = exception;
		}

		using var cleanup = new CancellationTokenSource(CleanupTimeout);
		var committed = await CommitHttpUploadAsync(
			accessToken,
			appId,
			transferSucceeded,
			upload.FileName,
			upload.Sha1,
			cleanup.Token);
		if (transferException is not null)
			ExceptionDispatchInfo.Capture(transferException).Throw();
		if (!transferSucceeded || !committed)
			throw new SteamWebApiException($"Steam Cloud did not commit {upload.FileName}.");
	}

	private async Task<HttpResponseMessage> PostAsync(
		string relativeUri,
		string accessToken,
		object input,
		CancellationToken cancellationToken)
	{
		using var form = new FormUrlEncodedContent(new Dictionary<string, string>
		{
			["access_token"] = accessToken,
			["input_json"] = JsonSerializer.Serialize(input),
		});
		var response = await _httpClient.PostAsync(new Uri(_baseUri, relativeUri), form, cancellationToken);
		try
		{
			EnsureSuccess(response);
			return response;
		}
		catch
		{
			response.Dispose();
			throw;
		}
	}

	private static SteamCloudFile ReadCloudFile(JsonElement file)
	{
		var downloadUri = file.TryGetProperty("url", out var urlValue) && urlValue.GetString() is { Length: > 0 } url
			? new Uri(url)
			: null;
		if (downloadUri is not null && downloadUri.Scheme != Uri.UriSchemeHttps)
			throw new UriFormatException("Steam Cloud returned a non-HTTPS download URL.");

		var sha1 = file.TryGetProperty("file_sha", out var shaValue) && shaValue.GetString() is { Length: > 0 } hash
			? hash.ToLowerInvariant()
			: null;
		if (sha1 is not null && (sha1.Length != 40 || !sha1.All(Uri.IsHexDigit)))
			throw new FormatException("Steam Cloud returned an invalid SHA-1 digest.");

		return new SteamCloudFile(
			ReadUInt32(file.GetProperty("appid")),
			ReadUInt64(file.GetProperty("ugcid")),
			file.GetProperty("filename").GetString()!,
			ReadUInt64(file.GetProperty("timestamp")),
			ReadUInt32(file.GetProperty("file_size")),
			downloadUri,
			ReadUInt64(file.GetProperty("steamid_creator")),
			ReadUInt32(file.GetProperty("flags")),
			file.TryGetProperty("platforms_to_sync", out var platforms)
				? ReadStringList(platforms)
				: Array.Empty<string>(),
			sha1);
	}

	private static IReadOnlyList<string> ReadStringList(JsonElement value)
	{
		if (value.ValueKind == JsonValueKind.Array)
			return value.EnumerateArray().Select(item => item.GetString()!).ToArray();
		if (value.ValueKind == JsonValueKind.String)
			return value.GetString()!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		throw new InvalidOperationException("Steam WebAPI value was not a string list.");
	}

	private static void EnsureSuccess(HttpResponseMessage response)
	{
		if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
			throw new SteamWebApiAuthorizationException(
				"Steam WebAPI authorization was rejected.",
				statusCode: response.StatusCode);
		if (!response.IsSuccessStatusCode)
			throw new SteamWebApiException(
				$"Steam WebAPI returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}.",
				statusCode: response.StatusCode);
		if (!response.Headers.TryGetValues("x-eresult", out var values))
			return;

		var resultValues = values.ToArray();
		if (resultValues.Length != 1 ||
			!int.TryParse(resultValues[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
			throw new SteamWebApiProtocolException("Steam WebAPI returned an invalid EResult header.");
		if (result == (int)Result.AccessDenied)
			throw new SteamWebApiAuthorizationException("Steam WebAPI authorization was rejected.", result);
		if (result != (int)Result.OK)
			throw new SteamWebApiException($"Steam WebAPI returned EResult {result}.", result);
	}

	private static async Task<JsonDocument> ParseResponseAsync(
		HttpResponseMessage response,
		CancellationToken cancellationToken)
	{
		await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
		try
		{
			return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
		}
		catch (JsonException exception)
		{
			throw new SteamWebApiProtocolException("Steam WebAPI returned invalid JSON.", exception);
		}
	}

	private static JsonElement Unwrap(JsonElement root)
		=> root.TryGetProperty("response", out var response) ? response : root;

	private static bool ReadBoolean(JsonElement value)
		=> value.ValueKind switch
		{
			JsonValueKind.True => true,
			JsonValueKind.False => false,
			JsonValueKind.Number => value.GetInt32() != 0,
			JsonValueKind.String => value.GetString() is "1" or "true" or "True",
			_ => throw new InvalidOperationException("Steam WebAPI value was not a boolean."),
		};

	private static uint ReadUInt32(JsonElement value)
		=> value.ValueKind == JsonValueKind.String
			? uint.Parse(value.GetString()!, CultureInfo.InvariantCulture)
			: value.GetUInt32();

	private static ulong ReadUInt64(JsonElement value)
		=> value.ValueKind == JsonValueKind.String
			? ulong.Parse(value.GetString()!, CultureInfo.InvariantCulture)
			: value.GetUInt64();

	private static T ReadProtocolValue<T>(string operation, Func<T> read)
	{
		try
		{
			return read();
		}
		catch (Exception exception) when (exception is KeyNotFoundException or
											InvalidOperationException or
											FormatException or
											OverflowException or
											ArgumentOutOfRangeException or
											UriFormatException)
		{
			throw new SteamWebApiProtocolException($"Steam WebAPI returned invalid {operation} data.", exception);
		}
	}
}
