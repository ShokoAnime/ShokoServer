using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Video.Services;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Services;
using Shoko.Server.Settings;

#pragma warning disable CA1822
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/DuplicateFiles"), Tags("ReleaseManagement")]
[ApiV3]
[Authorize]
public class DuplicateFilesController(ISettingsProvider settingsProvider,
    AnimeEpisodeRepository _animeEpisodes,
    AnimeSeriesRepository _animeSeries,
    AnidbTitleSearch _anidbTitles,
    IVideoService _videoService
) : BaseController(settingsProvider)
{
    /// <summary>
    /// Get episodes with duplicate files, with only the files with duplicates for each episode.
    /// A file has duplicates when it has more than one file group and not every group is one
    /// to keep: locations that are the same file (hard links, and symbolic links with their
    /// file) are one group, and a kept location's group is kept.
    /// </summary>
    /// <param name="includeDataFrom">Include data from the selected sources: AniDB, TMDB, or any metadata source a plugin registered, by value, alias or old spelling, whose linked entries are added under <c>Sources</c>.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="includeReleaseInfo">Include release info data.</param>
    /// <param name="includeStat">Include each location's stat, group and link.</param>
    /// <param name="includeKept">Treat no location as kept, so every file with more than one group is listed.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [HttpGet("Episodes")]
    public ActionResult<ListResult<Episode>> GetEpisodes(
        [FromQuery, ModelBinder(typeof(MetadataSourceSetModelBinder))] HashSet<MetadataSource>? includeDataFrom = null,
        [FromQuery] bool includeMediaInfo = true,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] bool includeReleaseInfo = false,
        [FromQuery] bool includeStat = false,
        [FromQuery] bool includeKept = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        var user = User;
        return EpisodesWithDuplicates(null, includeKept)
            .Where(tuple => tuple.Episode.AnimeSeries is { } series && user.AllowedSeries(series))
            .ToListResult(tuple => ToEpisode(tuple, includeDataFrom, includeXRefs, includeReleaseInfo, includeMediaInfo, includeStat, includeKept), page, pageSize);
    }

    /// <summary>
    /// Get the list of file location ids to auto remove across all series: every location of
    /// every group that is not one to keep, so no link is left pointing at a removed file.
    /// </summary>
    /// <param name="includeKept">Treat no location as kept, so only the group holding each file's first available location stays.</param>
    /// <returns></returns>
    [HttpGet("FileLocationsToAutoRemove")]
    public ActionResult<List<FileIdSet>> GetFileIdsWithPreference([FromQuery] bool includeKept = false)
    {
        var user = User;
        return ToRemove(EpisodesWithDuplicates(null, includeKept)
            .Where(tuple => tuple.Episode.AnimeSeries is { } series && user.AllowedSeries(series)), includeKept);
    }

    /// <summary>
    /// Get series with duplicate files, as the episodes route counts them.
    /// </summary>
    /// <param name="includeDataFrom">Include data from the selected sources: AniDB, TMDB, or any metadata source a plugin registered, by value, alias or old spelling, whose linked entries are added under <c>Sources</c>.</param>
    /// <param name="onlyFinishedSeries">Only show finished series.</param>
    /// <param name="search">Filter by series title. Matched case-insensitively against all main and official titles across all languages.</param>
    /// <param name="includeKept">Treat no location as kept, so every file with more than one group counts.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [HttpGet("Series")]
    public ActionResult<ListResult<Series.WithEpisodeCount>> GetSeriesWithDuplicateFiles(
        [FromQuery, ModelBinder(typeof(MetadataSourceSetModelBinder))] HashSet<MetadataSource>? includeDataFrom = null,
        [FromQuery] bool onlyFinishedSeries = false,
        [FromQuery] string? search = null,
        [FromQuery] bool includeKept = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        var enumerable = _animeSeries.GetWithDuplicateFiles(_videoService.GroupLinkedFiles)
            .Where(User.AllowedSeries);
        if (onlyFinishedSeries)
            enumerable = enumerable.Where(a => a.AniDB_Anime!.GetFinishedAiring());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = AnidbTitleSearch.NormalizeForSearch(search);
            enumerable = enumerable.Where(s => _anidbTitles.AnimeMatchesSearch(s.AniDB_ID, normalizedSearch));
        }

        // The query finds the series with any file in more than one stored group; the keep flags decide.
        return enumerable
            .Select(series => (Series: series, Count: EpisodesWithDuplicates(series.AniDB_ID, includeKept).Count()))
            .Where(tuple => tuple.Count > 0)
            .OrderBy(tuple => tuple.Series.Title)
            .ThenBy(tuple => tuple.Series.AniDB_ID)
            .ToListResult(tuple => new Series.WithEpisodeCount(tuple.Count, tuple.Series, User.ID, includeDataFrom), page, pageSize);
    }

    /// <summary>
    /// Get episodes with duplicate files for a series, with only the files with duplicates for each episode.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID</param>
    /// <param name="includeDataFrom">Include data from the selected sources: AniDB, TMDB, or any metadata source a plugin registered, by value, alias or old spelling, whose linked entries are added under <c>Sources</c>.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="includeReleaseInfo">Include release info data.</param>
    /// <param name="includeStat">Include each location's stat, group and link.</param>
    /// <param name="includeKept">Treat no location as kept, so every file with more than one group is listed.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [HttpGet("Series/{seriesID}/Episodes")]
    public ActionResult<ListResult<Episode>> GetEpisodesForSeries(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery, ModelBinder(typeof(MetadataSourceSetModelBinder))] HashSet<MetadataSource>? includeDataFrom = null,
        [FromQuery] bool includeMediaInfo = true,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] bool includeReleaseInfo = false,
        [FromQuery] bool includeStat = false,
        [FromQuery] bool includeKept = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        var series = _animeSeries.GetByID(seriesID);
        if (series == null)
            return new ListResult<Episode>();

        if (!User.AllowedSeries(series))
            return new ListResult<Episode>();

        return EpisodesWithDuplicates(series.AniDB_ID, includeKept)
            .ToListResult(tuple => ToEpisode(tuple, includeDataFrom, includeXRefs, includeReleaseInfo, includeMediaInfo, includeStat, includeKept), page, pageSize);
    }

    /// <summary>
    /// Get the list of file location ids to auto remove for the series, as the route across all
    /// series picks them.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID</param>
    /// <param name="includeKept">Treat no location as kept, so only the group holding each file's first available location stays.</param>
    /// <returns></returns>
    [HttpGet("Series/{seriesID}/FileLocationsToAutoRemove")]
    public ActionResult<List<FileIdSet>> GetFileLocationsIdsAcrossAllEpisodes(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery] bool includeKept = false
    )
    {
        var series = _animeSeries.GetByID(seriesID);
        if (series == null)
            return new List<FileIdSet>();

        if (!User.AllowedSeries(series))
            return new List<FileIdSet>();

        return ToRemove(EpisodesWithDuplicates(series.AniDB_ID, includeKept), includeKept);
    }

    /// <summary>
    /// The episodes with a file with duplicates, each with those files: the query finds the
    /// episodes with any file in more than one stored group, and the keep flags decide.
    /// </summary>
    private IEnumerable<(AnimeEpisode Episode, List<VideoLocal> Files)> EpisodesWithDuplicates(int? animeID, bool includeKept)
        => _animeEpisodes.GetWithDuplicateFiles(animeID, _videoService.GroupLinkedFiles)
            .Select(episode => (Episode: episode, Files: episode.VideoLocals
                .Where(file => file.Places.Count > 1 && _videoService.GetVideoFileGroups(file, ignoreKept: includeKept).Any(group => !group.IsPreferred))
                .ToList()))
            .Where(tuple => tuple.Files.Count > 0);

    private Episode ToEpisode((AnimeEpisode Episode, List<VideoLocal> Files) tuple, HashSet<MetadataSource>? includeDataFrom, bool includeXRefs, bool includeReleaseInfo, bool includeMediaInfo, bool includeStat, bool includeKept)
    {
        var dto = new Episode(HttpContext, tuple.Episode, includeDataFrom);
        dto.Size = tuple.Files.Count;
        dto.Files = tuple.Files
            .Select(file => new File(HttpContext, file, includeXRefs, includeReleaseInfo, includeMediaInfo, true, includeStat, includeStat ? _videoService.GetVideoFileGroups(file, ignoreKept: includeKept) : null))
            .ToList();
        return dto;
    }

    private List<FileIdSet> ToRemove(IEnumerable<(AnimeEpisode Episode, List<VideoLocal> Files)> episodes, bool includeKept)
        => episodes
            .SelectMany(tuple => tuple.Files
                .SelectMany(file => ((VideoService)_videoService).GetVideoFilesToAutoRemove(file, ignoreKept: includeKept))
                .Select(location => (tuple.Episode.AnimeSeriesID, tuple.Episode.AnimeEpisodeID, location.VideoID, location.ID)))
            .GroupBy(tuple => tuple.VideoID, tuple => (tuple.ID, tuple.AnimeEpisodeID, tuple.AnimeSeriesID))
            .Select(groupBy => new FileIdSet(groupBy))
            .ToList();

    public class FileIdSet(IGrouping<int, (int VideoLocal_Place_ID, int AnimeEpisodeID, int AnimeSeriesID)> grouping)
    {
        /// <summary>
        /// The file ID with duplicates to remove.
        /// </summary>
        public int FileID { get; set; } = grouping.Key;

        /// <summary>
        /// The series IDs with duplicates to remove.
        /// </summary>
        public List<int> AnimeSeriesIDs { get; set; } = grouping
            .Select(tuple => tuple.AnimeSeriesID)
            .Distinct()
            .ToList();

        /// <summary>
        /// The episode IDs with duplicates to remove.
        /// </summary>
        public List<int> AnimeEpisodeIDs { get; set; } = grouping
            .Select(tuple => tuple.AnimeEpisodeID)
            .Distinct()
            .ToList();

        /// <summary>
        /// The duplicate locations to remove from the files/episodes.
        /// </summary>
        public List<int> FileLocationIDs { get; set; } = grouping
            .Select(tuple => tuple.VideoLocal_Place_ID)
            .Distinct()
            .ToList();
    }
}
