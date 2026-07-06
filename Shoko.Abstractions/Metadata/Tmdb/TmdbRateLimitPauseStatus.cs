using System;

namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
///   Snapshot of the TMDB rate limiter's 5XX circuit-breaker pause state.
/// </summary>
public sealed class TmdbRateLimitPauseStatus
{
    /// <summary>
    ///   Whether TMDB requests are currently paused due to upstream errors.
    /// </summary>
    public required bool IsPaused { get; init; }

    /// <summary>
    ///   Time remaining until the pause elapses, or <see langword="null"/> if not paused.
    /// </summary>
    public required TimeSpan? RemainingPauseTime { get; init; }
}
