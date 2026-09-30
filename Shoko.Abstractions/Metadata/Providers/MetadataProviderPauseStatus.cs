using System;

namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   Whether a provider can take work right now, and if not, why and until
///   when.
/// </summary>
/// <remarks>
///   What <see cref="IPausableMetadataProvider.PauseStatus"/> reports, and what
///   <see cref="Services.IMetadataRefreshService.GetPauseStatus"/> hands on to
///   the queue and the API so they can say why a source's jobs are waiting.
/// </remarks>
public sealed record MetadataProviderPauseStatus
{
    /// <summary>
    ///   The status of a provider that can take work.
    /// </summary>
    public static MetadataProviderPauseStatus NotPaused { get; } = new() { IsPaused = false };

    /// <summary>
    ///   Whether the provider cannot take work right now.
    /// </summary>
    public required bool IsPaused { get; init; }

    /// <summary>
    ///   Why the provider cannot take work, in words a person can read, such as
    ///   "The source answered with server errors.", or <see langword="null"/>
    ///   when it is not paused or gives no reason.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    ///   When the provider expects to take work again, in UTC, or
    ///   <see langword="null"/> when it is not paused or does not know.
    /// </summary>
    public DateTime? ResumesAt { get; init; }

    /// <summary>
    ///   How long until <see cref="ResumesAt"/>, counted from now and never
    ///   below zero.
    /// </summary>
    /// <returns>
    ///   The time left, or <see langword="null"/> when there is no
    ///   <see cref="ResumesAt"/>.
    /// </returns>
    public TimeSpan? GetRemainingPauseTime()
        => ResumesAt is { } resumesAt
            ? resumesAt - DateTime.UtcNow is { Ticks: > 0 } remaining ? remaining : TimeSpan.Zero
            : null;
}
