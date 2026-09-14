using System;

namespace Shoko.Abstractions.Metadata.Anilist;

/// <summary>
///   Snapshot of the AniList rate limiter's pause state.
/// </summary>
public sealed class AnilistRateLimitPauseStatus
{
    /// <summary>
    ///   Whether AniList requests are currently paused, either because the
    ///   upstream errored repeatedly or because the request quota is exhausted.
    /// </summary>
    public required bool IsPaused { get; init; }

    /// <summary>
    ///   Time remaining until the pause elapses, or <see langword="null"/> if not paused.
    /// </summary>
    public required TimeSpan? RemainingPauseTime { get; init; }

    /// <summary>
    ///   Requests remaining in the current quota window as last reported by
    ///   AniList, or <see langword="null"/> if no response has been seen yet.
    /// </summary>
    public required int? RemainingRequests { get; init; }
}
