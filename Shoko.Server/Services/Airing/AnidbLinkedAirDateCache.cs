using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Services.Airing;

/// <summary>
/// The air dates the date-only entries borrow from linked episodes for the
/// regular AniDB episodes of anime that started before 1970. AniDB dates
/// those episodes with its 1970-01-01 placeholder, stored like a missing date.
/// </summary>
/// <remarks>
/// Built on the first read that needs it, then kept. A change to an anime's
/// episode links, to the anime itself or to a linked episode marks the anime
/// stale, and the next read rebuilds only the stale anime.
/// </remarks>
internal sealed class AnidbLinkedAirDateCache
{
    /// <summary>
    /// The first date AniDB gives, and the first date the cache holds none
    /// before: every date it holds is earlier.
    /// </summary>
    public static readonly DateOnly Cutoff = new(1970, 1, 1);

    private readonly Lazy<IMetadataCrossReferenceStore> _crossReferences;

    private readonly Lazy<IMetadataService> _metadataService;

    private readonly Lazy<IMetadataLinkingService> _linkingService;

    private readonly Lock _buildLock = new();

    private readonly ConcurrentDictionary<int, byte> _stale = [];

    private volatile Snapshot? _snapshot;

    private volatile bool _hasStale;

    private int _listening;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnidbLinkedAirDateCache"/> class.
    /// </summary>
    /// <param name="crossReferences">The link store the dates are read through.</param>
    /// <param name="metadataService">Raises the anime and episode updates.</param>
    /// <param name="linkingService">Raises the link changes.</param>
    /// <exception cref="ArgumentNullException">Any of the parameters is <c>null</c>.</exception>
    public AnidbLinkedAirDateCache(
        Lazy<IMetadataCrossReferenceStore> crossReferences,
        Lazy<IMetadataService> metadataService,
        Lazy<IMetadataLinkingService> linkingService
    )
    {
        ArgumentNullException.ThrowIfNull(crossReferences);
        ArgumentNullException.ThrowIfNull(metadataService);
        ArgumentNullException.ThrowIfNull(linkingService);

        _crossReferences = crossReferences;
        _metadataService = metadataService;
        _linkingService = linkingService;
    }

    #region Reading

    /// <summary>
    /// Whether an anime starting on a date is one the cache covers.
    /// </summary>
    /// <param name="airDate">The anime's start date, if any.</param>
    /// <returns><c>true</c> when it started before 1970.</returns>
    public static bool IsCovered(PartialDateOnly? airDate)
        => airDate is { Year: < 1970 };

    /// <summary>
    /// The earliest date linked to a regular AniDB episode, for an episode
    /// of an anime the cache covers.
    /// </summary>
    /// <param name="anidbEpisodeID">The AniDB episode ID.</param>
    /// <returns>The date, or <c>null</c> when no linked episode has one.</returns>
    public DateOnly? GetAirDate(int anidbEpisodeID)
        => GetSnapshot().Dates.TryGetValue(anidbEpisodeID, out var date) ? date : null;

    /// <summary>
    /// The AniDB episodes whose linked date falls between two dates. A range
    /// starting on or after <see cref="Cutoff"/> is answered without looking.
    /// </summary>
    /// <param name="firstDate">The first date of the range.</param>
    /// <param name="lastDate">The last date of the range.</param>
    /// <returns>The AniDB episode IDs, by date.</returns>
    public IReadOnlyList<int> GetEpisodesInRange(DateOnly firstDate, DateOnly lastDate)
    {
        if (firstDate >= Cutoff || lastDate < firstDate)
            return [];

        var byDate = GetSnapshot().ByDate;
        var index = Array.BinarySearch(byDate, (firstDate, int.MinValue));
        if (index < 0)
            index = ~index;

        var episodes = new List<int>();
        for (; index < byDate.Length && byDate[index].Date <= lastDate; index++)
            episodes.Add(byDate[index].EpisodeID);

        return episodes;
    }

