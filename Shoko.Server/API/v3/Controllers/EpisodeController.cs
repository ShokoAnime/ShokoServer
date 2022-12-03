using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Commons.Extensions;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

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

    /// <summary>
    /// Get the <see cref="Episode"/> entry for the given <paramref name="episodeID"/>.
    /// </summary>
    /// <param name="episodeID">Shoko ID</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("{episodeID}")]
    public ActionResult<Episode> GetEpisodeByEpisodeID([FromRoute] int episodeID,
        [FromQuery] HashSet<DataSource> includeDataFrom = null)
    {
        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
        {
            return NotFound(EpisodeNotFoundWithEpisodeID);
        }

        return new Episode(HttpContext, episode, includeDataFrom);
    }

    /// <summary>
    /// Get the <see cref="Episode.AniDB"/> entry for the given <paramref name="episodeID"/>.
    /// </summary>
    /// <param name="episodeID">Shoko ID</param>
    /// <returns></returns>
    [HttpGet("{episodeID}/AniDB")]
    public ActionResult<Episode.AniDB> GetEpisodeAnidbByEpisodeID([FromRoute] int episodeID)
    {
        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
        {
            return NotFound(EpisodeNotFoundWithEpisodeID);
        }

        var anidb = episode.AniDB_Episode;
        if (anidb == null)
        {
            return InternalError(AnidbNotFoundForEpisodeID);
        }

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
        {
            return NotFound(AnidbNotFoundForAnidbEpisodeID);
        }

        return new Episode.AniDB(anidb);
    }

    /// <summary>
    /// Get the <see cref="Episode"/> entry for the given <paramref name="anidbEpisodeID"/>, if any.
    /// </summary>
    /// <param name="anidbEpisodeID">AniDB Episode ID</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("AniDB/{anidbEpisodeID}/Episode")]
    public ActionResult<Episode> GetEpisode([FromRoute] int anidbEpisodeID,
        [FromQuery] HashSet<DataSource> includeDataFrom = null)
    {
        var anidb = RepoFactory.AniDB_Episode.GetByEpisodeID(anidbEpisodeID);
        if (anidb == null)
        {
            return NotFound(AnidbNotFoundForAnidbEpisodeID);
        }

        var episode = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(anidb.EpisodeID);
        if (episode == null)
        {
            return NotFound(EpisodeNotFoundForAnidbEpisodeID);
        }

        return new Episode(HttpContext, episode);
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
        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
        {
            return NotFound(EpisodeNotFoundWithEpisodeID);
        }

        if (vote.Value < 0)
        {
            return BadRequest("Value must be greater than or equal to 0.");
        }

        if (vote.Value > vote.MaxValue)
        {
            return BadRequest($"Value must be less than or equal to the set max value ({vote.MaxValue}).");
        }

        if (vote.MaxValue <= 0)
        {
            return BadRequest("Max value must be an integer above 0.");
        }

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
        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
        {
            return NotFound(EpisodeNotFoundWithEpisodeID);
        }

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
        var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
        if (episode == null)
        {
            return NotFound(EpisodeNotFoundWithEpisodeID);
        }

        episode.ToggleWatchedStatus(watched, true, DateTime.Now, true, User.JMMUserID, true);

        return Ok();
    }

    /// <summary>
    /// Get episodes with multiple files attached.
    /// </summary>
    /// <param name="ignoreVariations">Ignore manually toggled variations in the results.</param>
    /// <param name="onlyFinishedSeries">Only show finished series.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [HttpGet("WithMultipleFiles")]
    public ActionResult<ListResult<Episode>> GetSoftDuplicatesForEpisode([FromQuery] bool ignoreVariations = true,
        [FromQuery] bool onlyFinishedSeries = false, [FromQuery] [Range(0, 1000)] int pageSize = 100,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1)
    {
        IEnumerable<SVR_AnimeEpisode> enumerable =
            RepoFactory.AnimeEpisode.GetEpisodesWithMultipleFiles(ignoreVariations);
        if (onlyFinishedSeries)
        {
            var dictSeriesFinishedAiring = RepoFactory.AnimeSeries.GetAll()
                .ToDictionary(a => a.AnimeSeriesID, a => a.GetAnime().GetFinishedAiring());
            enumerable = enumerable.Where(episode =>
                dictSeriesFinishedAiring.TryGetValue(episode.AnimeSeriesID, out var finishedAiring) && finishedAiring);
        }

        return enumerable
            .ToListResult(episode => new Episode(HttpContext, episode), page, pageSize);
    }

    /// <summary>
    /// Get all episodes with no files.
    /// </summary>
    /// <param name="includeSpecials">Include specials in the list.</param>
    /// <param name="onlyFinishedSeries">Only show episodes for completed series.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [HttpGet("WithNoFiles")]
    public ActionResult<ListResult<Episode>> GetMissingEpisodes([FromQuery] bool includeSpecials = false,
        [FromQuery] bool onlyFinishedSeries = false, [FromQuery] [Range(0, 1000)] int pageSize = 100,
        [FromQuery] [Range(1, int.MaxValue)] int page = 1)
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
            .ToListResult(episode => new Episode(HttpContext, episode), page, pageSize);
    }
}
