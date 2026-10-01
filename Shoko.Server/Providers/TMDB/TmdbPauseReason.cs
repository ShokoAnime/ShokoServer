namespace Shoko.Server.Providers.TMDB;

/// <summary>
///   Why the TMDB jobs are paused.
/// </summary>
public enum TmdbPauseReason
{
    /// <summary>
    ///   They are not paused.
    /// </summary>
    None = 0,

    /// <summary>
    ///   TMDB answered a request with 429 and asked for a wait.
    /// </summary>
    RateLimited = 1,

    /// <summary>
    ///   TMDB answered with server errors and the circuit breaker tripped.
    /// </summary>
    ServerErrors = 2,
}
