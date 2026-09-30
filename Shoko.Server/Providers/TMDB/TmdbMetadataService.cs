using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.Services;

namespace Shoko.Server.Providers.TMDB;

/// <summary>
///   TMDB's own metadata service, kept for the callers that name TMDB, over
///   the generic refresh and purge services.
/// </summary>
/// <remarks>
///   It holds no TMDB logic of its own: a refresh, an image download, a
///   search and a purge each go through <see cref="IMetadataRefreshService"/>
///   and <see cref="IMetadataPurgeService"/> to the core's jobs for the TMDB
///   provider, as for any other source. Only the genre lists are read from
///   TMDB's client, having no generic counterpart.
/// </remarks>
public class TmdbMetadataService : ITmdbMetadataService
{
    #region Fields

    private static TmdbMetadataService? _instance;

    private static readonly System.Threading.Lock _instanceLock = new();

    private readonly TmdbApiClient _client;

    private readonly IMetadataRefreshService _refreshService;

    private readonly IMetadataPurgeService _purgeService;

    #endregion

    #region Constructors

    /// <summary>
    ///   Takes the services it passes the calls on to.
    /// </summary>
    /// <param name="client">Reads TMDB's genre lists.</param>
    /// <param name="refreshService">Queues the refreshes, images and searches, and tells when one runs.</param>
    /// <param name="purgeService">Queues the purges.</param>
    public TmdbMetadataService(
        TmdbApiClient client,
        IMetadataRefreshService refreshService,
        IMetadataPurgeService purgeService
    )
    {
        _client = client;
        _refreshService = refreshService;
        _purgeService = purgeService;
        _instance ??= this;
    }

    #endregion

    #region Instance

    /// <summary>
    ///   The service, for the few places that predate dependency injection.
    /// </summary>
    internal static TmdbMetadataService? Instance
    {
        get
        {
            if (_instance is not null)
                return _instance;

            lock (_instanceLock)
            {
                if (_instance is not null)
                    return _instance;

                if (!ISystemService.HasStaticServices)
                    return null;

#pragma warning disable CS0618
                return _instance = ISystemService.StaticServices.GetService<TmdbMetadataService>();
#pragma warning restore CS0618
            }
        }
    }

    #endregion

    #region Client

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<int, string>> GetMovieGenres()
        => _client.GetMovieGenres();

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<int, string>> GetShowGenres()
        => _client.GetShowGenres();

    /// <inheritdoc />
    public TmdbRateLimitPauseStatus GetPauseStatus()
    {
        var status = _refreshService.GetPauseStatus(MetadataSource.TMDB);
        return new() { IsPaused = status.IsPaused, RemainingPauseTime = status.IsPaused ? status.GetRemainingPauseTime() : null };
    }

    #endregion

    #region Movies

    /// <summary>
    ///   Whether a movie is being refreshed or purged.
    /// </summary>
    /// <param name="movieId">The TMDB movie ID.</param>
    /// <returns><see langword="true"/> while it is.</returns>
    public bool IsMovieUpdating(int movieId)
        => _refreshService.IsRefreshing(MovieID(movieId));

    /// <summary>
    ///   Wait for a movie's refresh or purge to end, if one is running.
    /// </summary>
    /// <param name="movieId">The TMDB movie ID.</param>
    /// <returns><see langword="true"/> when there was one to wait for.</returns>
    public bool WaitForMovieUpdate(int movieId)
        => _refreshService.WaitForRefresh(MovieID(movieId)).GetAwaiter().GetResult();

    /// <summary>
    ///   Wait for a collection's refresh or purge to end, if one is running.
    /// </summary>
    /// <param name="collectionId">The TMDB collection ID.</param>
    /// <returns><see langword="true"/> when there was one to wait for.</returns>
    public bool WaitForMovieCollectionUpdate(int collectionId)
        => _refreshService.WaitForRefresh(new(MetadataSource.TMDB, MetadataEntityType.Collection, collectionId.ToString())).GetAwaiter().GetResult();

    /// <inheritdoc />
    public Task UpdateAllMovies(bool force, bool saveImages)
        => _refreshService.RefreshAllLinked(MetadataSource.TMDB, force, new() { DownloadImages = saveImages, Reason = MetadataRefreshReason.Requested }, MetadataEntityType.Movie);

    /// <inheritdoc />
    public Task ScheduleUpdateOfMovie(TmdbMovieUpdateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return _refreshService.RefreshEntry(MovieID(options.MovieId), options.ForceRefresh, ToOptions(options));
    }

    /// <inheritdoc />
    public Task<bool> UpdateMovie(TmdbMovieUpdateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return _refreshService.RefreshEntry(MovieID(options.MovieId), options.ForceRefresh, ToOptions(options), immediate: true);
    }

    /// <inheritdoc />
    public Task ScheduleDownloadAllMovieImages(int movieId, bool forceDownload = false)
        => _refreshService.DownloadImages(MovieID(movieId), forceDownload);

    /// <inheritdoc />
    public Task PurgeAllUnusedMovies(DateTime? olderThan = null)
        => _purgeService.PurgeUnused(MetadataSource.TMDB, olderThan, MetadataEntityType.Movie);

