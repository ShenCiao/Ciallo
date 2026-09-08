#nullable enable

using System;
using System.IO;
using System.Net;
using System.Net.Http;

namespace Steamworks.WebApi;

public class SteamWebApiException : HttpRequestException
{
	public int? EResult { get; }

	public SteamWebApiException(string message, int? eResult = null, HttpStatusCode? statusCode = null)
		: base(message, null, statusCode)
	{
		EResult = eResult;
	}
}

public sealed class SteamWebApiAuthorizationException : SteamWebApiException
{
	public SteamWebApiAuthorizationException(string message, int? eResult = null, HttpStatusCode? statusCode = null)
		: base(message, eResult, statusCode)
	{
	}
}

public sealed class SteamWebApiProtocolException : IOException
{
	public SteamWebApiProtocolException(string message, Exception? innerException = null)
		: base(message, innerException)
	{
	}
}
