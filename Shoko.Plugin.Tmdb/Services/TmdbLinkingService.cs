using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Matching;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Plugin.Tmdb.Mapping;

namespace Shoko.Plugin.Tmdb.Services;

/// <summary>
///   Lines an anime's episodes up with a TMDB show's. The links are the
///   core's, written through its linking service; this picks the candidates.
/// </summary>
/// <remarks>
///   The core's matching engine lines the episodes up by air date and title
///   within TMDB's seasons, over the episodes the core's series store holds:
///   the whole show, one season with the specials, or one group of an
///   ordering, less those other anime are linked to when they count.
/// </remarks>
/// <param name="seriesStore">The core's store of shows.</param>
/// <param name="crossReferences">The core's store of links.</param>
/// <param name="matchingEngine">The core's episode matcher.</param>
/// <param name="metadataService">The core's metadata service, for an ordering's groups.</param>
/// <param name="configurationProvider">The plugin's configuration.</param>
public sealed class TmdbLinkingService(
    IMetadataSeriesStore seriesStore,
    IMetadataCrossReferenceStore crossReferences,
    IMetadataMatchingEngine matchingEngine,
    IMetadataService metadataService,
    ConfigurationProvider<TmdbConfiguration> configurationProvider
)
{
    #region Matching

    /// <summary>
    ///   Works out which of a show's episodes an anime's episodes line up
    ///   with, without writing anything.
    /// </summary>
    /// <param name="anime">The AniDB anime.</param>
    /// <param name="anidbEpisodes">The episodes in scope.</param>
    /// <param name="showID">The TMDB show ID.</param>
    /// <param name="seasonID">
    ///   One season of the show, whose specials come with it, or one group of
    ///   one of its orderings; <c>null</c> for the whole show.
    /// </param>
    /// <param name="existing">The links to honour, or <c>null</c>.</param>
    /// <param name="considerOtherLinks">Whether to leave out the episodes other anime are linked to; <c>null</c> follows the settings.</param>
    /// <returns>
    ///   The matches, or nothing when the show is not stored or the season
    ///   is not the show's, rather than widening the match to the whole show.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="anime"/> or <paramref name="anidbEpisodes"/> is <c>null</c>.</exception>
    public IReadOnlyList<EpisodeMatch> Match(
        IAnidbAnime anime,
        IReadOnlyList<IAnidbEpisode> anidbEpisodes,
        int showID,
        MetadataGuid? seasonID = null,
        IReadOnlyList<IMetadataEpisodeCrossReference>? existing = null,
        bool? considerOtherLinks = null
    )
    {
        ArgumentNullException.ThrowIfNull(anime);
        ArgumentNullException.ThrowIfNull(anidbEpisodes);

        if (seriesStore.GetSeries(TmdbIds.Series(showID)) is not { } series)
            return [];

        if (Candidates(series, seasonID) is not { } candidates)
            return [];

        if (considerOtherLinks ?? configurationProvider.Load().ConsiderExistingOtherLinks)
        {
            var claimed = crossReferences.GetEpisodeLinksInto(series.ID)
                .Where(link => link.AnidbAnimeID != anime.AnidbID && link.ProviderID is not null)
                .Select(link => link.ProviderID!)
                .ToHashSet();
            candidates = [.. candidates.Where(episode => !claimed.Contains(episode.ID))];
        }

        // A link to an episode TMDB removed from the show is matched again;
        // one into another show is left to the engine.
        var episodeIDs = series.Episodes.Select(episode => episode.ID).ToHashSet();
        var kept = existing?
            .Where(link => link.ProviderParentID != series.ID || link.ProviderID is not { } providerID || episodeIDs.Contains(providerID))
            .ToList();

        return matchingEngine.MatchEpisodes(anidbEpisodes, candidates, kept, new() { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons });
    }

    /// <summary>
    ///   The episodes an anime's episodes may be matched to.
    /// </summary>
    /// <param name="series">The stored show.</param>
    /// <param name="seasonID">A season of it, a group of one of its orderings, or <c>null</c>.</param>
    /// <returns>The candidates, or <c>null</c> when the season is not the show's.</returns>
    private IReadOnlyList<IEpisode>? Candidates(ISeries series, MetadataGuid? seasonID)
    {
        if (seasonID is null)
            return series.Episodes;

        if (seasonID.Source != MetadataSource.TMDB)
            return null;

        if (series.Seasons.FirstOrDefault(season => season.ID == seasonID) is { } own)
            return [.. series.Episodes.Where(episode => episode.SeasonNumber is 0 || episode.SeasonID == own.ID)];

        // A group of one of the show's orderings, numbered as the group numbers it.
        return metadataService.GetSeason(seasonID) is { } group && group.SeriesID == series.ID ? group.Episodes : null;
    }

    #endregion
}
