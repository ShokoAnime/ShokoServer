using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Repositories.Cached;

namespace Shoko.Server.Services.Ordering;

/// <summary>
///   Works out where each source places a Shoko special among the regular
///   episodes of its series, keeping the series and default orderings it
///   reads, so one is made for a whole listing. Made for one request.
/// </summary>
/// <remarks>
///   AniDB places the special by its titles, in the AniDB anime's default
///   ordering. A plugin source places it where its linked episode airs in
///   the default ordering of that episode's series; the episodes around it
///   are followed back through the episode links to the special's series.
///   Of the links to one source, the first in link order whose places map
///   back wins.
/// </remarks>
/// <param name="service">Finds the series and episodes and makes their default orderings' places.</param>
/// <param name="crossReferences">The episode links.</param>
/// <param name="shokoEpisodes">The Shoko episodes, found by their AniDB episodes.</param>
public sealed class EpisodePlacementResolver(
    MetadataOrderingService service,
    IMetadataCrossReferenceStore crossReferences,
    AnimeEpisodeRepository shokoEpisodes
)
{
    private readonly ConcurrentDictionary<MetadataGuid, OrderingPlaces?> _placements = new();

    private readonly ConcurrentDictionary<MetadataGuid, MetadataGuid?> _seriesOfEpisodes = new();

    private readonly ConcurrentDictionary<(MetadataGuid Anchor, int SeriesID, bool Last), MetadataGuid?> _anchors = new();

    #region Placements

    /// <summary>
    ///   Where each source places a Shoko special.
    /// </summary>
    /// <param name="episode">The Shoko episode.</param>
    /// <param name="source">One source, or every source when <c>null</c>.</param>
    /// <returns>The placements, AniDB's first, then by source; none for a regular episode.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="episode"/> is <c>null</c>.</exception>
    internal IReadOnlyList<IEpisodePlacement> GetPlacements(IShokoEpisode episode, MetadataSource? source = null)
    {
        ArgumentNullException.ThrowIfNull(episode);
        if (episode.Type is EpisodeType.Episode)
            return [];

        var placements = new List<IEpisodePlacement>();
        if ((source is null || source == MetadataSource.AniDB) && FromAnidb(episode) is { } anidb)
            placements.Add(anidb);
        if (source is not null && source.IsCore)
            return placements;

        var links = crossReferences.GetEpisodeLinks(episode.AnidbEpisodeID, source)
            .Where(link => !link.Source.IsCore && link.ProviderID is { } providerID && providerID.EntityType == MetadataEntityType.Episode)
            .GroupBy(link => link.Source)
            .OrderBy(group => group.Key);
        foreach (var group in links)
        {
            foreach (var link in group.OrderBy(link => link.Ordering))
            {
                if (FromLink(episode, link) is not { } placement)
                    continue;

                placements.Add(placement);
                break;
            }
        }

        return placements;
    }

    /// <summary>
    ///   Where the special's AniDB titles place it in the AniDB anime.
    /// </summary>
    /// <param name="episode">The Shoko episode.</param>
    /// <returns>The placement, or <c>null</c>.</returns>
    private EpisodePlacement? FromAnidb(IShokoEpisode episode)
    {
        var anidbID = new MetadataGuid(MetadataSource.AniDB, MetadataEntityType.Episode, episode.AnidbEpisodeID.ToString());
        return Place(episode, MetadataSource.AniDB, SeriesOf(anidbID), anidbID, (anchor, _) => FromAnidbAnchor(anchor, episode.ShokoSeriesID));
    }

    /// <summary>
    ///   Where a linked source episode airs in its series' default ordering.
    /// </summary>
    /// <param name="episode">The Shoko episode.</param>
    /// <param name="link">The link to the source episode.</param>
    /// <returns>The placement, or <c>null</c>.</returns>
    private EpisodePlacement? FromLink(IShokoEpisode episode, IMetadataEpisodeCrossReference link)
    {
        var sourceID = link.ProviderID!;
        return Place(episode, link.Source, link.ProviderParentID ?? SeriesOf(sourceID), sourceID, (anchor, last) => FromLinkedAnchor(anchor, episode.ShokoSeriesID, last));
    }

    /// <summary>
    ///   Reads where an episode airs in its series' default ordering, with
    ///   the regular episodes around it mapped to Shoko episodes.
    /// </summary>
    /// <param name="episode">The Shoko episode placed.</param>
    /// <param name="source">The source placing it.</param>
    /// <param name="seriesID">The source episode's series.</param>
    /// <param name="episodeID">The source episode.</param>
    /// <param name="map">Maps a regular episode to a Shoko episode, told whether it is the one before.</param>
    /// <returns>The placement, or <c>null</c> when the episode is not placed or a neighbour does not map.</returns>
    private EpisodePlacement? Place(IShokoEpisode episode, MetadataSource source, MetadataGuid? seriesID, MetadataGuid episodeID, Func<MetadataGuid, bool, MetadataGuid?> map)
    {
        if (seriesID is null || Placement(seriesID) is not { } places || places.AiringOf(episodeID) is not { } airing)
            return null;

        MetadataGuid? after = null, before = null;
        if (airing.AirsAfterEpisodeID is { } afterID && (after = map(afterID, true)) is null)
            return null;
        if (airing.AirsBeforeEpisodeID is { } beforeID && (before = map(beforeID, false)) is null)
            return null;

        return after is null && before is null ? null : new(source, episode.ID, episodeID, after, before);
    }

    #endregion

    #region Mapping

    /// <summary>
    ///   The Shoko episode of an AniDB episode, in the given series.
    /// </summary>
    /// <param name="anchor">The AniDB episode.</param>
    /// <param name="seriesID">The Shoko series.</param>
    /// <returns>The Shoko episode, or <c>null</c>.</returns>
    private MetadataGuid? FromAnidbAnchor(MetadataGuid anchor, int seriesID)
        => int.TryParse(anchor.ID, out var anidbEpisodeID) ? ShokoEpisode(anidbEpisodeID, seriesID)?.ID : null;

    /// <summary>
    ///   The Shoko episode a source episode is linked to, in the given
    ///   series: the last of several for the one a special airs after, the
    ///   first for the one it airs before.
    /// </summary>
    /// <param name="anchor">The source episode.</param>
    /// <param name="seriesID">The Shoko series.</param>
    /// <param name="last">Whether to take the last of several.</param>
    /// <returns>The Shoko episode, or <c>null</c>.</returns>
    private MetadataGuid? FromLinkedAnchor(MetadataGuid anchor, int seriesID, bool last)
        => _anchors.GetOrAdd((anchor, seriesID, last), key =>
        {
            var linked = crossReferences.GetLinksTo(key.Anchor)
                .OfType<IMetadataEpisodeCrossReference>()
                .Select(link => ShokoEpisode(link.AnidbEpisodeID, key.SeriesID))
                .OfType<IShokoEpisode>()
                .DistinctBy(episode => episode.ID)
                .ToList();
            return linked.Count switch
            {
                0 => null,
                1 => linked[0].ID,
                _ => (key.Last
                    ? linked.MaxBy(episode => (episode.Type, episode.EpisodeNumber))
                    : linked.MinBy(episode => (episode.Type, episode.EpisodeNumber)))!.ID,
            };
        });

    /// <summary>
    ///   The Shoko episode of an AniDB episode, when it is in the given series.
    /// </summary>
    /// <param name="anidbEpisodeID">The AniDB episode ID.</param>
    /// <param name="seriesID">The Shoko series.</param>
    /// <returns>The Shoko episode, or <c>null</c>.</returns>
    private IShokoEpisode? ShokoEpisode(int anidbEpisodeID, int seriesID)
        => shokoEpisodes.GetByAniDBEpisodeID(anidbEpisodeID) is { } shoko && shoko.AnimeSeriesID == seriesID ? shoko : null;

    #endregion

    #region Lookups

    /// <summary>
    ///   The series of an episode, looked up once.
    /// </summary>
    /// <param name="episodeID">The episode.</param>
    /// <returns>The series, or <c>null</c> when the episode is not available.</returns>
    private MetadataGuid? SeriesOf(MetadataGuid episodeID)
        => _seriesOfEpisodes.GetOrAdd(episodeID, id => service.GetEpisode(id)?.SeriesID);

    /// <summary>
    ///   The places of a series' default ordering, made once.
    /// </summary>
    /// <param name="seriesID">The series.</param>
    /// <returns>The places, or <c>null</c> when the series is not available.</returns>
    private OrderingPlaces? Placement(MetadataGuid seriesID)
        => _placements.GetOrAdd(seriesID, id => service.GetSeries(id) is { } series ? service.GetDefaultPlacement(series) : null);

    #endregion
}

/// <summary>
///   Where one source places a Shoko special, see <see cref="IEpisodePlacement"/>.
/// </summary>
/// <param name="Source">The source placing it.</param>
/// <param name="EpisodeID">The Shoko episode placed.</param>
/// <param name="SourceEpisodeID">The source's episode whose place this is.</param>
/// <param name="AirsAfterEpisodeID">The Shoko episode it airs right after, or <c>null</c> when it airs first.</param>
/// <param name="AirsBeforeEpisodeID">The Shoko episode it airs right before, or <c>null</c> when it airs last.</param>
public sealed record EpisodePlacement(
    MetadataSource Source,
    MetadataGuid EpisodeID,
    MetadataGuid SourceEpisodeID,
    MetadataGuid? AirsAfterEpisodeID,
    MetadataGuid? AirsBeforeEpisodeID
) : IEpisodePlacement;
