using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Repositories.Direct.TMDB;
using Shoko.Server.Repositories.Direct.TMDB.Optional;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using TMDbLib.Objects.General;

using TitleLanguage = Shoko.Abstractions.Metadata.Enums.TitleLanguage;

#pragma warning disable CS0618
// Suggestions we don't need in this file.
#pragma warning disable CA1822
#pragma warning disable CA1826

namespace Shoko.Server.Providers.TMDB;

/// <summary>
///   Brings TMDB's own tables up to date for the TMDB provider: shows with
///   their seasons, episodes, alternate orderings and networks, movies with
///   their collections, the people credited on them, companies, titles and
///   overviews, and removes what is no longer wanted.
/// </summary>
/// <remarks>
///   The core decides when an entry is due and holds its lock while this
///   works on it, so nothing here checks freshness or locks a show or a
///   movie. People, networks and collections are shared between entries, so
///   those are still locked here, one at a time.
/// </remarks>
public partial class TmdbMetadataUpdater
{
    #region Fields

    private readonly ILogger<TmdbMetadataUpdater> _logger;

    private readonly IQueueScheduler _scheduler;

    private readonly ISettingsProvider _settingsProvider;

    private readonly TmdbApiClient _client;

    private readonly TmdbImageService _imageService;

    private readonly Lazy<IMetadataLinkingService> _linkingService;

    private readonly MetadataLinkChangeTracker _linkChanges;

    private readonly Lazy<IMetadataRefreshService> _refreshService;

    private readonly TMDB_AlternateOrderingRepository _tmdbAlternateOrdering;

    private readonly TMDB_AlternateOrdering_EpisodeRepository _tmdbAlternateOrderingEpisodes;

    private readonly TMDB_AlternateOrdering_SeasonRepository _tmdbAlternateOrderingSeasons;

    private readonly TMDB_CollectionRepository _tmdbCollections;

    private readonly TMDB_CompanyRepository _tmdbCompany;

    private readonly TMDB_EpisodeRepository _tmdbEpisodes;

    private readonly TMDB_Episode_CastRepository _tmdbEpisodeCast;

    private readonly TMDB_Episode_CrewRepository _tmdbEpisodeCrew;

    private readonly TMDB_MovieRepository _tmdbMovies;

    private readonly TMDB_Movie_CastRepository _tmdbMovieCast;

    private readonly TMDB_Movie_CrewRepository _tmdbMovieCrew;

    private readonly TMDB_NetworkRepository _tmdbNetwork;

    private readonly TMDB_PersonRepository _tmdbPeople;

    private readonly TMDB_SeasonRepository _tmdbSeasons;

    private readonly TMDB_ShowRepository _tmdbShows;

    private readonly MetadataTextStore _textStore;

    private readonly CrossRef_AniDB_TMDB_MovieRepository _xrefAnidbTmdbMovies;

    private readonly CrossRef_AniDB_TMDB_ShowRepository _xrefAnidbTmdbShows;

    private readonly TMDB_Collection_MovieRepository _xrefTmdbCollectionMovies;

    private readonly TMDB_Company_EntityRepository _xrefTmdbCompanyEntity;

    private readonly TMDB_Show_NetworkRepository _xrefTmdbShowNetwork;

    private readonly TMDB_SuggestionRepository _tmdbSuggestion;

    private readonly KeyedEntityLockHelper _entityLock;

    private readonly MetadataEntryLocks _entryLocks;

    #endregion

    #region Constructors

