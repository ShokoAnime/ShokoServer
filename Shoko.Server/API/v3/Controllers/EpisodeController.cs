using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.AniDB;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.API.v3.Models.TMDB;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using EpisodeType = Shoko.Server.API.v3.Models.AniDB.EpisodeType;

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

    private readonly AnimeEpisodeService _episodeService;

    private readonly IUserDataService _userDataService;

    private readonly TmdbLinkingService _tmdbLinkingService;

    private readonly TmdbMetadataService _tmdbMetadataService;

    public EpisodeController(
        ISettingsProvider settingsProvider,
        AnimeSeriesService seriesService,
        AnimeGroupService groupService,
        AnimeEpisodeService episodeService,
        IUserDataService userDataService,
        TmdbLinkingService tmdbLinkingService,
        TmdbMetadataService tmdbMetadataService
    ) : base(settingsProvider)
    {
        _seriesService = seriesService;
        _groupService = groupService;
        _episodeService = episodeService;
        _userDataService = userDataService;
        _tmdbLinkingService = tmdbLinkingService;
        _tmdbMetadataService = tmdbMetadataService;
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
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="includeWatched">Include watched episodes in the list.</param>
    /// <param name="type">Filter episodes by the specified <see cref="EpisodeType"/>s.</param>
    /// <param name="includeFiles">Include files with the episodes.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
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
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] IncludeOnlyFilter includeWatched = IncludeOnlyFilter.True,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeType> type = null,
        [FromQuery] bool includeFiles = false,
        [FromQuery] bool includeMediaInfo = false,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] string search = null, [FromQuery] bool fuzzy = true)
    {
        var user = User;
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        var allowedSeriesDict = new ConcurrentDictionary<int, bool>();
        var episodes = RepoFactory.AnimeEpisode.GetAll()
            .AsParallel()
            .Where(episode =>
            {
                // Only show episodes the user is allowed to view.
                if (!allowedSeriesDict.TryGetValue(episode.AnimeSeriesID, out var isAllowed))
                    allowedSeriesDict.TryAdd(episode.AnimeSeriesID, isAllowed = user.AllowedSeries(episode.AnimeSeries));
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
                if (type != null && type.Count > 0)
                {
                    var mappedType = anidb.AbstractEpisodeType.ToV3Dto();
                    if (!type.Contains(mappedType))
                        return false;
                }

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
                    // Or if we should only show watched episodes and the the episode is not watched, then hide it.
                    var shouldHideWatched = includeWatched == IncludeOnlyFilter.False;
                    var isWatched = shoko.GetUserRecord(user.JMMUserID)?.WatchedDate != null;
                    if (shouldHideWatched == isWatched)
                        return false;
                }

                return true;
            });
        if (hasSearch)
        {
            var languages = SettingsProvider.GetSettings()
                .Language.EpisodeTitleLanguageOrder
                .Select(lang => lang.GetTitleLanguage())
                .Concat(new TitleLanguage[] { TitleLanguage.English, TitleLanguage.Romaji })
                .ToHashSet();
            return episodes
                .Search(
                    search,
                    ep => RepoFactory.AniDB_Episode_Title.GetByEpisodeID(ep.AniDB.EpisodeID)
                        .Where(title => title != null && languages.Contains(title.Language))
                        .Select(title => title.Title)
                        .Append(ep.Shoko.PreferredTitle)
                        .Distinct()
                        .ToList(),
                    fuzzy
                )
                .ToListResult(a => new Episode(HttpContext, a.Result.Shoko, includeDataFrom, includeFiles, includeMediaInfo, includeAbsolutePaths, includeXRefs), page, pageSize);
        }

        // Order the episodes since we're not using the search ordering.
        return episodes
            .OrderBy(episode => episode.Shoko.AnimeSeriesID)
            .ThenBy(episode => episode.AniDB.EpisodeType)
            .ThenBy(episode => episode.AniDB.EpisodeNumber)
            .ToListResult(a => new Episode(HttpContext, a.Shoko, includeDataFrom, includeFiles, includeMediaInfo, includeAbsolutePaths, includeXRefs), page, pageSize);
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
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<EpisodeType> type = null)
    {
        var user = User;
        var allowedAnimeDict = new ConcurrentDictionary<int, bool>();
        return RepoFactory.AniDB_Episode.GetAll()
            .AsParallel()
            .Where(episode =>
            {
                // Only show episodes the user is allowed to view.
                if (!allowedAnimeDict.TryGetValue(episode.AnimeID, out var isAllowed))
                {
                    // If this is an episode not tied to a missing anime, then
                    // just hide it.
                    var anime = RepoFactory.AniDB_Anime.GetByAnimeID(episode.AnimeID);
                    isAllowed = anime == null ? false : user.AllowedAnime(anime);

                    allowedAnimeDict.TryAdd(episode.AnimeID, isAllowed);
                }
                if (!isAllowed)
                    return false;

                // Filter by episode type, if specified
                if (type != null && type.Count > 0)
                {
                    var mappedType = episode.AbstractEpisodeType.ToV3Dto();
                    if (!type.Contains(mappedType))
                        return false;
                }

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
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("{episodeID}")]
    public ActionResult<Episode> GetEpisodeByEpisodeID(
        [FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromQuery] bool includeFiles = false,
        [FromQuery] bool includeMediaInfo = false,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        return new Episode(HttpContext, episode, includeDataFrom, includeFiles, includeMediaInfo, includeAbsolutePaths, includeXRefs);
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
        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);

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

            RepoFactory.AnimeEpisode.Save(episode);

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
        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
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

            RepoFactory.AnimeEpisode.Save(episode);

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
        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
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
        var anidb = RepoFactory.AniDB_Episode.GetByEpisodeID(anidbEpisodeID);
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
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("AniDB/{anidbEpisodeID}/Episode")]
    public ActionResult<Episode> GetEpisode(
        [FromRoute] int anidbEpisodeID,
        [FromQuery] bool includeFiles = false,
        [FromQuery] bool includeMediaInfo = false,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        var anidb = RepoFactory.AniDB_Episode.GetByEpisodeID(anidbEpisodeID);
        if (anidb == null)
            return NotFound(AnidbNotFoundForAnidbEpisodeID);

        var episode = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(anidb.EpisodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundForAnidbEpisodeID);

        return new Episode(HttpContext, episode, includeDataFrom, includeFiles, includeMediaInfo, includeAbsolutePaths, includeXRefs);
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
        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        if (vote.Value > vote.MaxValue)
            return ValidationProblem($"Value must be less than or equal to the set max value ({vote.MaxValue}).", nameof(vote.Value));

        await _episodeService.AddEpisodeVote(episode, vote.GetRating());

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
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbMovie.IncludeDetails> include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage> language = null
    )
    {
        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
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
                    movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movie.TmdbMovieID);
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
        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        await _tmdbLinkingService.AddMovieLinkForEpisode(episode.AniDB_EpisodeID, body.ID, additiveLink: !body.Replace);

        var needRefresh = RepoFactory.TMDB_Movie.GetByTmdbMovieID(body.ID) is null || body.Refresh;
        if (needRefresh)
            await _tmdbMetadataService.ScheduleUpdateOfMovie(body.ID, forceRefresh: body.Refresh, downloadImages: true);

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
        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        if (body != null && body.ID > 0)
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
        var episode = RepoFactory.AnimeEpisode.GetByID(seriesID);
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
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TmdbEpisode.IncludeDetails> include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<TitleLanguage> language = null
    )
    {
        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
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
                    episode = RepoFactory.TMDB_Episode.GetByTmdbEpisodeID(episode.TmdbEpisodeID);
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
        var episode = RepoFactory.AnimeEpisode.GetByID(seriesID);
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

    #region All images

    private static readonly HashSet<Image.ImageType> _allowedImageTypes = [Image.ImageType.Poster, Image.ImageType.Banner, Image.ImageType.Backdrop, Image.ImageType.Logo, Image.ImageType.Thumbnail];

    private const string InvalidIDForSource = "Invalid image id for selected source.";

    private const string InvalidImageIsDisabled = "Image is disabled.";

    /// <summary>
    /// Get all images for episode with ID, optionally with Disabled images, as well.
    /// </summary>
    /// <param name="episodeID">Shoko ID</param>
    /// <param name="includeDisabled"></param>
    /// <returns></returns>
    [HttpGet("{episodeID}/Images")]
    public ActionResult<Images> GetSeriesImages([FromRoute, Range(1, int.MaxValue)] int episodeID, [FromQuery] bool includeDisabled)
    {
        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        return episode.GetImages().ToDto(includeDisabled: includeDisabled, includeThumbnails: true);
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
        [FromRoute] Image.ImageType imageType)
    {
        if (!_allowedImageTypes.Contains(imageType))
            return NotFound();

        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        var imageEntityType = imageType.ToServer();
        var preferredImage = episode.GetPreferredImageForType(imageEntityType);
        if (preferredImage != null)
            return new Image(preferredImage);

        var images = episode.GetImages().ToDto(includeThumbnails: true);
        return imageEntityType switch
        {
            ImageEntityType.Poster => images.Posters.FirstOrDefault(),
            ImageEntityType.Banner => images.Banners.FirstOrDefault(),
            ImageEntityType.Backdrop => images.Backdrops.FirstOrDefault(),
            ImageEntityType.Logo => images.Logos.FirstOrDefault(),
            ImageEntityType.Thumbnail => images.Thumbnails.FirstOrDefault(),
            _ => null
        };
    }


    /// <summary>
    /// Set the default <see cref="Image"/> for the given <paramref name="imageType"/> for the <see cref="Episode"/>.
    /// </summary>
    /// <param name="episodeID">Episode ID</param>
    /// <param name="imageType">Poster, Banner, Fanart</param>
    /// <param name="body">The body containing the source and id used to set.</param>
    /// <returns></returns>
    [HttpPut("{episodeID}/Images/{imageType}")]
    public ActionResult<Image> SetEpisodeDefaultImageForType([FromRoute, Range(1, int.MaxValue)] int episodeID,
        [FromRoute] Image.ImageType imageType, [FromBody] Image.Input.DefaultImageBody body)
    {
        if (!_allowedImageTypes.Contains(imageType))
            return NotFound();

        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var anime = episode.AnimeSeries?.AniDB_Anime;
        if (anime is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedAnime(anime))
            return Forbid(EpisodeForbiddenForUser);

        // Check if the id is valid for the given type and source.
        var dataSource = body.Source.ToServer();
        var imageEntityType = imageType.ToServer();
        var image = ImageUtils.GetImageMetadata(dataSource, imageEntityType, body.ID);
        if (image is null)
            return ValidationProblem(InvalidIDForSource);
        if (!image.IsEnabled)
            return ValidationProblem(InvalidImageIsDisabled);

        // Create or update the entry.
        var defaultImage = RepoFactory.AniDB_Episode_PreferredImage.GetByAnidbEpisodeIDAndType(episode.AniDB_EpisodeID, imageEntityType) ??
            new(anime.AnimeID, episode.AniDB_EpisodeID, imageEntityType);
        defaultImage.ImageID = body.ID;
        defaultImage.ImageSource = dataSource;
        RepoFactory.AniDB_Episode_PreferredImage.Save(defaultImage);

        return new Image(body.ID, imageEntityType, dataSource, true);
    }

    /// <summary>
    /// Unset the default <see cref="Image"/> for the given <paramref name="imageType"/> for the <see cref="Episode"/>.
    /// </summary>
    /// <param name="episodeID">Episode ID</param>
    /// <param name="imageType">Poster, Banner, Fanart</param>
    /// <returns></returns>
    [HttpDelete("{episodeID}/Images/{imageType}")]
    public ActionResult DeleteEpisodeDefaultImageForType([FromRoute, Range(1, int.MaxValue)] int episodeID, [FromRoute] Image.ImageType imageType)
    {
        if (!_allowedImageTypes.Contains(imageType))
            return NotFound();

        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        // Check if a default image is set.
        var imageEntityType = imageType.ToServer();
        var defaultImage = RepoFactory.AniDB_Episode_PreferredImage.GetByAnidbEpisodeIDAndType(episode.AniDB_EpisodeID, imageEntityType);
        if (defaultImage == null)
            return ValidationProblem("No default banner.");

        // Delete the entry.
        RepoFactory.AniDB_Episode_PreferredImage.Delete(defaultImage);

        // Don't return any content.
        return NoContent();
    }

    #endregion

    #endregion

    #region Watch Status

    /// <summary>
    /// Set the watched status on an episode
    /// </summary>
    /// <param name="episodeID">Shoko ID</param>
    /// <param name="watched"></param>
    /// <returns></returns>
    [HttpPost("{episodeID}/Watched/{watched}")]
    public async Task<ActionResult> SetWatchedStatusOnEpisode([FromRoute, Range(1, int.MaxValue)] int episodeID, [FromRoute] bool watched)
    {
        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.AnimeSeries;
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        var user = User;
        if (!user.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        await _userDataService.SetEpisodeWatchedStatus(user, episode, watched);

        return Ok();
    }

    #endregion

    /// <summary>
    /// Get all episodes with no files.
    /// </summary>
    /// <param name="includeSpecials">Include specials in the list.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="onlyAiredEpisodes">Only show episodes which has aired.</param>
    /// <param name="onlyFinishedSeries">Only show episodes for completed series.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [HttpGet("WithNoFiles")]
    public ActionResult<ListResult<Episode>> GetMissingEpisodes(
        [FromQuery] bool includeSpecials = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] bool onlyAiredEpisodes = false,
        [FromQuery] bool onlyFinishedSeries = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        IEnumerable<SVR_AnimeEpisode> enumerable = RepoFactory.AnimeEpisode.GetEpisodesWithNoFiles(includeSpecials, onlyAiredEpisodes);
        if (onlyFinishedSeries)
        {
            var dictSeriesFinishedAiring = RepoFactory.AnimeSeries.GetAll()
                .ToDictionary(a => a.AnimeSeriesID, a => a.AniDB_Anime.GetFinishedAiring());
            enumerable = enumerable.Where(episode =>
                dictSeriesFinishedAiring.TryGetValue(episode.AnimeSeriesID, out var finishedAiring) && finishedAiring);
        }

        return enumerable
            .ToListResult(episode => new Episode(HttpContext, episode, withXRefs: includeXRefs), page, pageSize);
    }
}
