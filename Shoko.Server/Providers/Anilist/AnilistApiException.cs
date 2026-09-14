using System;
using System.Collections.Generic;
using System.Net;

#nullable enable
namespace Shoko.Server.Providers.Anilist;

/// <summary>
/// Thrown when the AniList API returns an error we can't recover from within
/// the request, such as a malformed query, an exhausted retry budget or a
/// non-transient HTTP failure.
/// </summary>
public class AnilistApiException : Exception
{
    /// <summary>
    /// The HTTP status code of the response, if the failure was HTTP-level.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// The GraphQL error messages, if the failure was reported in the response body.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Whether retrying later is likely to succeed.
    /// </summary>
    public bool IsTransient { get; }

    public AnilistApiException(string message, HttpStatusCode? statusCode = null, IReadOnlyList<string>? errors = null, bool isTransient = false, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Errors = errors ?? [];
        IsTransient = isTransient;
    }
}