    /// <summary>
    /// The current snapshot, built in full the first time and with the stale
    /// anime rebuilt afterwards.
    /// </summary>
    /// <returns>The snapshot.</returns>
    private Snapshot GetSnapshot()
    {
        if (_snapshot is { } current && !_hasStale)
            return current;

        lock (_buildLock)
        {
            Listen();
            if (_snapshot is { } snapshot && !_hasStale)
                return snapshot;

            // Taken before the rows are read, so a change made meanwhile is
            // picked up by the next read.
            _hasStale = false;
            var stale = _stale.Keys.ToList();
            foreach (var animeID in stale)
                _stale.TryRemove(animeID, out _);

            Dictionary<int, AnimeDates> anime;
            if (_snapshot is { } previous)
            {
                anime = new(previous.Anime);
                foreach (var animeID in stale)
                {
                    if (BuildAnime(animeID) is { } dates)
                        anime[animeID] = dates;
                    else
                        anime.Remove(animeID);
                }
            }
            else
            {
                anime = [];
                foreach (var row in RepoFactory.AniDB_Anime.GetAll())
                {
                    if (IsCovered(row.AirDate) && BuildAnime(row.AnimeID) is { } dates)
                        anime[row.AnimeID] = dates;
                }
            }

            return _snapshot = new(anime);
        }
    }

    /// <summary>
    /// The linked dates of one anime's undated regular episodes, and every
    /// linked episode they were read from.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <returns>The dates, or <c>null</c> when the cache does not cover the anime or it links nothing.</returns>
    private AnimeDates? BuildAnime(int animeID)
    {
        if (!IsCovered(RepoFactory.AniDB_Anime.GetByAnimeID(animeID)?.AirDate))
            return null;

        var undated = RepoFactory.AniDB_Episode.GetByAnimeID(animeID)
            .Where(episode => episode.EpisodeType is EpisodeType.Episode && episode.AirDate is 0)
            .Select(episode => episode.EpisodeID)
            .ToHashSet();
        if (undated.Count is 0)
            return null;

        var dates = new Dictionary<int, DateOnly>();
        var linked = new HashSet<MetadataGuid>();
        foreach (var link in _crossReferences.Value.GetEpisodeLinksForSeries(animeID))
        {
            if (!undated.Contains(link.AnidbEpisodeID) || link.ProviderID is not { } providerID)
                continue;

            // A later date is not what the placeholder stands for, and no
            // range read starting in 1970 or later looks here.
            linked.Add(providerID);
            if (link.Provider is not IEpisode { AirDate: { } date } || date >= Cutoff)
                continue;
            if (!dates.TryGetValue(link.AnidbEpisodeID, out var earliest) || date < earliest)
                dates[link.AnidbEpisodeID] = date;
        }

        return linked.Count is 0 ? null : new(dates, linked);
    }

    #endregion

    #region Invalidation

    /// <summary>
    /// Starts listening to the changes that make an anime stale, once.
    /// </summary>
    private void Listen()
    {
        if (Interlocked.Exchange(ref _listening, 1) is 1)
            return;

        var metadataService = _metadataService.Value;
        metadataService.SeriesAdded += OnSeriesChanged;
        metadataService.SeriesUpdated += OnSeriesChanged;
        metadataService.SeriesRemoved += OnSeriesChanged;
        metadataService.EpisodeAdded += OnEpisodeChanged;
        metadataService.EpisodeUpdated += OnEpisodeChanged;
        metadataService.EpisodeRemoved += OnEpisodeChanged;
        _linkingService.Value.LinksChanged += OnLinksChanged;
    }

    /// <summary>
    /// Marks an AniDB anime stale when the cache covers it, or did.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <param name="airDate">The anime's start date, if known; read from the anime otherwise.</param>
    private void MarkStale(int animeID, PartialDateOnly? airDate = null)
    {
        if (_snapshot?.Anime.ContainsKey(animeID) is true || IsCovered(airDate ?? RepoFactory.AniDB_Anime.GetByAnimeID(animeID)?.AirDate))
            AddStale(animeID);
    }

    /// <summary>
    /// Marks an AniDB anime stale.
    /// </summary>
    /// <param name="animeID">The AniDB anime ID.</param>
    private void AddStale(int animeID)
    {
        _stale[animeID] = 0;
        _hasStale = true;
    }

