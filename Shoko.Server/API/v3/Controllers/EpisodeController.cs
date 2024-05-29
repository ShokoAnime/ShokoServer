using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using EpisodeType = Shoko.Server.API.v3.Models.Shoko.EpisodeType;
using AniDBEpisodeType = Shoko.Models.Enums.EpisodeType;
using Shoko.Models.Server;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class EpisodeController : BaseController
{

    internal const string EpisodeWithZeroID = "episodeID must be greater than 0";

    internal const string EpisodeNotFoundWithEpisodeID = "No Episode entry for the given episodeID";

    internal const string EpisodeNotFoundForAnidbEpisodeID = "No Episode entry for the given anidbEpisodeID";

    internal const string AnidbNotFoundForEpisodeID = "No Episode.Anidb entry for the given episodeID";

    internal const string AnidbNotFoundForAnidbEpisodeID = "No Episode.Anidb entry for the given anidbEpisodeID";

    internal const string EpisodeForbiddenForUser = "Accessing Episode is not allowed for the current user";

    internal const string EpisodeNoSeriesForEpisodeID = "Unable to find a Series entry for given episodeID";

    /// <summary>
    /// Get all <see cref="Episode"/>s for the given filter.
    /// </summary>
    /// <remarks>
    /// <see cref="Filter"/> or <see cref="Group"/> is irrelevant at this level.
    /// </remarks>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="includeMissing">Include missing episodes in the list.</param>
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
                    allowedSeriesDict.TryAdd(episode.AnimeSeriesID, isAllowed = user.AllowedSeries(episode.GetAnimeSeries()));
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

                // Filter by hidden state, if spesified
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
                    var mappedType = Episode.MapAniDBEpisodeType((AniDBEpisodeType)anidb.EpisodeType);
                    if (!type.Contains(mappedType))
                        return false;
                }

                // Filter by availability, if specified
                if (includeMissing != IncludeOnlyFilter.True)
                {
                    // If we should hide missing episodes and the episode has no files, then hide it.
                    // Or if we should only show missing episodes and the episode has files, the hide it.
                    var shouldHideMissing = includeMissing == IncludeOnlyFilter.False;
                    var noFiles = shoko.GetVideoLocals().Count == 0;
                    if (shouldHideMissing == noFiles)
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
                .LanguagePreference
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
    /// Get all <see cref="Episode.AniDB"/>s. Admins only.
    /// </summary>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <param name="type">Filter episodes by the specified <see cref="EpisodeType"/>s.</param>
    /// <returns></returns>
    [HttpGet("AniDB")]
    [Authorize("admin")]
    public ActionResult<ListResult<Episode.AniDB>> GetAllAniDBEpisodes(
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
                    var mappedType = Episode.MapAniDBEpisodeType((AniDBEpisodeType)episode.EpisodeType);
                    if (!type.Contains(mappedType))
                        return false;
                }

                return true;
            })
            .OrderBy(episode => episode.AnimeID)
            .ThenBy(episode => episode.EpisodeType)
            .ThenBy(episode => episode.EpisodeNumber)
            .ToListResult(episode => new Episode.AniDB(episode), page, pageSize);
    }

    /// <summary>
    /// Get all <see cref="Episode.TvDB"/>s. Admins only.
    /// </summary>
    /// <remarks>
    /// It's admins only since i don't want to add the logic to
    /// </remarks>
    /// <param name="pageSize">The page size. Set to <code>0</code> to disable pagination.</param>
    /// <param name="page">The page index.</param>
    /// <returns></returns>
    [HttpGet("TvDB")]
    public ActionResult<ListResult<Episode.TvDB>> GetAllTvDBEpisodes(
        [FromQuery, Range(0, 1000)] int pageSize = 20,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        var user = User;
        var isAdmin = user.IsAdmin == 1;
        var allowedShowDict = new ConcurrentDictionary<int, bool>();
        return RepoFactory.TvDB_Episode.GetAll()
            .Where(episode =>
            {
                // Only show episodes the user is allowed to view.
                if (!allowedShowDict.TryGetValue(episode.SeriesID, out var isAllowed))
                {
                    // If this is an episode not tied to a missing show, then
                    // just hide it.
                    var show = RepoFactory.TvDB_Series.GetByTvDBID(episode.SeriesID);
                    if (show == null)
                    {
                        isAllowed = false;
                        goto addValue;
                    }

                    // If there are no cross-references, then hide it if the
                    // user is not an admin.
                    var xref = RepoFactory.CrossRef_AniDB_TvDB.GetByTvDBID(episode.SeriesID)
                        .FirstOrDefault();
                    if (xref == null)
                    {
                        isAllowed = isAdmin;
                        goto addValue;
                    }

                    // Or if the cross-reference is broken then also hide it if
                    // the user is not an admin, otherwise check if the user can
                    // view the series.
                    var anime = RepoFactory.AniDB_Anime.GetByAnimeID(xref.AniDBID);
                    isAllowed = anime == null ? isAdmin : user.AllowedAnime(anime);

addValue: allowedShowDict.TryAdd(episode.SeriesID, isAllowed);
                }
                if (!isAllowed)
                    return false;

                return true;
            })
            .OrderBy(episode => episode.SeriesID)
            .ThenBy(episode => episode.SeasonNumber)
            .ThenBy(episode => episode.EpisodeNumber)
            .ToListResult(episode => new Episode.TvDB(episode), page, pageSize);
    }

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
        [FromRoute] int episodeID,
        [FromQuery] bool includeFiles = false,
        [FromQuery] bool includeMediaInfo = false,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null)
    {
        if (episodeID == 0)
            return BadRequest(EpisodeWithZeroID);

        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.GetAnimeSeries();
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
    public ActionResult OverrideEpisodeTitle([FromRoute] int episodeID, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] EpisodeTitleOverride body)
    {
        if (episodeID == 0)
            return BadRequest(EpisodeWithZeroID);

        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);

        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.GetAnimeSeries();
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
    public ActionResult PostEpisodeSetHidden([FromRoute] int episodeID, [FromQuery] bool value = true, [FromQuery] bool updateStats = true)
    {
        if (episodeID == 0)
            return BadRequest(EpisodeWithZeroID);

        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.GetAnimeSeries();
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
                series.UpdateStats(true, true);
                series.TopLevelAnimeGroup.UpdateStatsFromTopLevel(true, true);
            }

            ShokoEventHandler.Instance.OnEpisodeUpdated(series, episode, UpdateReason.Updated);
        }

        return Ok();
    }

    /// <summary>
    /// Get the <see cref="Episode.AniDB"/> entry for the given <paramref name="episodeID"/>.
    /// </summary>
    /// <param name="episodeID">Shoko ID</param>
    /// <returns></returns>
    [HttpGet("{episodeID}/AniDB")]
    public ActionResult<Episode.AniDB> GetEpisodeAnidbByEpisodeID([FromRoute] int episodeID)
    {
        if (episodeID == 0)
            return BadRequest(EpisodeWithZeroID);

        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var anidb = episode.AniDB_Episode;
        if (anidb == null)
            return InternalError(AnidbNotFoundForEpisodeID);

        return new Episode.AniDB(anidb);
    }

    /// <summary>
    /// Get the <see cref="Episode.AniDB"/> entry for the given <paramref name="anidbEpisodeID"/>.
    /// </summary>
    /// <param name="anidbEpisodeID">AniDB Episode ID</param>
    /// <returns></returns>
    [HttpGet("AniDB/{anidbEpisodeID}")]
    public ActionResult<Episode.AniDB> GetEpisodeAnidbByAnidbEpisodeID([FromRoute] int anidbEpisodeID)
    {
        var anidb = RepoFactory.AniDB_Episode.GetByEpisodeID(anidbEpisodeID);
        if (anidb == null)
            return NotFound(AnidbNotFoundForAnidbEpisodeID);

        return new Episode.AniDB(anidb);
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
    public ActionResult PostEpisodeVote([FromRoute] int episodeID, [FromBody] Vote vote)
    {
        if (episodeID == 0)
            return BadRequest(EpisodeWithZeroID);

        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.GetAnimeSeries();
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        if (vote.Value > vote.MaxValue)
            return ValidationProblem($"Value must be less than or equal to the set max value ({vote.MaxValue}).", nameof(vote.Value));

        Episode.AddEpisodeVote(HttpContext, episode, User.JMMUserID, vote);

        return NoContent();
    }

    /// <summary>
    /// Get the TvDB details for episode with Shoko ID
    /// </summary>
    /// <param name="episodeID">Shoko ID</param>
    /// <returns></returns>
    [HttpGet("{episodeID}/TvDB")]
    public ActionResult<List<Episode.TvDB>> GetEpisodeTvDBDetails([FromRoute] int episodeID)
    {
        if (episodeID == 0)
            return BadRequest(EpisodeWithZeroID);

        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.GetAnimeSeries();
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        return episode.TvDBEpisodes
            .Select(a => new Episode.TvDB(a))
            .ToList();
    }

    /// <summary>
    /// Set the watched status on an episode
    /// </summary>
    /// <param name="episodeID">Shoko ID</param>
    /// <param name="watched"></param>
    /// <returns></returns>
    [HttpPost("{episodeID}/Watched/{watched}")]
    public ActionResult SetWatchedStatusOnEpisode([FromRoute] int episodeID, [FromRoute] bool watched)
    {
        if (episodeID == 0)
            return BadRequest(EpisodeWithZeroID);

        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
            return NotFound(EpisodeNotFoundWithEpisodeID);

        var series = episode.GetAnimeSeries();
        if (series is null)
            return InternalError(EpisodeNoSeriesForEpisodeID);

        if (!User.AllowedSeries(series))
            return Forbid(EpisodeForbiddenForUser);

        episode.ToggleWatchedStatus(watched, true, DateTime.Now, true, User.JMMUserID, true);

        return Ok();
    }

    /// <summary>
    /// Get all episodes with no files.
    /// </summary>
    /// <param name="includeSpecials">Include specials in the list.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="onlyFinishedSeries">Only show episodes for completed series.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [HttpGet("WithNoFiles")]
    public ActionResult<ListResult<Episode>> GetMissingEpisodes(
        [FromQuery] bool includeSpecials = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] bool onlyFinishedSeries = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        IEnumerable<SVR_AnimeEpisode> enumerable = RepoFactory.AnimeEpisode.GetEpisodesWithNoFiles(includeSpecials);
        if (onlyFinishedSeries)
        {
            var dictSeriesFinishedAiring = RepoFactory.AnimeSeries.GetAll()
                .ToDictionary(a => a.AnimeSeriesID, a => a.GetAnime().GetFinishedAiring());
            enumerable = enumerable.Where(episode =>
                dictSeriesFinishedAiring.TryGetValue(episode.AnimeSeriesID, out var finishedAiring) && finishedAiring);
        }

        return enumerable
            .ToListResult(episode => new Episode(HttpContext, episode, withXRefs: includeXRefs), page, pageSize);
    }

    public EpisodeController(ISettingsProvider settingsProvider) : base(settingsProvider)
    {
    }
}
