using System;
using System.Threading.Tasks;

namespace Shoko.Abstractions.Metadata.Services;

/// <summary>
/// Provides supplementary metadata for a series after its primary (AniDB)
/// data has been confirmed or refreshed. Implementations are called
/// automatically by the server after <c>GetAniDBAnimeJob</c> completes.
/// Plugins implement this to add external data sources (e.g. TMDB, Anilist).
/// </summary>
public interface ISupplementaryMetadataProvider
{
    /// <summary>Display name for this provider.</summary>
    string Name { get; }

    /// <summary>Optional description.</summary>
    string? Description => null;

    /// <summary>Provider version, defaults to the assembly version.</summary>
    Version Version => GetType().Assembly.GetName().Version ?? new Version(0, 0, 0, 0);

    /// <summary>
    /// Called after AniDB data for <paramref name="anidbAnimeID"/> has been
    /// confirmed or refreshed. Schedule any supplementary work here
    /// (search, fetch, image downloads, etc.).
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="isNew">
    /// <c>true</c> if no <c>AnimeSeries</c> existed for this anime before
    /// this call, i.e. it is newly added to Shoko.
    /// </param>
    Task ScheduleForAnime(int anidbAnimeID, bool isNew);

    /// <summary>
    /// Called when a Shoko series is permanently removed. Clean up any
    /// supplementary data linked to the anime.
    /// </summary>
    Task OnSeriesRemoved(int anidbAnimeID) => Task.CompletedTask;
}

/// <summary>
/// Typed variant of <see cref="ISupplementaryMetadataProvider"/> for
/// providers that expose plugin-level configuration.
/// </summary>
public interface ISupplementaryMetadataProvider<TConfiguration> : ISupplementaryMetadataProvider
    where TConfiguration : ISupplementaryMetadataProviderConfiguration { }

/// <summary>Marker interface for supplementary provider configuration types.</summary>
public interface ISupplementaryMetadataProviderConfiguration { }