    /// <inheritdoc />
    public Task SchedulePurgeOfMovie(int movieId)
        => _purgeService.PurgeEntry(MovieID(movieId), force: true);

    /// <inheritdoc />
    public Task PurgeMovie(int movieId)
        => _purgeService.PurgeEntry(MovieID(movieId), force: true);

    /// <inheritdoc />
    public Task PurgeAllMovieCollections()
        => _purgeService.PurgeCollections(MetadataSource.TMDB);

    #endregion

    #region Shows

    /// <summary>
    ///   Whether a show is being refreshed or purged.
    /// </summary>
    /// <param name="showId">The TMDB show ID.</param>
    /// <returns><see langword="true"/> while it is.</returns>
    public bool IsShowUpdating(int showId)
        => _refreshService.IsRefreshing(ShowID(showId));

    /// <summary>
    ///   Wait for a show's refresh or purge to end, if one is running.
    /// </summary>
    /// <param name="showId">The TMDB show ID.</param>
    /// <returns><see langword="true"/> when there was one to wait for.</returns>
    public bool WaitForShowUpdate(int showId)
        => _refreshService.WaitForRefresh(ShowID(showId)).GetAwaiter().GetResult();

    /// <inheritdoc />
    public Task UpdateAllShows(bool force = false, bool downloadImages = false)
        => _refreshService.RefreshAllLinked(MetadataSource.TMDB, force, new() { DownloadImages = downloadImages, Reason = MetadataRefreshReason.Requested }, MetadataEntityType.Series);

    /// <inheritdoc />
    public Task ScheduleUpdateOfShow(TmdbShowUpdateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.ShowId <= 0
            ? Task.CompletedTask
            : _refreshService.RefreshEntry(ShowID(options.ShowId), options.ForceRefresh, ToOptions(options));
    }

    /// <inheritdoc />
    public Task<bool> UpdateShow(TmdbShowUpdateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.ShowId <= 0
            ? Task.FromResult(false)
            : _refreshService.RefreshEntry(ShowID(options.ShowId), options.ForceRefresh, ToOptions(options), immediate: true);
    }

    /// <inheritdoc />
    public Task ScheduleDownloadAllShowImages(int showId, bool forceDownload = false)
        => _refreshService.DownloadImages(ShowID(showId), forceDownload);

    /// <inheritdoc />
    public Task PurgeAllUnusedShows(DateTime? olderThan = null)
        => _purgeService.PurgeUnused(MetadataSource.TMDB, olderThan, MetadataEntityType.Series);

    /// <inheritdoc />
    public Task SchedulePurgeOfShow(int showId)
        => _purgeService.PurgeEntry(ShowID(showId), force: true);

    /// <inheritdoc />
    public Task PurgeShow(int showId)
        => _purgeService.PurgeEntry(ShowID(showId), force: true);

    #endregion

    #region Search/Matching

    /// <inheritdoc />
    public Task ScheduleSearchForMatch(int anidbId, bool force)
        => _refreshService.AutoSearch(MetadataSource.TMDB, anidbId, force);

    /// <inheritdoc />
    public Task ScanForMatches()
        => _refreshService.AutoSearchAll(MetadataSource.TMDB);

    #endregion

    #region Helpers

    /// <summary>
    ///   The identifier of a TMDB show.
    /// </summary>
    /// <param name="showId">The TMDB show ID.</param>
    /// <returns>The identifier.</returns>
    private static MetadataGuid ShowID(int showId)
        => new(MetadataSource.TMDB, MetadataEntityType.Series, showId.ToString());

    /// <summary>
    ///   The identifier of a TMDB movie.
    /// </summary>
    /// <param name="movieId">The TMDB movie ID.</param>
    /// <returns>The identifier.</returns>
    private static MetadataGuid MovieID(int movieId)
        => new(MetadataSource.TMDB, MetadataEntityType.Movie, movieId.ToString());

    /// <summary>
    ///   TMDB's show update options as refresh options, asked for by
    ///   somebody.
    /// </summary>
    /// <param name="options">The show update options.</param>
    /// <returns>The refresh options.</returns>
    private static MetadataRefreshOptions ToOptions(TmdbShowUpdateOptions options)
        => new()
        {
            DownloadImages = options.DownloadImages,
            DownloadCrewAndCast = options.DownloadCrewAndCast,
            DownloadAlternateOrdering = options.DownloadAlternateOrdering,
            DownloadNetworks = options.DownloadNetworks,
            QuickRefresh = options.QuickRefresh,
            Reason = MetadataRefreshReason.Requested,
        };

    /// <summary>
    ///   TMDB's movie update options as refresh options, asked for by
    ///   somebody.
    /// </summary>
    /// <param name="options">The movie update options.</param>
    /// <returns>The refresh options.</returns>
    private static MetadataRefreshOptions ToOptions(TmdbMovieUpdateOptions options)
        => new()
        {
            DownloadImages = options.DownloadImages,
            DownloadCrewAndCast = options.DownloadCrewAndCast,
            DownloadCollections = options.DownloadCollections,
            Reason = MetadataRefreshReason.Requested,
        };

    #endregion
}
