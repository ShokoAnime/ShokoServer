using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models;
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
    /// <param name="releaseGroupID">The preferred release group ID if available.</param>
    /// <param name="onlyUnwatched">Only show the next unwatched episode.</param>
    /// <param name="includeSpecials">Include specials in the search.</param>
    /// <param name="includeOthers">Include other type episodes in the search.</param>
    /// <param name="includeRewatching">Include already watched episodes in the
    /// search if we determine the user is "re-watching" the series.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <returns></returns>
    [HttpGet("Generate")]
    public ActionResult<IReadOnlyList<(Episode, List<File>)>> GetGeneratedPlaylistJson(
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] string[]? items = null,
        [FromQuery] int? releaseGroupID = null,
        [FromQuery] bool onlyUnwatched = false,
        [FromQuery] bool includeSpecials = false,
        [FromQuery] bool includeOthers = false,
        [FromQuery] bool includeRewatching = false,
        [FromQuery] bool includeMediaInfo = false,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource>? includeDataFrom = null
    )
    {
        var playlist = GetGeneratedPlaylistInternal(items, releaseGroupID, onlyUnwatched, includeSpecials, includeOthers, includeRewatching);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        return playlist
            .Select(tuple => (
                new Episode(HttpContext, (tuple.episode as SVR_AnimeEpisode)!, includeDataFrom, withXRefs: includeXRefs),
                tuple.videos
                    .Select(video => new File(HttpContext, (video as SVR_VideoLocal)!, withXRefs: includeXRefs, includeDataFrom, includeMediaInfo, includeAbsolutePaths))
                    .ToList()
            ))
            .ToList();
    }

    /// <summary>
    /// Generate an on-demand playlist for the specified list of items, as a .m3u8 file.
    /// </summary>
    /// <param name="items">The list of item IDs to include in the playlist. If no prefix is provided for an id then it will be assumed to be a series id.</param>
    /// <param name="releaseGroupID">The preferred release group ID if available.</param>
    /// <param name="onlyUnwatched">Only show the next unwatched episode.</param>
    /// <param name="includeSpecials">Include specials in the search.</param>
    /// <param name="includeOthers">Include other type episodes in the search.</param>
    /// <param name="includeRewatching">Include already watched episodes in the
    /// search if we determine the user is "re-watching" the series.</param>
    /// <returns></returns>
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    [ProducesResponseType(404)]
    [Produces("application/x-mpegURL")]
    [HttpGet("Generate.m3u8")]
    [HttpHead("Generate.m3u8")]
    public ActionResult GetGeneratedPlaylistM3U8(
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] string[]? items = null,
        [FromQuery] int? releaseGroupID = null,
        [FromQuery] bool onlyUnwatched = false,
        [FromQuery] bool includeSpecials = false,
        [FromQuery] bool includeOthers = false,
        [FromQuery] bool includeRewatching = false
    )
    {
        var playlist = GetGeneratedPlaylistInternal(items, releaseGroupID, onlyUnwatched, includeSpecials, includeOthers, includeRewatching);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);
        return _playlistService.GeneratePlaylist(playlist, "Mixed");
    }
    
    private IReadOnlyList<(IShokoEpisode episode, IReadOnlyList<IVideo> videos)> GetGeneratedPlaylistInternal(
        string[]? items,
        int? releaseGroupID,
        bool onlyUnwatched = true,
        bool includeSpecials = true,
        bool includeOthers = false,
        bool includeRewatching = false
    )
    {
        items ??= [];
        var playlist = new List<(IShokoEpisode, IReadOnlyList<IVideo>)>();
        var index = -1;
        foreach (var id in items)
        {
            index++;
            if (string.IsNullOrEmpty(id))
                continue;

            switch (id[0]) {
                case 'f':
                {
                    if (!int.TryParse(id[1..], out var fileID) || fileID <= 0 || _videoRepository.GetByID(fileID) is not { } video)
                    {
                        ModelState.AddModelError(index.ToString(), $"Invalid file ID \"{id}\" at index {index}");
                        continue;
                    }

                    foreach (var tuple in _playlistService.GetListForVideo(video))
                        playlist.Add(tuple);
                    break;
                }

                case 'e':
                {
                    if (!int.TryParse(id[1..], out var episodeID) || episodeID <= 0 || _episodeRepository.GetByID(episodeID) is not { } episode)
                    {
                        ModelState.AddModelError(index.ToString(), $"Invalid episode ID \"{id}\" at index {index}");
                        continue;
                    }

                    foreach (var tuple in _playlistService.GetListForEpisode(episode, releaseGroupID))
                        playlist.Add(tuple);
                    break;
                }

                case 's':
                {
                    if (!int.TryParse(id[1..], out var seriesID) || seriesID <= 0 || _seriesRepository.GetByID(seriesID) is not { } series)
                    {
                        ModelState.AddModelError(index.ToString(), $"Invalid series ID \"{id}\" at index {index}");
                        continue;
                    }

                    foreach (var tuple in _playlistService.GetListForSeries(series, releaseGroupID, new()
                    {
                        IncludeCurrentlyWatching = !onlyUnwatched,
                        IncludeSpecials = includeSpecials,
                        IncludeOthers = includeOthers,
                        IncludeRewatching = includeRewatching,
                    }))
                        playlist.Add(tuple);
                    break;
                }

                default:
                {
                    if (!int.TryParse(id, out var seriesID) || seriesID <= 0 || _seriesRepository.GetByID(seriesID) is not { } series)
                    {
                        ModelState.AddModelError(index.ToString(), $"Invalid series ID \"{id}\" at index {index}");
                        continue;
                    }

                    foreach (var tuple in _playlistService.GetListForSeries(series, releaseGroupID, new()
                    {
                        IncludeCurrentlyWatching = !onlyUnwatched,
                        IncludeSpecials = includeSpecials,
                        IncludeOthers = includeOthers,
                        IncludeRewatching = includeRewatching,
                    }))
                        playlist.Add(tuple);
                    break;
                }
            }
        }
        return playlist;
    }
}