    /// <summary>
    ///   Takes the tables it keeps up to date and what it asks of TMDB and the
    ///   core.
    /// </summary>
    /// <param name="logger">Where the updates are logged.</param>
    /// <param name="scheduler">Queues the retries of people TMDB could not give for now.</param>
    /// <param name="settingsProvider">Holds TMDB's settings and the preferred languages.</param>
    /// <param name="client">Calls TMDB.</param>
    /// <param name="imageService">Links the logos and unlinks the images of what is removed.</param>
    /// <param name="linkingService">Matches and saves the episodes of a refreshed show.</param>
    /// <param name="refreshService">Queues the refresh of the entries a removed person was credited on.</param>
    /// <param name="tmdbAlternateOrdering">TMDB's alternate orderings.</param>
    /// <param name="tmdbAlternateOrderingEpisodes">The episodes of the alternate orderings.</param>
    /// <param name="tmdbAlternateOrderingSeasons">The seasons of the alternate orderings.</param>
    /// <param name="tmdbCollections">TMDB's collections.</param>
    /// <param name="tmdbCompany">TMDB's companies.</param>
    /// <param name="tmdbEpisodes">TMDB's episodes.</param>
    /// <param name="tmdbEpisodeCast">The episodes' cast.</param>
    /// <param name="tmdbEpisodeCrew">The episodes' crew.</param>
    /// <param name="tmdbMovies">TMDB's movies.</param>
    /// <param name="tmdbMovieCast">The movies' cast.</param>
    /// <param name="tmdbMovieCrew">The movies' crew.</param>
    /// <param name="tmdbNetwork">TMDB's networks.</param>
    /// <param name="tmdbPeople">TMDB's people.</param>
    /// <param name="tmdbSeasons">TMDB's seasons.</param>
    /// <param name="tmdbShows">TMDB's shows.</param>
    /// <param name="xrefAnidbTmdbMovies">The movie links.</param>
    /// <param name="xrefAnidbTmdbShows">The show links.</param>
    /// <param name="xrefTmdbCollectionMovies">Which movies each collection holds.</param>
    /// <param name="xrefTmdbCompanyEntity">Which companies worked on what.</param>
    /// <param name="xrefTmdbShowNetwork">Which networks aired which shows.</param>
    /// <param name="tmdbSuggestion">TMDB's suggestions.</param>
    /// <param name="entryLocks">The core's locks, which the image job takes for a person or network too.</param>
    /// <param name="textStore">Stores the titles and overviews TMDB lists.</param>
    /// <param name="linkChanges">Reports the episode links a refresh matches again as auto-linked.</param>
    public TmdbMetadataUpdater(
        ILogger<TmdbMetadataUpdater> logger,
        IQueueScheduler scheduler,
        ISettingsProvider settingsProvider,
        TmdbApiClient client,
        TmdbImageService imageService,
        Lazy<IMetadataLinkingService> linkingService,
        Lazy<IMetadataRefreshService> refreshService,
        TMDB_AlternateOrderingRepository tmdbAlternateOrdering,
        TMDB_AlternateOrdering_EpisodeRepository tmdbAlternateOrderingEpisodes,
        TMDB_AlternateOrdering_SeasonRepository tmdbAlternateOrderingSeasons,
        TMDB_CollectionRepository tmdbCollections,
        TMDB_CompanyRepository tmdbCompany,
        TMDB_EpisodeRepository tmdbEpisodes,
        TMDB_Episode_CastRepository tmdbEpisodeCast,
        TMDB_Episode_CrewRepository tmdbEpisodeCrew,
        TMDB_MovieRepository tmdbMovies,
        TMDB_Movie_CastRepository tmdbMovieCast,
        TMDB_Movie_CrewRepository tmdbMovieCrew,
        TMDB_NetworkRepository tmdbNetwork,
        TMDB_PersonRepository tmdbPeople,
        TMDB_SeasonRepository tmdbSeasons,
        TMDB_ShowRepository tmdbShows,
        CrossRef_AniDB_TMDB_MovieRepository xrefAnidbTmdbMovies,
        CrossRef_AniDB_TMDB_ShowRepository xrefAnidbTmdbShows,
        TMDB_Collection_MovieRepository xrefTmdbCollectionMovies,
        TMDB_Company_EntityRepository xrefTmdbCompanyEntity,
        TMDB_Show_NetworkRepository xrefTmdbShowNetwork,
        TMDB_SuggestionRepository tmdbSuggestion,
        MetadataEntryLocks entryLocks,
        MetadataTextStore textStore,
        MetadataLinkChangeTracker linkChanges
    )
    {
        _logger = logger;
        _linkChanges = linkChanges;
        _scheduler = scheduler;
        _settingsProvider = settingsProvider;
        _client = client;
        _imageService = imageService;
        _linkingService = linkingService;
        _refreshService = refreshService;
        _tmdbAlternateOrdering = tmdbAlternateOrdering;
        _tmdbAlternateOrderingEpisodes = tmdbAlternateOrderingEpisodes;
        _tmdbAlternateOrderingSeasons = tmdbAlternateOrderingSeasons;
        _tmdbCollections = tmdbCollections;
        _tmdbCompany = tmdbCompany;
        _tmdbEpisodes = tmdbEpisodes;
        _tmdbEpisodeCast = tmdbEpisodeCast;
        _tmdbEpisodeCrew = tmdbEpisodeCrew;
        _tmdbMovies = tmdbMovies;
        _tmdbMovieCast = tmdbMovieCast;
        _tmdbMovieCrew = tmdbMovieCrew;
        _tmdbNetwork = tmdbNetwork;
        _tmdbPeople = tmdbPeople;
        _tmdbSeasons = tmdbSeasons;
        _tmdbShows = tmdbShows;
        _textStore = textStore;
        _xrefAnidbTmdbMovies = xrefAnidbTmdbMovies;
        _xrefAnidbTmdbShows = xrefAnidbTmdbShows;
        _xrefTmdbCollectionMovies = xrefTmdbCollectionMovies;
        _xrefTmdbCompanyEntity = xrefTmdbCompanyEntity;
        _xrefTmdbShowNetwork = xrefTmdbShowNetwork;
        _tmdbSuggestion = tmdbSuggestion;
        _entityLock = new(logger);
        _entryLocks = entryLocks;
    }

