using System;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Metadata.Services;

/// <summary>
/// Provides supplementary metadata for a series from a source other than
/// AniDB (e.g. TMDB, AniList). The server asks every provider to schedule its
/// work whenever an anime is refreshed from AniDB or a file is linked to one.
/// </summary>
public interface ISupplementaryMetadataProvider
{
    /// <summary>
    /// Display name for this provider.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Optional description.
    /// </summary>
    string? Description => null;

    /// <summary>
    /// Provider version, defaults to the assembly version.
    /// </summary>
    Version Version => GetType().Assembly.GetName().Version ?? new Version(0, 0, 0, 0);

    /// <summary>
    /// Schedule any supplementary work for <paramref name="anidbAnimeID"/>
    /// here (search, fetch, image downloads, etc.). Queue it rather than doing
    /// it inline, since the caller waits for every provider in turn.
    /// </summary>
    /// <remarks>
    /// Called from inside an AniDB refresh, while the refresh job is still
    /// running, unless the refresh was asked to skip supplementary updates. It
    /// is also called when a release links a file to the anime, and that call
    /// comes before the AniDB data is fetched if it isn't cached yet, so don't
    /// assume the anime or its series exist. The same anime can be asked about
    /// more than once in quick succession: a refresh that creates the series
    /// calls with <paramref name="isNew"/> <c>true</c> and then again with
    /// <c>false</c> when it finishes.
    /// </remarks>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="isNew">
    /// <c>true</c> when the call comes from the refresh that just created the
    /// Shoko series for this anime.
    /// </param>
    Task ScheduleForAnime(int anidbAnimeID, bool isNew);

    /// <summary>
    /// Meant to be called when a Shoko series is permanently removed, to clean
    /// up any supplementary data linked to the anime. Nothing in the server
    /// calls it yet, so don't rely on it running.
    /// </summary>
    Task OnSeriesRemoved(int anidbAnimeID) => Task.CompletedTask;
}

/// <summary>
/// Typed variant of <see cref="ISupplementaryMetadataProvider"/> for
/// providers that expose plugin-level configuration.
/// </summary>
public interface ISupplementaryMetadataProvider<TConfiguration> : ISupplementaryMetadataProvider
    where TConfiguration : ISupplementaryMetadataProviderConfiguration { }

/// <summary>
/// Marker interface for supplementary provider configuration types.
/// </summary>
public interface ISupplementaryMetadataProviderConfiguration { }
