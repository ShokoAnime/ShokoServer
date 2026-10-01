using System;

namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
///   Snapshot of the TMDB rate limiter's pause state, for a 429 or the 5XX circuit breaker.
/// </summary>
public sealed class TmdbRateLimitPauseStatus
{
    /// <summary>
    ///   Whether TMDB requests are currently paused due to rate limiting or upstream errors.
    /// </summary>
    public required bool IsPaused { get; init; }

    /// <summary>
    ///   Time remaining until the pause elapses, or <see langword="null"/> if not paused.
    /// </summary>
    public required TimeSpan? RemainingPauseTime { get; init; }
}
