using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.CrossReference.Embedded;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.Metadata;

using LinkRowChange = Shoko.Server.Services.MetadataLinkChangeTracker.LinkRowChange;

namespace Shoko.Server.Services;

/// <summary>
///   Where every source's cross-references are kept, one table per level.
/// </summary>
/// <param name="seriesRepository">The series-level links.</param>
/// <param name="movieRepository">The film-level links.</param>
/// <param name="episodeRepository">The episode-level links.</param>
/// <param name="storedEpisodes">
///   The series store's episodes, which fill in what an episode link leaves
///   out and keep its numbers in step.
/// </param>
/// <param name="linkChanges">
///   Optional. Where every write reports the links it changed, so
///   <see cref="Shoko.Abstractions.Metadata.Services.IMetadataLinkingService.LinksChanged"/>
///   is raised for it.
/// </param>
public class MetadataCrossReferenceStore(
    CrossRef_AniDB_Metadata_SeriesRepository seriesRepository,
    CrossRef_AniDB_Metadata_MovieRepository movieRepository,
    CrossRef_AniDB_Metadata_EpisodeRepository episodeRepository,
    Metadata_EpisodeRepository storedEpisodes,
    MetadataLinkChangeTracker? linkChanges = null
) : IMetadataCrossReferenceStore
{
    /// <summary>
    ///   One lock per entry, so two callers adding links to the same anime do
    ///   not read the same free position. The unique indexes are what catches
    ///   a writer that went around this.
    /// </summary>
    private readonly ConcurrentDictionary<(MetadataEntityType, MetadataSource, int, int), object> _locks = new();

    #region Reading

    /// <inheritdoc />
    public IReadOnlyList<IMetadataSeriesCrossReference> GetSeriesLinks(int anidbAnimeID, MetadataSource? source = null)
        => [.. Ordered(seriesRepository.GetByAnidbAnimeID(anidbAnimeID, source))];

    /// <inheritdoc />
    public IReadOnlyList<IMetadataMovieCrossReference> GetMovieLinks(int anidbEpisodeID, MetadataSource? source = null)
        => [.. Ordered(movieRepository.GetByAnidbEpisodeID(anidbEpisodeID, source))];

    /// <inheritdoc />
    public IReadOnlyList<IMetadataMovieCrossReference> GetMovieLinksForSeries(int anidbAnimeID, MetadataSource? source = null)
        => [.. Ordered(movieRepository.GetByAnidbAnimeID(anidbAnimeID, source), xref => xref.AnidbEpisodeID)];

    /// <inheritdoc />
    public IReadOnlyList<IMetadataEpisodeCrossReference> GetEpisodeLinks(int anidbEpisodeID, MetadataSource? source = null)
        => [.. Ordered(episodeRepository.GetByAnidbEpisodeID(anidbEpisodeID, source))];

    /// <inheritdoc />
    public IReadOnlyList<IMetadataEpisodeCrossReference> GetEpisodeLinksForSeries(int anidbAnimeID, MetadataSource? source = null)
        => [.. Ordered(episodeRepository.GetByAnidbAnimeID(anidbAnimeID, source), xref => xref.AnidbEpisodeID)];

    /// <inheritdoc />
    public IReadOnlyList<IMetadataCrossReference> GetLinksTo(MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.EntityType == MetadataEntityType.Series)
            return [.. Ordered(SeriesLevelLinksTo(entry))];

        // A film is claimed at its own level, and whole at the series' level.
        if (entry.EntityType == MetadataEntityType.Movie)
            return [
                .. Ordered(SeriesLevelLinksTo(entry)),
                .. Ordered(movieRepository.GetByProviderID(entry.Source, entry.ID), xref => xref.AnidbEpisodeID),
            ];
        if (entry.EntityType == MetadataEntityType.Episode)
            return [.. Ordered(episodeRepository.GetByProviderID(entry.Source, entry.ID), xref => xref.AnidbEpisodeID)];

        return [];
    }

    /// <inheritdoc />
    public IReadOnlyList<IMetadataEpisodeCrossReference> GetEpisodeLinksInto(MetadataGuid series)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (series.EntityType != MetadataEntityType.Series)
            return [];

        var byEpisode = series.Source.IsCore
            ? []
            : storedEpisodes.GetBySeriesID(series.Source, series.ID)
                .SelectMany(episode => episodeRepository.GetByProviderID(series.Source, episode.ProviderID));
        return
        [
            .. Ordered(
                episodeRepository.GetByProviderParentID(series.Source, series.ID)
                    .Concat(byEpisode)
                    .DistinctBy(xref => xref.CrossRef_AniDB_Metadata_EpisodeID),
                xref => xref.AnidbEpisodeID
            ),
        ];
    }

    /// <summary>
    ///   The series-level links naming an entry, which is a series or a film
    ///   claiming a whole anime.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The links, in no particular order.</returns>
    private IEnumerable<CrossRef_AniDB_Metadata_Series> SeriesLevelLinksTo(MetadataGuid entry)
        => seriesRepository.GetByProviderID(entry.Source, entry.ID).Where(xref => xref.ProviderEntityType == entry.EntityType);

    /// <inheritdoc />
    public IReadOnlyList<IMetadataSeriesCrossReference> GetAllSeriesLinks(MetadataSource? source = null)
        => [.. OfSource(seriesRepository, source).OrderBy(xref => xref.Source).ThenBy(xref => xref.AnidbAnimeID).ThenBy(xref => xref.Ordering)];

    /// <inheritdoc />
    public IReadOnlyList<IMetadataMovieCrossReference> GetAllMovieLinks(MetadataSource? source = null)
        => [.. OrderedByEpisode(OfSource(movieRepository, source), xref => xref.AnidbEpisodeID)];

    /// <inheritdoc />
    public IReadOnlyList<IMetadataEpisodeCrossReference> GetAllEpisodeLinks(MetadataSource? source = null)
        => [.. OrderedByEpisode(OfSource(episodeRepository, source), xref => xref.AnidbEpisodeID)];

    /// <inheritdoc />
    public IReadOnlyList<IMetadataSeasonCrossReference> GetAllSeasonLinks(MetadataSource? source = null)
        => [
            .. OrderedByEpisode(OfSource(episodeRepository, source), xref => xref.AnidbEpisodeID)
                .GroupBy(xref => xref.AnidbAnimeID)
                .OrderBy(group => group.Key)
                .SelectMany(group => MetadataSeasonCrossReference.Project(group.Key, group, StoredSeason)),
        ];

    /// <summary>
    ///   A season's number and series, read off an episode the series store
    ///   holds in it.
    /// </summary>
    /// <param name="seasonID">The season.</param>
    /// <returns>The number and series, or <c>null</c> when the store has no episode in it.</returns>
    private (int SeasonNumber, MetadataGuid SeriesID)? StoredSeason(MetadataGuid seasonID)
        => seasonID.Source.IsCore ||
            storedEpisodes.GetBySeasonID(seasonID.Source, seasonID.ID).FirstOrDefault(episode => episode.SeasonNumber is not null) is not { } episode
                ? null
                : (episode.SeasonNumber!.Value, new(seasonID.Source, MetadataEntityType.Series, episode.SeriesID));

    /// <summary>
    ///   Every link at one level, for one source or all of them.
    /// </summary>
    /// <typeparam name="TRow">The row that level is stored as.</typeparam>
    /// <param name="repository">The level's rows.</param>
    /// <param name="source">One source, or every source when left out.</param>
    /// <returns>The links, in no particular order.</returns>
    private static IEnumerable<TRow> OfSource<TRow>(BaseCrossRef_AniDB_MetadataRepository<TRow> repository, MetadataSource? source)
        where TRow : CrossRef_AniDB_Metadata, new()
        => source is { } onlySource ? repository.GetAll().Where(xref => xref.Source == onlySource) : repository.GetAll();

    /// <summary>
    ///   Links from many entries, by source, anime, episode and position.
    /// </summary>
    /// <typeparam name="TRow">The row that level is stored as.</typeparam>
    /// <param name="xrefs">The links to order.</param>
    /// <param name="episodeOf">The AniDB episode a link names.</param>
    /// <returns>The links, ordered.</returns>
    private static IEnumerable<TRow> OrderedByEpisode<TRow>(IEnumerable<TRow> xrefs, Func<TRow, int> episodeOf) where TRow : CrossRef_AniDB_Metadata
        => xrefs.OrderBy(xref => xref.Source).ThenBy(xref => xref.AnidbAnimeID).ThenBy(episodeOf).ThenBy(xref => xref.Ordering);

    private static IEnumerable<TRow> Ordered<TRow>(IEnumerable<TRow> xrefs, Func<TRow, int>? episodeOf = null) where TRow : CrossRef_AniDB_Metadata
        => episodeOf is null ? xrefs.OrderBy(xref => xref.Ordering) : xrefs.OrderBy(episodeOf).ThenBy(xref => xref.Ordering);

    #endregion

    #region Writing

    /// <inheritdoc />
    public Task<IReadOnlyList<IMetadataSeriesCrossReference>> MergeSeriesLinks(
        IEnumerable<MetadataSeriesLinkData> links,
        IEnumerable<IMetadataSeriesCrossReference>? removals = null,
        MetadataLinkUpdateOptions? options = null,
        CancellationToken cancellationToken = default
    )
        => Task.FromResult<IReadOnlyList<IMetadataSeriesCrossReference>>(Merge(
            links, removals, options, seriesRepository,
            CheckSeries,
            link => (link.Source, link.AnidbAnimeID, 0),
            ApplySeries,
            cancellationToken
        ));

    /// <inheritdoc />
    public Task<IReadOnlyList<IMetadataMovieCrossReference>> MergeMovieLinks(
        IEnumerable<MetadataMovieLinkData> links,
        IEnumerable<IMetadataMovieCrossReference>? removals = null,
        MetadataLinkUpdateOptions? options = null,
        CancellationToken cancellationToken = default
    )
        => Task.FromResult<IReadOnlyList<IMetadataMovieCrossReference>>(Merge(
            links, removals, options, movieRepository,
            CheckMovie,
            link => (link.Source, link.AnidbAnimeID, link.AnidbEpisodeID),
            static (xref, link) =>
            {
                xref.AnidbAnimeID = link.AnidbAnimeID;
                xref.AnidbEpisodeID = link.AnidbEpisodeID;
            },
            cancellationToken
        ));

    /// <inheritdoc />
    public Task<IReadOnlyList<IMetadataEpisodeCrossReference>> MergeEpisodeLinks(
        IEnumerable<MetadataEpisodeLinkData> links,
        IEnumerable<IMetadataEpisodeCrossReference>? removals = null,
        MetadataLinkUpdateOptions? options = null,
        CancellationToken cancellationToken = default
    )
        => Task.FromResult<IReadOnlyList<IMetadataEpisodeCrossReference>>(Merge(
            links, removals, options, episodeRepository,
            CheckEpisode,
            link => (link.Source, link.AnidbAnimeID, link.AnidbEpisodeID),
            ApplyEpisode,
            cancellationToken
        ));

    /// <summary>
    ///   The one write path behind the three typed ones.
    /// </summary>
    /// <typeparam name="TData">The kind of link being written.</typeparam>
    /// <typeparam name="TRow">The row that level is stored as.</typeparam>
    /// <param name="links">The links to write.</param>
    /// <param name="removals">The links to take away.</param>
    /// <param name="options">How to write them.</param>
    /// <param name="repository">The level's rows.</param>
    /// <param name="check">Refuses a link that names the wrong kind of entry.</param>
    /// <param name="slotOf">Where a link's position is counted.</param>
    /// <param name="apply">Writes the level's own columns onto a row.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The links this write wrote, removals included.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="links"/> is or holds <c>null</c>.</exception>
    /// <exception cref="ArgumentException">
    ///   A link names an entry on another source or of the wrong kind, or a
    ///   series or film link names none.
    /// </exception>
    private List<TRow> Merge<TData, TRow>(
        IEnumerable<TData> links,
        IEnumerable<IMetadataCrossReference>? removals,
        MetadataLinkUpdateOptions? options,
        BaseCrossRef_AniDB_MetadataRepository<TRow> repository,
        Action<TData> check,
        Func<TData, (MetadataSource, int, int)> slotOf,
        Action<TRow, TData> apply,
        CancellationToken cancellationToken
    )
        where TData : MetadataLinkData
        where TRow : CrossRef_AniDB_Metadata, new()
    {
        ArgumentNullException.ThrowIfNull(links);

        var writer = options?.WrittenBy;
        var wanted = links.ToList();
        var gone = removals?.ToList() ?? [];

        // Every link is checked before anything is written, so one naming the
        // wrong entry leaves the store as it was.
        foreach (var link in wanted)
        {
            ArgumentNullException.ThrowIfNull(link, nameof(links));
            check(link);
        }

        // What was written before a failure is reported all the same.
        var written = new List<TRow>();
        var changes = new List<LinkRowChange>();
        try
        {
            foreach (var link in gone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (RemoveOne(repository, (link.Source, link.AnidbAnimeID, EpisodeOf(link)), link.ProviderID, changes) is { } removed)
                    written.Add(removed);
            }

            foreach (var group in wanted.GroupBy(slotOf))
            {
                cancellationToken.ThrowIfCancellationRequested();
                written.AddRange(options?.ReplaceExisting ?? false
                    ? ReplaceGroup(repository, group.Key, [.. group], apply, writer, changes)
                    : [.. group.Select(link => AddOne(repository, group.Key, link, apply, writer, changes))]);
            }
        }
        finally
        {
            linkChanges?.Record(changes);
        }

        return written;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IMetadataCrossReference>> OrderLinks(IEnumerable<IMetadataCrossReference> links, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(links);

        var moved = new List<IMetadataCrossReference>();
        foreach (var group in links.GroupBy(link => (link.EntityType, link.Source, link.AnidbAnimeID, EpisodeOf(link))))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var slot = (group.Key.Source, group.Key.AnidbAnimeID, group.Key.Item4);
            var named = group.Select(link => link.ProviderID).ToList();
            moved.AddRange(group.Key.EntityType switch
            {
                _ when group.Key.EntityType == MetadataEntityType.Series => Reorder(seriesRepository, slot, named),
                _ when group.Key.EntityType == MetadataEntityType.Movie => Reorder(movieRepository, slot, named),
                _ when group.Key.EntityType == MetadataEntityType.Episode => Reorder(episodeRepository, slot, named),
                _ => [],
            });
        }

        return Task.FromResult<IReadOnlyList<IMetadataCrossReference>>(moved);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IMetadataCrossReference>> RemoveLinksForSeries(
        MetadataSource source,
        int anidbAnimeID,
        MetadataEntityType? entityType = null,
        CancellationToken cancellationToken = default
    )
        => Task.FromResult<IReadOnlyList<IMetadataCrossReference>>(RemoveAllForSeries(source, anidbAnimeID, entityType));

    #endregion

    #region Syncing

    /// <summary>
    ///   Copies the season and numbers of the series store's episodes onto
    ///   the episode links naming them, so the links follow what the source
    ///   says now. A link to an episode the store does not hold is left as it
    ///   was written, and so is a link naming no series: that is filled in,
    ///   never replaced.
    /// </summary>
    /// <param name="source">One plugin source, or every one when left out.</param>
    /// <param name="seriesID">
    ///   The source's own ID for one stored series whose episodes' links are
    ///   synced, or every link of the source when left out.
    /// </param>
    /// <param name="progress">Told how far the sync is, from 0 to 100.</param>
    /// <param name="cancellationToken">Stops the sync between two anime's links.</param>
    /// <returns>The links that changed.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    internal IReadOnlyList<CrossRef_AniDB_Metadata_Episode> SyncFromSeriesStore(
        MetadataSource? source = null,
        string? seriesID = null,
        IProgress<decimal>? progress = null,
        CancellationToken cancellationToken = default
    )
    {
        var links = source is null
            ? episodeRepository.GetAll().Where(link => !link.Source.IsCore)
            : string.IsNullOrEmpty(seriesID)
                ? OfSource(episodeRepository, source)
                : storedEpisodes.GetBySeriesID(source, seriesID).SelectMany(episode => episodeRepository.GetByProviderID(source, episode.ProviderID));

        var changed = new List<CrossRef_AniDB_Metadata_Episode>();
        var slots = links.Select(link => link.Slot).Distinct().ToList();
        var items = new ItemProgress(progress, slots.Count);
        items.Report(0);
        foreach (var slot in slots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            items.Increment();
            lock (LockFor<CrossRef_AniDB_Metadata_Episode>(slot))
            {
                // Read again under the lock, so a write that got there first is
                // what is compared.
                var saving = new List<CrossRef_AniDB_Metadata_Episode>();
                foreach (var link in episodeRepository.GetBySlot(slot))
                {
                    if (link.IsUnlinked || storedEpisodes.GetByProviderID(link.Source, link.ProviderID) is not { } stored)
                        continue;

                    var seasonID = string.IsNullOrEmpty(stored.SeasonID) ? null : stored.SeasonID;
                    var parentID = string.IsNullOrEmpty(link.ProviderParentID) ? stored.SeriesID : link.ProviderParentID;
                    if (link.ProviderSeasonID == seasonID &&
                        link.SeasonNumber == stored.SeasonNumber &&
                        link.EpisodeNumber == stored.EpisodeNumber &&
                        link.ProviderParentID == parentID)
                        continue;

                    link.ProviderSeasonID = seasonID;
                    link.SeasonNumber = stored.SeasonNumber;
                    link.EpisodeNumber = stored.EpisodeNumber;
                    link.ProviderParentID = parentID;
                    saving.Add(link);
                }

                if (saving.Count is 0)
                    continue;

                episodeRepository.Save(saving);
                changed.AddRange(saving);
            }
        }

        return changed;
    }

    #endregion

    #region The server's own writes

    /// <summary>
    ///   Begins an operation reporting the writes made in the flow until it
    ///   is disposed as one change, for a caller writing several links.
    /// </summary>
    /// <returns>
    ///   The operation, or <c>null</c> when changes are not
    ///   tracked.
    /// </returns>
    internal IDisposable? BeginChanges()
        => linkChanges?.Begin();

    /// <summary>
    ///   Adds or updates one series link, without asking whose it is.
    /// </summary>
    /// <param name="source">The source the linked entry belongs to.</param>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="providerID">The linked entry.</param>
    /// <param name="matchRating">How the link was arrived at.</param>
    /// <returns>The stored link.</returns>
    /// <exception cref="ArgumentException">
    ///   <paramref name="providerID"/> is <c>null</c>, on another source or
    ///   of the wrong kind.
    /// </exception>
    internal CrossRef_AniDB_Metadata_Series AddSeriesLink(MetadataSource source, int anidbAnimeID, MetadataGuid? providerID, MatchRating matchRating)
    {
        var link = new MetadataSeriesLinkData { Source = source, AnidbAnimeID = anidbAnimeID, ProviderID = providerID, MatchRating = matchRating };
        CheckSeries(link);
        return Recorded(changes => AddOne(seriesRepository, (source, anidbAnimeID, 0), link, ApplySeries, null, changes));
    }

    /// <summary>
    ///   Removes one series link, closing the gap it leaves behind.
    /// </summary>
    /// <param name="source">The source the link belongs to.</param>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="providerID">The linked entry, or <c>null</c> for none.</param>
    /// <returns>The removed link, or <c>null</c> when there was none.</returns>
    internal CrossRef_AniDB_Metadata_Series? RemoveSeriesLink(MetadataSource source, int anidbAnimeID, MetadataGuid? providerID)
        => Recorded(changes => RemoveOne(seriesRepository, (source, anidbAnimeID, 0), providerID, changes));

    /// <summary>
    ///   Adds or updates one film link, without asking whose it is.
    /// </summary>
    /// <param name="source">The source the linked entry belongs to.</param>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="anidbEpisodeID">The AniDB episode standing for the film.</param>
    /// <param name="providerID">The linked entry.</param>
    /// <param name="matchRating">How the link was arrived at.</param>
    /// <returns>The stored link.</returns>
    /// <exception cref="ArgumentException">
    ///   <paramref name="providerID"/> is <c>null</c>, on another source or
    ///   of the wrong kind.
    /// </exception>
    internal CrossRef_AniDB_Metadata_Movie AddMovieLink(
        MetadataSource source,
        int anidbAnimeID,
        int anidbEpisodeID,
        MetadataGuid? providerID,
        MatchRating matchRating
    )
    {
        var link = new MetadataMovieLinkData
        {
            Source = source,
            AnidbAnimeID = anidbAnimeID,
            AnidbEpisodeID = anidbEpisodeID,
            ProviderID = providerID,
            MatchRating = matchRating,
        };
        CheckMovie(link);
        return Recorded(changes => AddOne(
            movieRepository,
            (source, anidbAnimeID, anidbEpisodeID),
            link,
            static (xref, link) =>
            {
                xref.AnidbAnimeID = link.AnidbAnimeID;
                xref.AnidbEpisodeID = link.AnidbEpisodeID;
            },
            null,
            changes
        ));
    }

    /// <summary>
    ///   Removes one film link, closing the gap it leaves behind.
    /// </summary>
    /// <param name="source">The source the link belongs to.</param>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="anidbEpisodeID">The AniDB episode standing for the film.</param>
    /// <param name="providerID">The linked entry, or <c>null</c> for none.</param>
    /// <returns>The removed link, or <c>null</c> when there was none.</returns>
    internal CrossRef_AniDB_Metadata_Movie? RemoveMovieLink(MetadataSource source, int anidbAnimeID, int anidbEpisodeID, MetadataGuid? providerID)
        => Recorded(changes => RemoveOne(movieRepository, (source, anidbAnimeID, anidbEpisodeID), providerID, changes));

    /// <summary>
    ///   Adds or updates one episode link, without asking whose it is.
    /// </summary>
    /// <param name="source">The source the linked entry belongs to.</param>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="anidbEpisodeID">The AniDB episode ID.</param>
    /// <param name="providerID">The linked entry, or <c>null</c> for none.</param>
    /// <param name="providerParentID">The provider's series the entry sits in, when known.</param>
    /// <param name="matchRating">How the link was arrived at.</param>
    /// <param name="ordering">Where the caller wants it to sit, or nothing to leave that to the store.</param>
    /// <returns>The stored link.</returns>
    /// <exception cref="ArgumentException">
    ///   <paramref name="providerID"/> or <paramref name="providerParentID"/>
    ///   is on another source or of the wrong kind.
    /// </exception>
    internal CrossRef_AniDB_Metadata_Episode AddEpisodeLink(
        MetadataSource source,
        int anidbAnimeID,
        int anidbEpisodeID,
        MetadataGuid? providerID,
        MetadataGuid? providerParentID,
        MatchRating matchRating,
        int? ordering = null
    )
    {
        var link = new MetadataEpisodeLinkData
        {
            Source = source,
            AnidbAnimeID = anidbAnimeID,
            AnidbEpisodeID = anidbEpisodeID,
            ProviderID = providerID,
            ProviderParentID = providerParentID,
            MatchRating = matchRating,
        };
        CheckEpisode(link);
        return Recorded(changes => AddOne(episodeRepository, (source, anidbAnimeID, anidbEpisodeID), link, ApplyEpisode, null, changes, ordering));
    }

    /// <summary>
    ///   Removes one episode link, closing the gap it leaves behind.
    /// </summary>
    /// <param name="source">The source the link belongs to.</param>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="anidbEpisodeID">The AniDB episode ID.</param>
    /// <param name="providerID">The linked entry, or <c>null</c> for none.</param>
    /// <returns>The removed link, or <c>null</c> when there was none.</returns>
    internal CrossRef_AniDB_Metadata_Episode? RemoveEpisodeLink(MetadataSource source, int anidbAnimeID, int anidbEpisodeID, MetadataGuid? providerID)
        => Recorded(changes => RemoveOne(episodeRepository, (source, anidbAnimeID, anidbEpisodeID), providerID, changes));

    /// <summary>
    ///   Removes every link an anime has for a source.
    /// </summary>
    /// <param name="source">The source to forget.</param>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <param name="entityType">One level, or every level when left out.</param>
    /// <returns>The removed links.</returns>
    internal List<IMetadataCrossReference> RemoveAllForSeries(MetadataSource source, int anidbAnimeID, MetadataEntityType? entityType = null)
    {
        List<IMetadataCrossReference> removed = [];
        if (entityType is null || entityType == MetadataEntityType.Series)
            removed.AddRange(RemoveAll(seriesRepository, source, anidbAnimeID));
        if (entityType is null || entityType == MetadataEntityType.Movie)
            removed.AddRange(RemoveAll(movieRepository, source, anidbAnimeID));
        if (entityType is null || entityType == MetadataEntityType.Episode)
            removed.AddRange(RemoveAll(episodeRepository, source, anidbAnimeID));

        linkChanges?.Record([.. removed.Cast<CrossRef_AniDB_Metadata>().Select(row => LinkRowChange.Of(row, row.MatchRating, null))]);
        return removed;
    }

    #endregion

    #region Helpers

    /// <summary>
    ///   Runs one of the server's own writes and reports what it changed.
    /// </summary>
    /// <typeparam name="T">What the write gives back.</typeparam>
    /// <param name="write">The write, recording its changes into the list it is given.</param>
    /// <returns>What the write gave back.</returns>
    private T Recorded<T>(Func<List<LinkRowChange>, T> write)
    {
        var changes = new List<LinkRowChange>();
        try
        {
            return write(changes);
        }
        finally
        {
            linkChanges?.Record(changes);
        }
    }

    /// <summary>
    ///   Adds or updates one link, giving a new one the next free position.
    /// </summary>
    /// <typeparam name="TData">The kind of link being written.</typeparam>
    /// <typeparam name="TRow">The row that level is stored as.</typeparam>
    /// <param name="repository">The level's rows.</param>
    /// <param name="slot">Where the link's position is counted.</param>
    /// <param name="link">The link to store.</param>
    /// <param name="apply">Writes the level's own columns onto the row.</param>
    /// <param name="writtenBy">The provider that wrote it, when one did.</param>
    /// <param name="changes">Where the change is recorded.</param>
    /// <param name="ordering">
    ///   Where the caller wants it to sit, when it is the caller's to decide.
    ///   The rest of the entry's links close up around it. Left out, an
    ///   existing link keeps its place and a new one goes last.
    /// </param>
    /// <returns>The stored link.</returns>
    private TRow AddOne<TData, TRow>(
        BaseCrossRef_AniDB_MetadataRepository<TRow> repository,
        (MetadataSource, int, int) slot,
        TData link,
        Action<TRow, TData> apply,
        Guid? writtenBy,
        List<LinkRowChange> changes,
        int? ordering = null
    )
        where TData : MetadataLinkData
        where TRow : CrossRef_AniDB_Metadata, new()
    {
        ArgumentNullException.ThrowIfNull(link);

        lock (LockFor<TRow>(slot))
        {
            var existing = repository.GetBySlot(slot);
            var xref = existing.FirstOrDefault(row => row.Names(link.ProviderID));
            var position = xref?.Ordering ?? (existing.Count is 0 ? 0 : existing.Max(row => row.Ordering) + 1);
            var before = xref?.MatchRating;
            xref ??= new();
            Fill(xref, link, apply, writtenBy);
            if (ordering is not { } wanted)
            {
                xref.Ordering = position;
                repository.Save(xref);
                changes.Add(LinkRowChange.Of(xref, before, xref.MatchRating));
                return xref;
            }

            // The caller picked the place, so everything else shuffles around
            // it rather than the other way round.
            var ordered = existing.Where(row => row != xref).ToList();
            ordered.Insert(Math.Clamp(wanted, 0, ordered.Count), xref);
            Assign(repository, ordered);
            changes.Add(LinkRowChange.Of(xref, before, xref.MatchRating));
            return xref;
        }
    }

    /// <summary>
    ///   Removes one link, closing the gap it leaves behind.
    /// </summary>
    /// <typeparam name="TRow">The row that level is stored as.</typeparam>
    /// <param name="repository">The level's rows.</param>
    /// <param name="slot">Where the link's position is counted.</param>
    /// <param name="providerID">The entry the link names, or <c>null</c> for a link to nothing.</param>
    /// <param name="changes">Where the change is recorded.</param>
    /// <returns>The removed link, or <c>null</c> when there was none.</returns>
    private TRow? RemoveOne<TRow>(
        BaseCrossRef_AniDB_MetadataRepository<TRow> repository,
        (MetadataSource, int, int) slot,
        MetadataGuid? providerID,
        List<LinkRowChange> changes
    )
        where TRow : CrossRef_AniDB_Metadata, new()
    {
        lock (LockFor<TRow>(slot))
        {
            var existing = repository.GetBySlot(slot);
            if (existing.FirstOrDefault(row => row.Names(providerID)) is not { } xref)
                return null;

            repository.Delete(xref);
            changes.Add(LinkRowChange.Of(xref, xref.MatchRating, null));
            Assign(repository, [.. existing.Where(row => row != xref)]);
            return xref;
        }
    }

    /// <summary>
    ///   Replaces an entry's links with exactly the ones given, in that order.
    /// </summary>
    /// <typeparam name="TData">The kind of link being written.</typeparam>
    /// <typeparam name="TRow">The row that level is stored as.</typeparam>
    /// <param name="repository">The level's rows.</param>
    /// <param name="slot">Where the links' positions are counted.</param>
    /// <param name="links">The links to keep, all of one entry.</param>
    /// <param name="apply">Writes the level's own columns onto a row.</param>
    /// <param name="writtenBy">The provider that wrote them, when one did.</param>
    /// <param name="changes">Where the changes are recorded.</param>
    /// <returns>The stored links.</returns>
    private List<TRow> ReplaceGroup<TData, TRow>(
        BaseCrossRef_AniDB_MetadataRepository<TRow> repository,
        (MetadataSource, int, int) slot,
        List<TData> links,
        Action<TRow, TData> apply,
        Guid? writtenBy,
        List<LinkRowChange> changes
    )
        where TData : MetadataLinkData
        where TRow : CrossRef_AniDB_Metadata, new()
    {
        if (links.Select(link => link.ProviderID).Distinct().Count() != links.Count)
            throw new ArgumentException("The same entry cannot be linked twice in one write.", nameof(links));

        lock (LockFor<TRow>(slot))
        {
            var existing = repository.GetBySlot(slot);
            var before = existing.ToDictionary<TRow, TRow, MatchRating>(row => row, row => row.MatchRating, ReferenceEqualityComparer.Instance);
            var stored = new List<TRow>();
            foreach (var link in links)
            {
                var xref = existing.FirstOrDefault(row => row.Names(link.ProviderID)) ?? new();
                Fill(xref, link, apply, writtenBy);
                stored.Add(xref);
            }

            var removed = existing.Except(stored).ToList();
            repository.Delete(removed);
            Assign(repository, stored);
            changes.AddRange(removed.Select(row => LinkRowChange.Of(row, row.MatchRating, null)));
            changes.AddRange(stored.Select(row => LinkRowChange.Of(row, before.TryGetValue(row, out var rating) ? rating : null, row.MatchRating)));
            return stored;
        }
    }

    /// <summary>
    ///   Puts an entry's links in the order the provider IDs are given, with
    ///   whatever it left out keeping its order behind them.
    /// </summary>
    /// <typeparam name="TRow">The row that level is stored as.</typeparam>
    /// <param name="repository">The level's rows.</param>
    /// <param name="slot">Where the links' positions are counted.</param>
    /// <param name="providerIDs">The entries the links name, in the order asked for.</param>
    /// <returns>The links whose position changed.</returns>
    private List<TRow> Reorder<TRow>(BaseCrossRef_AniDB_MetadataRepository<TRow> repository, (MetadataSource, int, int) slot, List<MetadataGuid?> providerIDs)
        where TRow : CrossRef_AniDB_Metadata, new()
    {
        lock (LockFor<TRow>(slot))
        {
            var existing = repository.GetBySlot(slot);
            var named = new List<TRow>();
            foreach (var providerID in providerIDs)
                if (existing.FirstOrDefault(row => row.Names(providerID)) is { } row && !named.Contains(row))
                    named.Add(row);

            return Assign(repository, [.. named, .. existing.Except(named)]);
        }
    }

    /// <summary>
    ///   Removes every link an anime has for a source at one level.
    /// </summary>
    /// <typeparam name="TRow">The row that level is stored as.</typeparam>
    /// <param name="repository">The level's rows.</param>
    /// <param name="source">The source to forget.</param>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <returns>The removed links.</returns>
    private static List<TRow> RemoveAll<TRow>(BaseCrossRef_AniDB_MetadataRepository<TRow> repository, MetadataSource source, int anidbAnimeID)
        where TRow : CrossRef_AniDB_Metadata, new()
    {
        var existing = repository.GetByAnidbAnimeID(anidbAnimeID, source);
        if (existing.Count is 0)
            return [];

        repository.Delete(existing);
        return [.. existing];
    }

    /// <summary>
    ///   The AniDB episode a link names, where its level has one.
    /// </summary>
    /// <param name="link">The link being read.</param>
    /// <returns>The AniDB episode ID, or <c>0</c> at the series' level.</returns>
    private static int EpisodeOf(IMetadataCrossReference link)
        => link switch
        {
            IMetadataMovieCrossReference movie => movie.AnidbEpisodeID,
            IMetadataEpisodeCrossReference episode => episode.AnidbEpisodeID,
            _ => 0,
        };

    /// <summary>
    ///   Writes a series link's own columns onto its row.
    /// </summary>
    /// <param name="xref">The row.</param>
    /// <param name="link">The link.</param>
    private static void ApplySeries(CrossRef_AniDB_Metadata_Series xref, MetadataSeriesLinkData link)
    {
        xref.AnidbAnimeID = link.AnidbAnimeID;
        xref.ProviderType = link.ProviderID?.EntityType == MetadataEntityType.Movie ? MetadataEntityType.Movie : MetadataEntityType.Series;
    }

    /// <summary>
    ///   Writes an episode link's own columns onto its row, with what it left
    ///   out filled from the core's series store.
    /// </summary>
    /// <remarks>
    ///   A core source's link keeps no season or numbers: it reads them live
    ///   from the source's own tables, which a stored copy would fall behind.
    /// </remarks>
    /// <param name="xref">The row.</param>
    /// <param name="link">The link.</param>
    private void ApplyEpisode(CrossRef_AniDB_Metadata_Episode xref, MetadataEpisodeLinkData link)
    {
        link = link.Source.IsCore
            ? link with { SeasonID = null, SeasonNumber = null, EpisodeNumber = null }
            : FillFromSeriesStore(link);
        xref.AnidbAnimeID = link.AnidbAnimeID;
        xref.AnidbEpisodeID = link.AnidbEpisodeID;
        xref.ProviderParentID = CrossRef_AniDB_Metadata.ToStored(link.ProviderParentID);
        xref.ProviderSeasonID = link.SeasonID?.ID;
        xref.SeasonNumber = link.SeasonNumber;
        xref.EpisodeNumber = link.EpisodeNumber;
    }

    /// <summary>
    ///   Fills an episode link's missing series, season and numbers from the
    ///   core's series store, when the linked episode is there. Only what the
    ///   link left <c>null</c> is filled.
    /// </summary>
    /// <param name="link">The link as written.</param>
    /// <returns>The link, with what the store knows filled in.</returns>
    private MetadataEpisodeLinkData FillFromSeriesStore(MetadataEpisodeLinkData link)
    {
        if (link.ProviderID is not { } providerID || storedEpisodes.GetByProviderID(providerID.Source, providerID.ID) is not { } stored)
            return link;

        return link with
        {
            ProviderParentID = link.ProviderParentID ?? new(providerID.Source, MetadataEntityType.Series, stored.SeriesID),
            SeasonID = link.SeasonID ?? (string.IsNullOrEmpty(stored.SeasonID) ? null : new(providerID.Source, MetadataEntityType.Season, stored.SeasonID)),
            SeasonNumber = link.SeasonNumber ?? stored.SeasonNumber,
            EpisodeNumber = link.EpisodeNumber ?? stored.EpisodeNumber,
        };
    }

    /// <summary>
    ///   Refuses a series link naming no entry, or one the series' level does
    ///   not store: a series, or a film claiming the whole anime.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <exception cref="ArgumentException">The link names no entry, or one <see cref="CheckLinked"/> refuses.</exception>
    private void CheckSeries(MetadataSeriesLinkData link)
    {
        // A film claiming the whole anime is kept at this level too.
        var entityType = link.ProviderID?.EntityType == MetadataEntityType.Movie ? MetadataEntityType.Movie : MetadataEntityType.Series;
        CheckLinked(link.Source, link.ProviderID, entityType, required: true);
    }

    /// <summary>
    ///   Refuses a film link naming no film, or an entry that is not one.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <exception cref="ArgumentException">The link names no entry, or one <see cref="CheckLinked"/> refuses.</exception>
    private void CheckMovie(MetadataMovieLinkData link)
        => CheckLinked(link.Source, link.ProviderID, MetadataEntityType.Movie, required: true);

    /// <summary>
    ///   Refuses an episode link whose episode, series or season is not what
    ///   it says it is. An episode may be linked to nothing, which is how it
    ///   is kept from being matched again.
    /// </summary>
    /// <param name="link">The link.</param>
    /// <exception cref="ArgumentException">An entry the link names is one <see cref="CheckLinked"/> refuses.</exception>
    private void CheckEpisode(MetadataEpisodeLinkData link)
    {
        CheckLinked(link.Source, link.ProviderID, MetadataEntityType.Episode);
        CheckLinked(link.Source, link.ProviderParentID, MetadataEntityType.Series);
        CheckLinked(link.Source, link.SeasonID, MetadataEntityType.Season);
    }

    /// <summary>
    ///   Refuses an entry a link names that is not on the link's source or not
    ///   of the kind the link's level stores, and a missing entry where one is
    ///   required.
    /// </summary>
    /// <param name="source">The link's source.</param>
    /// <param name="entry">The entry named, or <c>null</c> for none.</param>
    /// <param name="entityType">The kind the entry must be.</param>
    /// <param name="required">Whether the link must name an entry.</param>
    /// <exception cref="ArgumentException">
    ///   The entry is missing where required, on another source or of another
    ///   kind.
    /// </exception>
    private void CheckLinked(MetadataSource source, MetadataGuid? entry, MetadataEntityType entityType, bool required = false)
    {
        if (entry is null)
        {
            if (required)
                throw new ArgumentException(
                    $"A {entityType.Value} link on {source.Value} must name an entry; only an episode can be linked to nothing.",
                    "links"
                );
            return;
        }

        if (entry.Source != source || entry.EntityType != entityType)
            throw new ArgumentException($"A {entityType.Value} link on {source.Value} cannot name \"{entry}\".", "links");
    }

    private object LockFor<TRow>((MetadataSource Source, int AnidbAnimeID, int AnidbEpisodeID) slot) where TRow : CrossRef_AniDB_Metadata, new()
        => _locks.GetOrAdd((new TRow().EntityType, slot.Source, slot.AnidbAnimeID, slot.AnidbEpisodeID), _ => new object());

    private static void Fill<TData, TRow>(TRow xref, TData link, Action<TRow, TData> apply, Guid? writtenBy)
        where TData : MetadataLinkData
        where TRow : CrossRef_AniDB_Metadata
    {
        xref.Source = link.Source;
        xref.ProviderID = CrossRef_AniDB_Metadata.ToStored(link.ProviderID);
        xref.MatchRating = link.MatchRating;
        xref.WrittenBy = writtenBy;
        apply(xref, link);
    }

    /// <summary>
    ///   Writes a list in its own order, so the first link sits at 0.
    /// </summary>
    /// <typeparam name="TRow">The row that level is stored as.</typeparam>
    /// <param name="repository">The level's rows.</param>
    /// <param name="ordered">The links, in the order they should sit in.</param>
    /// <returns>The links written.</returns>
    private static List<TRow> Assign<TRow>(BaseCrossRef_AniDB_MetadataRepository<TRow> repository, List<TRow> ordered)
        where TRow : CrossRef_AniDB_Metadata, new()
        => Assign(ordered, repository.Save);

    /// <summary>
    ///   Writes a list in its own order, so the first link sits at 0.
    /// </summary>
    /// <typeparam name="TRow">The row that level is stored as.</typeparam>
    /// <param name="ordered">The links, in the order they should sit in.</param>
    /// <param name="save">Where the rows are written, which is the level's repository.</param>
    /// <returns>The links written.</returns>
    internal static List<TRow> Assign<TRow>(List<TRow> ordered, Action<IReadOnlyCollection<TRow>> save)
        where TRow : CrossRef_AniDB_Metadata
    {
        if (ordered.Count is 0)
            return ordered;

        // A swap would land on the slot it is leaving, which the unique index
        // refuses, so whatever is in the way is parked first.
        var moving = ordered.Where((xref, position) => xref.RowID is not 0 && xref.Ordering != position).ToList();
        if (moving.Count > 0)
        {
            for (var index = 0; index < moving.Count; index++)
                moving[index].Ordering = -(index + 1);
            save(moving);
        }

        // Everything is written, not only what moved: a link's contents may
        // have changed while it stayed where it was.
        for (var position = 0; position < ordered.Count; position++)
            ordered[position].Ordering = position;
        save(ordered);
        return ordered;
    }

    #endregion
}
