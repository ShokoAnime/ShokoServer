using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Config;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Base interface for all airing schedule providers to implement. A provider
///   fetches schedules in its own jobs and pushes them through
///   <c>IAiringScheduleService</c>; the only call the core makes into a
///   provider is a refresh request.
/// </summary>
public interface IAiringScheduleProvider
{
    /// <summary>
    ///   Friendly name of the airing schedule provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///   Optional. Description of the airing schedule provider.
    /// </summary>
    string? Description { get => null; }

    /// <summary>
    ///   Version of the airing schedule provider.
    /// </summary>
    Version Version { get => GetType().Assembly.GetName().Version ?? new Version(0, 0, 0, 0); }

    /// <summary>
    ///   The kinds this provider can supply. Submitting a track of any other
    ///   kind is rejected, and only the kinds enabled in the settings are
    ///   visible to readers.
    /// </summary>
    IReadOnlySet<AiringKind> AvailableKinds { get; }

    /// <summary>
    ///   Refresh what this provider knows about the series. Any series may
    ///   arrive — a shoko series, an AniDB anime, a TMDB show — so handle what
    ///   is recognised and ignore the rest.
    /// </summary>
    /// <param name="series">
    ///   The series to refresh.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the refresh.
    /// </param>
    /// <returns>
    ///   <c>true</c> when the provider did work, or <c>false</c> when it had
    ///   nothing to do for the series.
    /// </returns>
    Task<bool> RefreshAsync(ISeries series, CancellationToken cancellationToken = default);

    /// <summary>
    ///   Optional. Refresh what this provider knows about the season. Defaults
    ///   to refreshing the season's series, so a provider that only refreshes
    ///   whole runs still answers.
    /// </summary>
    /// <param name="season">
    ///   The season to refresh.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the refresh.
    /// </param>
    /// <returns>
    ///   <c>true</c> when the provider did work, or <c>false</c> when it had
    ///   nothing to do for the season.
    /// </returns>
    Task<bool> RefreshAsync(ISeason season, CancellationToken cancellationToken = default)
        => season.Series is { } series ? RefreshAsync(series, cancellationToken) : Task.FromResult(false);

    /// <summary>
    ///   Optional. Refresh what this provider knows about the episode. Defaults
    ///   to refreshing the episode's series, so a provider that only refreshes
    ///   whole runs still answers.
    /// </summary>
    /// <param name="episode">
    ///   The episode to refresh.
    /// </param>
    /// <param name="cancellationToken">
    ///   Optional. A cancellation token for cancelling the refresh.
    /// </param>
    /// <returns>
    ///   <c>true</c> when the provider did work, or <c>false</c> when it had
    ///   nothing to do for the episode.
    /// </returns>
    Task<bool> RefreshAsync(IEpisode episode, CancellationToken cancellationToken = default)
        => episode.Series is { } series ? RefreshAsync(series, cancellationToken) : Task.FromResult(false);

    /// <summary>
    ///   Optional. How many refreshes the service runs in parallel for this
    ///   provider. Defaults to one at a time.
    /// </summary>
    int MaxConcurrentRefreshes { get => 1; }
}

/// <summary>
///   Indicates that the airing schedule provider supports configuration, and
///   which configuration type to display in the UI.
/// </summary>
/// <typeparam name="TConfiguration">
///   The airing schedule provider configuration type.
/// </typeparam>
public interface IAiringScheduleProvider<TConfiguration> : IAiringScheduleProvider where TConfiguration : IAiringScheduleProviderConfiguration { }

/// <summary>
///   Interface for signaling that the configuration is tied to an airing
///   schedule provider.
/// </summary>
public interface IAiringScheduleProviderConfiguration : IHiddenConfiguration { }
