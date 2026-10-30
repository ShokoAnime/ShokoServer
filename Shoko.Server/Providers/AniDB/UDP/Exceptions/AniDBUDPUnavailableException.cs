using System;

namespace Shoko.Server.Providers.AniDB.UDP.Exceptions;

/// <summary>
///   AniDB answered a UDP request with one of its "try again later" codes:
///   600 internal server error, 601 out of service, 602 server busy or 604
///   timeout. The request was not carried out and should be sent again once
///   the backoff has passed.
/// </summary>
[Serializable]
public class AniDBUDPUnavailableException : UnexpectedUDPResponseException
{
    /// <summary>
    ///   The backoff AniDB's answer started, after which the request may be
    ///   sent again.
    /// </summary>
    public TimeSpan RetryAfter { get; }

    public AniDBUDPUnavailableException(UDPReturnCode code, string response, string request, TimeSpan retryAfter)
        : base(code, response, request)
    {
        RetryAfter = retryAfter;
    }
}
