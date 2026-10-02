using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image;
using Shoko.Abstractions.Metadata.Matching;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.Services;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using TMDbLib.Objects.Exceptions;
using TMDbLib.Objects.Movies;
using TMDbLib.Objects.Search;
using TMDbLib.Objects.TvShows;

using TmdbImage = TMDbLib.Objects.General.ImageData;

namespace Shoko.Server.Providers.TMDB;

/// <summary>
///   Supplies TMDB metadata through the same contract a plugin provider
///   implements.
/// </summary>
/// <remarks>
///   TMDB keeps its own tables rather than the core's stores: a refresh writes
///   them, <see cref="CleanUp"/> purges them, and the core reads entries and
///   their last refresh back from them. A forced refresh comes without
///   <see cref="MetadataRefreshOptions.LastRefreshedAt"/> for an entry fetched
///   before, and fetches everything again, the people included.
/// </remarks>
public class TmdbMetadataProvider : IMetadataSeriesLinkingProvider, IMetadataMovieLinkingProvider, IMetadataCollectionProvider,
    IMetadataAutoLinkingProvider, IMetadataImageProvider, IPausableMetadataProvider, ICoreMetadataOrphanPurger
{
    #region Fields

    private readonly ILogger<TmdbMetadataProvider> _logger;

    private readonly TmdbMetadataUpdater _updater;

    private readonly TmdbApiClient _client;

    private readonly TmdbRateLimiter _rateLimiter;

    private readonly ISettingsProvider _settingsProvider;

    private readonly IMetadataMatchingEngine _matchingEngine;

    private readonly TmdbSearchService _searchService;

    private readonly AniDB_AnimeRepository _anidbAnime;

    private readonly TMDB_ShowRepository _tmdbShows;

    private readonly TMDB_SeasonRepository _tmdbSeasons;

    private readonly TMDB_EpisodeRepository _tmdbEpisodes;

    private readonly TMDB_MovieRepository _tmdbMovies;

    private readonly TMDB_AlternateOrdering_SeasonRepository _tmdbAlternateOrderingSeasons;

    private readonly CrossRef_AniDB_TMDB_EpisodeRepository _xrefAnidbTmdbEpisodes;

    #endregion

    #region Constructors

    /// <summary>
    ///   Takes the helpers that do TMDB's work.
    /// </summary>
    /// <param name="logger">Where the auto-searches are logged.</param>
    /// <param name="updater">Brings TMDB's tables up to date and clears them.</param>
    /// <param name="client">Calls TMDB for the images.</param>
    /// <param name="rateLimiter">Tells when TMDB's requests are paused.</param>
    /// <param name="settingsProvider">Holds whether other anime's episode links are considered by default.</param>
    /// <param name="matchingEngine">Lines an anime's episodes up with a show's.</param>
    /// <param name="searchService">Searches TMDB for users and for auto-linking.</param>
    /// <param name="anidbAnime">The AniDB anime to auto-search for.</param>
    /// <param name="tmdbShows">TMDB's shows.</param>
    /// <param name="tmdbSeasons">TMDB's seasons.</param>
    /// <param name="tmdbEpisodes">TMDB's episodes.</param>
    /// <param name="tmdbMovies">TMDB's movies.</param>
    /// <param name="tmdbAlternateOrderingSeasons">The seasons of TMDB's alternate orderings.</param>
    /// <param name="xrefAnidbTmdbEpisodes">TMDB's view of the episode links.</param>
    public TmdbMetadataProvider(
        ILogger<TmdbMetadataProvider> logger,
        TmdbMetadataUpdater updater,
        TmdbApiClient client,
        TmdbRateLimiter rateLimiter,
        ISettingsProvider settingsProvider,
        IMetadataMatchingEngine matchingEngine,
        TmdbSearchService searchService,
        AniDB_AnimeRepository anidbAnime,
        TMDB_ShowRepository tmdbShows,
        TMDB_SeasonRepository tmdbSeasons,
        TMDB_EpisodeRepository tmdbEpisodes,
        TMDB_MovieRepository tmdbMovies,
        TMDB_AlternateOrdering_SeasonRepository tmdbAlternateOrderingSeasons,
        CrossRef_AniDB_TMDB_EpisodeRepository xrefAnidbTmdbEpisodes
    )
    {
        _logger = logger;
        _updater = updater;
        _client = client;
        _rateLimiter = rateLimiter;
        _settingsProvider = settingsProvider;
        _matchingEngine = matchingEngine;
        _searchService = searchService;
        _anidbAnime = anidbAnime;
        _tmdbShows = tmdbShows;
        _tmdbSeasons = tmdbSeasons;
        _tmdbEpisodes = tmdbEpisodes;
        _tmdbMovies = tmdbMovies;
        _tmdbAlternateOrderingSeasons = tmdbAlternateOrderingSeasons;
        _xrefAnidbTmdbEpisodes = xrefAnidbTmdbEpisodes;
        _rateLimiter.PauseStateChanged += (_, _) => PauseStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Provider

    /// <inheritdoc />
    public string Name => "TMDB";

    /// <inheritdoc />
    public string? Description => "The Movie Database, the core's source for shows, films and the collections they belong to.";

    /// <inheritdoc />
    public MetadataSource Source => MetadataSource.TMDB;

    /// <inheritdoc />
    public bool AutoLinkByDefault => true;

    /// <inheritdoc />
    public bool AutoLinkRestrictedByDefault => true;

    /// <inheritdoc />
    public bool IsConfigured => _client.HasApiKey;

    /// <inheritdoc />
    public string? NotConfiguredReason => _client.HasApiKey ? null : TmdbApiKeyUnavailableException.Reason;

    /// <inheritdoc />
    /// <remarks>
    ///   TMDB's requests are paced by its rate limiter whatever runs them, so
    ///   this only keeps a library-wide refresh from crowding out the rest of
    ///   the queue.
    /// </remarks>
    public int? MaxConcurrentJobs => 4;

    /// <inheritdoc />
    /// <remarks>
    ///   Removes what TMDB's own tables hold for a purged show, movie or
    ///   collection.
    /// </remarks>
    public async Task CleanUp(MetadataGuid entryID, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entryID);
        if (entryID.Source != MetadataSource.TMDB || !entryID.TryGetNumericID<int>(out var tmdbID) || tmdbID <= 0)
            return;

        if (entryID.EntityType == MetadataEntityType.Series)
            await _updater.PurgeShow(tmdbID).ConfigureAwait(false);
        else if (entryID.EntityType == MetadataEntityType.Movie)
            await _updater.PurgeMovie(tmdbID).ConfigureAwait(false);
        else if (entryID.EntityType == MetadataEntityType.Collection)
            await _updater.PurgeCollection(tmdbID).ConfigureAwait(false);
    }

    /// <summary>
    ///   Clears what TMDB's tables still hold for unlinked shows, movies and
    ///   collections with no row of their own, queues a refresh of the linked
    ///   ones with no row, then stamps the people and networks nothing uses
    ///   any more and removes the ones stamped before a cutoff.
    /// </summary>
    /// <param name="orphanedBefore">Remove only the people and networks orphaned before this time.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>How many leftover entries, people and networks were removed.</returns>
    async Task<int> ICoreMetadataOrphanPurger.PurgeOrphaned(DateTime orphanedBefore, CancellationToken cancellationToken)
    {
        var removed = await _updater.PurgeLeftovers(cancellationToken).ConfigureAwait(false);
        await _updater.RestoreLinkedLeftovers(cancellationToken).ConfigureAwait(false);
        return removed +
            await _updater.PurgeOrphanedPeople(orphanedBefore).ConfigureAwait(false) +
            await _updater.PurgeUnlinkedShowNetworks(orphanedBefore).ConfigureAwait(false);
    }

    #endregion

    #region Refresh

    /// <inheritdoc />
    public async Task RefreshSeries(MetadataGuid seriesID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seriesID);
        ArgumentNullException.ThrowIfNull(options);
        if (!TryGetID(seriesID, MetadataEntityType.Series, out var showID))
            return;

        var show = _tmdbShows.GetByTmdbShowID(showID);
        var forced = options.LastRefreshedAt is null && show is not null && show.CreatedAt != show.LastUpdatedAt;
        await _updater.UpdateShow(showID, options, forced).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RefreshMovie(MetadataGuid movieID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(movieID);
        ArgumentNullException.ThrowIfNull(options);
        if (!TryGetID(movieID, MetadataEntityType.Movie, out var tmdbMovieID))
            return;

        var movie = _tmdbMovies.GetByTmdbMovieID(tmdbMovieID);
        var forced = options.LastRefreshedAt is null && movie is not null && movie.CreatedAt != movie.LastUpdatedAt;
        await _updater.UpdateMovie(tmdbMovieID, options, forced).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RefreshCollection(MetadataGuid collectionID, MetadataRefreshOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(collectionID);
        ArgumentNullException.ThrowIfNull(options);
        if (!TryGetID(collectionID, MetadataEntityType.Collection, out var tmdbCollectionID))
            return;

        await _updater.UpdateCollection(tmdbCollectionID).ConfigureAwait(false);
    }

    /// <summary>
    ///   Reads the TMDB ID out of an entry of a kind.
    /// </summary>
    /// <param name="entryID">The entry.</param>
    /// <param name="entityType">The kind it must be.</param>
    /// <param name="tmdbID">The TMDB ID.</param>
    /// <returns><see langword="true"/> when the entry is a TMDB one of the kind with a valid ID.</returns>
    private static bool TryGetID(MetadataGuid entryID, MetadataEntityType entityType, out int tmdbID)
    {
        tmdbID = 0;
        return entryID.Source == MetadataSource.TMDB && entryID.EntityType == entityType && entryID.TryGetNumericID(out tmdbID) && tmdbID > 0;
    }

    #endregion

    #region Pausing

    /// <inheritdoc />
    public MetadataProviderPauseStatus PauseStatus
    {
        get
        {
            var (isPaused, reason, remaining) = _rateLimiter.GetPauseSnapshot();
            if (!isPaused)
                return MetadataProviderPauseStatus.NotPaused;

            return new()
            {
                IsPaused = true,
                Reason = reason is TmdbPauseReason.RateLimited
                    ? "TMDB is limiting the rate of requests, so its requests are paused for a while."
                    : "TMDB answered with server errors, so its requests are paused for a while.",
                ResumesAt = remaining is { } left ? DateTime.UtcNow + left : null,
            };
        }
    }

    /// <inheritdoc />
    public event EventHandler? PauseStatusChanged;

    #endregion

    #region Search

    /// <inheritdoc />
    public async Task<(IReadOnlyList<MetadataSeriesSearchResult> Page, int TotalCount)> SearchSeries(MetadataSearchOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var (page, total) = await Upstream(() => ((ITmdbSearchService)_searchService)
            .SearchShows(options.Query, options.IncludeRestricted, options.Year ?? 0, options.Page, options.PageSize)
        ).ConfigureAwait(false);
        return ([.. page.Select(result => new MetadataSeriesSearchResult
        {
            ID = new(MetadataSource.TMDB, MetadataEntityType.Series, result.ID.ToString()),
            Title = result.Title,
            OriginalTitle = result.OriginalTitle,
            OriginalLanguageCode = result.OriginalLanguage,
            Overview = result.Overview,
            // TMDB has a restricted flag for shows, but the client library can't reach it yet.
            UserRating = result.UserRating,
            UserVotes = result.UserVotes,
            PosterUrl = ToImageUrl(result.PosterPath),
            BackdropUrl = ToImageUrl(result.BackdropPath),
            Genres = result.Genres,
            FirstAiredAt = result.FirstAiredAt is { } aired ? PartialDateOnly.FromDateOnly(aired) : null,
        })], total);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<MetadataMovieSearchResult> Page, int TotalCount)> SearchMovies(MetadataSearchOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var (page, total) = await Upstream(() => ((ITmdbSearchService)_searchService)
            .SearchMovies(options.Query, options.IncludeRestricted, options.Year ?? 0, options.Page, options.PageSize)
        ).ConfigureAwait(false);
        return ([.. page.Select(result => new MetadataMovieSearchResult
        {
            ID = new(MetadataSource.TMDB, MetadataEntityType.Movie, result.ID.ToString()),
            Title = result.Title,
            OriginalTitle = result.OriginalTitle,
            OriginalLanguageCode = result.OriginalLanguage,
            Overview = result.Overview,
            IsRestricted = result.IsRestricted,
            UserRating = result.UserRating,
            UserVotes = result.UserVotes,
            PosterUrl = ToImageUrl(result.PosterPath),
            BackdropUrl = ToImageUrl(result.BackdropPath),
            Genres = result.Genres,
            ReleasedAt = result.ReleasedAt is { } released ? PartialDateOnly.FromDateOnly(released) : null,
            IsStandaloneVideo = result.IsVideo,
        })], total);
    }

    /// <inheritdoc />
    /// <remarks>
    ///   A show already stored is answered from TMDB's tables; any other is
    ///   asked of TMDB.
    /// </remarks>
    public async Task<MetadataSeriesSearchResult?> LookupSeries(MetadataGuid seriesID, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seriesID);
        if (!TryGetID(seriesID, MetadataEntityType.Series, out var showID))
            return null;

        if (_tmdbShows.GetByTmdbShowID(showID) is { } show)
            return ToSearchResult(show);

        return await Upstream(() => _client.UseClient(c => c.GetTvShowAsync(showID, cancellationToken: cancellationToken), $"Get show {showID}")).ConfigureAwait(false) is { } remote
            ? ToSearchResult(remote)
            : null;
    }

    /// <inheritdoc />
    /// <remarks>
    ///   A movie already stored is answered from TMDB's tables; any other is
    ///   asked of TMDB.
    /// </remarks>
    public async Task<MetadataMovieSearchResult?> LookupMovie(MetadataGuid movieID, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(movieID);
        if (!TryGetID(movieID, MetadataEntityType.Movie, out var tmdbMovieID))
            return null;

        if (_tmdbMovies.GetByTmdbMovieID(tmdbMovieID) is { } movie)
            return ToSearchResult(movie);

        return await Upstream(() => _client.UseClient(c => c.GetMovieAsync(tmdbMovieID, cancellationToken: cancellationToken), $"Get movie {tmdbMovieID}")).ConfigureAwait(false) is { } remote
            ? ToSearchResult(remote)
            : null;
    }

    /// <summary>
    ///   A stored show, the way a search would have offered it.
    /// </summary>
    /// <param name="show">The show.</param>
    /// <returns>The search result.</returns>
    internal static MetadataSeriesSearchResult ToSearchResult(TMDB_Show show)
        => new()
        {
            ID = new(MetadataSource.TMDB, MetadataEntityType.Series, show.TmdbShowID.ToString()),
            Title = show.EnglishTitle,
            OriginalTitle = show.OriginalTitle,
            OriginalLanguageCode = show.OriginalLanguageCode,
            Overview = show.EnglishOverview,
            IsRestricted = show.IsRestricted,
            UserRating = (decimal)show.UserRating,
            UserVotes = show.UserVotes,
            PosterUrl = ToImageUrl(show.PosterPath),
            BackdropUrl = ToImageUrl(show.BackdropPath),
            Genres = show.Genres,
            FirstAiredAt = show.FirstAiredAt is { } aired ? PartialDateOnly.FromDateOnly(aired) : null,
            EpisodeCount = show.EpisodeCount > 0 ? show.EpisodeCount : null,
        };

    /// <summary>
    ///   A show as TMDB answered it, the way a search would have offered it.
    /// </summary>
    /// <remarks>
    ///   The translated names come along when the show was fetched with its
    ///   translations, and the regular seasons always do, specials left out.
    /// </remarks>
    /// <param name="show">The show.</param>
    /// <returns>The search result.</returns>
    internal static MetadataSeriesSearchResult ToSearchResult(TvShow show)
        => new()
        {
            ID = new(MetadataSource.TMDB, MetadataEntityType.Series, show.Id.ToString()),
            Title = show.Name ?? string.Empty,
            OriginalTitle = show.OriginalName,
            AlternateTitles = [.. (show.Translations?.Translations ?? []).Select(translation => translation.Data?.Name).WhereNotNull().Where(name => !string.IsNullOrWhiteSpace(name))],
            Seasons = [.. (show.Seasons ?? []).Where(season => season.SeasonNumber > 0).Select(season => new MetadataSearchResultSeason
            {
                SeasonNumber = season.SeasonNumber,
                EpisodeCount = season.EpisodeCount,
                FirstAiredAt = season.AirDate is { } seasonAired ? PartialDateOnly.FromDateTime(seasonAired) : null,
            })],
            OriginalLanguageCode = show.OriginalLanguage,
            Overview = show.Overview,
            UserRating = (decimal)show.VoteAverage,
            UserVotes = show.VoteCount,
            PosterUrl = ToImageUrl(show.PosterPath),
            BackdropUrl = ToImageUrl(show.BackdropPath),
            Genres = show.GetGenres(),
            FirstAiredAt = show.FirstAirDate is { } aired ? PartialDateOnly.FromDateTime(aired) : null,
            EpisodeCount = show.NumberOfEpisodes > 0 ? show.NumberOfEpisodes : null,
        };

    /// <summary>
    ///   A show a TMDB search turned up, the way a search offers it.
    /// </summary>
    /// <param name="show">The show.</param>
    /// <returns>The search result.</returns>
    internal static MetadataSeriesSearchResult ToSearchResult(SearchTv show)
        => new()
        {
            ID = new(MetadataSource.TMDB, MetadataEntityType.Series, show.Id.ToString()),
            Title = show.Name ?? string.Empty,
            OriginalTitle = show.OriginalName,
            OriginalLanguageCode = show.OriginalLanguage,
            Overview = show.Overview,
            UserRating = (decimal)show.VoteAverage,
            UserVotes = show.VoteCount,
            PosterUrl = ToImageUrl(show.PosterPath),
            BackdropUrl = ToImageUrl(show.BackdropPath),
            Genres = show.GetGenres(),
            FirstAiredAt = show.FirstAirDate is { } aired ? PartialDateOnly.FromDateTime(aired) : null,
        };

    /// <summary>
    ///   A stored movie, the way a search would have offered it.
    /// </summary>
    /// <param name="movie">The movie.</param>
    /// <returns>The search result.</returns>
    internal static MetadataMovieSearchResult ToSearchResult(TMDB_Movie movie)
        => new()
        {
            ID = new(MetadataSource.TMDB, MetadataEntityType.Movie, movie.TmdbMovieID.ToString()),
            Title = movie.EnglishTitle,
            OriginalTitle = movie.OriginalTitle,
            OriginalLanguageCode = movie.OriginalLanguageCode,
            Overview = movie.EnglishOverview,
            IsRestricted = movie.IsRestricted,
            UserRating = (decimal)movie.UserRating,
            UserVotes = movie.UserVotes,
            PosterUrl = ToImageUrl(movie.PosterPath),
            BackdropUrl = ToImageUrl(movie.BackdropPath),
            Genres = movie.Genres,
            ReleasedAt = movie.ReleasedAt is { } released ? PartialDateOnly.FromDateOnly(released) : null,
            IsStandaloneVideo = movie.IsVideo,
        };

    /// <summary>
    ///   A movie as TMDB answered it, the way a search would have offered it.
    /// </summary>
    /// <remarks>
    ///   The translated titles and the release dates in every country come
    ///   along when the movie was fetched with them.
    /// </remarks>
    /// <param name="movie">The movie.</param>
    /// <returns>The search result.</returns>
    internal static MetadataMovieSearchResult ToSearchResult(Movie movie)
        => new()
        {
            ID = new(MetadataSource.TMDB, MetadataEntityType.Movie, movie.Id.ToString()),
            Title = movie.Title ?? string.Empty,
            OriginalTitle = movie.OriginalTitle,
            AlternateTitles = [.. (movie.Translations?.Translations ?? []).Select(translation => translation.Data?.Name).WhereNotNull().Where(name => !string.IsNullOrWhiteSpace(name))],
            OtherReleaseDates = [.. (movie.ReleaseDates?.Results ?? []).SelectMany(country => country.ReleaseDates ?? []).Select(release => DateOnly.FromDateTime(release.ReleaseDate))],
            OriginalLanguageCode = movie.OriginalLanguage,
            Overview = movie.Overview,
            IsRestricted = movie.Adult,
            UserRating = (decimal)movie.VoteAverage,
            UserVotes = movie.VoteCount,
            PosterUrl = ToImageUrl(movie.PosterPath),
            BackdropUrl = ToImageUrl(movie.BackdropPath),
            Genres = movie.GetGenres(),
            ReleasedAt = movie.ReleaseDate is { } released ? PartialDateOnly.FromDateTime(released) : null,
            IsStandaloneVideo = movie.Video,
        };

    /// <summary>
    ///   A movie a TMDB search turned up, the way a search offers it.
    /// </summary>
    /// <param name="movie">The movie.</param>
    /// <returns>The search result.</returns>
    internal static MetadataMovieSearchResult ToSearchResult(SearchMovie movie)
        => new()
        {
            ID = new(MetadataSource.TMDB, MetadataEntityType.Movie, movie.Id.ToString()),
            Title = movie.Title ?? string.Empty,
            OriginalTitle = movie.OriginalTitle,
            OriginalLanguageCode = movie.OriginalLanguage,
            Overview = movie.Overview,
            IsRestricted = movie.Adult,
            UserRating = (decimal)movie.VoteAverage,
            UserVotes = movie.VoteCount,
            PosterUrl = ToImageUrl(movie.PosterPath),
            BackdropUrl = ToImageUrl(movie.BackdropPath),
            Genres = movie.GetGenres(),
            ReleasedAt = movie.ReleaseDate is { } released ? PartialDateOnly.FromDateTime(released) : null,
            IsStandaloneVideo = movie.Video,
        };

    /// <summary>
    ///   TMDB hands back host-relative paths, so they are resolved here rather
    ///   than leaving a caller to know its image host.
    /// </summary>
    /// <param name="path">The host-relative path.</param>
    /// <returns>The full URL, or <see langword="null"/> for no path.</returns>
    private static string? ToImageUrl(string? path)
        => string.IsNullOrEmpty(path) ? null : $"{TmdbApiClient.ImageServerUrl}original{path}";

    #endregion

    #region Upstream Failures

    /// <summary>
    ///   Runs a call a person waits on, and turns TMDB being down or out of
    ///   reach into a <see cref="MetadataProviderUnavailableException"/>, so
    ///   the API can tell them to retry later.
    /// </summary>
    /// <typeparam name="T">What the call returns.</typeparam>
    /// <param name="call">The call.</param>
    /// <returns>What the call returned.</returns>
    /// <exception cref="MetadataProviderUnavailableException">TMDB answered with a server error, kept limiting the rate, or could not be reached.</exception>
    private async Task<T> Upstream<T>(Func<Task<T>> call)
    {
        try
        {
            return await call().ConfigureAwait(false);
        }
        catch (Exception ex) when (ToUnavailable(ex, _rateLimiter.GetPauseSnapshot().Remaining) is { } unavailable)
        {
            throw unavailable;
        }
    }

    /// <summary>
    ///   The <see cref="MetadataProviderUnavailableException"/> a failed call
    ///   to TMDB stands for, when the failure is one that passes.
    /// </summary>
    /// <param name="exception">What the call threw.</param>
    /// <param name="remainingPause">What is left of TMDB's pause, if it is paused.</param>
    /// <returns>
    ///   The exception to throw instead, or <see langword="null"/> for a
    ///   failure that retrying later would not mend.
    /// </returns>
    internal static MetadataProviderUnavailableException? ToUnavailable(Exception exception, TimeSpan? remainingPause)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception switch
        {
            AggregateException { InnerExceptions: [MetadataProviderUnavailableException inner] } => inner,
            AggregateException { InnerExceptions: [var inner] } => ToUnavailable(inner, remainingPause),
            RequestLimitExceededException limited => new(MetadataSource.TMDB, "TMDB kept limiting the rate of requests.", limited.RetryAfter ?? remainingPause, exception),
            GeneralHttpException { HttpStatusCode: >= HttpStatusCode.InternalServerError } http =>
                new(MetadataSource.TMDB, $"TMDB answered with a server error ({(int)http.HttpStatusCode}).", remainingPause, exception),
            HttpRequestException => new(MetadataSource.TMDB, "TMDB could not be reached.", remainingPause, exception),
            _ => null,
        };
    }

    #endregion

    #region Linking

    /// <inheritdoc />
    public IReadOnlySet<MetadataEntityType> LinkableEntityTypes { get; } =
        FrozenSet.ToFrozenSet([MetadataEntityType.Series, MetadataEntityType.Episode, MetadataEntityType.Movie]);

    /// <inheritdoc />
    /// <remarks>
    ///   Searches whether or not the anime is linked: a film per AniDB episode
    ///   standing for one, or a show, also by the earliest prequel's titles.
    ///   Every candidate judged comes back (rejected ones with the engine's
    ///   reason), then the prequel's linked shows for context only, then the
    ///   entries named by AniDB resources or other sources' links, which win
    ///   ties and may be taken by the core when the search took nothing better.
    /// </remarks>
    public async Task<IReadOnlyList<MetadataAutoLinkCandidate>> FindAutoLinks(int anidbAnimeID, CancellationToken cancellationToken = default)
    {
        if (_anidbAnime.GetByAnimeID(anidbAnimeID) is not { } anime)
        {
            _logger.LogWarning("Anime not found locally: {AnimeID}", anidbAnimeID);
            return [];
        }

        var results = await Upstream(() => _searchService.FindAutoMatches(anime)).ConfigureAwait(false);
        return [.. results.Select(ToCandidate)];
    }

    /// <summary>
    ///   One auto-search candidate, as the core takes it.
    /// </summary>
    /// <param name="result">The candidate.</param>
    /// <returns>The candidate.</returns>
    internal static MetadataAutoLinkCandidate ToCandidate(TmdbAutoSearchResult result)
        => new()
        {
            Result = result.Candidate ?? (result.IsMovie ? ToSearchResult(result.TmdbMovieRaw) : ToSearchResult(result.TmdbShowRaw)),
            AnidbAnimeID = result.AnidbAnime.AnimeID,
            AnidbEpisodeID = result.IsMovie ? result.AnidbEpisode.EpisodeID : null,
            MatchRating = result.MatchRating,
            IsLocal = result.IsLocal,
            IsRemote = result.IsRemote,
            Rejection = result.Rejection,
            Origin = result.Origin,
            LinkMatchRating = result.LinkMatchRating,
            PrequelAnidbAnimeID = result.PrequelAnidbAnimeID,
        };

    /// <inheritdoc />
    /// <remarks>
    ///   The core's matching engine lines the episodes up by air date and title
    ///   within TMDB's seasons. TMDB only picks the candidates: the whole show,
    ///   one season with the specials, or one alternate ordering group, less
    ///   episodes other anime are linked to when those are considered.
    /// </remarks>
    public Task<IReadOnlyList<EpisodeMatch>> MatchEpisodes(
        IAnidbAnime anime,
        IReadOnlyList<IAnidbEpisode> anidbEpisodes,
        MetadataGuid providerSeriesID,
        MetadataGuid? providerSeasonID = null,
        IReadOnlyList<IMetadataEpisodeCrossReference>? existing = null,
        bool? considerOtherLinks = null,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(anime);
        ArgumentNullException.ThrowIfNull(anidbEpisodes);
        ArgumentNullException.ThrowIfNull(providerSeriesID);

        if (!providerSeriesID.TryGetNumericID<int>(out var showID) || showID <= 0 || _tmdbShows.GetByTmdbShowID(showID) is null)
            return Task.FromResult<IReadOnlyList<EpisodeMatch>>([]);

        // Refused rather than widened to the whole show, which would overwrite every season's links.
        if (GetEpisodeCandidates(showID, providerSeasonID) is not { } candidates)
            return Task.FromResult<IReadOnlyList<EpisodeMatch>>([]);

        if (considerOtherLinks ?? _settingsProvider.GetSettings().TMDB.ConsiderExistingOtherLinks)
        {
            var claimed = _xrefAnidbTmdbEpisodes.GetByTmdbShowID(showID)
                .Where(link => link.AnidbAnimeID != anime.AnidbID && link.TmdbEpisodeID is not 0)
                .Select(link => link.TmdbEpisodeID)
                .ToHashSet();
            candidates = [.. candidates.Where(episode => !claimed.Contains(episode.TmdbID))];
        }

        // Links to an episode TMDB removed from the show are matched again. Links into
        // other shows are left to the engine, as their episodes may not be stored yet.
        var showEpisodeIDs = _tmdbEpisodes.GetByTmdbShowID(showID).Select(episode => episode.TmdbEpisodeID).ToHashSet();
        var kept = existing?
            .Where(link => link.ProviderParentID is not { } parentID ||
                !parentID.TryGetNumericID<int>(out var parentShowID) ||
                parentShowID != showID ||
                (link.ProviderID is { } episodeID && episodeID.TryGetNumericID<int>(out var tmdbEpisodeID) && showEpisodeIDs.Contains(tmdbEpisodeID)))
            .ToList();

        return Task.FromResult(_matchingEngine.MatchEpisodes(
            anidbEpisodes,
            candidates,
            kept,
            new() { Strategy = EpisodeMatchStrategy.DateAndTitleWithinSeasons }
        ));
    }

    /// <summary>
    ///   The TMDB episodes an anime's episodes may be matched to.
    /// </summary>
    /// <param name="showID">The TMDB show ID.</param>
    /// <param name="seasonID">
    ///   One regular season of the show, whose specials come with it, or one
    ///   group of an alternate ordering of it; <see langword="null"/> for the
    ///   whole show.
    /// </param>
    /// <returns>The candidates, or <see langword="null"/> when the season is not the show's.</returns>
    private IReadOnlyList<ITmdbEpisode>? GetEpisodeCandidates(int showID, MetadataGuid? seasonID)
    {
        if (seasonID is null)
            return _tmdbEpisodes.GetByTmdbShowID(showID);

        if (seasonID.TryGetNumericID<int>(out var tmdbSeasonID) && _tmdbSeasons.GetByTmdbSeasonID(tmdbSeasonID) is { } season)
            return season.TmdbShowID == showID
                ? [.. _tmdbEpisodes.GetByTmdbShowID(showID).Where(episode => episode.SeasonNumber is 0 || episode.TmdbSeasonID == tmdbSeasonID)]
                : null;

        // An alternate ordering group; an episode TMDB no longer lists is left out.
        return _tmdbAlternateOrderingSeasons.GetByTmdbEpisodeGroupID(seasonID.ID) is { } group && group.TmdbShowID == showID
            ? [.. group.TmdbAlternateOrderingEpisodes.Where(episode => episode.TmdbEpisode is not null)]
            : null;
    }

    #endregion

    #region Images

    /// <inheritdoc />
    /// <remarks>
    ///   An episode's stills are its backdrops. No candidate is the default, so
    ///   the preferred languages and counts decide what is downloaded. People's
    ///   images come with the person's refresh, so nothing is asked for them
    ///   here. Collections are left alone; company and network logos are
    ///   linked when the entry crediting them is refreshed.
    /// </remarks>
    public async Task<IReadOnlyList<ImageCandidate>?> GetImages(MetadataGuid entityID, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entityID);
        if (entityID.Source != MetadataSource.TMDB || !entityID.TryGetNumericID<int>(out var tmdbID) || tmdbID <= 0)
            return null;

        var entityType = entityID.EntityType;
        if (entityType == MetadataEntityType.Series)
        {
            if (_tmdbShows.GetByTmdbShowID(tmdbID) is not { } show)
                return null;

            var images = await _client.UseClient(c => c.GetTvShowImagesAsync(tmdbID), $"Get images for show {tmdbID}").ConfigureAwait(false);
            return images is null ? null :
            [
                .. ToCandidates(images.Posters, ImageEntityType.Primary),
                .. ToCandidates(images.Logos, ImageEntityType.Logo),
                .. ToCandidates(images.Backdrops, ImageEntityType.Backdrop),
            ];
        }

        if (entityType == MetadataEntityType.Season)
        {
            if (_tmdbSeasons.GetByTmdbSeasonID(tmdbID) is not { } season)
                return null;

            var images = await _client.UseClient(c => c.GetTvSeasonImagesAsync(season.TmdbShowID, season.SeasonNumber), $"Get images for season {season.SeasonNumber} in show {season.TmdbShowID}")
                .ConfigureAwait(false);
            return images is null ? null : ToCandidates(images.Posters, ImageEntityType.Primary);
        }

        if (entityType == MetadataEntityType.Episode)
        {
            if (_tmdbEpisodes.GetByTmdbEpisodeID(tmdbID) is not { } episode)
                return null;

            var images = await _client.UseClient(
                c => c.GetTvEpisodeImagesAsync(episode.TmdbShowID, episode.SeasonNumber, episode.EpisodeNumber),
                $"Get images for episode {episode.EpisodeNumber} in season {episode.SeasonNumber} in show {episode.TmdbShowID}"
            ).ConfigureAwait(false);
            return images is null ? null : ToCandidates(images.Stills, ImageEntityType.Backdrop);
        }

        if (entityType == MetadataEntityType.Movie)
        {
            if (_tmdbMovies.GetByTmdbMovieID(tmdbID) is not { } movie)
                return null;

            var images = await _client.UseClient(c => c.GetMovieImagesAsync(tmdbID), $"Get images for movie {tmdbID}").ConfigureAwait(false);
            return images is null ? null :
            [
                .. ToCandidates(images.Posters, ImageEntityType.Primary),
                .. ToCandidates(images.Logos, ImageEntityType.Logo),
                .. ToCandidates(images.Backdrops, ImageEntityType.Backdrop),
            ];
        }

        if (entityType == MetadataEntityType.Creator)
            return _updater.GetFetchedPersonImages(tmdbID) is { } profiles
                ? ToCandidates(profiles, ImageEntityType.Primary)
                : null;

        return null;
    }

    /// <summary>
    ///   Turns TMDB's images of one type into candidates, in TMDB's order,
    ///   none of them marked as the default.
    /// </summary>
    /// <param name="images">The images.</param>
    /// <param name="imageType">Their type.</param>
    /// <returns>The candidates, without the images that have no path.</returns>
    internal static List<ImageCandidate> ToCandidates(IEnumerable<TmdbImage>? images, ImageEntityType imageType)
    {
        return
        [
            .. (images ?? [])
                .Where(image => !string.IsNullOrEmpty(image.FilePath))
                .Select(image =>
                {
                    var resourceID = TmdbImageService.SafeTransformResourceID(image.FilePath!);
                    var hasRating = image.VoteCount > 0 && image.VoteAverage >= 1;
                    return new ImageCandidate
                    {
                        ResourceID = resourceID,
                        ImageType = imageType,
                        Width = image.Width,
                        Height = image.Height,
                        LanguageCode = image.Iso_639_1,
                        CountryCode = image.Iso_3166_1,
                        Rating = hasRating ? image.VoteAverage : null,
                        RatingVotes = hasRating ? image.VoteCount : null,
                    };
                }),
        ];
    }

    #endregion
}