    #endregion

    #region Helpers

    /// <summary>
    ///   Runs work over items, a few at a time, and gives up on the rest once
    ///   as many have failed as run at once.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="maxConcurrent">How many run at once.</param>
    /// <param name="enumerable">The items.</param>
    /// <param name="processAsync">The work for one item.</param>
    /// <param name="onDropped">Told about each item given up on.</param>
    /// <returns>A task that completes once every item is done or given up on.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxConcurrent"/> is below one.</exception>
    /// <exception cref="AggregateException">One or more items failed.</exception>
    private static async Task ProcessWithConcurrencyAsync<T>(
        int maxConcurrent,
        IEnumerable<T> enumerable,
        Func<T, Task> processAsync,
        Action<T>? onDropped = null
    )
    {
        if (maxConcurrent < 1)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrent), "Concurrency level must be at least 1.");

        var exceptions = new ConcurrentBag<Exception>();
        using var cts = new CancellationTokenSource();
        var failureCount = 0;
        var block = new ActionBlock<T>(
            async item =>
            {
                if (cts.IsCancellationRequested) { onDropped?.Invoke(item); return; }
                try { await processAsync(item); }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    if (Interlocked.Increment(ref failureCount) >= maxConcurrent)
                        await cts.CancelAsync();
                }
            },
            new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = maxConcurrent }
        );
        foreach (var item in enumerable)
            block.Post(item);
        block.Complete();
        await block.Completion.ConfigureAwait(false);
        if (!exceptions.IsEmpty)
            throw new AggregateException(exceptions);
    }

    #endregion

    #region External IDs

    /// <summary>
    /// Update TvDB ID for the TMDB show if needed and the ID is available.
    /// </summary>
    /// <param name="show">TMDB Show.</param>
    /// <param name="externalIds">External IDs.</param>
    /// <returns>Indicates that the ID was updated.</returns>
    private bool UpdateShowExternalIDs(TMDB_Show show, ExternalIdsTvShow externalIds)
    {
        if (string.IsNullOrEmpty(externalIds.TvdbId))
        {
            if (!show.TvdbShowID.HasValue)
                return false;

            show.TvdbShowID = null;
            return true;
        }

        if (!int.TryParse(externalIds.TvdbId, out var tvdbId) || tvdbId <= 0 || show.TvdbShowID == tvdbId)
            return false;

        show.TvdbShowID = tvdbId;
        return true;
    }

    /// <summary>
    /// Update TvDB ID for the TMDB episode if needed and the ID is available.
    /// </summary>
    /// <param name="episode">TMDB Episode.</param>
    /// <param name="externalIds">External IDs.</param>
    /// <returns>Indicates that the ID was updated.</returns>
    private bool UpdateEpisodeExternalIDs(TMDB_Episode episode, ExternalIdsTvEpisode externalIds)
    {
        if (string.IsNullOrEmpty(externalIds.TvdbId))
        {
            if (!episode.TvdbEpisodeID.HasValue)
                return false;

            episode.TvdbEpisodeID = null;
            return true;
        }

        if (!int.TryParse(externalIds.TvdbId, out var tvdbId) || tvdbId <= 0 || episode.TvdbEpisodeID == tvdbId)
            return false;

        episode.TvdbEpisodeID = tvdbId;
        return true;
    }

    /// <summary>
    /// Update IMDb ID for the TMDB movie if needed and the ID is available.
    /// </summary>
    /// <param name="movie">TMDB Movie.</param>
    /// <param name="externalIds">External IDs.</param>
    /// <returns>Indicates that the ID was updated.</returns>
    private bool UpdateMovieExternalIDs(TMDB_Movie movie, ExternalIdsMovie externalIds)
    {
        if (movie.ImdbMovieID == externalIds.ImdbId)
            return false;

        movie.ImdbMovieID = externalIds.ImdbId;
        return true;
    }

    #endregion

    #region Locking

    /// <summary>
    ///   Takes the lock of an entity shared between entries.
    /// </summary>
    /// <remarks>
    ///   A person or network takes the core's lock for it, which the image
    ///   reconciler takes while it links the entity's images, so they are
    ///   never linked to one being removed here.
    /// </remarks>
    /// <param name="entityType">The kind of entity.</param>
    /// <param name="id">Its TMDB ID.</param>
    /// <param name="metadataKey">What is locked.</param>
    /// <param name="reason">Why, for the log.</param>
    /// <returns>A handle that releases the lock when disposed.</returns>
    private Task<IDisposable> GetLockForEntity(MetadataEntityType entityType, int id, string metadataKey, string reason)
        => entityType == MetadataEntityType.Creator || entityType == MetadataEntityType.Network
            ? _entryLocks.Acquire(new(MetadataSource.TMDB, entityType, id.ToString()))
            : _entityLock.GetLockForEntityAsync(entityType, id, metadataKey, reason);

    #endregion

    #region Nested Types

    private readonly record struct TmdbSeasonEpisodeCounts(int EpisodeCount, int HiddenEpisodeCount);

    private readonly record struct TmdbSeasonEpisodeUpdateResult(
        bool EpisodesOrSeasonsUpdated,
        Dictionary<TMDB_Season, UpdateReason> UpdatedSeasons,
        Dictionary<TMDB_Episode, UpdateReason> UpdatedEpisodes,
        int EpisodeCount,
        int HiddenEpisodeCount);

    private sealed class ShowSyncState
    {
        public required bool DownloadCrewAndCast { get; init; }
        public required bool QuickRefresh { get; init; }
        public required bool ShouldFireEvents { get; init; }
        public required HashSet<TitleLanguage>? PreferredTitleLanguages { get; init; }
        public required HashSet<TitleLanguage>? PreferredOverviewLanguages { get; init; }
        public required TmdbShowChangedItems? ChangedItems { get; init; }

        public int SeasonsAdded;
        public int EpisodesAdded;
        public int TotalEpisodeCount;
        public int TotalHiddenEpisodeCount;
        public readonly HashSet<int> SeasonsToSkip = [];
        public readonly List<TMDB_Season> SeasonsToSave = [];
        public readonly Dictionary<TMDB_Season, UpdateReason> SeasonEvents = [];
        public readonly HashSet<int> EpisodesToSkip = [];
        public readonly List<TMDB_Episode> EpisodesToSave = [];
        public readonly Dictionary<TMDB_Episode, UpdateReason> EpisodeEvents = [];
        public readonly HashSet<int> PeopleToAddOrKeep = [];
        public readonly HashSet<int> PeopleToPotentiallyRemove = [];
    }

    #endregion
}
