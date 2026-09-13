#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace Steamworks.WebApi;

public static class SteamCloudPlatforms
{
	public const string All = "all";
	public const string Windows = "windows";
	public const string MacOS = "macos";
	public const string Linux = "linux";
	public const string Android = "android";
	public const string IPhoneOS = "iphoneos";
	public const string Switch = "switch";
}

public enum SteamCloudBatchResult : uint
{
	Success = 1,
	Failure = 2,
}

public sealed record SteamCloudFile(
	uint AppId,
	ulong UgcId,
	string FileName,
	ulong Timestamp,
	uint FileSize,
	Uri? DownloadUri,
	ulong SteamIdCreator,
	uint Flags,
	IReadOnlyList<string> PlatformsToSync,
	string? Sha1);

public sealed record SteamCloudFilePage(
	IReadOnlyList<SteamCloudFile> Files,
	uint TotalFiles);

public sealed record SteamCloudUploadBatch(
	ulong BatchId,
	ulong AppChangeNumber);

public sealed record SteamCloudHttpHeader(string Name, string Value);

public sealed record SteamCloudHttpUploadTarget(
	ulong UgcId,
	ulong Timestamp,
	string UrlHost,
	string UrlPath,
	bool UseHttps,
	IReadOnlyList<SteamCloudHttpHeader> RequestHeaders)
{
	public Uri UploadUri => new((UseHttps ? Uri.UriSchemeHttps : Uri.UriSchemeHttp) + "://" + UrlHost + UrlPath);
}

public sealed class SteamCloudUploadFile
{
	private static readonly IReadOnlyList<string> DefaultPlatforms = new[] { SteamCloudPlatforms.All };
	private readonly Func<Stream> _openRead;

	public string FileName { get; }
	public uint ByteLength { get; }
	public string Sha1 { get; }
	public bool IsPublic { get; }
	public IReadOnlyList<string> PlatformsToSync { get; }

	public SteamCloudUploadFile(
		string fileName,
		byte[] bytes,
		IReadOnlyList<string>? platformsToSync = null,
		bool isPublic = false)
		: this(
			fileName,
			checked((uint)bytes.Length),
			Convert.ToHexString(SHA1.HashData(bytes)).ToLowerInvariant(),
			() => new MemoryStream(bytes, writable: false),
			platformsToSync,
			isPublic)
	{
	}

	public SteamCloudUploadFile(
		string fileName,
		int byteLength,
		string sha1,
		Func<byte[]> readBytes,
		IReadOnlyList<string>? platformsToSync = null,
		bool isPublic = false)
		: this(
			fileName,
			checked((uint)byteLength),
			sha1,
			() => new MemoryStream(readBytes(), writable: false),
			platformsToSync,
			isPublic)
	{
	}

	public SteamCloudUploadFile(
		string fileName,
		uint byteLength,
		string sha1,
		Func<Stream> openRead,
		IReadOnlyList<string>? platformsToSync = null,
		bool isPublic = false)
	{
		FileName = fileName;
		ByteLength = byteLength;
		Sha1 = sha1;
		_openRead = openRead;
		PlatformsToSync = platformsToSync ?? DefaultPlatforms;
		IsPublic = isPublic;
	}

	internal HttpContent CreateContent()
	{
		var content = new StreamContent(_openRead());
		content.Headers.ContentLength = ByteLength;
		content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
		return content;
	}
}
