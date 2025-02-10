using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Extensions;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

#pragma warning disable CA1822
namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/ReleaseManagement/DuplicateFiles")]
[ApiV3]
public class ReleaseManagementDuplicateFilesController(ISettingsProvider settingsProvider) : BaseController(settingsProvider)
{
    /// <summary>
    /// Get episodes with duplicate files, with only the files with duplicates for each episode.
    /// </summary>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="includeReleaseInfo">Include release info data.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [HttpGet("Episodes")]
    public ActionResult<ListResult<Episode>> GetEpisodes(
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] bool includeMediaInfo = true,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] bool includeReleaseInfo = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        var enumerable = RepoFactory.AnimeEpisode.GetWithDuplicateFiles();

        return enumerable
            .ToListResult(episode =>
            {
                var duplicateFiles = episode.VideoLocals
                    .Select(file => (file, locations: file.Places.ExceptBy((file.FirstValidPlace ?? file.FirstResolvedPlace) is { } fileLocation ? [fileLocation.VideoLocal_Place_ID] : [], b => b.VideoLocal_Place_ID).ToList()))
                    .Where(tuple => tuple.locations.Count > 0)
                    .ToList();
                var dto = new Episode(HttpContext, episode, includeDataFrom);
                dto.Size = duplicateFiles.Count;
                dto.Files = duplicateFiles
                    .Select(tuple => new File(HttpContext, tuple.file, includeXRefs, includeReleaseInfo, includeMediaInfo, true))
                    .ToList();
                return dto;
            }, page, pageSize);
    }

    /// <summary>
    /// Get the list of file location ids to auto remove across all series.
    /// </summary>
    /// <returns></returns>
    [HttpGet("FileLocationsToAutoRemove")]
    public ActionResult<List<FileIdSet>> GetFileIdsWithPreference()
    {
        var enumerable = RepoFactory.AnimeEpisode.GetWithDuplicateFiles();

        return enumerable
            .SelectMany(episode =>
                episode.VideoLocals
                    .SelectMany(a => a.Places.ExceptBy((a.FirstValidPlace ?? a.FirstResolvedPlace) is { } fileLocation ? [fileLocation.VideoLocal_Place_ID] : [], b => b.VideoLocal_Place_ID))
                    .Select(file => (episode.AnimeSeriesID, episode.AnimeEpisodeID, file.VideoLocalID, file.VideoLocal_Place_ID))
            )
            .GroupBy(tuple => tuple.VideoLocalID, tuple => (tuple.VideoLocal_Place_ID, tuple.AnimeEpisodeID, tuple.AnimeSeriesID))
            .Select(groupBy => new FileIdSet(groupBy))
            .ToList();
    }

    /// <summary>
    /// Get series with duplicate files.
    /// </summary>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="onlyFinishedSeries">Only show finished series.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [HttpGet("Series")]
    public ActionResult<ListResult<Series.WithEpisodeCount>> GetSeriesWithDuplicateFiles(
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] bool onlyFinishedSeries = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        var enumerable = RepoFactory.AnimeSeries.GetWithDuplicateFiles();
        if (onlyFinishedSeries)
            enumerable = enumerable.Where(a => a.AniDB_Anime.GetFinishedAiring());

        return enumerable
            .OrderBy(series => series.PreferredTitle)
            .ThenBy(series => series.AniDB_ID)
            .ToListResult(series => new Series.WithEpisodeCount(RepoFactory.AnimeEpisode.GetWithDuplicateFiles(series.AniDB_ID).Count(), series, User.JMMUserID, includeDataFrom), page, pageSize);
    }

    /// <summary>
    /// Get episodes with duplicate files for a series, with only the files with duplicates for each episode.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeXRefs">Include file/episode cross-references with the episodes.</param>
    /// <param name="includeReleaseInfo">Include release info data.</param>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [HttpGet("Series/{seriesID}/Episodes")]
    public ActionResult<ListResult<Episode>> GetEpisodesForSeries(
        [FromRoute, Range(1, int.MaxValue)] int seriesID,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] bool includeMediaInfo = true,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] bool includeReleaseInfo = false,
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1)
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return new ListResult<Episode>();

        if (!User.AllowedSeries(series))
            return new ListResult<Episode>();

        var enumerable = RepoFactory.AnimeEpisode.GetWithDuplicateFiles(series.AniDB_ID);

        return enumerable
            .ToListResult(episode =>
            {
                var duplicateFiles = episode.VideoLocals
                    .Select(file => (file, locations: file.Places.ExceptBy((file.FirstValidPlace ?? file.FirstResolvedPlace) is { } fileLocation ? [fileLocation.VideoLocal_Place_ID] : [], b => b.VideoLocal_Place_ID).ToList()))
                    .Where(tuple => tuple.locations.Count > 0)
                    .ToList();
                var dto = new Episode(HttpContext, episode, includeDataFrom);
                dto.Size = duplicateFiles.Count;
                dto.Files = duplicateFiles
                    .Select(tuple => new File(HttpContext, tuple.file, includeXRefs, includeReleaseInfo, includeMediaInfo, true))
                    .ToList();
                return dto;
            }, page, pageSize);
    }

    /// <summary>
    /// Get the list of file location ids to auto remove for the series.
    /// </summary>
    /// <param name="seriesID">Shoko Series ID</param>
    /// <returns></returns>
    [HttpGet("Series/{seriesID}/FileLocationsToAutoRemove")]
    public ActionResult<List<FileIdSet>> GetFileLocationsIdsAcrossAllEpisodes(
        [FromRoute, Range(1, int.MaxValue)] int seriesID
    )
    {
        var series = RepoFactory.AnimeSeries.GetByID(seriesID);
        if (series == null)
            return new List<FileIdSet>();

        if (!User.AllowedSeries(series))
            return new List<FileIdSet>();

        var enumerable = RepoFactory.AnimeEpisode.GetWithDuplicateFiles(series.AniDB_ID);

        return enumerable
            .SelectMany(episode =>
                episode.VideoLocals
                    .SelectMany(a => a.Places.ExceptBy((a.FirstValidPlace ?? a.FirstResolvedPlace) is { } fileLocation ? [fileLocation.VideoLocal_Place_ID] : [], b => b.VideoLocal_Place_ID))
                    .Select(file => (episode.AnimeSeriesID, episode.AnimeEpisodeID, file.VideoLocalID, file.VideoLocal_Place_ID))
            )
            .GroupBy(tuple => tuple.VideoLocalID, tuple => (tuple.VideoLocal_Place_ID, tuple.AnimeEpisodeID, tuple.AnimeSeriesID))
            .Select(groupBy => new FileIdSet(groupBy))
            .ToList();
    }

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
