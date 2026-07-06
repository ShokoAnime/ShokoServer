using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.User.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.AniDB;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.API.v3.Models.TMDB;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class EpisodeController : BaseController
{

    internal const string EpisodeNotFoundWithEpisodeID = "No Episode entry for the given episodeID";

    internal const string EpisodeNotFoundForAnidbEpisodeID = "No Episode entry for the given anidbEpisodeID";

    internal const string AnidbNotFoundForEpisodeID = "No Episode.Anidb entry for the given episodeID";

    internal const string AnidbNotFoundForAnidbEpisodeID = "No Episode.Anidb entry for the given anidbEpisodeID";

    internal const string EpisodeForbiddenForUser = "Accessing Episode is not allowed for the current user";

    internal const string EpisodeNoSeriesForEpisodeID = "Unable to find a Series entry for given episodeID";

    private readonly AnimeSeriesService _seriesService;

    private readonly AnimeGroupService _groupService;

    private readonly IImageManager _imageManager;

    private readonly IUserDataService _userDataService;

    private readonly TmdbLinkingService _tmdbLinkingService;

    private readonly TmdbMetadataService _tmdbMetadataService;
    private readonly AniDB_AnimeRepository _anidbAnimes;
    private readonly AniDB_EpisodeRepository _anidbEpisodes;
    private readonly AnimeEpisodeRepository _animeEpisodes;
    private readonly AnimeEpisode_UserRepository _animeEpisodeUsers;
    private readonly TMDB_EpisodeRepository _tmdbEpisodes;
    private readonly TMDB_MovieRepository _tmdbMovies;
    private readonly VideoLocalRepository _videoLocals;
    private readonly VideoLocal_PlaceRepository _videoLocalPlaces;
    private readonly VideoReleaseGroupingService _releaseGrouper;
    private readonly ReleaseComparisonService _releaseComparer;

    public EpisodeController(
        ISettingsProvider settingsProvider,
        AnimeSeriesService seriesService,
        AnimeGroupService groupService,
        IImageManager imageManager,
        IUserDataService userDataService,
        TmdbLinkingService tmdbLinkingService,
        TmdbMetadataService tmdbMetadataService,
        AniDB_AnimeRepository anidbAnimes,
        AniDB_EpisodeRepository anidbEpisodes,
        AnimeEpisodeRepository animeEpisodes,
        AnimeEpisode_UserRepository animeEpisodeUsers,
        TMDB_EpisodeRepository tmdbEpisodes,
        TMDB_MovieRepository tmdbMovies,
        VideoLocalRepository videoLocals,
        VideoLocal_PlaceRepository videoLocalPlaces,
        VideoReleaseGroupingService releaseGrouper,
        ReleaseComparisonService releaseComparer
    ) : base(settingsProvider)
    {
        _seriesService = seriesService;
        _groupService = groupService;
        _imageManager = imageManager;
        _userDataService = userDataService;
        _tmdbLinkingService = tmdbLinkingService;
        _tmdbMetadataService = tmdbMetadataService;
        _anidbAnimes = anidbAnimes;
        _anidbEpisodes = anidbEpisodes;
        _animeEpisodes = animeEpisodes;
        _animeEpisodeUsers = animeEpisodeUsers;
        _tmdbEpisodes = tmdbEpisodes;
        _tmdbMovies = tmdbMovies;
        _videoLocals = videoLocals;
        _videoLocalPlaces = videoLocalPlaces;
        _releaseGrouper = releaseGrouper;
        _releaseComparer = releaseComparer;
    }

    /// <summary>
    /// Get all <see cref="Episode"/>s for the given filter.
    /// </summary>
    /// <remarks>
    /// <see cref="Filter"/> or <see cref="Group"/> is irrelevant at this level.
    /// </remarks>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="includeMissing">Include missing episodes in the list.</param>
    /// <param name="includeUnaired">Include unaired episodes in the list.</param>
    /// <param name="includeHidden">Include hidden episodes in the list.</param>
    /// <param name="includeVoted">Include voted episodes in the list.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSourceType"/>s.</param>
    /// <param name="includeWatched">Include watched episodes in the list.</param>
    /// <param name="type">Filter episodes by the specified <see cref="EpisodeType"/>s.</param>
    /// <param name="includeFiles">Include files with the episodes.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="includeReleaseInfo">Include release info data.</param>
    /// <param name="search">An optional search query to filter episodes based on their titles.</param>
    /// <param name="fuzzy">Indicates that fuzzy-matching should be used for the search query.</param>
    /// <returns>A list of episodes based on the specified filters.</returns>
    [HttpGet]
    public ActionResult<ListResult<Episode>> GetAllEpisodes(
        [FromQuery, Range(0, 1000)] int pageSize = 20,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] IncludeOnlyFilter includeMissing = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeUnaired = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeHidden = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeVoted = IncludeOnlyFilter.True,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSourceType>? includeDataFrom = null,
        [FromQuery] IncludeOnlyFilter includeWatched = IncludeOnlyFilter.True,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeType>? type = null,
        [FromQuery] bool includeFiles = false,
        [FromQuery] bool includeMediaInfo = false,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] bool includeReleaseInfo = false,
        [FromQuery] string? search = null, [FromQuery] bool fuzzy = true)
    {
        var user = User;
        var allowedSeriesDict = new ConcurrentDictionary<int, bool>();
        var episodes = _animeEpisodes.GetAll()
            .AsParallel()
            .Where(episode =>
            {
                // Only show episodes the user is allowed to view.
                if (!allowedSeriesDict.TryGetValue(episode.AnimeSeriesID, out var isAllowed))
                    allowedSeriesDict.TryAdd(episode.AnimeSeriesID, isAllowed = user.AllowedSeries(episode.AnimeSeries!));
                return isAllowed;
            })
            .Select(episode => new { Shoko = episode, AniDB = episode?.AniDB_Episode })
            .Where(both =>
            {
                // Make sure we have an anidb entry for the episode, otherwise,
                // just hide it.
                var shoko = both.Shoko;
                var anidb = both.AniDB;
                if (anidb == null || shoko == null)
                    return false;

                // Filter by hidden state, if specified
                if (includeHidden != IncludeOnlyFilter.True)
                {
                    // If we should hide hidden episodes and the episode is hidden, then hide it.
                    // Or if we should only show hidden episodes and the episode is not hidden, then hide it.
                    var shouldHideHidden = includeHidden == IncludeOnlyFilter.False;
                    if (shouldHideHidden == shoko.IsHidden)
                        return false;
                }

                // Filter by episode type, if specified
                if (type is not null && type.Count > 0 && !type.Contains(anidb.EpisodeType))
                    return false;

                // Filter by availability, if specified
                if (includeMissing != IncludeOnlyFilter.True)
                {
                    // If we should hide missing episodes and the episode has no files, then hide it.
                    // Or if we should only show missing episodes and the episode has files, the hide it.
                    var shouldHideMissing = includeMissing == IncludeOnlyFilter.False;
                    var isMissing = shoko.VideoLocals.Count == 0 && anidb.HasAired;
                    if (shouldHideMissing == isMissing)
                        return false;
                }
                if (includeUnaired != IncludeOnlyFilter.True)
                {
                    // If we should hide unaired episodes and the episode has no files, then hide it.
                    // Or if we should only show unaired episodes and the episode has files, the hide it.
                    var shouldHideUnaired = includeUnaired == IncludeOnlyFilter.False;
                    var isUnaired = shoko.VideoLocals.Count == 0 && !anidb.HasAired;
                    if (shouldHideUnaired == isUnaired)
                        return false;
                }

                // Filter by user watched status, if specified
                if (includeWatched != IncludeOnlyFilter.True)
                {
                    // If we should hide watched episodes and the episode is watched, then hide it.
                    // Or if we should only show watched episodes and the episode is not watched, then hide it.
                    var shouldHideWatched = includeWatched == IncludeOnlyFilter.False;
                    var isWatched = shoko.GetUserRecord(user.JMMUserID)?.WatchedDate is not null;
                    if (shouldHideWatched == isWatched)
                        return false;
                }

                // Filter by voted status, if specified
                if (includeVoted != IncludeOnlyFilter.True)
                {
                    // If we should hide voted episodes and the episode is voted, then hide it.
                    // Or if we should only show voted episodes and the episode is not voted, then hide it.
                    var shouldHideVoted = includeVoted == IncludeOnlyFilter.False;
                    var isVoted = _animeEpisodeUsers.GetByUserAndEpisodeID(user.JMMUserID, shoko.AniDB_EpisodeID) is { HasUserRating: true };
                    if (shouldHideVoted == isVoted)
                        return false;
                }

                return true;
            });
        if (!string.IsNullOrWhiteSpace(search))
        {
            var languages = SettingsProvider.GetSettings()
                .Language.EpisodeTitleLanguageOrder
                .Select(lang => lang.GetTitleLanguage())
                .Concat([TitleLanguage.English, TitleLanguage.Romaji])
                .ToHashSet();
            return episodes
                .Search(
                    search,
                    ep => ep.AniDB!.GetTitles()
                        .Where(title => title is not null && languages.Contains(title.Language))
                        .Select(title => title.Title)
                        .Append(ep.Shoko.Title)
                        .Distinct()
                        .ToList(),
                    fuzzy
                )
                .ToListResult(a => new Episode(HttpContext, a.Result.Shoko, includeDataFrom, includeFiles, includeMediaInfo, includeAbsolutePaths, includeXRefs, includeReleaseInfo), page, pageSize);
        }

        // Order the episodes since we're not using the search ordering.
        return episodes
            .OrderBy(episode => episode.Shoko.AnimeSeriesID)
            .ThenBy(episode => episode.AniDB!.EpisodeType)
            .ThenBy(episode => episode.AniDB!.EpisodeNumber)
            .ToListResult(a => new Episode(HttpContext, a.Shoko, includeDataFrom, includeFiles, includeMediaInfo, includeAbsolutePaths, includeXRefs, includeReleaseInfo), page, pageSize);
    }

    /// <summary>
    /// Get all <see cref="AnidbEpisode"/>s. Admins only.
    /// </summary>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="type">Filter episodes by the specified <see cref="EpisodeType"/>s.</param>
    /// <returns></returns>
    [HttpGet("AniDB")]
    [Authorize("admin")]
    public ActionResult<ListResult<AnidbEpisode>> GetAllAniDBEpisodes(
        [FromQuery, Range(0, 1000)] int pageSize = 20,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeType>? type = null)
    {
        var user = User;
        var allowedAnimeDict = new ConcurrentDictionary<int, bool>();
        return _anidbEpisodes.GetAll()
            .AsParallel()
            .Where(episode =>
            {
                // Only show episodes the user is allowed to view.
                if (!allowedAnimeDict.TryGetValue(episode.AnimeID, out var isAllowed))
                {
                    // If this is an episode not tied to a missing anime, then
                    // just hide it.
                    var anime = _anidbAnimes.GetByAnimeID(episode.AnimeID);
                    isAllowed = anime == null ? false : user.AllowedAnime(anime);

                    allowedAnimeDict.TryAdd(episode.AnimeID, isAllowed);
                }
                if (!isAllowed)
                    return false;

                // Filter by episode type, if specified
                if (type is not null && type.Count > 0 && !type.Contains(episode.EpisodeType))
                    return false;

                return true;
            })
            .OrderBy(episode => episode.AnimeID)
            .ThenBy(episode => episode.EpisodeType)
            .ThenBy(episode => episode.EpisodeNumber)
            .ToListResult(episode => new AnidbEpisode(episode), page, pageSize);
    }

    #region Shoko

    /// <summary>
    /// Get the <see cref="Episode"/> entry for the given <paramref name="episodeID"/>.
    /// </summary>
    /// <param name="episodeID">Shoko ID</param>
    /// <param name="includeFiles">Include files with the episodes.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="includeReleaseInfo">Include release info data.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSourceType"/>s.</param>
    /// <returns></returns>
    [HttpGet("{episodeID}")]
    public ActionResult<Episode> GetEpisodeByEpisodeID(
        [FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromQuery] bool includeFiles = false,
        [FromQuery] bool includeMediaInfo = false,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] bool includeReleaseInfo = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSourceType>? includeDataFrom = null)
    {
        var episode = _animeEpisodes.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        return new Episode(HttpContext, episode, includeDataFrom, includeFiles, includeMediaInfo, includeAbsolutePaths, includeXRefs, includeReleaseInfo);
    }

    /// <summary>
    /// Override the title of a Episode.
    /// </summary>
    /// <param name="episodeID">The ID of the Episode</param>
    /// <param name="body"></param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("{episodeID}/OverrideTitle")]
    public ActionResult OverrideEpisodeTitle([FromRoute, Range(1, int.MaxValue)] int episodeID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Episode.Input.EpisodeTitleOverrideBody body)
    {
        var episode = _animeEpisodes.GetByID(episodeID);

        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        if (!string.Equals(episode.EpisodeNameOverride, body.Title))
        {
            episode.EpisodeNameOverride = body.Title;

            _animeEpisodes.Save(episode);

            ShokoEventHandler.Instance.OnEpisodeUpdated(series, episode, UpdateReason.Updated);
        }

        return Ok();
    }

    /// <summary>
    /// Set or unset the episode hidden status by the given <paramref name="episodeID"/>.
    /// </summary>
    /// <param name="episodeID">Shoko episode ID</param>
    /// <param name="value">The new value to set.</param>
    /// <param name="updateStats">Update series and group stats.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("{episodeID}/SetHidden")]
    public ActionResult PostEpisodeSetHidden([FromRoute, Range(1, int.MaxValue)] int episodeID, [FromQuery] bool value = true, [FromQuery] bool updateStats = true)
    {
        var episode = _animeEpisodes.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        if (episode.IsHidden != value)
        {
            episode.IsHidden = value;

            _animeEpisodes.Save(episode);

            if (updateStats)
            {
                _seriesService.UpdateStats(series, true, true);
                _groupService.UpdateStatsFromTopLevel(series.TopLevelAnimeGroup, true, true);
            }

            ShokoEventHandler.Instance.OnEpisodeUpdated(series, episode, UpdateReason.Updated);
        }

        return Ok();
    }

    #endregion

    #region AniDB

    /// <summary>
    /// Get the <see cref="AnidbEpisode"/> entry for the given <paramref name="episodeID"/>.
    /// </summary>
    /// <param name="episodeID">Shoko ID</param>
    /// <returns></returns>
    [HttpGet("{episodeID}/AniDB")]
    public ActionResult<AnidbEpisode> GetEpisodeAnidbByEpisodeID([FromRoute, Range(1, int.MaxValue)] int episodeID)
    {
        var episode = _animeEpisodes.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var anidb = episode.AniDB_Episode;
        if (anidb == null)
            return InternalError(AnidbNotFoundForEpisodeID);

        return new AnidbEpisode(anidb);
    }

    /// <summary>
    /// Get the <see cref="AnidbEpisode"/> entry for the given <paramref name="anidbEpisodeID"/>.
    /// </summary>
    /// <param name="anidbEpisodeID">AniDB Episode ID</param>
    /// <returns></returns>
    [HttpGet("AniDB/{anidbEpisodeID}")]
    public ActionResult<AnidbEpisode> GetEpisodeAnidbByAnidbEpisodeID([FromRoute] int anidbEpisodeID)
    {
        var anidb = _anidbEpisodes.GetByEpisodeID(anidbEpisodeID);
        if (anidb == null)
            return NotFound(AnidbNotFoundForAnidbEpisodeID);

        return new AnidbEpisode(anidb);
    }

    /// <summary>
    /// Get the <see cref="Episode"/> entry for the given <paramref name="anidbEpisodeID"/>, if any.
    /// </summary>
    /// <param name="anidbEpisodeID">AniDB Episode ID</param>
    /// <param name="includeFiles">Include files with the episodes.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="includeReleaseInfo">Include release info data.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSourceType"/>s.</param>
    /// <returns></returns>
    [HttpGet("AniDB/{anidbEpisodeID}/Episode")]
    public ActionResult<Episode> GetEpisode(
        [FromRoute] int anidbEpisodeID,
        [FromQuery] bool includeFiles = false,
        [FromQuery] bool includeMediaInfo = false,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] bool includeReleaseInfo = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSourceType>? includeDataFrom = null)
    {
        var anidb = _anidbEpisodes.GetByEpisodeID(anidbEpisodeID);
        if (anidb == null)
            return NotFound(AnidbNotFoundForAnidbEpisodeID);

        var episode = _animeEpisodes.GetByAniDBEpisodeID(anidb.EpisodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundForAnidbEpisodeID);

        return new Episode(HttpContext, episode, includeDataFrom, includeFiles, includeMediaInfo, includeAbsolutePaths, includeXRefs, includeReleaseInfo);
    }

    /// <summary>
    /// Add a permanent user-submitted rating for the episode.
    /// </summary>
    /// <param name="episodeID"></param>
    /// <param name="vote"></param>
    /// <returns></returns>
    [HttpPost("{episodeID}/Vote")]
    public async Task<ActionResult> PostEpisodeVote([FromRoute, Range(1, int.MaxValue)] int episodeID, [FromBody] Vote vote)
    {
        var episode = _animeEpisodes.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        if (vote.Value > vote.MaxValue)
            return ValidationProblem($"Value must be less than or equal to the set max value ({vote.MaxValue}).", nameof(vote.Value));

        await _userDataService.RateEpisode(episode, User, vote.GetRating());

        return NoContent();
    }

    #endregion

    #region TMDB

    /// <summary>
    /// Get all TMDB Movies linked directly to the Shoko Episode by ID.
    /// </summary>
    /// <param name="episodeID">Shoko Episode ID.</param>
    /// <param name="include">Extra details to include.</param>
    /// <param name="language">Language to fetch some details in.</param>
    /// <returns>All TMDB Movies linked directly to the Shoko Episode.</returns>
    [HttpGet("{episodeID}/TMDB/Movie")]
    public ActionResult<List<TmdbMovie>> GetTmdbMoviesByEpisodeID(
        [FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbMovie.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var episode = _animeEpisodes.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        return episode.TmdbMovieCrossReferences
            .Select(xref =>
            {
                var movie = xref.TmdbMovie;
                if (movie is not null && _tmdbMetadataService.WaitForMovieUpdate(movie.TmdbMovieID))
                    movie = _tmdbMovies.GetByTmdbMovieID(movie.TmdbMovieID);
                return movie;
            })
            .WhereNotNull()
            .Select(tmdbMovie => new TmdbMovie(tmdbMovie, include?.CombineFlags(), language))
            .ToList();
    }


    /// <summary>
    /// Add a new TMDB Movie cross-reference to the Shoko Episode by ID.
    /// </summary>
    /// <param name="episodeID">Shoko Episode ID.</param>
    /// <param name="body">Body containing the information about the new cross-reference to be made.</param>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpPost("{episodeID}/TMDB/Movie")]
    public async Task<ActionResult> AddLinkToTMDBMoviesByEpisodeID(
        [FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] Series.Input.LinkCommonBody body
    )
    {
        var episode = _animeEpisodes.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        await _tmdbLinkingService.AddMovieLinkForEpisode(episode.AniDB_EpisodeID, body.ID, additiveLink: !body.Replace);

        var needRefresh = _tmdbMovies.GetByTmdbMovieID(body.ID) is null || body.Refresh;
        if (needRefresh)
            await _tmdbMetadataService.ScheduleUpdateOfMovie(new() { MovieId = body.ID, ForceRefresh = body.Refresh, DownloadImages = true });

        return NoContent();
    }

    /// <summary>
    /// Remove one or all TMDB Movie links from the episode.
    /// </summary>
    /// <param name="episodeID">Shoko Episode ID.</param>
    /// <param name="body">Optional. Body containing information about the link to be removed.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("{episodeID}/TMDB/Movie")]
    public async Task<ActionResult> RemoveLinkToTMDBMoviesByEpisodeID(
        [FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] Series.Input.UnlinkMovieBody body
    )
    {
        var episode = _animeEpisodes.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        if (body is not null && body.ID > 0)
            await _tmdbLinkingService.RemoveMovieLinkForEpisode(episode.AniDB_EpisodeID, body.ID, body.Purge);
        else
            await _tmdbLinkingService.RemoveAllMovieLinksForEpisode(episode.AniDB_EpisodeID, body?.Purge ?? false);

        return NoContent();
    }

    /// <summary>
    /// Get all TMDB Movie cross-references for the Shoko Episode by ID.
    /// </summary>
    /// <param name="seriesID">Shoko Episode ID.</param>
    /// <returns>All TMDB Movie cross-references for the Shoko Episode.</returns>
    [HttpGet("{seriesID}/TMDB/Movie/CrossReferences")]
    public ActionResult<IReadOnlyList<TmdbMovie.CrossReference>> GetTMDBMovieCrossReferenceByEpisodeID(
        [FromRoute, Range(1, int.MaxValue)] int seriesID
    )
    {
        var episode = _animeEpisodes.GetByID(seriesID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        return episode.TmdbMovieCrossReferences
            .Select(xref => new TmdbMovie.CrossReference(xref))
            .OrderBy(xref => xref.TmdbMovieID)
            .ToList();
    }

    /// <summary>
    /// Get all TMDB Episodes linked to the Shoko Episode by ID.
    /// </summary>
    /// <param name="episodeID">Shoko Episode ID.</param>
    /// <param name="include">Extra details to include.</param>
    /// <param name="language">Language to fetch some details for.</param>
    /// <returns>All TMDB Episodes linked to the Shoko Episode.</returns>
    [HttpGet("{episodeID}/TMDB/Episode")]
    public ActionResult<List<TmdbEpisode>> GetTmdbEpisodesByEpisodeID(
        [FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbEpisode.IncludeDetails>? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage>? language = null
    )
    {
        var episode = _animeEpisodes.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        return episode.TmdbEpisodeCrossReferences
            .Select(xref =>
            {
                var episode = xref.TmdbEpisode;
                if (episode is not null && _tmdbMetadataService.WaitForShowUpdate(episode.TmdbShowID))
                    episode = _tmdbEpisodes.GetByTmdbEpisodeID(episode.TmdbEpisodeID);
                return episode;
            })
            .WhereNotNull()
            .GroupBy(tmdbEpisode => tmdbEpisode.TmdbShowID)
            .Select(groupBy => (TmdbShow: groupBy.First().TmdbShow!, TmdbEpisodes: groupBy.ToList()))
            .Where(tuple => tuple.TmdbShow is not null)
            .SelectMany(tuple0 =>
                string.IsNullOrEmpty(tuple0.TmdbShow.PreferredAlternateOrderingID)
                    ? tuple0.TmdbEpisodes.Select(tmdbEpisode => new TmdbEpisode(tuple0.TmdbShow, tmdbEpisode, include?.CombineFlags(), language))
                    : tuple0.TmdbEpisodes
                        .Select(tmdbEpisode => (TmdbEpisode: tmdbEpisode, TmdbAlternateOrdering: tmdbEpisode.GetTmdbAlternateOrderingEpisodeById(tuple0.TmdbShow.PreferredAlternateOrderingID)))
                        .Where(tuple1 => tuple1.TmdbAlternateOrdering is not null)
                        .Select(tuple1 => new TmdbEpisode(tuple0.TmdbShow, tuple1.TmdbEpisode, tuple1.TmdbAlternateOrdering, include?.CombineFlags(), language)
            ))
            .ToList();
    }

    /// <summary>
    /// Get all TMDB Episode cross-references for the Shoko Episode by ID.
    /// </summary>
    /// <param name="seriesID">Shoko Episode ID.</param>
    /// <returns>All TMDB Episode cross-references for the Shoko Episode.</returns>
    [HttpGet("{seriesID}/TMDB/Episode/CrossReferences")]
    public ActionResult<IReadOnlyList<TmdbEpisode.CrossReference>> GetTMDBEpisodeCrossReferenceByEpisodeID(
        [FromRoute, Range(1, int.MaxValue)] int seriesID
    )
    {
        var episode = _animeEpisodes.GetByID(seriesID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        return episode.TmdbEpisodeCrossReferences
            .Select(xref => new TmdbEpisode.CrossReference(xref))
            .OrderBy(xref => xref.TmdbEpisodeID)
            .ToList();
    }

    #endregion

    #region Images

    private const string InvalidIDForSource = "Invalid image id for selected source.";

    #region All images

    /// <summary>
    /// Get all images for episode with ID, optionally with Disabled images, as well.
    /// </summary>
    /// <param name="episodeID">Shoko ID</param>
    /// <param name="showLinkedIDs"></param>
    /// <param name="includeDisabled"></param>
    /// <param name="includeUndesired"></param>
    /// <returns></returns>
    [HttpGet("{episodeID}/Images")]
    public ActionResult<Images> GetSeriesImages(
        [FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromQuery] bool showLinkedIDs = false,
        [FromQuery] bool includeDisabled = false,
        [FromQuery] bool includeUndesired = false
    )
    {
        var episode = _animeEpisodes.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        return ((IWithImages)episode).GetImages(new() { IsEnabled = includeDisabled ? null : true, IsDesired = includeUndesired ? null : true })
            .OrderBy(a => a.Type)
            .ThenBy(a => a.Source)
            .ThenByDescending(a => a.LanguageCode is null)
            .ThenBy(a => a.LanguageCode)
            .ThenByDescending(a => a.CountryCode is null)
            .ThenBy(a => a.CountryCode)
            .ToDto(showLinkedIDs: showLinkedIDs);
    }

    #endregion

    #region Default image

    /// <summary>
    /// Get the default <see cref="Image"/> for the given <paramref name="imageType"/> for the <see cref="Episode"/>.
    /// </summary>
    /// <param name="episodeID">Episode ID</param>
    /// <param name="imageType">Poster, Banner, Fanart</param>
    /// <returns></returns>
    [HttpGet("{episodeID}/Images/{imageType}")]
    public ActionResult<Image> GetEpisodeDefaultImageForType([FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromRoute] Image.LegacyImageType imageType)
    {
        var episode = _animeEpisodes.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        var imageEntityType = imageType.ToServer();
        var preferredImage = ((IWithImages)episode).GetPreferredImageForType(imageEntityType);
        if (preferredImage is not null)
            return new Image(preferredImage);

        var images = ((IWithImages)episode).GetImages(new() { ImageType = imageEntityType }).ToDto();
        var image = imageEntityType switch
        {
            ImageEntityType.Primary => images.Posters.FirstOrDefault(),
            ImageEntityType.Banner => images.Banners.FirstOrDefault(),
            ImageEntityType.Backdrop => images.Backdrops.FirstOrDefault(),
            ImageEntityType.Logo => images.Logos.FirstOrDefault(),
            ImageEntityType.Disc => images.Discs.FirstOrDefault(),
            _ => null,
        };
        if (image is null)
            return NotFound("Default image for episode not found.");

        return image;
    }

    /// <summary>
    /// Set the default <see cref="Image"/> for the given <paramref name="imageType"/> for the <see cref="Episode"/>.
    /// </summary>
    /// <remarks>
    /// <b>Deprecated:</b> Use the image management controller's set preferred endpoint instead.
    /// </remarks>
    /// <param name="episodeID">Episode ID</param>
    /// <param name="imageType">Poster, Banner, Fanart</param>
    /// <param name="body">The body containing the source and id used to set.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPut("{episodeID}/Images/{imageType}")]
    [Obsolete("Use the image management controller's set preferred endpoint instead.")]
    public ActionResult<Image> SetEpisodeDefaultImageForType([FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromRoute] Image.LegacyImageType imageType, [FromBody] Image.Input.DefaultImageBody body)
    {
        var episode = _animeEpisodes.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        // Check if the id is valid for the given type and source.
        var dataSource = body.Source;
        var imageEntityType = imageType.ToServer();
        var image = Guid.TryParse(body.ID, out var imageID)
            ? _imageManager.GetImageByID(imageID)
            : int.TryParse(body.ID, out var legacyImageID)
                ? _imageManager.GetImageByID(legacyImageID)
                : null;
        if (image is null || (dataSource is not DataSource.None && dataSource != image.Source))
            return ValidationProblem(InvalidIDForSource);

        var xref = _imageManager.SetPreferredImageForEntity(episode, imageEntityType, image);
        return new Image(ImageStub.Wrap(image, xref));
    }

    /// <summary>
    /// Unset the default <see cref="Image"/> for the given <paramref name="imageType"/> for the <see cref="Episode"/>.
    /// </summary>
    /// <remarks>
    /// <b>Deprecated:</b> Use the image management controller's unset preferred endpoint instead.
    /// </remarks>
    /// <param name="episodeID">Episode ID</param>
    /// <param name="imageType">Poster, Banner, Fanart</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("{episodeID}/Images/{imageType}")]
    [Obsolete("Use the image management controller's unset preferred endpoint instead.")]
    public ActionResult DeleteEpisodeDefaultImageForType([FromRoute, Range(1, int.MaxValue)] int episodeID, [FromRoute] Image.LegacyImageType imageType)
    {
        var episode = _animeEpisodes.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        // Check if a default image is set.
        var imageEntityType = imageType.ToServer();
        var xref = _imageManager
            .GetImageCrossReferencesForEntity(episode, new() { ImageType = imageEntityType, IsPreferred = true }).FirstOrDefault();
        if (xref is null)
            return ValidationProblem("No default image for the selected type.");

        switch (xref)
        {
            // Unset the preferred if it's not a user xref, or if it's a user xref and a user uploaded image.
            case { Source: not DataSource.User }:
            case { Source: DataSource.User, ImageSource: DataSource.User }:
                _imageManager.UnsetPreferredImageForEntity(xref);
                break;
            // Otherwise remove the user created xref.
            default:
                _imageManager.RemoveImageCrossReference(xref);
                break;
        }

        // Don't return any content.
        return NoContent();
    }

    #endregion

    #endregion


    #region User Data

    /// <summary>
    /// Return the user stats for the episode with the given <paramref name="episodeID"/>.
    /// </summary>
    /// <param name="episodeID">Shoko episode ID</param>
    /// <returns>The user stats if found.</returns>
    [HttpGet("{episodeID}/UserData")]
    public ActionResult<Episode.EpisodeUserData> GetEpisodeUserData([FromRoute, Range(1, int.MaxValue)] int episodeID)
    {
        var episode = _animeEpisodes.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var user = HttpContext.GetUser();
        var userData = _userDataService.GetEpisodeUserData(episode, user);
        return new Episode.EpisodeUserData(userData);
    }

    /// <summary>
    /// Put a <see cref="Episode.EpisodeUserData"/> object down for the <see cref="Episode"/> with the given <paramref name="episodeID"/>.
    /// </summary>
    /// <param name="episodeID">Shoko episode ID</param>
    /// <param name="episodeUserStats">The new and/or update episode stats to put for the episode.</param>
    /// <returns>The new and/or updated user stats.</returns>
    [HttpPut("{episodeID}/UserData")]
    public ActionResult<Episode.EpisodeUserData> PutEpisodeUserData([FromRoute, Range(1, int.MaxValue)] int episodeID, [FromBody] Episode.EpisodeUserData episodeUserStats)
    {
        var episode = _animeEpisodes.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        // Get the user data.
        var user = HttpContext.GetUser();

        // Merge with the existing entry and return an updated version of the stats.
        return episodeUserStats.MergeWithExisting(user, episode);
    }

    /// <summary>
    /// Patch a <see cref="Episode.EpisodeUserData"/> object down for the <see cref="Episode"/> with the given <paramref name="episodeID"/>.
    /// </summary>
    /// <param name="episodeID">Shoko episode ID</param>
    /// <param name="patchDocument">The JSON patch document to apply to the existing <see cref="Episode.EpisodeUserData"/>.</param>
    /// <returns>The new and/or updated user stats.</returns>
    [HttpPatch("{episodeID}/UserData")]
    public ActionResult<Episode.EpisodeUserData> PatchEpisodeUserData([FromRoute, Range(1, int.MaxValue)] int episodeID, [FromBody] JsonPatchDocument<Episode.EpisodeUserData> patchDocument)
    {
        var episode = _animeEpisodes.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        // Get the user data.
        var user = HttpContext.GetUser();
        var userData = _userDataService.GetEpisodeUserData(episode, user);

        // Patch the body with the existing model.
        var body = new Episode.EpisodeUserData(userData);
        patchDocument.ApplyTo(body, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Merge with the existing entry and return an updated version of the stats.
        return body.MergeWithExisting(user, episode);
    }

    /// <summary>
    /// Set the watched status on an episode
    /// </summary>
    /// <param name="episodeID">Shoko ID</param>
    /// <param name="watched"></param>
    /// <param name="updateFiles">Update the watched status on the files.</param>
    /// <returns></returns>
    [HttpPost("{episodeID}/Watched/{watched}")]
    public async Task<ActionResult> SetWatchedStatusOnEpisode(
        [FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromRoute] bool watched,
        [FromQuery] bool updateFiles = true)
    {
        var episode = _animeEpisodes.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        var user = User;
        if (!user.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        await _userDataService.SetEpisodeWatchedStatus(episode, user, watched, noVideoPropagation: !updateFiles);

        return Ok();
    }

    #endregion

    #region Release Management

    /// <summary>
    /// Get the primary release chosen for the given episode and the reason it was selected.
    /// </summary>
    /// <remarks>
    /// Groups all files for the episode's series into release candidates, ranks them by the
    /// configured signal priority, and returns the highest-ranked candidate that covers this
    /// episode together with a brief explanation: <c>OnlyRelease</c> when there is only one
    /// option, or <c>Ranked</c> when multiple candidates were compared — in which case
    /// <c>DecidingSignal</c>, <c>PrimaryValue</c>, and <c>RunnerUpValue</c> describe the
    /// first signal that broke the tie.
    /// </remarks>
    /// <param name="episodeID">Shoko Episode ID.</param>
    /// <returns>Primary release details and selection reason, or 404 if no release covers the episode.</returns>
    [HttpGet("{episodeID}/PrimaryRelease")]
    [ProducesResponseType(typeof(PrimaryReleaseInfo), 200)]
    [ProducesResponseType(404)]
    public ActionResult<PrimaryReleaseInfo> GetPrimaryRelease([FromRoute, Range(1, int.MaxValue)] int episodeID)
    {
        var episode = _animeEpisodes.GetByID(episodeID);
        if (episode is null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var anidbEpisode = episode.AniDB_Episode;
        if (anidbEpisode is null)
            return NotFound(AnidbNotFoundForEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        // Collect all places for every video linked to this series.
        var places = _videoLocals
            .GetByAniDBAnimeID(series.AniDB_ID)
            .SelectMany(v => _videoLocalPlaces.GetByVideoLocal(v.VideoLocalID))
            .ToList();

        // Group into release candidates and filter to those covering this episode.
        var episodeKey = (anidbEpisode.EpisodeType, anidbEpisode.EpisodeNumber);
        var covering = _releaseGrouper.Group(places)
            .Where(c => c.EpisodeCoverage.Contains(episodeKey))
            .ToList();

        if (covering.Count == 0)
            return NotFound("No release found for this episode.");

        var ranked = _releaseComparer.Rank(covering);
        var primary = ranked[0];

        if (ranked.Count == 1)
        {
            return Ok(new PrimaryReleaseInfo
            {
                CandidateCount = 1,
                Primary = new ReleaseCandidateSummary(primary),
                Reason = PrimaryReleaseReason.OnlyRelease,
            });
        }

        var decision = _releaseComparer.CompareWithDecision(primary, ranked[1]);
        return Ok(new PrimaryReleaseInfo
        {
            CandidateCount = ranked.Count,
            Primary = new ReleaseCandidateSummary(primary),
            Reason = PrimaryReleaseReason.Ranked,
            DecidingSignal = decision.DecidingSignal,
            PrimaryValue = decision.PrimaryValue,
            RunnerUpValue = decision.RunnerUpValue,
        });
    }

    #endregion
}