    /// <summary>
    /// Marks an AniDB anime stale when it is added, updated or removed.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="eventArgs">The update.</param>
    private void OnSeriesChanged(object? sender, SeriesInfoUpdatedEventArgs eventArgs)
    {
        var series = eventArgs.SeriesInfo;
        if (series.Source == MetadataSource.AniDB && int.TryParse(series.ID.ID, out var animeID))
            MarkStale(animeID, series.AirDate);
    }

    /// <summary>
    /// Marks the anime linked to an episode stale when another source adds,
    /// updates or removes it.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="eventArgs">The update.</param>
    private void OnEpisodeChanged(object? sender, EpisodeInfoUpdatedEventArgs eventArgs)
    {
        if (_snapshot is not { } snapshot || !snapshot.AnimeByLinkedEpisode.TryGetValue(eventArgs.EpisodeInfo.ID, out var animeIDs))
            return;

        foreach (var animeID in animeIDs)
            AddStale(animeID);
    }

    /// <summary>
    /// Marks the anime whose episode links changed stale.
    /// </summary>
    /// <param name="sender">The sender.</param>
    /// <param name="eventArgs">The change.</param>
    private void OnLinksChanged(object? sender, MetadataLinksChangedEventArgs eventArgs)
    {
        var animeIDs = eventArgs.Changes
            .Where(change => change.EntityType == MetadataEntityType.Episode)
            .Select(change => change.AnidbAnimeID)
            .Distinct();
        foreach (var animeID in animeIDs)
            MarkStale(animeID);
    }

    #endregion

    #region Snapshot

    /// <summary>
    /// The linked dates of one anime.
    /// </summary>
    /// <param name="Dates">The earliest linked date, by AniDB episode ID.</param>
    /// <param name="LinkedEpisodes">Every linked episode, dated or not, so an update to one marks the anime stale.</param>
    private sealed record AnimeDates(IReadOnlyDictionary<int, DateOnly> Dates, IReadOnlySet<MetadataGuid> LinkedEpisodes);

    /// <summary>
    /// One build of the cache, never changed once published.
    /// </summary>
    private sealed class Snapshot
    {
        /// <summary>
        /// The covered anime with links, by AniDB anime ID.
        /// </summary>
        public IReadOnlyDictionary<int, AnimeDates> Anime { get; }

        /// <summary>
        /// The linked date of every dated episode, by AniDB episode ID.
        /// </summary>
        public IReadOnlyDictionary<int, DateOnly> Dates { get; }

        /// <summary>
        /// Every dated episode, by date and then ID, for the range reads.
        /// </summary>
        public (DateOnly Date, int EpisodeID)[] ByDate { get; }

        /// <summary>
        /// The anime linking to each linked episode.
        /// </summary>
        public IReadOnlyDictionary<MetadataGuid, List<int>> AnimeByLinkedEpisode { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Snapshot"/> class.
        /// </summary>
        /// <param name="anime">The covered anime with links, by AniDB anime ID.</param>
        public Snapshot(Dictionary<int, AnimeDates> anime)
        {
            Anime = anime;
            var dates = new Dictionary<int, DateOnly>();
            var byLinkedEpisode = new Dictionary<MetadataGuid, List<int>>();
            foreach (var (animeID, entry) in anime)
            {
                foreach (var (episodeID, date) in entry.Dates)
                    dates[episodeID] = date;
                foreach (var linkedEpisode in entry.LinkedEpisodes)
                {
                    if (!byLinkedEpisode.TryGetValue(linkedEpisode, out var animeIDs))
                        byLinkedEpisode[linkedEpisode] = animeIDs = [];
                    animeIDs.Add(animeID);
                }
            }

            var byDate = new (DateOnly Date, int EpisodeID)[dates.Count];
            var index = 0;
            foreach (var (episodeID, date) in dates)
                byDate[index++] = (date, episodeID);
            Array.Sort(byDate);

            Dates = dates;
            ByDate = byDate;
            AnimeByLinkedEpisode = byLinkedEpisode;
        }
    }

    #endregion
}
