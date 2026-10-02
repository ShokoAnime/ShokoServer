using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Repositories.Cached.Metadata;
using Shoko.Server.Scheduling.Jobs.Metadata;
using Shoko.Server.Services.MetadataStorage;

namespace Shoko.Server.Services;

/// <summary>
///   Keeps every plugin source's series, seasons and episodes in the store's
///   own tables, cached in memory, with their titles and descriptions in the
///   text table.
/// </summary>
/// <param name="seriesRepository">The series.</param>
/// <param name="seasonRepository">The seasons.</param>
/// <param name="episodeRepository">The episodes.</param>
/// <param name="contentRatingRepository">The series' content ratings.</param>
/// <param name="textStore">Keeps the titles and descriptions.</param>
/// <param name="cleanup">Removes what the other stores hold for a removed season or episode, but not the people it credited.</param>
/// <param name="orderings">Removes the orderings of a removed series.</param>
/// <param name="crossReferences">Removes the links to the episodes a save drops.</param>
/// <param name="scheduler">Queues the sync of the episode links a write touches.</param>
/// <param name="logger">The logger.</param>
public class MetadataSeriesStore(
    Metadata_SeriesRepository seriesRepository,
    Metadata_SeasonRepository seasonRepository,
    Metadata_EpisodeRepository episodeRepository,
    Metadata_ContentRatingRepository contentRatingRepository,
    MetadataTextStore textStore,
    MetadataEntityCleanup cleanup,
    Lazy<MetadataOrderingService> orderings,
    Lazy<IMetadataCrossReferenceStore> crossReferences,
    IQueueScheduler scheduler,
    ILogger<MetadataSeriesStore> logger
) : IMetadataSeriesStore
{
    /// <summary>
    ///   Held around every write, so two writers never read the same state
    ///   and both add the same row.
    /// </summary>
    private readonly object _writeLock = new();

    #region Reading

    /// <inheritdoc />
    public ISeries? GetSeries(MetadataGuid id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.EntityType == MetadataEntityType.Series ? seriesRepository.GetByProviderID(id.Source, id.ID) : null;
    }

    /// <inheritdoc />
    public ISeason? GetSeason(MetadataGuid id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.EntityType == MetadataEntityType.Season && seasonRepository.GetByProviderID(id.Source, id.ID) is { } season && HasSeries(season.Source, season.SeriesID)
            ? season
            : null;
    }

    /// <inheritdoc />
    public IEpisode? GetEpisode(MetadataGuid id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.EntityType == MetadataEntityType.Episode && episodeRepository.GetByProviderID(id.Source, id.ID) is { } episode && HasSeries(episode.Source, episode.SeriesID)
            ? episode
            : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<ISeries> GetAllSeries(MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return seriesRepository.GetBySource(source);
    }

    /// <inheritdoc />
    public IReadOnlyList<ISeason> GetAllSeasons(MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return [.. seasonRepository.GetBySource(source).Where(season => HasSeries(season.Source, season.SeriesID))];
    }

    /// <inheritdoc />
    public IReadOnlyList<IEpisode> GetAllEpisodes(MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return [.. episodeRepository.GetBySource(source).Where(episode => HasSeries(episode.Source, episode.SeriesID))];
    }

    /// <summary>
    ///   Whether a series is stored, which its seasons and episodes need to be
    ///   handed out.
    /// </summary>
    /// <param name="source">The series' source.</param>
    /// <param name="seriesID">The source's ID for the series.</param>
    /// <returns><c>true</c> when the series has its row.</returns>
    private bool HasSeries(MetadataSource source, string seriesID)
        => seriesRepository.GetByProviderID(source, seriesID) is not null;

    #endregion

    #region Writing

    /// <inheritdoc />
    /// <remarks>
    ///   A season or episode given twice keeps the last copy. A season or
    ///   episode stored under another series moves to this one. A season or
    ///   episode removed takes what the other stores hold for it along, and
    ///   the links naming a removed episode go too.
    /// </remarks>
    public int SaveSeries(MetadataSeriesData series)
    {
        ArgumentNullException.ThrowIfNull(series);
        MetadataEntries.CheckEntry(series.ID, MetadataEntityType.Series, nameof(series));

        // Everything is checked before anything is written, so a bad season
        // or episode leaves the series as it was.
        var source = series.ID.Source;
        var seasons = Latest(series.Seasons, season => season.ID, source, MetadataEntityType.Season);
        var episodes = Latest(series.Episodes, episode => episode.ID, source, MetadataEntityType.Episode);
        var episodeResources = new Dictionary<MetadataGuid, List<Resource>>();
        var episodeCrossSourceIDs = new Dictionary<MetadataGuid, List<MetadataGuid>>();
        foreach (var episode in episodes.Values)
        {
            if (episode.SeasonID is { } seasonID && !seasons.ContainsKey(seasonID))
                throw new ArgumentException($"The episode \"{episode.ID}\" names the season \"{seasonID}\", which the series does not have.", nameof(series));
            episodeResources[episode.ID] = MetadataEntries.CheckResources(episode.Resources, nameof(series));
            episodeCrossSourceIDs[episode.ID] = MetadataEntries.CheckCrossSourceIDs(episode.CrossSourceIDs, nameof(series));
        }

        var originalLanguageCode = MetadataEntries.CheckLanguageCode(series.OriginalLanguageCode, nameof(series));
        var resources = MetadataEntries.CheckResources(series.Resources, nameof(series));
        var crossSourceIDs = MetadataEntries.CheckCrossSourceIDs(series.CrossSourceIDs, nameof(series));
        var contentRatings = MetadataContentRatings.Check(series.ContentRatings, nameof(series));
        List<(MetadataGuid Entry, IReadOnlyList<ITitle> Titles, IReadOnlyList<IText> Descriptions)> texts =
        [
            (series.ID, series.Titles ?? [], series.Overviews ?? []),
            .. seasons.Values.Select(season => (season.ID, season.Titles ?? [], season.Overviews ?? [])),
            .. episodes.Values.Select(episode => (episode.ID, episode.Titles ?? [], episode.Overviews ?? [])),
        ];

        var removed = new List<MetadataGuid>();
        int count;
        lock (_writeLock)
        {
            var now = DateTime.Now;
            var storedSeries = seriesRepository.GetByProviderID(source, series.ID.ID);
            var seriesRow = MetadataRows.Copy(storedSeries) ?? new Metadata_Series();
            seriesRow.Source = source;
            seriesRow.ProviderID = series.ID.ID;
            seriesRow.Type = series.Type;
            seriesRow.AirDate = series.AirDate;
            seriesRow.EndDate = series.EndDate;
            // Ratings are stored with two decimals on every backend.
            seriesRow.Rating = Math.Round(series.Rating, 2);
            seriesRow.RatingVotes = series.RatingVotes;
            seriesRow.IsRestricted = series.Restricted;
            seriesRow.ReleaseStatus = series.ReleaseStatus;
            seriesRow.SourceMaterial = series.SourceMaterial;
            seriesRow.OriginalLanguageCode = originalLanguageCode;
            seriesRow.Popularity = series.Popularity;
            seriesRow.FavoriteCount = series.FavoriteCount;
            seriesRow.Resources = resources;
            seriesRow.CrossSourceIDs = crossSourceIDs;
            var (ratingsSaving, ratingsDeleting) = MetadataContentRatings.Plan(contentRatingRepository, series.ID, contentRatings);

            var seasonRows = seasons.Values.Select(season =>
            {
                var stored = seasonRepository.GetByProviderID(source, season.ID.ID);
                var row = MetadataRows.Copy(stored) ?? new Metadata_Season();
                row.Source = source;
                row.ProviderID = season.ID.ID;
                row.SeriesID = series.ID.ID;
                row.SeasonNumber = season.SeasonNumber;
                return (Stored: stored, Row: row);
            }).ToList();
            var episodeRows = episodes.Values.Select(episode =>
            {
                var stored = episodeRepository.GetByProviderID(source, episode.ID.ID);
                var row = MetadataRows.Copy(stored) ?? new Metadata_Episode();
                row.Source = source;
                row.ProviderID = episode.ID.ID;
                row.SeriesID = series.ID.ID;
                row.SeasonID = episode.SeasonID?.ID;
                row.SeasonNumber = episode.SeasonNumber ?? (episode.SeasonID is { } seasonID ? seasons[seasonID].SeasonNumber : null);
                row.EpisodeNumber = episode.EpisodeNumber;
                row.Type = episode.Type;
                row.Rating = Math.Round(episode.Rating, 2);
                row.RatingVotes = episode.RatingVotes;
                row.RuntimeSeconds = (int)Math.Round(episode.Runtime.TotalSeconds);
                row.AirDateWithTime = episode.AirDateWithTime;
                row.AirDate = episode.AirDateWithTime is { } airedAt
                    ? DateOnly.FromDateTime(airedAt.Kind is DateTimeKind.Local ? airedAt.ToUniversalTime() : airedAt)
                    : episode.AirDate;
                row.Resources = episodeResources[episode.ID];
                row.CrossSourceIDs = episodeCrossSourceIDs[episode.ID];
                return (Stored: stored, Row: row);
            }).ToList();

            var keptSeasons = seasonRows.Select(season => season.Row.ProviderID).ToHashSet(StringComparer.Ordinal);
            var keptEpisodes = episodeRows.Select(episode => episode.Row.ProviderID).ToHashSet(StringComparer.Ordinal);
            var removedSeasons = seasonRepository.GetBySeriesID(source, series.ID.ID).Where(season => !keptSeasons.Contains(season.ProviderID)).ToList();
            var removedEpisodes = episodeRepository.GetBySeriesID(source, series.ID.ID).Where(episode => !keptEpisodes.Contains(episode.ProviderID)).ToList();

            // A row is written when it is new, when its columns changed or
            // when its texts did, and only then is it said to have changed.
            var seriesChange = UpdateReason.None;
            var seasonChanges = new List<(Metadata_Season Row, UpdateReason Reason)>();
            var episodeChanges = new List<(Metadata_Episode Row, UpdateReason Reason)>();
            textStore.WriteWithTexts(texts, [.. removedSeasons.Select(season => season.ID), .. removedEpisodes.Select(episode => episode.ID)], changedTexts =>
            {
                seriesChange = Reason(
                    storedSeries is null,
                    storedSeries?.SameAs(seriesRow) ?? false,
                    changedTexts.Contains(series.ID) || ratingsSaving.Count + ratingsDeleting.Count > 0
                );
                seasonChanges.AddRange(seasonRows
                    .Select(season => (season.Row, Reason: Reason(season.Stored is null, season.Stored?.SameAs(season.Row) ?? false, changedTexts.Contains(season.Row.ID))))
                    .Where(season => season.Reason is not UpdateReason.None)
                    .Concat(removedSeasons.Select(season => (Row: season, Reason: UpdateReason.Removed))));
                episodeChanges.AddRange(episodeRows
                    .Select(episode => (episode.Row, Reason: Reason(episode.Stored is null, episode.Stored?.SameAs(episode.Row) ?? false, changedTexts.Contains(episode.Row.ID))))
                    .Where(episode => episode.Reason is not UpdateReason.None)
                    .Concat(removedEpisodes.Select(episode => (Row: episode, Reason: UpdateReason.Removed))));

                if (seriesChange is not UpdateReason.None)
                    seriesRow.LastUpdatedAt = now;
                foreach (var (row, _) in seasonChanges.Where(season => season.Reason is not UpdateReason.Removed))
                    row.LastUpdatedAt = now;
                foreach (var (row, _) in episodeChanges.Where(episode => episode.Reason is not UpdateReason.Removed))
                    row.LastUpdatedAt = now;

                return
                [
                    new MetadataRowChanges<Metadata_Series>(seriesRepository, seriesChange is UpdateReason.None ? Array.Empty<Metadata_Series>() : [seriesRow], []),
                    new MetadataRowChanges<Metadata_ContentRating>(contentRatingRepository, ratingsSaving, ratingsDeleting),
                    new MetadataRowChanges<Metadata_Season>(
                        seasonRepository,
                        [.. seasonChanges.Where(season => season.Reason is not UpdateReason.Removed).Select(season => season.Row)],
                        removedSeasons
                    ),
                    new MetadataRowChanges<Metadata_Episode>(
                        episodeRepository,
                        [.. episodeChanges.Where(episode => episode.Reason is not UpdateReason.Removed).Select(episode => episode.Row)],
                        removedEpisodes
                    ),
                ];
            });

            count = (seriesChange is UpdateReason.None ? 0 : 1) + seasonChanges.Count + episodeChanges.Count;
            if (count is 0)
                return 0;

            // A series whose own row is the same is still updated when
            // anything under it changed.
            var seriesStored = seriesChange is UpdateReason.None ? storedSeries! : seriesRow;
            RaiseEvents(seriesStored, seriesChange is UpdateReason.None ? UpdateReason.Updated : seriesChange, seasonChanges, episodeChanges);

            // The links to an episode that was added or changed carry its
            // season and numbers, so they are brought in step.
            if (episodeChanges.Any(episode => episode.Reason is not UpdateReason.Removed))
                _ = ScheduleLinkSync(series.ID);
            removed.AddRange([.. removedSeasons.Select(season => season.ID), .. removedEpisodes.Select(episode => episode.ID)]);
        }

        // Outside the lock, since the other stores take their own. The links
        // naming dropped episodes go too, as nothing finds them later.
        var droppedEpisodes = removed.Where(entity => entity.EntityType == MetadataEntityType.Episode).ToList();
        cleanup.Remove(removed);
        RemoveLinksTo(droppedEpisodes);
        return count;
    }

    /// <inheritdoc />
    public int RemoveSeries(MetadataGuid id)
    {
        MetadataEntries.CheckEntry(id, MetadataEntityType.Series, nameof(id));

        List<MetadataGuid> removed;
        int count;
        lock (_writeLock)
        {
            var seriesRow = seriesRepository.GetByProviderID(id.Source, id.ID);
            var seasons = seasonRepository.GetBySeriesID(id.Source, id.ID);
            var episodes = episodeRepository.GetBySeriesID(id.Source, id.ID);
            count = (seriesRow is null ? 0 : 1) + seasons.Count + episodes.Count;
            if (count is 0)
                return 0;

            textStore.WriteWithTexts(
                [],
                [id, .. seasons.Select(season => season.ID), .. episodes.Select(episode => episode.ID)],
                _ =>
                [
                    new MetadataRowChanges<Metadata_Series>(seriesRepository, [], seriesRow is null ? [] : [seriesRow]),
                    new MetadataRowChanges<Metadata_ContentRating>(contentRatingRepository, [], contentRatingRepository.GetByEntry(id)),
                    new MetadataRowChanges<Metadata_Season>(seasonRepository, [], seasons),
                    new MetadataRowChanges<Metadata_Episode>(episodeRepository, [], episodes),
                ]
            );

            if (seriesRow is not null)
                RaiseEvents(
                    seriesRow,
                    UpdateReason.Removed,
                    [.. seasons.Select(season => (season, UpdateReason.Removed))],
                    [.. episodes.Select(episode => (episode, UpdateReason.Removed))]
                );
            removed = [id, .. seasons.Select(season => season.ID), .. episodes.Select(episode => episode.ID)];
        }

        // Outside the lock, since the other stores take their own. The
        // orderings of the series go with it, whoever made them.
        cleanup.Remove(removed);
        orderings.Value.RemoveForSeries(id);
        return count;
    }

    #endregion

    #region Ordering State

    /// <summary>
    ///   Chooses an ordering for a stored series on the series' own row,
    ///   leaving the rest of it as it is.
    /// </summary>
    /// <param name="seriesID">The series.</param>
    /// <param name="orderingID">The ordering, or <c>null</c> for the series' default one.</param>
    /// <returns><c>true</c> if the choice changed; <c>false</c> when it stays or the series is not stored.</returns>
    internal bool SetPreferredOrdering(MetadataGuid seriesID, MetadataGuid? orderingID)
    {
        lock (_writeLock)
        {
            if (seriesRepository.GetByProviderID(seriesID.Source, seriesID.ID) is not { } stored || stored.PreferredOrderingID == orderingID)
                return false;

            // A copy, so the cached row stays as it was until the write has committed.
            var row = MetadataRows.Copy(stored)!;
            row.PreferredOrderingID = orderingID;
            textStore.WriteWithoutEntries([], new MetadataRowChanges<Metadata_Series>(seriesRepository, [row], []));
            return true;
        }
    }

    /// <summary>
    ///   Hides or shows a stored episode on the episode's own row, leaving
    ///   the rest of it as it is.
    /// </summary>
    /// <param name="episodeID">The episode.</param>
    /// <param name="hidden">Whether to hide it.</param>
    /// <returns><c>true</c> if the flag changed; <c>false</c> when it stays or the episode is not stored.</returns>
    internal bool SetEpisodeHidden(MetadataGuid episodeID, bool hidden)
    {
        lock (_writeLock)
        {
            if (episodeRepository.GetByProviderID(episodeID.Source, episodeID.ID) is not { } stored || stored.IsHidden == hidden)
                return false;

            // A copy, so the cached row stays as it was until the write has committed.
            var row = MetadataRows.Copy(stored)!;
            row.IsHidden = hidden;
            textStore.WriteWithoutEntries([], new MetadataRowChanges<Metadata_Episode>(episodeRepository, [row], []));
            return true;
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    ///   Removes the episode links naming episodes a save dropped. A series
    ///   that is removed whole leaves its links to the purge instead.
    /// </summary>
    /// <param name="episodes">The dropped episodes.</param>
    private void RemoveLinksTo(IReadOnlyList<MetadataGuid> episodes)
    {
        if (episodes.Count is 0)
            return;

        var links = episodes
            .SelectMany(episode => crossReferences.Value.GetLinksTo(episode))
            .OfType<IMetadataEpisodeCrossReference>()
            .ToList();
        if (links.Count is 0)
            return;

        // The store writes synchronously, so the task is already complete.
        crossReferences.Value.MergeEpisodeLinks([], links).GetAwaiter().GetResult();
        logger.LogDebug("Removed {Count} links to {Episodes} dropped episodes.", links.Count, episodes.Count);
    }

    /// <summary>
    ///   Queues the sync of the episode links naming a series' episodes.
    ///   Failing to queue it is logged, since the recurring sweep catches up.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>A task that completes once the job is queued.</returns>
    private async Task ScheduleLinkSync(MetadataGuid series)
    {
        try
        {
            await scheduler.Enqueue<SyncEpisodeLinksJob>(job =>
            {
                job.Source = series.Source.Value;
                job.SeriesID = series.ID;
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to queue the episode link sync for {Series}.", series);
        }
    }

    /// <summary>
    ///   What a write did to one row.
    /// </summary>
    /// <param name="isNew">Whether the row was not stored before.</param>
    /// <param name="isSame">Whether its columns are the same as the stored ones.</param>
    /// <param name="textsChanged">Whether its titles, descriptions or other rows kept with it changed.</param>
    /// <returns>Added, updated, or none.</returns>
    private static UpdateReason Reason(bool isNew, bool isSame, bool textsChanged)
        => isNew ? UpdateReason.Added : !isSame || textsChanged ? UpdateReason.Updated : UpdateReason.None;

    /// <summary>
    ///   Raises the series event, with the seasons and episodes that changed
    ///   along with it, and the season events on their own.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="reason">What happened to the series.</param>
    /// <param name="seasons">The seasons that changed.</param>
    /// <param name="episodes">The episodes that changed.</param>
    private static void RaiseEvents(
        Metadata_Series series,
        UpdateReason reason,
        IReadOnlyList<(Metadata_Season Row, UpdateReason Reason)> seasons,
        IReadOnlyList<(Metadata_Episode Row, UpdateReason Reason)> episodes
    )
    {
        var events = MetadataStoredEntry.Events;
        events.OnSeriesUpdated(
            series,
            reason,
            [.. seasons.Select(season => ((ISeason)season.Row, season.Reason))],
            [.. episodes.Select(episode => ((IEpisode)episode.Row, episode.Reason))]
        );
        foreach (var (season, seasonReason) in seasons)
            events.OnSeasonUpdated(series, season, seasonReason);
    }

    /// <summary>
    ///   Checks the seasons or episodes of a series being saved, keeping the
    ///   last copy of each.
    /// </summary>
    /// <typeparam name="TItem">The kind of item.</typeparam>
    /// <param name="items">The items, which may be left out.</param>
    /// <param name="idOf">The identifier of an item.</param>
    /// <param name="source">The series' source, which every item must be on.</param>
    /// <param name="entityType">The kind every item must name itself as.</param>
    /// <returns>The items by identifier, in the order first given.</returns>
    /// <exception cref="ArgumentNullException">An item, or its identifier, is <c>null</c>.</exception>
    /// <exception cref="ArgumentException">An item is on another source, or of another kind.</exception>
    private static Dictionary<MetadataGuid, TItem> Latest<TItem>(
        IReadOnlyList<TItem>? items,
        Func<TItem, MetadataGuid> idOf,
        MetadataSource source,
        MetadataEntityType entityType
    )
        where TItem : class
    {
        var latest = new Dictionary<MetadataGuid, TItem>();
        foreach (var item in items ?? [])
        {
            ArgumentNullException.ThrowIfNull(item, "series");
            var id = idOf(item);
            MetadataEntries.CheckReference(id, source, entityType, "series");
            latest[id] = item;
        }

        return latest;
    }

    #endregion
}
