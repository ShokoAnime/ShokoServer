using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

[ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3, Authorize]
public class PlaylistController : BaseController
{
    private readonly GeneratedPlaylistService _playlistService;

    private readonly AnimeSeriesRepository _seriesRepository;

    private readonly AnimeEpisodeRepository _episodeRepository;

    private readonly VideoLocalRepository _videoRepository;

    public PlaylistController(ISettingsProvider settingsProvider, GeneratedPlaylistService playlistService, AnimeSeriesRepository animeSeriesRepository, AnimeEpisodeRepository animeEpisodeRepository, VideoLocalRepository videoRepository) : base(settingsProvider)
    {
        _playlistService = playlistService;
        _seriesRepository = animeSeriesRepository;
        _episodeRepository = animeEpisodeRepository;
        _videoRepository = videoRepository;
    }

    /// <summary>
    /// Generate an on-demand playlist for the specified list of items.
    /// </summary>
    /// <param name="items">The list of item IDs to include in the playlist. If no prefix is provided for an id then it will be assumed to be a series id.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="includeReleaseInfo">Include release info data.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSourceType"/>s.</param>
    /// <returns></returns>
    [HttpGet("Generate")]
    public ActionResult<IReadOnlyList<PlaylistItem>> GetGeneratedPlaylistJson(
        [FromQuery(Name = "playlist"), ModelBinder(typeof(CommaDelimitedModelBinder))] string[]? items = null,
        [FromQuery] bool includeMediaInfo = false,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] bool includeReleaseInfo = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSourceType>? includeDataFrom = null
    )
    {
        if (!_playlistService.TryParsePlaylist(items ?? [], out var playlist, ModelState))
            return ValidationProblem(ModelState);

        return playlist
            .Select(tuple => new PlaylistItem(
                tuple.episodes
                    .Select(episode => new Episode(HttpContext, (episode as AnimeEpisode)!, includeDataFrom, withXRefs: includeXRefs))
                    .ToList(),
                tuple.videos
                    .Select(video => new File(HttpContext, (video as VideoLocal)!, withXRefs: includeXRefs, includeReleaseInfo, includeMediaInfo, includeAbsolutePaths))
                    .ToList()
            ))
            .ToList();
    }

    /// <summary>
    /// Generate an on-demand playlist for the specified list of items, as a .m3u8 file.
    /// </summary>
    /// <param name="items">The list of item IDs to include in the playlist. If no prefix is provided for an id then it will be assumed to be a series id.</param>
    /// <returns></returns>
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(404)]
    [Produces("application/x-mpegURL")]
    [HttpGet("Generate.m3u8")]
    [HttpHead("Generate.m3u8")]
    public ActionResult GetGeneratedPlaylistM3U8(
        [FromQuery(Name = "playlist"), ModelBinder(typeof(CommaDelimitedModelBinder))] string[]? items = null
    )
    {
        if (!_playlistService.TryParsePlaylist(items ?? [], out var playlist, ModelState))
            return ValidationProblem(ModelState);

        return _playlistService.GeneratePlaylist(playlist, "Mixed");
    }
}
