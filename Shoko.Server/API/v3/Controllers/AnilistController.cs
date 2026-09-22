using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.AniDB;
using Shoko.Server.API.v3.Models.Anilist;
using Shoko.Server.API.v3.Models.Anilist.Input;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Providers.Anilist;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.Anilist;
using Shoko.Server.Scheduling.Jobs.Anilist;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

using AnimeType = Shoko.Abstractions.Metadata.Enums.AnimeType;
using EpisodeType = Shoko.Abstractions.Metadata.Enums.EpisodeType;
using File = Shoko.Server.API.v3.Models.Shoko.File;
using MatchRating = Shoko.Abstractions.Metadata.Enums.MatchRating;

#pragma warning disable CA1822
#nullable enable
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public partial class AnilistController(
    ISettingsProvider settingsProvider,
    ILogger<AnilistController> _logger,
    AnilistSearchService _anilistSearchService,
    AnilistMetadataService _anilistMetadataService,
    AnilistApiClient _anilistApiClient,
    IJobFactory _jobFactory,
    IQueueScheduler _scheduler,
    IImageManager _imageManager,
    AniDB_AnimeRepository _anidbAnime,
    AniDB_EpisodeRepository _anidbEpisodes,
    AnimeSeriesRepository _animeSeries,
    Anilist_AnimeRepository _anilistAnime,
    Anilist_EpisodeRepository _anilistEpisodes,
    Anilist_TagRepository _anilistTags,
    Anilist_Anime_TagRepository _anilistAnimeTags,
    CrossRef_AniDB_Anilist_AnimeRepository _crossRefAnidbAnilistAnime,
    CrossRef_AniDB_Anilist_EpisodeRepository _crossRefAnidbAnilistEpisodes
) : BaseController(settingsProvider)
{
    // Mirrors TmdbController.TryQueueWhenPaused: while the AniList breaker is tripped, refuse an
    // immediate request with a 503 + Retry-After, or queue the job (prioritized) and still answer 503
    // so the caller knows nothing happened yet. Every job type dispatched here also carries
    // [AnilistApiRateLimited], so dispatch is blocked at the queue layer regardless.
    private async Task<ActionResult?> TryQueueWhenPaused<T>(Action<T> configure, string jobDescription, bool immediate) where T : class, IQueueJob
    {
        var status = _anilistMetadataService.GetPauseStatus();
        if (!status.IsPaused)
            return null;

        var seconds = (int)(status.RemainingPauseTime?.TotalSeconds ?? 0);
        if (immediate)
        {
            _logger.LogInformation("AniList is currently paused. {Job} was requested immediately and has been refused; retry in approximately {Seconds} second(s).", jobDescription, seconds);
        }
        else
        {
            _logger.LogInformation("AniList is currently paused. {Job} has been queued and will start in approximately {Seconds} second(s).", jobDescription, seconds);
            await _scheduler.StartJob(configure, prioritize: true);
        }
        Response.Headers.RetryAfter = seconds.ToString();
        return StatusCode(503);
    }

    // For endpoints that call AniList inline with nothing to queue.
    private ActionResult? RefuseWhenPaused()
    {
        var status = _anilistMetadataService.GetPauseStatus();
        if (!status.IsPaused)
            return null;

        var seconds = (int)(status.RemainingPauseTime?.TotalSeconds ?? 0);
        _logger.LogInformation("AniList is currently paused. Online lookup refused; retry in approximately {Seconds} second(s).", seconds);
        Response.Headers.RetryAfter = seconds.ToString();
        return StatusCode(503);
    }

    // Episode rows are synthesized during an anime refresh; if the parent anime is mid-update,
    // wait for it and re-read so the caller gets the finalized row.
    private Anilist_Episode? GetEpisodeWaitingForUpdate(int episodeID)
    {
        var episode = _anilistEpisodes.GetByAnilistEpisodeID(episodeID);
        if (episode is not null && _anilistMetadataService.WaitForAnimeUpdate(episode.AnilistAnimeID))
            episode = _anilistEpisodes.GetByAnilistEpisodeID(episodeID);
        return episode;
    }

    #region Anime

    #region Constants

    internal const string AnimeNotFound = "An Anilist.Anime by the given `animeID` was not found.";

    internal const string AnimeCrossReferenceWithIdHeader = "AnidbAnimeId,AnilistAnimeId,Rating";

    internal const string EpisodeCrossReferenceWithIdHeader = "AnidbAnimeId,AnidbEpisodeId,AnilistAnimeId,AnilistEpisodeId,Rating";

    #endregion

    #region Basics

    /// <summary>
    /// List all locally available Anilist anime.
    /// </summary>
    /// <param name="search"></param>
    /// <param name="fuzzy"></param>
    /// <param name="include"></param>
    /// <param name="restricted"></param>
    /// <param name="pageSize"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    [HttpGet("Anime")]
    public ActionResult<ListResult<AnilistAnime>> GetAnilistAnimes(
        [FromQuery] string? search = null,
        [FromQuery] bool fuzzy = true,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AnilistAnime.IncludeDetails>? include = null,
        [FromQuery] IncludeOnlyFilter restricted = IncludeOnlyFilter.True,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var hasSearch = !string.IsNullOrWhiteSpace(search);
        var animes = _anilistAnime.GetAll()
            .AsParallel()
            .Where(anime =>
            {
                if (restricted != IncludeOnlyFilter.True)
                {
                    var includeRestricted = restricted == IncludeOnlyFilter.Only;
                    var isRestricted = anime.IsRestricted;
                    if (isRestricted != includeRestricted)
                        return false;
                }

                return true;
            });

        if (hasSearch)
        {
            return animes
                .Search(
                    search!,
                    anime => new List<string>
                    {
                        anime.EnglishTitle,
                        anime.MainTitle,
                        anime.NativeTitle
                    }
                    .Concat(anime.Synonyms)
                    .Where(t => !string.IsNullOrEmpty(t))
                    .Distinct()
                    .ToList(),
                    fuzzy
                )
                .ToListResult(searchResult =>
                {
                    var anime = searchResult.Result;
                    if (_anilistMetadataService.WaitForAnimeUpdate(anime.AnilistAnimeID))
                        anime = _anilistAnime.GetByAnilistAnimeID(anime.AnilistAnimeID) ?? anime;
                    return new AnilistAnime(anime, include?.CombineFlags());
                }, page, pageSize);
        }

        return animes
            .OrderBy(anime => anime.PreferredTitle)
            .ThenBy(anime => anime.AnilistAnimeID)
            .ToListResult(anime =>
            {
                if (_anilistMetadataService.WaitForAnimeUpdate(anime.AnilistAnimeID))
                    anime = _anilistAnime.GetByAnilistAnimeID(anime.AnilistAnimeID) ?? anime;
                return new AnilistAnime(anime, include?.CombineFlags());
            }, page, pageSize);
    }

    /// <summary>
    /// Get the local metadata for an Anilist anime.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <param name="include"></param>
    /// <returns></returns>
    [HttpGet("Anime/{animeID}")]
    public ActionResult<AnilistAnime> GetAnilistAnimeByAnimeID(
        [FromRoute] int animeID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AnilistAnime.IncludeDetails>? include = null
    )
    {
        var anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is not null && _anilistMetadataService.WaitForAnimeUpdate(anime.AnilistAnimeID))
            anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is null)
            return NotFound(AnimeNotFound);

        return new AnilistAnime(anime, include?.CombineFlags());
    }

    /// <summary>
    /// Get multiple local Anilist anime at once.
    /// </summary>
    /// <param name="body">Body containing the IDs and details to include.</param>
    /// <returns></returns>
    [HttpPost("Anime/Bulk")]
    public ActionResult<List<AnilistAnime>> BulkGetAnilistAnimeByAnimeIDs([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] AnilistBulkFetchBody<AnilistAnime.IncludeDetails> body) =>
        body.IDs
            .Select(animeID => animeID <= 0 ? null : _anilistAnime.GetByAnilistAnimeID(animeID))
            .WhereNotNull()
            .Select(anime =>
            {
                if (_anilistMetadataService.WaitForAnimeUpdate(anime.AnilistAnimeID))
                    anime = _anilistAnime.GetByAnilistAnimeID(anime.AnilistAnimeID) ?? anime;
                return new AnilistAnime(anime, body.Include?.CombineFlags());
            })
            .ToList();

    /// <summary>
    /// Get all titles for an Anilist anime.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <returns></returns>
    [HttpGet("Anime/{animeID}/Titles")]
    public ActionResult<IReadOnlyList<Title>> GetTitlesForAnilistAnimeByAnimeID([FromRoute] int animeID)
    {
        var anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is not null && _anilistMetadataService.WaitForAnimeUpdate(anime.AnilistAnimeID))
            anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is null)
            return NotFound(AnimeNotFound);

        var withTitles = (IWithTitles)anime;
        var defaultTitle = withTitles.DefaultTitle;
        return withTitles.Titles.Select(title => new Title(title, defaultTitle.Value, withTitles.PreferredTitle)).ToList();
    }

    /// <summary>
    /// Get all overviews for an Anilist anime.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <returns></returns>
    [HttpGet("Anime/{animeID}/Overviews")]
    public ActionResult<IReadOnlyList<Overview>> GetOverviewsForAnilistAnimeByAnimeID([FromRoute] int animeID)
    {
        var anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is not null && _anilistMetadataService.WaitForAnimeUpdate(anime.AnilistAnimeID))
            anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is null)
            return NotFound(AnimeNotFound);

        var withDescriptions = (IWithDescriptions)anime;
        return withDescriptions.Descriptions.Select(overview => new Overview(overview, withDescriptions.DefaultDescription?.Value, withDescriptions.PreferredDescription)).ToList();
    }

    /// <summary>
    /// Get every image for the Anilist anime, grouped by image type.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <param name="includeDisabled">Include disabled images.</param>
    /// <param name="includeUndesired">Include images that won't be downloaded automatically.</param>
    /// <param name="includeRemoteUrl">Include the remote URL for the images.</param>
    /// <returns></returns>
    [HttpGet("Anime/{animeID}/Images")]
    public ActionResult<Images> GetImagesForAnilistAnimeByAnimeID(
        [FromRoute] int animeID,
        [FromQuery] bool includeDisabled = false,
        [FromQuery] bool includeUndesired = false,
        [FromQuery] RemoteUrlInclusion includeRemoteUrl = RemoteUrlInclusion.WhenUnavailable
    )
    {
        var anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is not null && _anilistMetadataService.WaitForAnimeUpdate(anime.AnilistAnimeID))
            anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is null)
            return NotFound(AnimeNotFound);

        return ((IWithImages)anime).GetImages(new() { IsEnabled = includeDisabled ? null : true, IsDesired = includeUndesired ? null : true }).ToDto(includeRemoteUrl: includeRemoteUrl, remoteUrlTemplate: _imageManager.GetTemplateUrlForSource);
    }

    /// <summary>
    /// Get all tags for an Anilist anime.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <param name="excludeDescriptions">Exclude the tag descriptions.</param>
    /// <returns></returns>
    [HttpGet("Anime/{animeID}/Tags")]
    public ActionResult<IReadOnlyList<Tag>> GetTagsForAnilistAnimeByAnimeID(
        [FromRoute] int animeID,
        [FromQuery] bool excludeDescriptions = false
    )
    {
        var anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is not null && _anilistMetadataService.WaitForAnimeUpdate(anime.AnilistAnimeID))
            anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is null)
            return NotFound(AnimeNotFound);

        return anime.Tags
            .Where(animeTag => animeTag.Tag is not null)
            .Select(animeTag => new Tag(animeTag, excludeDescriptions))
            .ToList();
    }

    /// <summary>
    /// Get all studios for an Anilist anime.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <returns></returns>
    [HttpGet("Anime/{animeID}/Studios")]
    public ActionResult<IReadOnlyList<Studio>> GetStudiosForAnilistAnimeByAnimeID([FromRoute] int animeID)
    {
        var anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is not null && _anilistMetadataService.WaitForAnimeUpdate(anime.AnilistAnimeID))
            anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is null)
            return NotFound(AnimeNotFound);

        return anime.Studios
            .Select(animeStudio => animeStudio.Studio)
            .WhereNotNull()
            .Select(studio => new Studio(studio))
            .ToList();
    }

    /// <summary>
    /// Get the cast for an Anilist anime.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <returns></returns>
    [HttpGet("Anime/{animeID}/Cast")]
    public ActionResult<IReadOnlyList<Role>> GetCastForAnilistAnimeByAnimeID([FromRoute] int animeID)
    {
        var anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is not null && _anilistMetadataService.WaitForAnimeUpdate(anime.AnilistAnimeID))
            anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is null)
            return NotFound(AnimeNotFound);

        return ((IWithCastAndCrew)anime).Cast
            .OfType<global::Shoko.Server.Models.Anilist.Embedded.Anilist_Cast>()
            .Select(Role.FromAnilist)
            .ToList();
    }

    /// <summary>
    /// Get the crew for an Anilist anime.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <returns></returns>
    [HttpGet("Anime/{animeID}/Crew")]
    public ActionResult<IReadOnlyList<Role>> GetCrewForAnilistAnimeByAnimeID([FromRoute] int animeID)
    {
        var anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is not null && _anilistMetadataService.WaitForAnimeUpdate(anime.AnilistAnimeID))
            anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is null)
            return NotFound(AnimeNotFound);

        return ((IWithCastAndCrew)anime).Crew
            .OfType<global::Shoko.Server.Models.Anilist.Embedded.Anilist_Crew>()
            .Select(Role.FromAnilist)
            .WhereNotNull()
            .ToList();
    }

    /// <summary>
    /// Remove the local copy of the metadata for an Anilist anime.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("Anime/{animeID}")]
    public async Task<ActionResult> RemoveAnilistAnimeByAnimeID([FromRoute] int animeID)
    {
        await _anilistMetadataService.SchedulePurgeOfAnime(animeID);

        return NoContent();
    }

    /// <summary>
    /// Get all cross-references for an Anilist anime.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <returns></returns>
    [HttpGet("Anime/{animeID}/CrossReferences")]
    public ActionResult<IReadOnlyList<AnilistAnime.CrossReference>> GetCrossReferencesForAnilistAnimeByAnimeID(
        [FromRoute] int animeID
    )
    {
        var anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is not null && _anilistMetadataService.WaitForAnimeUpdate(anime.AnilistAnimeID))
            anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is null)
            return NotFound(AnimeNotFound);

        return anime.CrossReferences
            .Select(xref => new AnilistAnime.CrossReference(xref))
            .OrderBy(xref => xref.AnidbAnimeID)
            .ToList();
    }

    /// <summary>
    /// Get all external resources for an Anilist anime. Covers the Anilist
    /// page itself, the MyAnimeList cross-reference, the trailer and every
    /// external link Anilist lists, such as the official site or a streaming
    /// service.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <returns></returns>
    [HttpGet("Anime/{animeID}/Resources")]
    public ActionResult<IReadOnlyList<Resource>> GetResourcesForAnilistAnimeByAnimeID(
        [FromRoute] int animeID
    )
    {
        var anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is not null && _anilistMetadataService.WaitForAnimeUpdate(anime.AnilistAnimeID))
            anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is null)
            return NotFound(AnimeNotFound);

        return anime.Resources
            .Select(resource => new Resource(resource))
            .ToList();
    }

    /// <summary>
    /// Get every day of the week the anime has aired an episode on.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <returns></returns>
    [HttpGet("Anime/{animeID}/DaysOfWeek")]
    public ActionResult<IReadOnlyList<string>> GetDaysOfWeekForAnilistAnimeByAnimeID(
        [FromRoute] int animeID
    )
    {
        var anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is not null && _anilistMetadataService.WaitForAnimeUpdate(anime.AnilistAnimeID))
            anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is null)
            return NotFound(AnimeNotFound);

        return anime.Episodes
            .Select(episode => episode.AiredAt?.DayOfWeek.ToString())
            .WhereNotNullOrDefault()
            .Distinct()
            .Order()
            .ToList();
    }

    [GeneratedRegex(@"^(?<qualified>[#eE])?(?<number>\d+)$", RegexOptions.Compiled)]
    private static partial Regex EpisodeNumberSearchRegex();

    /// <summary>
    /// Get all episodes for an Anilist anime.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <param name="include">Extra details to include.</param>
    /// <param name="search">Optional filter matched against episode numbers. A bare number is matched anywhere in the number, so "1" matches 1, 10, 21 and 213. An <c>E</c> or <c>#</c> prefix says the number is the episode, and matches only that one, so "E1" and "#01" both match episode 1 alone. Anything else matches nothing.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <returns></returns>
    [HttpGet("Anime/{animeID}/Episode")]
    public ActionResult<ListResult<AnilistEpisode>> GetEpisodesForAnilistAnimeByAnimeID(
        [FromRoute] int animeID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AnilistEpisode.IncludeDetails>? include = null,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] string? search = null
    )
    {
        var anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is not null && _anilistMetadataService.WaitForAnimeUpdate(anime.AnilistAnimeID))
            anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is null)
            return NotFound(AnimeNotFound);

        IEnumerable<Anilist_Episode> episodes = anime.Episodes;
        if (!string.IsNullOrWhiteSpace(search))
        {
            // AniList keeps no episode titles, so a number is all there is to
            // search by. A bare number is a loose filter over the digits, while
            // an "E" or "#" prefix says the number is the episode and nothing
            // else will do.
            if (EpisodeNumberSearchRegex().Match(search.Trim()) is not { Success: true } match)
                return new ListResult<AnilistEpisode>(0, []);

            var numberText = match.Groups["number"].Value;
            if (match.Groups["qualified"].Success)
            {
                // More digits than an episode number can hold matches nothing,
                // rather than throwing on the way to the comparison.
                if (!int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out var episodeNumber))
                    return new ListResult<AnilistEpisode>(0, []);

                episodes = episodes.Where(episode => episode.EpisodeNumber == episodeNumber);
            }
            else
            {
                episodes = episodes.Where(episode => episode.EpisodeNumber.ToString(CultureInfo.InvariantCulture).Contains(numberText, StringComparison.Ordinal));
            }
        }

        return episodes
            .OrderBy(episode => episode.EpisodeNumber)
            .ToListResult(episode => new AnilistEpisode(episode, include?.CombineFlags()), page, pageSize);
    }

    /// <summary>
    /// Get all episode cross-references for an Anilist anime.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <returns>The list of episode cross-references.</returns>
    [HttpGet("Anime/{animeID}/Episode/CrossReferences")]
    public ActionResult<ListResult<AnilistEpisode.CrossReference>> GetEpisodeCrossReferencesForAnilistAnimeByAnimeID(
        [FromRoute] int animeID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is not null && _anilistMetadataService.WaitForAnimeUpdate(anime.AnilistAnimeID))
            anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is null)
            return NotFound(AnimeNotFound);

        return anime.EpisodeCrossReferences
            .ToListResult(xref => new AnilistEpisode.CrossReference(xref), page, pageSize);
    }

    /// <summary>
    /// Shows all existing episode cross-references for an Anilist anime grouped
    /// by their corresponding cross-reference groups.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="page">The page index.</param>
    /// <returns>The list of grouped episode cross-references.</returns>
    [HttpGet("Anime/{animeID}/Episode/CrossReferences/EpisodeGroups")]
    public ActionResult<ListResult<List<AnilistEpisode.CrossReference>>> GetGroupedEpisodeCrossReferencesForAnilistAnimeByAnimeID(
        [FromRoute] int animeID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is not null && _anilistMetadataService.WaitForAnimeUpdate(anime.AnilistAnimeID))
            anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is null)
            return NotFound(AnimeNotFound);

        return anime.EpisodeCrossReferences
            .GroupByCrossReferenceType()
            .ToListResult(list => list.Select((xref, index) => new AnilistEpisode.CrossReference(xref, index)).ToList(), page, pageSize);
    }

    #endregion

    #region Cross-Source Linked Entries

    /// <summary>
    /// Get all AniDB anime linked to an Anilist anime.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <returns></returns>
    [HttpGet("Anime/{animeID}/AniDB/Anime")]
    public ActionResult<List<AnidbAnime>> GetAniDBAnimeByAnilistAnimeID(
        [FromRoute] int animeID
    )
    {
        var anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is not null && _anilistMetadataService.WaitForAnimeUpdate(anime.AnilistAnimeID))
            anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is null)
            return NotFound(AnimeNotFound);

        return anime.CrossReferences
            .Select(xref => _anidbAnime.GetByAnimeID(xref.AnidbAnimeID))
            .WhereNotNull()
            .Select(anidb => new AnidbAnime(anidb))
            .ToList();
    }

    /// <summary>
    /// Get all Shoko series linked to an Anilist anime.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <param name="randomImages">Randomize images shown for the <see cref="Series"/>.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSourceType"/>s.</param>
    /// <returns></returns>
    [HttpGet("Anime/{animeID}/Shoko/Series")]
    public ActionResult<List<Series>> GetShokoSeriesByAnilistAnimeID(
        [FromRoute] int animeID,
        [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSourceType>? includeDataFrom = null
    )
    {
        var anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is not null && _anilistMetadataService.WaitForAnimeUpdate(anime.AnilistAnimeID))
            anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is null)
            return NotFound(AnimeNotFound);

        return anime.CrossReferences
            .Select(xref => xref.AnimeSeries)
            .WhereNotNull()
            .Select(series => new Series(series, User.JMMUserID, randomImages, includeDataFrom))
            .ToList();
    }

    /// <summary>
    /// Get all files linked to an Anilist anime.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <param name="exclude">Exclude items of certain types</param>
    /// <param name="include_only">Filter to only include items of certain types</param>
    /// <param name="releaseProviders">Filter to only include files from certain release providers. Append <c>!</c> to the provider name to exclude the files</param>
    /// <param name="sortOrder">Sort ordering. Attach '-' at the start to reverse the order of the criteria.</param>
    /// <returns></returns>
    [HttpGet("Anime/{animeID}/Shoko/File")]
    public ActionResult<ListResult<File>> GetShokoFilesByAnilistAnimeID(
        [FromRoute] int animeID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[]? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileExcludeTypes[]? exclude = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileIncludeOnlyType[]? include_only = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? releaseProviders = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? sortOrder = null
    )
    {
        var anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is not null && _anilistMetadataService.WaitForAnimeUpdate(anime.AnilistAnimeID))
            anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is null)
            return NotFound(AnimeNotFound);

        var videoLocals = anime.EpisodeCrossReferences
            .Select(xref => xref.AnimeEpisode)
            .WhereNotNull()
            .SelectMany(shokoEpisode => shokoEpisode.VideoLocals)
            .DistinctBy(video => video.VideoLocalID);
        return ModelHelper.FilterFiles(videoLocals, User, pageSize, page, include, exclude, include_only, releaseProviders, sortOrder);
    }

    #endregion

    #region Actions

    /// <summary>
    /// Refresh or download the metadata for an Anilist anime.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <param name="body">Body containing options for refreshing or downloading metadata.</param>
    /// <returns>
    /// If <c>body.Immediate</c> is <see langword="true"/>, returns an <see cref="OkResult"/>,
    /// otherwise returns a <see cref="NoContentResult"/>.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("Anime/{animeID}/Action/Refresh")]
    public async Task<ActionResult> RefreshAnilistAnimeByAnimeID(
        [FromRoute] int animeID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] AnilistRefreshAnimeBody body
    )
    {
        // If we want quick results, we're already running an update, and we already have episodes to use, then
        // just return early. This is answered entirely from local state, so it must run before the pause check
        // below — it never touches AniList and shouldn't be refused just because AniList itself is unavailable.
        if (body.Immediate && body.QuickRefresh && _anilistMetadataService.IsAnimeUpdating(animeID) && _anilistEpisodes.GetByAnilistAnimeID(animeID).Count > 0)
            return Ok();

        if (body.SkipIfExists && _anilistAnime.GetByAnilistAnimeID(animeID) is not null)
            return Ok();

        // QuickRefresh is only meaningful for a synchronous, immediate caller waiting on the
        // result — a queued/background refresh always does the full job.
        var isQuickRefresh = body.Immediate && body.QuickRefresh;
        Action<UpdateAnilistAnimeJob> configure = j =>
        {
            j.AnilistAnimeID = animeID;
            j.ForceRefresh = !isQuickRefresh && body.Force;
            j.QuickRefresh = isQuickRefresh;
            j.DownloadImages = body.DownloadImages;
            j.DownloadCharactersAndStaff = body.DownloadCharactersAndStaff;
        };
        if (await TryQueueWhenPaused(configure, "Anime refresh", body.Immediate) is { } paused)
            return paused;

        if (body.Immediate)
        {
            await _jobFactory.Execute(configure);
            return Ok();
        }

        await _scheduler.StartJob(configure);
        return NoContent();
    }

    /// <summary>
    /// Register and download the images for an Anilist anime.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <param name="body">Body containing the options for downloading the images.</param>
    /// <returns>
    /// If <c>body.Immediate</c> is <see langword="true"/>, returns an <see cref="OkResult"/>,
    /// otherwise returns a <see cref="NoContentResult"/>.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("Anime/{animeID}/Action/DownloadImages")]
    public async Task<ActionResult> DownloadImagesForAnilistAnimeByAnimeID(
        [FromRoute] int animeID,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] AnilistDownloadImagesBody body
    )
    {
        var anime = _anilistAnime.GetByAnilistAnimeID(animeID);
        if (anime is null)
            return NotFound(AnimeNotFound);

        Action<DownloadAnilistAnimeImagesJob> configure = j =>
        {
            j.AnilistAnimeID = animeID;
            j.ForceDownload = body.Force;
        };
        if (await TryQueueWhenPaused(configure, "Anime image download", body.Immediate) is { } paused)
            return paused;

        if (body.Immediate)
        {
            await _jobFactory.Execute(configure);
            return Ok();
        }

        await _scheduler.StartJob(configure);
        return NoContent();
    }

    #endregion

    #region Online Search

    /// <summary>
    /// Search Anilist for anime.
    /// </summary>
    /// <param name="query">Query to search for.</param>
    /// <param name="includeRestricted">Include restricted (adult) anime.</param>
    /// <param name="year">Only include anime that started airing in this year.</param>
    /// <param name="season">Only include anime released in this season. Combine with <paramref name="seasonYear"/> to pin a specific yearly season.</param>
    /// <param name="seasonYear">Only include anime released in this season year. Unlike <paramref name="year"/> this matches AniList's own season year, which can differ from the start date for late-December premieres.</param>
    /// <param name="type">Only include anime of these types.</param>
    /// <param name="pageSize">The page size. Set to 0 to only grab the total.</param>
    /// <param name="page">The page index.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("Anime/Online/Search")]
    public async Task<ActionResult<ListResult<AnilistSearch.RemoteSearchAnime>>> SearchOnlineForAnilistAnime(
        [FromQuery] string query,
        [FromQuery] bool includeRestricted = false,
        [FromQuery, Range(0, int.MaxValue)] int year = 0,
        [FromQuery] YearlySeason? season = null,
        [FromQuery, Range(0, int.MaxValue)] int seasonYear = 0,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AnimeType>? type = null,
        [FromQuery, Range(0, 100)] int pageSize = 6,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        if (RefuseWhenPaused() is { } paused)
            return paused;

        var (results, total) = await _anilistSearchService.SearchAnime(new()
        {
            Query = query,
            IncludeRestricted = includeRestricted,
            Year = year > 0 ? year : null,
            Season = season,
            SeasonYear = seasonYear > 0 ? seasonYear : null,
            Types = type is { Count: > 0 } ? [.. type] : null,
            Page = page,
            PageSize = pageSize,
        });

        return new ListResult<AnilistSearch.RemoteSearchAnime>(total, results.Select(a => new AnilistSearch.RemoteSearchAnime(a)));
    }

    /// <summary>
    /// Get Anilist anime by ID.
    /// </summary>
    /// <param name="animeID">Anilist Anime ID.</param>
    /// <returns>
    /// If the anime is already in the database, returns the local copy.
    /// Otherwise, fetches from Anilist and returns the remote copy.
    /// If the anime is not found on Anilist, returns 404.
    /// </returns>
    [HttpGet("Anime/Online/{animeID}")]
    public async Task<ActionResult<AnilistSearch.RemoteSearchAnime>> GetAnilistAnimeOnlineByAnimeID(
        [FromRoute] int animeID
    )
    {
        if (_anilistAnime.GetByAnilistAnimeID(animeID) is { } localAnime)
            return new AnilistSearch.RemoteSearchAnime(localAnime);

        if (RefuseWhenPaused() is { } paused)
            return paused;

        if (await _anilistApiClient.GetAnimeByIdAsync(animeID) is not { } remoteAnime)
            return NotFound("Anime not found on Anilist.");

        return new AnilistSearch.RemoteSearchAnime(new AnilistAnimeSearchResult(remoteAnime));
    }

    /// <summary>
    /// Look up multiple Anilist anime by ID, using the local copy when
    /// available and fetching the rest from Anilist.
    /// </summary>
    /// <remarks>
    /// If any of the IDs are not found, a <see cref="ValidationProblemDetails"/> is returned.
    /// </remarks>
    /// <param name="body">Body containing the IDs to look up.</param>
    /// <returns>
    /// A list of <see cref="AnilistSearch.RemoteSearchAnime"/> containing the results.
    /// The order of the returned anime is determined by the order of the IDs in <paramref name="body"/>.
    /// </returns>
    [Authorize("admin")]
    [HttpPost("Anime/Online/Bulk")]
    public async Task<ActionResult<List<AnilistSearch.RemoteSearchAnime>>> SearchBulkForAnilistAnime(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] AnilistBulkSearchBody body
    )
    {
        // We don't care if the inputs are non-unique, but we don't want to double fetch,
        // so we do a distinct here, then at the end we map back to the original order.
        var uniqueIds = body.IDs.Distinct().ToList();
        var animeDict = uniqueIds
            .Select(id => id <= 0 ? null : _anilistAnime.GetByAnilistAnimeID(id))
            .WhereNotNull()
            .Select(anime => new AnilistSearch.RemoteSearchAnime(anime))
            .ToDictionary(anime => anime.ID);

        var remainingIds = uniqueIds.Except(animeDict.Keys).Where(id => id > 0).ToList();
        // Refuse before the first remote lookup instead of part-way through it, so a
        // pause never throws away the anime already fetched.
        if (remainingIds.Count > 0 && RefuseWhenPaused() is { } paused)
            return paused;

        foreach (var id in remainingIds)
        {
            if (await _anilistApiClient.GetAnimeByIdAsync(id) is not { } remoteAnime)
                continue;

            animeDict[id] = new AnilistSearch.RemoteSearchAnime(new AnilistAnimeSearchResult(remoteAnime));
        }

        var unknownAnime = uniqueIds.Except(animeDict.Keys).ToList();
        if (unknownAnime.Count > 0)
        {
            foreach (var id in unknownAnime)
                ModelState.AddModelError(nameof(body.IDs), $"Anime with id '{id}' not found.");

            return ValidationProblem(ModelState);
        }

        return body.IDs
            .Select(id => animeDict[id])
            .ToList();
    }

    #endregion

    #endregion

    #region Episodes

    #region Constants

    internal const string EpisodeNotFound = "An Anilist.Episode by the given `episodeID` was not found.";

    internal const string AnimeNotFoundByEpisodeID = "An Anilist.Anime by the given `episodeID` was not found.";

    #endregion

    #region Basics

    /// <summary>
    /// Get multiple local Anilist episodes at once.
    /// </summary>
    /// <param name="body">Body containing the IDs and details to include.</param>
    /// <returns></returns>
    [HttpPost("Episode/Bulk")]
    public ActionResult<List<AnilistEpisode>> BulkGetAnilistEpisodesByEpisodeIDs([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] AnilistBulkFetchBody<AnilistEpisode.IncludeDetails> body) =>
        body.IDs
            .Select(episodeID => episodeID <= 0 ? null : _anilistEpisodes.GetByAnilistEpisodeID(episodeID))
            .WhereNotNull()
            .GroupBy(episode => episode.AnilistAnimeID)
            .SelectMany(group =>
            {
                // Episode rows are synthesized during an anime refresh; if one is mid-update,
                // wait for it and re-read so the caller gets the finalized row.
                if (!_anilistMetadataService.WaitForAnimeUpdate(group.Key))
                    return group.AsEnumerable();

                return group
                    .Select(episode => _anilistEpisodes.GetByAnilistEpisodeID(episode.AnilistEpisodeID) ?? episode)
                    .ToList();
            })
            .Select(episode => new AnilistEpisode(episode, body.Include?.CombineFlags()))
            .ToList();

    /// <summary>
    /// Get the local metadata for an Anilist episode.
    /// </summary>
    /// <param name="episodeID">Anilist Episode ID.</param>
    /// <param name="include">Extra details to include.</param>
    /// <returns></returns>
    [HttpGet("Episode/{episodeID}")]
    public ActionResult<AnilistEpisode> GetAnilistEpisodeByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AnilistEpisode.IncludeDetails>? include = null
    )
    {
        if (GetEpisodeWaitingForUpdate(episodeID) is not { } episode)
            return NotFound(EpisodeNotFound);

        return new AnilistEpisode(episode, include?.CombineFlags());
    }

    /// <summary>
    /// Get all cross-references for an Anilist episode.
    /// </summary>
    /// <param name="episodeID">Anilist Episode ID.</param>
    /// <returns></returns>
    [HttpGet("Episode/{episodeID}/CrossReferences")]
    public ActionResult<IReadOnlyList<AnilistEpisode.CrossReference>> GetCrossReferencesForAnilistEpisodeByEpisodeID(
        [FromRoute] int episodeID
    )
    {
        if (GetEpisodeWaitingForUpdate(episodeID) is not { } episode)
            return NotFound(EpisodeNotFound);

        return episode.CrossReferences
            .Select(xref => new AnilistEpisode.CrossReference(xref))
            .OrderBy(xref => xref.AnidbEpisodeID)
            .ToList();
    }

    /// <summary>
    /// Get all file cross-references for an Anilist episode.
    /// </summary>
    /// <param name="episodeID">Anilist Episode ID.</param>
    /// <returns></returns>
    [HttpGet("Episode/{episodeID}/FileCrossReferences")]
    public ActionResult<IReadOnlyList<FileCrossReference>> GetFileCrossReferencesForAnilistEpisodeByEpisodeID(
        [FromRoute] int episodeID
    )
    {
        if (GetEpisodeWaitingForUpdate(episodeID) is not { } episode)
            return NotFound(EpisodeNotFound);

        return FileCrossReference.From(episode.FileCrossReferences);
    }

    #endregion

    #region Same-Source Linked Entries

    /// <summary>
    /// Get the Anilist anime the episode belongs to.
    /// </summary>
    /// <param name="episodeID">Anilist Episode ID.</param>
    /// <param name="include">Extra details to include.</param>
    /// <returns></returns>
    [HttpGet("Episode/{episodeID}/Anime")]
    public ActionResult<AnilistAnime> GetAnilistAnimeByEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AnilistAnime.IncludeDetails>? include = null
    )
    {
        if (GetEpisodeWaitingForUpdate(episodeID) is not { } episode)
            return NotFound(EpisodeNotFound);

        if (_anilistAnime.GetByAnilistAnimeID(episode.AnilistAnimeID) is not { } anime)
            return NotFound(AnimeNotFoundByEpisodeID);

        return new AnilistAnime(anime, include?.CombineFlags());
    }

    #endregion

    #region Cross-Source Linked Entries

    /// <summary>
    /// Get all AniDB anime linked to an Anilist episode.
    /// </summary>
    /// <param name="episodeID">Anilist Episode ID.</param>
    /// <returns></returns>
    [HttpGet("Episode/{episodeID}/AniDB/Anime")]
    public ActionResult<List<AnidbAnime>> GetAniDBAnimeByAnilistEpisodeID(
        [FromRoute] int episodeID
    )
    {
        if (GetEpisodeWaitingForUpdate(episodeID) is not { } episode)
            return NotFound(EpisodeNotFound);

        return episode.CrossReferences
            .DistinctBy(xref => xref.AnidbAnimeID)
            .Select(xref => _anidbAnime.GetByAnimeID(xref.AnidbAnimeID))
            .WhereNotNull()
            .Select(anime => new AnidbAnime(anime))
            .ToList();
    }

    /// <summary>
    /// Get all AniDB episodes linked to an Anilist episode.
    /// </summary>
    /// <param name="episodeID">Anilist Episode ID.</param>
    /// <returns></returns>
    [HttpGet("Episode/{episodeID}/AniDB/Episode")]
    public ActionResult<List<AnidbEpisode>> GetAniDBEpisodesByAnilistEpisodeID(
        [FromRoute] int episodeID
    )
    {
        if (GetEpisodeWaitingForUpdate(episodeID) is not { } episode)
            return NotFound(EpisodeNotFound);

        return episode.CrossReferences
            .DistinctBy(xref => xref.AnidbEpisodeID)
            .Select(xref => xref.AnidbEpisode)
            .WhereNotNull()
            .Select(anidbEpisode => new AnidbEpisode(anidbEpisode))
            .ToList();
    }

    /// <summary>
    /// Get all Shoko series linked to an Anilist episode.
    /// </summary>
    /// <param name="episodeID">Anilist Episode ID.</param>
    /// <param name="randomImages">Randomize images shown for the <see cref="Series"/>.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSourceType"/>s.</param>
    /// <returns></returns>
    [HttpGet("Episode/{episodeID}/Shoko/Series")]
    public ActionResult<List<Series>> GetShokoSeriesByAnilistEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery] bool randomImages = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSourceType>? includeDataFrom = null
    )
    {
        if (GetEpisodeWaitingForUpdate(episodeID) is not { } episode)
            return NotFound(EpisodeNotFound);

        return episode.CrossReferences
            .DistinctBy(xref => xref.AnidbAnimeID)
            .Select(xref => xref.AnimeSeries)
            .WhereNotNull()
            .Select(series => new Series(series, User.JMMUserID, randomImages, includeDataFrom))
            .ToList();
    }

    /// <summary>
    /// Get all Shoko episodes linked to an Anilist episode.
    /// </summary>
    /// <param name="episodeID">Anilist Episode ID.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSourceType"/>s.</param>
    /// <returns></returns>
    [HttpGet("Episode/{episodeID}/Shoko/Episode")]
    public ActionResult<List<Episode>> GetShokoEpisodesByAnilistEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSourceType>? includeDataFrom = null
    )
    {
        if (GetEpisodeWaitingForUpdate(episodeID) is not { } episode)
            return NotFound(EpisodeNotFound);

        return episode.CrossReferences
            .DistinctBy(xref => xref.AnidbEpisodeID)
            .Select(xref => xref.AnimeEpisode)
            .WhereNotNull()
            .Select(shokoEpisode => new Episode(HttpContext, shokoEpisode, includeDataFrom))
            .ToList();
    }

    /// <summary>
    /// Get all files linked to an Anilist episode.
    /// </summary>
    /// <param name="episodeID">Anilist Episode ID.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="include">Include items that are not included by default</param>
    /// <param name="exclude">Exclude items of certain types</param>
    /// <param name="include_only">Filter to only include items of certain types</param>
    /// <param name="releaseProviders">Filter to only include files from certain release providers. Append <c>!</c> to the provider name to exclude the files</param>
    /// <param name="sortOrder">Sort ordering. Attach '-' at the start to reverse the order of the criteria.</param>
    /// <returns></returns>
    [HttpGet("Episode/{episodeID}/Shoko/File")]
    public ActionResult<ListResult<File>> GetShokoFilesByAnilistEpisodeID(
        [FromRoute] int episodeID,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileNonDefaultIncludeType[]? include = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileExcludeTypes[]? exclude = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] FileIncludeOnlyType[]? include_only = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? releaseProviders = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string>? sortOrder = null
    )
    {
        if (GetEpisodeWaitingForUpdate(episodeID) is not { } episode)
            return NotFound(EpisodeNotFound);

        var videoLocals = episode.CrossReferences
            .Select(xref => xref.AnimeEpisode)
            .WhereNotNull()
            .SelectMany(shokoEpisode => shokoEpisode.VideoLocals)
            .DistinctBy(video => video.VideoLocalID);
        return ModelHelper.FilterFiles(videoLocals, User, pageSize, page, include, exclude, include_only, releaseProviders, sortOrder);
    }

    #endregion

    #endregion

    #region Tags

    #region Constants

    internal const string TagNotFound = "An Anilist.Tag by the given `tagID` was not found.";

    #endregion

    /// <summary>
    /// Get the local metadata for an Anilist tag.
    /// </summary>
    /// <param name="tagID">Anilist Tag ID.</param>
    /// <param name="excludeDescription">Exclude the tag description.</param>
    /// <param name="includeCount">Include the number of local anime the tag is set on.</param>
    /// <returns></returns>
    [HttpGet("Tag/{tagID}")]
    public ActionResult<Tag> GetAnilistTagByTagID(
        [FromRoute] int tagID,
        [FromQuery] bool excludeDescription = false,
        [FromQuery] bool includeCount = false
    )
    {
        var tag = _anilistTags.GetByAnilistTagID(tagID);
        if (tag is null)
            return NotFound(TagNotFound);

        return new Tag(tag, excludeDescription, includeCount ? _anilistAnimeTags.GetByAnilistTagID(tag.AnilistTagID).Count : null);
    }

    /// <summary>
    /// List the local Anilist anime an Anilist tag is set on.
    /// </summary>
    /// <param name="tagID">Anilist Tag ID.</param>
    /// <param name="include"></param>
    /// <param name="restricted"></param>
    /// <param name="pageSize"></param>
    /// <param name="page"></param>
    /// <returns></returns>
    [HttpGet("Tag/{tagID}/Anime")]
    public ActionResult<ListResult<AnilistAnime>> GetAnilistAnimeByTagID(
        [FromRoute] int tagID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<AnilistAnime.IncludeDetails>? include = null,
        [FromQuery] IncludeOnlyFilter restricted = IncludeOnlyFilter.True,
        [FromQuery, Range(0, 1000)] int pageSize = 50,
        [FromQuery, Range(1, int.MaxValue)] int page = 1
    )
    {
        var tag = _anilistTags.GetByAnilistTagID(tagID);
        if (tag is null)
            return NotFound(TagNotFound);

        return _anilistAnimeTags.GetByAnilistTagID(tag.AnilistTagID)
            .Select(animeTag => _anilistAnime.GetByAnilistAnimeID(animeTag.AnilistAnimeID))
            .WhereNotNull()
            .Where(anime => restricted is IncludeOnlyFilter.True || anime.IsRestricted == (restricted is IncludeOnlyFilter.Only))
            .OrderBy(anime => anime.PreferredTitle)
            .ThenBy(anime => anime.AnilistAnimeID)
            .ToListResult(anime => new AnilistAnime(anime, include?.CombineFlags()), page, pageSize);
    }

    #endregion

    #region Export/Import

    private static string MapAnimeType(AnimeType? type) =>
        type?.ToString() ?? "Unknown";

    /// <summary>
    /// Export a cross-reference CSV file with Anilist cross-references.
    /// </summary>
    /// <param name="body">Body containing the export options.</param>
    /// <returns>A CSV file with the cross-references.</returns>
    [Authorize("admin")]
    [HttpPost("Export")]
    public ActionResult ExportAnimeCrossReferences(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] AnilistExportBody? body
    )
    {
        body ??= new AnilistExportBody();
        var sections = body.SectionSet is { Count: > 0 }
            ? body.SectionSet.Aggregate(default(AnilistCrossReferenceExportType), (a, b) => a | b)
            : AnilistCrossReferenceExportType.Anime | AnilistCrossReferenceExportType.Episode;

        var stringBuilder = new StringBuilder();

        if (sections.HasFlag(AnilistCrossReferenceExportType.Anime))
        {
            var animeCrossReferences = _crossRefAnidbAnilistAnime.GetAll()
                .Where(xref =>
                {
                    if (xref.AnilistAnimeID is 0)
                        return false;

                    if (body.Automatic != IncludeOnlyFilter.True)
                    {
                        var includeAutomatic = body.Automatic == IncludeOnlyFilter.Only;
                        var isAutomatic = xref.MatchRating != MatchRating.UserVerified;
                        if (isAutomatic != includeAutomatic)
                            return false;
                    }
                    if (body.WithEpisodes != IncludeOnlyFilter.True)
                    {
                        var includeWithEpisode = body.WithEpisodes == IncludeOnlyFilter.Only;
                        var hasEpisode = _crossRefAnidbAnilistEpisodes.GetOnlyByAnidbAnimeAndAnilistAnimeIDs(xref.AnidbAnimeID, xref.AnilistAnimeID).Any(xref => xref.AnilistEpisodeID is > 0);
                        if (hasEpisode != includeWithEpisode)
                            return false;
                    }
                    return body.ShouldKeep(xref);
                })
                .OrderBy(xref => xref.AnidbAnimeID)
                .ThenBy(xref => xref.AnilistAnimeID)
                .SelectMany(xref =>
                {
                    var rating = xref.MatchRating.ToString();
                    var entry = $"{xref.AnidbAnimeID},{xref.AnilistAnimeID},{rating}";
                    if (!body.IncludeComments)
                        return new string[1] { entry };

                    var anidbAnime = _anidbAnime.GetByAnimeID(xref.AnidbAnimeID);
                    var anidbAnimeTitle = anidbAnime?.MainTitle ?? "<missing title>";
                    var anilistAnimeTitle = xref.AnilistAnime?.PreferredTitle ?? "<missing title>";
                    return
                    [
                        "",
                        $"# AniDB: {MapAnimeType(anidbAnime?.AnimeType)} ``{anidbAnimeTitle}`` (a{xref.AnidbAnimeID}) → Anilist: ``{anilistAnimeTitle}`` (al{xref.AnilistAnimeID})",
                        entry,
                    ];
                })
                .ToList();
            if (animeCrossReferences.Count > 0)
            {
                if (body.IncludeComments)
                    stringBuilder.AppendLine("#".PadRight(AnimeCrossReferenceWithIdHeader.Length, '-'))
                        .AppendLine("# AniDB/Anilist Anime Cross-References");
                stringBuilder.AppendLine(AnimeCrossReferenceWithIdHeader);
                if (body.IncludeComments)
                    stringBuilder.AppendLine("#".PadRight(AnimeCrossReferenceWithIdHeader.Length, '-'))
                        .AppendLine();
                foreach (var line in animeCrossReferences)
                    stringBuilder.AppendLine(line);
            }
        }

        if (body.IncludeComments && sections.HasFlag(AnilistCrossReferenceExportType.Anime) && sections.HasFlag(AnilistCrossReferenceExportType.Episode))
            stringBuilder
                .AppendLine()
                .AppendLine();

        if (sections.HasFlag(AnilistCrossReferenceExportType.Episode))
        {
            var episodeCrossReferences = _crossRefAnidbAnilistEpisodes.GetAll()
                .Where(xref =>
                {
                    if (body.Automatic != IncludeOnlyFilter.True)
                    {
                        var includeAutomatic = body.Automatic == IncludeOnlyFilter.Only;
                        var isAutomatic = xref.MatchRating != MatchRating.UserVerified;
                        if (isAutomatic != includeAutomatic)
                            return false;
                    }
                    if (body.WithEpisodes != IncludeOnlyFilter.True)
                    {
                        var includeWithEpisode = body.WithEpisodes == IncludeOnlyFilter.Only;
                        var hasEpisode = xref.AnilistEpisodeID is > 0;
                        if (hasEpisode != includeWithEpisode)
                            return false;
                    }
                    return body.ShouldKeep(xref);
                })
                .OrderBy(xref => xref.AnidbAnimeID)
                .ThenBy(xref => xref.AnidbEpisodeID)
                .ThenBy(xref => xref.Ordering)
                .SelectMany(xref =>
                {
                    var rating = xref.MatchRating.ToString();
                    var entry = $"{xref.AnidbAnimeID},{xref.AnidbEpisodeID},{xref.AnilistAnimeID},{xref.AnilistEpisodeID},{rating}";
                    if (!body.IncludeComments)
                        return new string[1] { entry };

                    var anidbAnime = _anidbAnime.GetByAnimeID(xref.AnidbAnimeID);
                    var anidbAnimeTitle = anidbAnime?.MainTitle ?? "<missing title>";
                    var anidbEpisode = _anidbEpisodes.GetByEpisodeID(xref.AnidbEpisodeID);
                    var anidbEpisodeNumber = "???";
                    if (anidbEpisode is not null)
                        if (anidbEpisode.EpisodeType is EpisodeType.Episode)
                            anidbEpisodeNumber = anidbEpisode.EpisodeNumber.ToString().PadLeft(3, '0');
                        else
                            anidbEpisodeNumber = $"{anidbEpisode.EpisodeType.ToString()[0]}{anidbEpisode.EpisodeNumber.ToString().PadLeft(2, '0')}";
                    var anidbEpisodeTitle = anidbEpisode?.DefaultTitle is { } defaultTile ? defaultTile.Value : "<missing title>";
                    var anilistAnimeTitle = xref.AnilistAnime?.PreferredTitle ?? "<missing title>";
                    return
                    [
                        "",
                        $"# AniDB: {MapAnimeType(anidbAnime?.AnimeType)} ``{anidbAnimeTitle}`` (a{xref.AnidbAnimeID}) {anidbEpisodeNumber} ``{anidbEpisodeTitle}`` (e{xref.AnidbEpisodeID}) → Anilist: ``{anilistAnimeTitle}`` (al{xref.AnilistAnimeID}) E{xref.EpisodeNumber.ToString().PadLeft(3, '0')} (ale{xref.AnilistEpisodeID})",
                        entry,
                    ];
                })
                .ToList();
            if (episodeCrossReferences.Count > 0)
            {
                if (body.IncludeComments)
                    stringBuilder.AppendLine("#".PadRight(EpisodeCrossReferenceWithIdHeader.Length, '-'))
                        .AppendLine("# AniDB/Anilist Episode Cross-References");
                stringBuilder.AppendLine(EpisodeCrossReferenceWithIdHeader);
                if (body.IncludeComments)
                    stringBuilder.AppendLine("#".PadRight(EpisodeCrossReferenceWithIdHeader.Length, '-'))
                        .AppendLine();
                foreach (var line in episodeCrossReferences)
                    stringBuilder.AppendLine(line);
            }
        }

        var bytes = Encoding.UTF8.GetBytes(stringBuilder.ToString());
        return File(bytes, "text/csv", "anidb_anilist_xrefs.csv");
    }

    /// <summary>
    /// Import a cross-reference CSV file in the same format we export.
    /// </summary>
    /// <remarks>
    /// This will take care of creating/updating all cross-reference entries
    /// for everything we can export, be it anime cross-references, episode
    /// cross-references, or anything we might add in the future. If we can
    /// export it then we can import it!
    /// </remarks>
    /// <param name="file">The CSV file to import.</param>
    /// <param name="removeExisting">Remove existing cross-references for the same AniDB anime/episodes.</param>
    /// <param name="addMissingAnime">Add missing anime from Anilist.</param>
    /// <returns>Void.</returns>
    [Authorize("admin")]
    [HttpPost("Import")]
    public async Task<ActionResult> ImportAnimeCrossReferences(
        IFormFile file,
        [FromQuery] bool removeExisting = true,
        [FromQuery] bool addMissingAnime = true
    )
    {
        if (file is null || file.Length == 0)
            ModelState.AddModelError("Body", "Body cannot be empty.");

        if (file is not null && file.Name != "file")
            ModelState.AddModelError("Body", "Invalid field name for import file");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        using var stream = new StreamReader(file!.OpenReadStream(), Encoding.UTF8, true);

        string? line;
        var lineNumber = 0;
        var currentHeader = "";
        var animeIdXrefs = new List<(int anidbAnime, int anilistAnime, MatchRating rating)>();
        var episodeIdXrefs = new List<(int anidbAnime, int anidbEpisode, int anilistAnime, int anilistEpisode, int episodeNumber, MatchRating rating)>();
        while ((line = stream.ReadLine()) is not null)
        {
            lineNumber++;
            if (line.Length == 0 || line[0] == '#')
                continue;

            switch (line)
            {
                case AnimeCrossReferenceWithIdHeader:
                case EpisodeCrossReferenceWithIdHeader:
                    currentHeader = line;
                    continue;
            }

            if (string.IsNullOrEmpty(currentHeader))
            {
                ModelState.AddModelError("Body", "Invalid or missing CSV header for import file.");
                break;
            }

            switch (currentHeader)
            {
                default:
                case "":
                    ModelState.AddModelError("Body", $"Unable to parse unknown cross-reference at line {lineNumber}.");
                    break;

                case AnimeCrossReferenceWithIdHeader:
                {
                    var (animeId, anilistId, rating) = line.Split(",");
                    if (
                        !int.TryParse(animeId, out var anidbAnimeId) || anidbAnimeId <= 0 ||
                        !int.TryParse(anilistId, out var anilistAnimeId) || anilistAnimeId <= 0 ||
                        !Enum.TryParse<MatchRating>(rating, true, out var matchRating)
                    )
                    {
                        ModelState.AddModelError("Body", $"Unable to parse anime cross-reference at line {lineNumber}.");
                        continue;
                    }

                    animeIdXrefs.Add((anidbAnimeId, anilistAnimeId, matchRating));
                    break;
                }
                case EpisodeCrossReferenceWithIdHeader:
                {
                    var (anime, anidbEpisode, anilistAnime, anilistEpisode, rating) = line.Split(",");
                    if (
                        !int.TryParse(anime, out var anidbAnimeId) || anidbAnimeId <= 0 ||
                        !int.TryParse(anidbEpisode, out var anidbEpisodeId) || anidbEpisodeId <= 0 ||
                        !int.TryParse(anilistAnime, out var anilistAnimeId) || anilistAnimeId < 0 ||
                        !int.TryParse(anilistEpisode, out var anilistEpisodeId) || anilistEpisodeId < 0 ||
                        !Enum.TryParse<MatchRating>(rating, true, out var matchRating)
                    )
                    {
                        ModelState.AddModelError("Body", $"Unable to parse episode cross-reference at line {lineNumber}.");
                        continue;
                    }

                    // The episode number is packed into the episode id, so it doesn't need its own column.
                    var episodeNumber = anilistEpisodeId is 0 ? 0 : AnilistUtility.UnpackEpisodeID(anilistEpisodeId).EpisodeNumber;
                    episodeIdXrefs.Add((anidbAnimeId, anidbEpisodeId, anilistAnimeId, anilistEpisodeId, episodeNumber, matchRating));
                    break;
                }
            }
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (animeIdXrefs.Count == 0 && episodeIdXrefs.Count == 0)
            ModelState.AddModelError("Body", "File contained no lines to import.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Process anime cross-references
        var animeToPull = new HashSet<int>();
        var existingAnimeXrefs = _crossRefAnidbAnilistAnime.GetAll()
            .GroupBy(xref => xref.AnidbAnimeID)
            .ToDictionary(groupBy => groupBy.Key, groupBy => groupBy.ToDictionary(xref => xref.AnilistAnimeID));
        var animeXrefsToSave = new List<CrossRef_AniDB_Anilist_Anime>();
        var animeXrefsToRemove = new List<CrossRef_AniDB_Anilist_Anime>();

        foreach (var (anidbAnimeId, anilistAnimeId, matchRating) in animeIdXrefs)
        {
            CrossRef_AniDB_Anilist_Anime? xref = null;
            if (existingAnimeXrefs.TryGetValue(anidbAnimeId, out var xrefDict))
            {
                xrefDict.TryGetValue(anilistAnimeId, out xref);
                if (removeExisting)
                {
                    foreach (var existing in xrefDict.Values.Where(x => x.AnilistAnimeID != anilistAnimeId))
                        animeXrefsToRemove.Add(existing);
                }
            }

            if (xref is null)
            {
                xref = new CrossRef_AniDB_Anilist_Anime(anidbAnimeId, anilistAnimeId, matchRating);
                animeXrefsToSave.Add(xref);
            }
            else if (xref.MatchRating != matchRating)
            {
                xref.MatchRating = matchRating;
                animeXrefsToSave.Add(xref);
            }

            if (addMissingAnime)
            {
                var seriesExists = _animeSeries.GetByAnimeID(anidbAnimeId) is not null;
                var anilistAnimeExists = _anilistAnime.GetByAnilistAnimeID(anilistAnimeId) is not null;
                if (seriesExists && !anilistAnimeExists)
                    animeToPull.Add(anilistAnimeId);
            }
        }

        // Process episode cross-references
        var existingEpisodeXrefs = _crossRefAnidbAnilistEpisodes.GetAll()
            .GroupBy(xref => $"{xref.AnidbAnimeID}:{xref.AnidbEpisodeID}")
            .ToDictionary(groupBy => groupBy.Key, groupBy => groupBy.ToList());
        var episodeXrefsToSave = new List<CrossRef_AniDB_Anilist_Episode>();
        var episodeXrefsToRemove = new List<CrossRef_AniDB_Anilist_Episode>();

        foreach (var (anidbAnimeId, anidbEpisodeId, anilistAnimeId, anilistEpisodeId, episodeNumber, matchRating) in episodeIdXrefs)
        {
            var key = $"{anidbAnimeId}:{anidbEpisodeId}";
            CrossRef_AniDB_Anilist_Episode? xref = null;

            if (existingEpisodeXrefs.TryGetValue(key, out var existingList))
            {
                xref = existingList.FirstOrDefault(x => x.AnilistAnimeID == anilistAnimeId && x.AnilistEpisodeID == anilistEpisodeId);
                if (removeExisting)
                {
                    foreach (var existing in existingList.Where(x => x != xref))
                        episodeXrefsToRemove.Add(existing);
                }
            }

            if (xref is null)
            {
                xref = new CrossRef_AniDB_Anilist_Episode(anidbAnimeId, anidbEpisodeId, anilistAnimeId, anilistEpisodeId, episodeNumber, matchRating);
                episodeXrefsToSave.Add(xref);
            }
            else
            {
                var updated = false;
                if (xref.EpisodeNumber != episodeNumber)
                {
                    xref.EpisodeNumber = episodeNumber;
                    updated = true;
                }
                if (xref.MatchRating != matchRating)
                {
                    xref.MatchRating = matchRating;
                    updated = true;
                }
                if (updated)
                    episodeXrefsToSave.Add(xref);
            }

            if (addMissingAnime && anilistAnimeId > 0)
            {
                var seriesExists = _animeSeries.GetByAnimeID(anidbAnimeId) is not null;
                var anilistAnimeExists = _anilistAnime.GetByAnilistAnimeID(anilistAnimeId) is not null;
                if (seriesExists && !anilistAnimeExists)
                    animeToPull.Add(anilistAnimeId);
            }
        }

        // Save changes
        if (animeXrefsToSave.Count > 0 || animeXrefsToRemove.Count > 0)
        {
            _logger.LogDebug(
                "Imported {SavedCount} anime cross-references, removed {RemovedCount} existing anime cross-references.",
                animeXrefsToSave.Count,
                animeXrefsToRemove.Count
            );

            if (animeXrefsToSave.Count > 0)
                _crossRefAnidbAnilistAnime.Save(animeXrefsToSave);
            if (animeXrefsToRemove.Count > 0)
                _crossRefAnidbAnilistAnime.Delete(animeXrefsToRemove);
        }

        if (episodeXrefsToSave.Count > 0 || episodeXrefsToRemove.Count > 0)
        {
            _logger.LogDebug(
                "Imported {SavedCount} episode cross-references, removed {RemovedCount} existing episode cross-references.",
                episodeXrefsToSave.Count,
                episodeXrefsToRemove.Count
            );

            if (episodeXrefsToSave.Count > 0)
                _crossRefAnidbAnilistEpisodes.Save(episodeXrefsToSave);
            if (episodeXrefsToRemove.Count > 0)
                _crossRefAnidbAnilistEpisodes.Delete(episodeXrefsToRemove);
        }

        // Schedule anime updates
        foreach (var animeId in animeToPull)
            await _anilistMetadataService.ScheduleUpdateOfAnime(new() { AnimeId = animeId, DownloadImages = true });

        return NoContent();
    }

    #endregion
}
