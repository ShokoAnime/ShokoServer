using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Logging;
using Shoko.Models.Enums;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Commands;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Shoko.Models.Server;

using CommandRequestPriority = Shoko.Server.Server.CommandRequestPriority;
using AVDump = Shoko.Server.API.v3.Models.Shoko.AVDump;
using File = Shoko.Server.API.v3.Models.Shoko.File;
using FileSortCriteria = Shoko.Server.API.v3.Models.Shoko.File.FileSortCriteria;
using Path = System.IO.Path;
using MediaInfo = Shoko.Server.API.v3.Models.Shoko.MediaInfo;
using DataSource = Shoko.Server.API.v3.Models.Common.DataSource;

namespace Shoko.Server.API.v3.Controllers;

[ApiController, Route("/api/v{version:apiVersion}/[controller]"), ApiV3]
[Authorize]
public class FileController : BaseController
{
    private const string FileUserStatsNotFoundWithFileID = "No FileUserStats entry for the given fileID for the current user";

    private const string FileNoPath = "Unable to resolve file location.";

    private const string AnidbNotFoundForFileID = "No File.Anidb entry for the given fileID";

    internal const string FileNotFoundWithFileID = "No File entry for the given fileID";

    private readonly ILogger<FileController> _logger;

    private readonly TraktTVHelper _traktHelper;

    private readonly ICommandRequestFactory _commandFactory;

    public FileController(ILogger<FileController> logger, TraktTVHelper traktHelper, ICommandRequestFactory commandFactory, ISettingsProvider settingsProvider) : base(settingsProvider)
    {
        _logger = logger;
        _traktHelper = traktHelper;
        _commandFactory = commandFactory;
    }

    internal const string FileForbiddenForUser = "Accessing File is not allowed for the current user";

    /// <summary>
    /// Get or search through the files accessible to the current user.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeMissing">Include missing files among the results.</param>
    /// <param name="includeIgnored">Include ignored files among the results.</param>
    /// <param name="includeVariations">Include files marked as a variation among the results.</param>
    /// <param name="includeDuplicates">Include files with multiple locations (and thus have duplicates) among the results.</param>
    /// <param name="includeUnrecognized">Include unrecognized files among the results.</param>
    /// <param name="includeLinked">Include manually linked files among the results.</param>
    /// <param name="includeViewed">Include previously viewed files among the results.</param>
    /// <param name="includeWatched">Include previously watched files among the results</param>
    /// <param name="sortOrder">Sort ordering. Attach '-' at the start to reverse the order of the criteria.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <param name="includeXRefs">Include series and episode cross-references.</param>
    /// <param name="seriesID">Filter the search to only files for a given shoko series.</param>
    /// <param name="episodeID">Filter the search to only files for a given shoko episode.</param>
    /// <param name="anidbSeriesID">Filter the search to only files for a given anidb series.</param>
    /// <param name="anidbEpisodeID">Filter the search to only files for a given anidb episode.</param>
    /// <param name="search">An optional search query to filter files based on their absolute paths.</param>
    /// <param name="fuzzy">Indicates that fuzzy-matching should be used for the search query.</param>
    /// <returns>A sliced part of the results for the current query.</returns>
    [HttpGet]
    public ActionResult<ListResult<File>> GetFiles(
        [FromQuery, Range(0, 1000)] int pageSize = 100,
        [FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery] IncludeOnlyFilter includeMissing = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeIgnored = IncludeOnlyFilter.False,
        [FromQuery] IncludeOnlyFilter includeVariations = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeDuplicates = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeUnrecognized = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeLinked = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeViewed = IncludeOnlyFilter.True,
        [FromQuery] IncludeOnlyFilter includeWatched = IncludeOnlyFilter.True,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] List<string> sortOrder = null,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] bool includeMediaInfo = false,
        [FromQuery] bool includeAbsolutePaths = false,
        [FromQuery] bool includeXRefs = false,
        [FromQuery] int? seriesID = null,
        [FromQuery] int? episodeID = null,
        [FromQuery] int? anidbSeriesID = null,
        [FromQuery] int? anidbEpisodeID = null,
        [FromQuery] string search = null,
        [FromQuery] bool fuzzy = true)
    {
        // Map shoko series id to anidb series id and check if the series
        // exists.
        if (seriesID.HasValue)
        {
            if (seriesID.Value <= 0)
                return new ListResult<File>();

            var series = RepoFactory.AnimeSeries.GetByID(seriesID.Value);
            if (series == null)
                return new ListResult<File>();

            anidbSeriesID = series.AniDB_ID;
        }
        // Map shoko episode id to anidb episode id and check if the episode
        // exists.
        if (episodeID.HasValue)
        {
            if (episodeID.Value <= 0)
                return new ListResult<File>();

            var episode = RepoFactory.AnimeEpisode.GetByID(episodeID.Value);
            if (episode == null)
                return new ListResult<File>();

            anidbEpisodeID = episode.AniDB_EpisodeID;
        }
        // Check if the anidb episode exists locally, and if it is part of the
        // same anidb series if both are provided.
        if (anidbEpisodeID.HasValue)
        {
            var anidbEpisode = RepoFactory.AniDB_Episode.GetByEpisodeID(anidbEpisodeID.Value);
            if (anidbEpisode == null)
                return new ListResult<File>();

            if (anidbSeriesID.HasValue && anidbEpisode.AnimeID != anidbSeriesID.Value)
                return new ListResult<File>();
        }
        // Check if the anidb anime exists locally.
        else if (anidbSeriesID.HasValue)
        {
            var anidbSeries = RepoFactory.AniDB_Anime.GetByAnimeID(anidbSeriesID.Value);
            if (anidbSeries == null)
                return new ListResult<File>();
        }
        // Filtering.
        var user = User;
        var includeLocations = includeDuplicates != IncludeOnlyFilter.True ||
            !string.IsNullOrEmpty(search) ||
            (sortOrder?.Any(criteria => criteria.Contains(FileSortCriteria.DuplicateCount.ToString())) ?? false);
        var includeUserRecord = includeViewed != IncludeOnlyFilter.True ||
            includeWatched != IncludeOnlyFilter.True ||
            (sortOrder?.Any(criteria => criteria.Contains(FileSortCriteria.ViewedAt.ToString()) || criteria.Contains(FileSortCriteria.WatchedAt.ToString())) ?? false);
        var enumerable = RepoFactory.VideoLocal.GetAll()
            .Select(video => (
                Video: video,
                BestLocation: video.GetBestVideoLocalPlace(includeMissing != IncludeOnlyFilter.True),
                Locations: includeLocations ? video.Places : null,
                UserRecord: includeUserRecord ? video.GetUserRecord(user.JMMUserID) : null
            ))
            .Where(tuple =>
            {
                var (video, bestLocation, locations, userRecord) = tuple;
                var xrefs = video.EpisodeCrossRefs;
                var isAnimeAllowed = xrefs
                    .Select(xref => xref.AnimeID)
                    .Distinct()
                    .Select(anidbID => RepoFactory.AniDB_Anime.GetByAnimeID(anidbID))
                    .Where(anime => anime != null)
                    .All(user.AllowedAnime);
                if (!isAnimeAllowed)
                    return false;

                if (anidbSeriesID.HasValue || anidbEpisodeID.HasValue)
                {
                    var isLinkedToAnimeOrEpisode = xrefs
                        .Where(
                            anidbSeriesID.HasValue && anidbEpisodeID.HasValue ? (
                                xref => xref.AnimeID == anidbSeriesID.Value && xref.EpisodeID == anidbEpisodeID.Value
                            ) : anidbSeriesID.HasValue ? (
                                xref => xref.AnimeID == anidbSeriesID.Value
                            ) : (
                                xref => xref.EpisodeID == anidbEpisodeID.Value
                            )
                         )
                         .Any();
                    if (!isLinkedToAnimeOrEpisode)
                        return false;
                }

                if (includeMissing != IncludeOnlyFilter.True)
                {
                    var shouldHideMissing = includeMissing == IncludeOnlyFilter.False;
                    var fileIsMissing = bestLocation == null;
                    if (shouldHideMissing == fileIsMissing)
                        return false;
                }

                if (includeIgnored != IncludeOnlyFilter.True)
                {
                    var shouldHideIgnored = includeIgnored == IncludeOnlyFilter.False;
                    if (shouldHideIgnored == video.IsIgnored)
                        return false;
                }

                if (includeVariations != IncludeOnlyFilter.True)
                {
                    var shouldHideVariation = includeVariations == IncludeOnlyFilter.False;
                    if (shouldHideVariation == video.IsVariation)
                        return false;
                }

                if (includeDuplicates != IncludeOnlyFilter.True)
                {
                    var shouldHideDuplicate = includeDuplicates == IncludeOnlyFilter.False;
                    var hasDuplicates = locations.Count > 1;
                    if (shouldHideDuplicate == hasDuplicates)
                        return false;
                }

                if (includeUnrecognized != IncludeOnlyFilter.True)
                {
                    var shouldHideUnrecognized = includeUnrecognized == IncludeOnlyFilter.False;
                    var fileIsUnrecognized = xrefs.Count == 0;
                    if (shouldHideUnrecognized == fileIsUnrecognized)
                        return false;
                }

                if (includeLinked != IncludeOnlyFilter.True)
                {
                    var shouldHideLinked = includeLinked == IncludeOnlyFilter.False;
                    var fileIsLinked = xrefs.Count > 0 && xrefs.Any(xref => xref.CrossRefSource != (int)CrossRefSource.AniDB);
                    if (shouldHideLinked == fileIsLinked)
                        return false;
                }

                if (includeViewed != IncludeOnlyFilter.True)
                {
                    var shouldHideViewed = includeViewed == IncludeOnlyFilter.False;
                    var fileIsViewed = userRecord != null;
                    if (shouldHideViewed == fileIsViewed)
                        return false;
                }

                if (includeWatched != IncludeOnlyFilter.True)
                {
                    var shouldHideWatched = includeWatched == IncludeOnlyFilter.False;
                    var fileIsWatched = userRecord?.WatchedDate != null;
                    if (shouldHideWatched == fileIsWatched)
                        return false;
                }

                return true;
            });

        // Search.
        if (!string.IsNullOrEmpty(search))
            enumerable = enumerable
                .Search(search, tuple => tuple.Locations.Select(place => place.FullServerPath).Where(path => path != null), fuzzy)
                .Select(result => result.Result);

        // Sorting.
        if (sortOrder != null && sortOrder.Count > 0)
            enumerable = Models.Shoko.File.OrderBy(enumerable, sortOrder);
        else if (string.IsNullOrEmpty(search))
            enumerable = Models.Shoko.File.OrderBy(enumerable, new()
            {
                // First sort by import folder from A-Z.
                FileSortCriteria.ImportFolderName.ToString(),
                // Then by the relative path inside the import folder, from A-Z.
                FileSortCriteria.RelativePath.ToString(),
            });

        // Skip and limit.
        return enumerable
            .ToListResult(tuple => new File(tuple.UserRecord, tuple.Video, includeXRefs, includeDataFrom, includeMediaInfo, includeAbsolutePaths), page, pageSize);
    }

    /// <summary>
    /// Get File Details
    /// </summary>
    /// <param name="fileID">Shoko VideoLocalID</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <returns></returns>
    [HttpGet("{fileID}")]
    public ActionResult<File> GetFile([FromRoute] int fileID, [FromQuery] bool includeXRefs = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] bool includeMediaInfo = false, [FromQuery] bool includeAbsolutePaths = false)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        return new File(HttpContext, file, includeXRefs, includeDataFrom, includeMediaInfo, includeAbsolutePaths);
    }

    /// <summary>
    /// Delete a file.
    /// </summary>
    /// <param name="fileID">The VideoLocal_Place ID. This cares about which location we are deleting from.</param>
    /// <param name="removeFiles">Remove all physical file locations.</param>
    /// <param name="removeFolder">This causes the empty folder removal to skipped if set to false.
    /// This significantly speeds up batch deleting if you are deleting many files in the same folder.
    /// It may be specified in the query.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpDelete("{fileID}")]
    public ActionResult DeleteFile([FromRoute] int fileID, [FromQuery] bool removeFiles = true, [FromQuery] bool removeFolder = true)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        foreach (var place in file.Places)
            if (removeFiles)
                place.RemoveRecordAndDeletePhysicalFile(removeFolder);
            else
                place.RemoveRecord();
        return Ok();
    }

    /// <summary>
    /// Get the <see cref="File.AniDB"/> using the <paramref name="fileID"/>.
    /// </summary>
    /// <remarks>
    /// This isn't a list because AniDB only has one File mapping even if there are multiple episodes.
    /// </remarks>
    /// <param name="fileID">Shoko File ID</param>
    /// <returns></returns>
    [HttpGet("{fileID}/AniDB")]
    public ActionResult<File.AniDB> GetFileAnidbByFileID([FromRoute] int fileID)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var anidb = file.GetAniDBFile();
        if (anidb == null)
            return NotFound(AnidbNotFoundForFileID);

        return new File.AniDB(anidb);
    }

    /// <summary>
    /// Get the <see cref="File.AniDB"/> using the <paramref name="anidbFileID"/>.
    /// </summary>
    /// <remarks>
    /// This isn't a list because AniDB only has one File mapping even if there are multiple episodes.
    /// </remarks>
    /// <param name="anidbFileID">AniDB File ID</param>
    /// <returns></returns>
    [HttpGet("AniDB/{anidbFileID}")]
    public ActionResult<File.AniDB> GetFileAnidbByAnidbFileID([FromRoute] int anidbFileID)
    {
        var anidb = RepoFactory.AniDB_File.GetByFileID(anidbFileID);
        if (anidb == null)
            return NotFound(AnidbNotFoundForFileID);

        return new File.AniDB(anidb);
    }

    /// <summary>
    /// Get the <see cref="File.AniDB"/>for file using the <paramref name="anidbFileID"/>.
    /// </summary>
    /// <remarks>
    /// This isn't a list because AniDB only has one File mapping even if there are multiple episodes.
    /// </remarks>
    /// <param name="anidbFileID">AniDB File ID</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="includeMediaInfo">Include media info data.</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <returns></returns>
    [HttpGet("AniDB/{anidbFileID}/File")]
    public ActionResult<File> GetFileByAnidbFileID([FromRoute] int anidbFileID, [FromQuery] bool includeXRefs = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [FromQuery] bool includeMediaInfo = false, [FromQuery] bool includeAbsolutePaths = false)
    {
        var anidb = RepoFactory.AniDB_File.GetByFileID(anidbFileID);
        if (anidb == null)
            return NotFound(FileNotFoundWithFileID);

        var file = RepoFactory.VideoLocal.GetByHash(anidb.Hash);
        if (file == null)
            return NotFound(AnidbNotFoundForFileID);

        return new File(HttpContext, file, includeXRefs, includeDataFrom, includeMediaInfo, includeAbsolutePaths);
    }

    /// <summary>
    /// Returns a file stream for the specified file ID.
    /// </summary>
    /// <param name="fileID">Shoko ID</param>
    /// <returns>A file stream for the specified file.</returns>
    [HttpGet("{fileID}/Stream")]
    public ActionResult GetFileStream([FromRoute] int fileID)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var bestLocation = file.GetBestVideoLocalPlace();

        var fileInfo = bestLocation.GetFile();
        if (fileInfo == null)
            return InternalError("Unable to find physical file for reading the stream data.");

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(fileInfo.FullName, out var contentType))
            contentType = "application/octet-stream";

        return PhysicalFile(fileInfo.FullName, contentType, enableRangeProcessing: true);
    }

    /// <summary>
    /// Get the MediaInfo model for file with VideoLocal ID
    /// </summary>
    /// <param name="fileID">Shoko ID</param>
    /// <returns></returns>
    [HttpGet("{fileID}/MediaInfo")]
    public ActionResult<MediaInfo> GetFileMediaInfo([FromRoute] int fileID)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var mediaContainer = file?.Media;
        if (mediaContainer == null)
            return InternalError("Unable to find media container for File");

        return new MediaInfo(file, mediaContainer);
    }

    /// <summary>
    /// Return the user stats for the file with the given <paramref name="fileID"/>.
    /// </summary>
    /// <param name="fileID">Shoko file ID</param>
    /// <returns>The user stats if found.</returns>
    [HttpGet("{fileID}/UserStats")]
    public ActionResult<File.FileUserStats> GetFileUserStats([FromRoute] int fileID)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var user = HttpContext.GetUser();
        var userStats = file.GetUserRecord(user.JMMUserID);

        if (userStats == null)
            return NotFound(FileUserStatsNotFoundWithFileID);

        return new File.FileUserStats(userStats);
    }

    /// <summary>
    /// Put a <see cref="File.FileUserStats"/> object down for the <see cref="File"/> with the given <paramref name="fileID"/>.
    /// </summary>
    /// <param name="fileID">Shoko file ID</param>
    /// <param name="fileUserStats">The new and/or update file stats to put for the file.</param>
    /// <returns>The new and/or updated user stats.</returns>
    [HttpPut("{fileID}/UserStats")]
    public ActionResult<File.FileUserStats> PutFileUserStats([FromRoute] int fileID, [FromBody] File.FileUserStats fileUserStats)
    {
        // Make sure the file exists.
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        // Get the user data.
        var user = HttpContext.GetUser();
        var userStats = file.GetOrCreateUserRecord(user.JMMUserID);

        // Merge with the existing entry and return an updated version of the stats.
        return fileUserStats.MergeWithExisting(userStats, file);
    }

    /// <summary>
    /// Mark a file as watched or unwatched.
    /// </summary>
    /// <param name="fileID">VideoLocal ID. Watched Status is kept per file, no matter how many copies or where they are.</param>
    /// <param name="watched">Is it watched?</param>
    /// <returns></returns>
    [HttpPost("{fileID}/Watched/{watched?}")]
    public ActionResult SetWatchedStatusOnFile([FromRoute] int fileID, [FromRoute] bool watched = true)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        file.ToggleWatchedStatus(watched, User.JMMUserID);

        return Ok();
    }

    /// <summary>
    /// Update either watch status, resume position, or both.
    /// </summary>
    /// <param name="fileID">VideoLocal ID. Watch status and resume position is kept per file, regardless of how many duplicates the file has.</param>
    /// <param name="eventName">The name of the event that triggered the scrobble.</param>
    /// <param name="episodeID">The episode id to scrobble to trakt.</param>
    /// <param name="watched">True if file should be marked as watched, false if file should be unmarked, or null if it shall not be updated.</param>
    /// <param name="resumePosition">Number of ticks into the video to resume from, or null if it shall not be updated.</param>
    /// <returns></returns>
    [HttpPatch("{fileID}/Scrobble")]
    public ActionResult ScrobbleFileAndEpisode([FromRoute] int fileID, [FromQuery(Name = "event")] string eventName = null, [FromQuery] int? episodeID = null, [FromQuery] bool? watched = null, [FromQuery] long? resumePosition = null)
    {

        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        // Handle legacy scrobble events.
        if (string.IsNullOrEmpty(eventName))
        {
            return ScrobbleStatusOnFile(file, watched, resumePosition);
        }

        var episode = episodeID.HasValue ? RepoFactory.AnimeEpisode.GetByID(episodeID.Value) : file.GetAnimeEpisodes()?.FirstOrDefault();
        if (episode == null)
            return ValidationProblem($"Could not get Episode with ID: {episodeID}", nameof(episodeID));

        var playbackPositionTicks = resumePosition ?? 0;
        if (playbackPositionTicks >= file.Duration)
        {
            watched = true;
            playbackPositionTicks = 0;
        }

        switch (eventName)
        {
            // The playback was started.
            case "play":
            // The playback was resumed after a pause.
            case "resume":
                ScrobbleToTrakt(file, episode, playbackPositionTicks, ScrobblePlayingStatus.Start);
                break;
            // The playback was paused.
            case "pause":
                ScrobbleToTrakt(file, episode, playbackPositionTicks, ScrobblePlayingStatus.Pause);
                break;
            // The playback was ended.
            case "stop":
                ScrobbleToTrakt(file, episode, playbackPositionTicks, ScrobblePlayingStatus.Stop);
                break;
            // The playback is still active, but the playback position changed.
            case "scrobble":
                break;
            // A user interaction caused the watch state to change.
            case "user-interaction":
                break;
        }

        if (watched.HasValue)
            file.ToggleWatchedStatus(watched.Value, User.JMMUserID);
        file.SetResumePosition(playbackPositionTicks, User.JMMUserID);

        return NoContent();
    }

    [NonAction]
    private void ScrobbleToTrakt(SVR_VideoLocal file, SVR_AnimeEpisode episode, long position, ScrobblePlayingStatus status)
    {
        if (User.IsTraktUser == 0)
            return;

        float percentage = 100 * (position / file.Duration);
        ScrobblePlayingType scrobbleType = episode.GetAnimeSeries()?.GetAnime()?.AnimeType == (int)AnimeType.Movie
            ? ScrobblePlayingType.movie
            : ScrobblePlayingType.episode;

        _traktHelper.Scrobble(scrobbleType, episode.AnimeEpisodeID.ToString(), status, percentage);
    }

    [NonAction]
    private ActionResult ScrobbleStatusOnFile(SVR_VideoLocal file, bool? watched, long? resumePosition)
    {
        if (!(watched ?? false) && resumePosition != null)
        {
            var safeRP = resumePosition ?? 0;
            if (safeRP < 0) safeRP = 0;

            if (safeRP >= file.Duration)
                watched = true;
            else
                file.SetResumePosition(safeRP, User.JMMUserID);
        }

        if (watched != null)
        {
            var safeWatched = watched ?? false;
            file.ToggleWatchedStatus(safeWatched, User.JMMUserID);
            if (safeWatched)
                file.SetResumePosition(0, User.JMMUserID);

        }

        return Ok();
    }

    /// <summary>
    /// Mark or unmark a file as ignored.
    /// </summary>
    /// <param name="fileID">VideoLocal ID</param>
    /// <param name="value">Thew new ignore value.</param>
    /// <returns></returns>
    [HttpPut("{fileID}/Ignore")]
    public ActionResult IgnoreFile([FromRoute] int fileID, [FromQuery] bool value = true)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        file.IsIgnored = value;
        RepoFactory.VideoLocal.Save(file, false);

        return Ok();
    }

    /// <summary>
    /// Run a file through AVDump and return the result.
    /// </summary>
    /// <param name="fileID">VideoLocal ID</param>
    /// <param name="priority">Increase the priority to the max for the queued command.</param>
    /// <param name="immediate">Immediately run the AVDump, without adding the command to the queue.</param>
    /// <returns></returns>
    [HttpPost("{fileID}/AVDump")]
    public ActionResult<AVDump.Result> AvDumpFile([FromRoute] int fileID, [FromQuery] bool priority = false,
        [FromQuery] bool immediate = true)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var settings = SettingsProvider.GetSettings();
        if (string.IsNullOrWhiteSpace(settings.AniDb.AVDumpKey))
            ModelState.AddModelError("Settings", "Missing AVDump API key");

        var filePath = file.GetBestVideoLocalPlace(true)?.FullServerPath;
        if (string.IsNullOrEmpty(filePath))
            ModelState.AddModelError("File", FileNoPath);

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var command = _commandFactory.Create<CommandRequest_AVDumpFile>(
            c => c.Videos = new() { { file.VideoLocalID, filePath } }
        );
        if (immediate)
        {
            command.BubbleExceptions = true;
            command.ProcessCommand();
            var result = command.Result;
            return new AVDump.Result
            {
                FullOutput = result.StandardOutput,
                Ed2k = result.ED2Ks.FirstOrDefault(),
            };
        }

        if (priority)
            command.Priority = (int) CommandRequestPriority.Priority1;

        _commandFactory.Save(command);
        return Ok();
    }

    /// <summary>
    /// Rescan a file on AniDB.
    /// </summary>
    /// <param name="fileID">VideoLocal ID</param>
    /// <param name="priority">Increase the priority to the max for the queued command.</param>
    /// <returns></returns>
    [HttpPost("{fileID}/Rescan")]
    public ActionResult RescanFile([FromRoute] int fileID, [FromQuery] bool priority = false)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var filePath = file.GetBestVideoLocalPlace(true)?.FullServerPath;
        if (string.IsNullOrEmpty(filePath))
            return ValidationProblem(FileNoPath, "File");

        var command = _commandFactory.Create<CommandRequest_ProcessFile>(
            c =>
            {
                c.VideoLocalID = file.VideoLocalID;
                c.ForceAniDB = true;
            }
        );
        if (priority) command.Priority = (int) CommandRequestPriority.Priority1;
        _commandFactory.Save(command);
        return Ok();
    }

    /// <summary>
    /// Rehash a file.
    /// </summary>
    /// <param name="fileID">VideoLocal ID</param>
    /// <returns></returns>
    [HttpPost("{fileID}/Rehash")]
    public ActionResult RehashFile([FromRoute] int fileID)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var filePath = file.GetBestVideoLocalPlace(true)?.FullServerPath;
        if (string.IsNullOrEmpty(filePath))
            return ValidationProblem(FileNoPath, "File");

        _commandFactory.CreateAndSave<CommandRequest_HashFile>(
            c =>
            {
                c.FileName = filePath;
                c.ForceHash = true;
            }
        );

        return Ok();
    }

    /// <summary>
    /// Retrieves all file locations associated with a given file ID.
    /// </summary>
    /// <param name="fileID">File ID</param>
    /// <param name="includeAbsolutePaths">Include absolute paths for the file locations.</param>
    /// <returns>A list of file locations associated with the specified file ID.</returns>
    [HttpGet("{fileID}/Location")]
    public ActionResult<List<File.Location>> GetFileLocations([FromRoute] int fileID, [FromQuery] bool includeAbsolutePaths = false)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        return file.Places
            .Select(location => new File.Location(location, includeAbsolutePaths))
            .ToList();
    }

    /// <summary>
    /// Adds a new file location, creating a copy or hard-link of an existing
    /// file location. Tries to use hard-linking to an existing location if
    /// possible.
    /// </summary>
    /// <param name="fileID">File ID</param>
    /// <param name="body">New location.</param>
    /// <returns>Returns the newly created file location.</returns>
    [HttpPost("{fileID}/Location")]
    public ActionResult<File.Location> AddFileLocation([FromRoute] int fileID, [FromBody] File.Location.NewLocationBody body)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        var importFolder = RepoFactory.ImportFolder.GetByID(body.ImportFolderID);
        if (importFolder == null)
            return BadRequest($"Unknown import folder with the given id `{body.ImportFolderID}`.");

        // Sanitize relative path and reject paths leading to outside the import folder.
        var fullPath = Path.GetFullPath(Path.Combine(importFolder.ImportFolderLocation, body.RelativePath));
        if (!fullPath.StartsWith(importFolder.ImportFolderLocation, StringComparison.OrdinalIgnoreCase))
            return BadRequest("The provided relative path leads outside the import folder.");
        var sanitizedRelativePath = Path.GetRelativePath(importFolder.ImportFolderLocation, fullPath);

        var closestLocation = file.Places.FirstOrDefault(loc => loc.ImportFolderID == importFolder.ImportFolderID) ??
            file.GetBestVideoLocalPlace();
        if (closestLocation == null)
            return InternalError("Unable to find a location to use for the operation.");

        _logger.LogTrace("Selected closest location {ClosestFullPath} for target {TargetFullPath} for File {FileID}", closestLocation.FullServerPath, fullPath, file.VideoLocalID);

        var newLocation = FileSystemUtils.CreateHardLinkOrCopy(closestLocation, importFolder, sanitizedRelativePath);
        if (newLocation == null)
            return InternalError("Unable to create a new hard link or copy");

        return new File.Location(newLocation, true);
    }

    /// <summary>
    /// Retrieves information about a specific file location.
    /// </summary>
    /// <param name="locationID">The ID of the file location to be retrieved.
    /// </param>
    /// <returns>Returns the file location information.</returns>
    [HttpGet("Location/{locationID}")]
    public ActionResult<File.Location> GetFileLocation([FromRoute] int locationID)
    {
        var fileLocation = RepoFactory.VideoLocalPlace.GetByID(locationID);
        if (fileLocation == null)
            return NotFound(FileNotFoundWithFileID);

        return new File.Location(fileLocation, true);
    }

    /// <summary>
    /// Deletes a file location, optionally also deleting the physical file.
    /// </summary>
    /// <param name="locationID">The ID of the file location to be deleted.
    /// </param>
    /// <param name="deleteFile">Whether to delete the physical file.</param>
    /// <returns>Returns a result indicating if the deletion was successful.
    /// </returns>
    [HttpDelete("Location/{locationID}")]
    public ActionResult DeleteFileLocation([FromRoute] int locationID, [FromQuery] bool deleteFile = true)
    {
        var fileLocation = RepoFactory.VideoLocalPlace.GetByID(locationID);
        if (fileLocation == null)
            return NotFound("No location by id");

        if (deleteFile)
            fileLocation.RemoveRecordAndDeletePhysicalFile();
        else
            fileLocation.RemoveRecord();

        return Ok();
    }

    /// <summary>
    /// Directly relocates a file to a new location specified by the user.
    /// </summary>
    /// <param name="locationID">The ID of the file location to be relocated.</param>
    /// <param name="body">New location information.</param>
    /// <returns>A result object containing information about the relocation process.</returns>
    [HttpPost("Location/{locationID}/DirectlyRelocate")]
    public ActionResult<File.Location.RelocateResult> DirectlyRelocateFileLocation([FromRoute] int locationID, [FromBody] File.Location.NewLocationBody body)
    {
        var fileLocation = RepoFactory.VideoLocalPlace.GetByID(locationID);
        if (fileLocation == null)
            return NotFound("No location by id");

        var importFolder = RepoFactory.ImportFolder.GetByID(body.ImportFolderID);
        if (importFolder == null)
            return BadRequest($"Unknown import folder with the given id `{body.ImportFolderID}`.");

        // Sanitize relative path and reject paths leading to outside the import folder.
        var fullPath = Path.GetFullPath(Path.Combine(importFolder.ImportFolderLocation, body.RelativePath));
        if (!fullPath.StartsWith(importFolder.ImportFolderLocation, StringComparison.OrdinalIgnoreCase))
            return BadRequest("The provided relative path leads outside the import folder.");
        var sanitizedRelativePath = Path.GetRelativePath(importFolder.ImportFolderLocation, fullPath);

        // Store the old import folder id and relative path for comparission.
        var oldImportFolderId = fileLocation.ImportFolderID;
        var oldRelativePath = fileLocation.FilePath;

        // Rename and move the file.
        var result = fileLocation.DirectlyRelocateFile(new() { ImportFolder = importFolder, RelativePath = body.RelativePath });
        if (!result.Success)
            return new File.Location.RelocateResult
            {
                ID = fileLocation.VideoLocal_Place_ID,
                FileID = fileLocation.VideoLocalID,
                ErrorMessage = result.ErrorMessage,
                IsSuccess = false,
            };

        // Check if it was actually relocated, or if we landed on the same location as earlier.
        var relocated = !string.Equals(oldRelativePath, result.RelativePath, StringComparison.InvariantCultureIgnoreCase) || oldImportFolderId != result.ImportFolder.ImportFolderID;
        return new File.Location.RelocateResult
        {
            ID = fileLocation.VideoLocal_Place_ID,
            FileID = fileLocation.VideoLocalID,
            ImportFolderID = result.ImportFolder.ImportFolderID,
            RelativePath = result.RelativePath,
            IsSuccess = true,
            IsRelocated = relocated,
        };
    }

    /// <summary>
    /// Automatically relocates a file to a new location based on predefined rules.
    /// </summary>
    /// <param name="locationID">The ID of the file location to be relocated.</param>
    /// <param name="body">Parameters for the automatic relocation process.</param>
    /// <returns>A result object containing information about the relocation process.</returns>
    [HttpPost("Location/{locationID}/AutoRelocate")]
    public ActionResult<File.Location.RelocateResult> AutomaticallyRelocateFileLocation([FromRoute] int locationID, [FromBody] File.Location.AutoRelocateBody body)
    {
        var fileLocation = RepoFactory.VideoLocalPlace.GetByID(locationID);
        if (fileLocation == null)
            return NotFound("No location by id");

        // Make sure we have a valid script to use.
        RenameScript script;
        if (!body.ScriptID.HasValue || body.ScriptID.Value <= 0)
        {
            script = RepoFactory.RenameScript.GetDefaultOrFirst();
            if (script == null)
                return BadRequest($"No default script have been selected! Select one before continuing.");
        }
        else
        {
            script = RepoFactory.RenameScript.GetByID(body.ScriptID.Value);
            if (script == null)
                return BadRequest($"Unknown script with id \"{body.ScriptID.Value}\"! Omit `ScriptID` or set it to 0 to use the default script!");

            if (string.Equals(script.ScriptName, Shoko.Models.Constants.Renamer.TempFileName))
                return BadRequest("Do not attempt to use a temp file to rename.");
        }

        // Store the old import folder id and relative path for comparission.
        var oldImportFolderId = fileLocation.ImportFolderID;
        var oldRelativePath = fileLocation.FilePath;
        var settings = SettingsProvider.GetSettings();

        // Rename and move the file, or preview where it would land if we did.
        var result = fileLocation.AutoRelocateFile(new()
            {
                Preview = body.Preview,
                DeleteEmptyDirectories = body.DeleteEmptyDirectories,
                ScriptName = script.ScriptName,
                SkipMove = body.SkipMove.HasValue ? body.SkipMove.Value : settings.Import.MoveOnImport,
                SkipRename = body.SkipRename.HasValue ? body.SkipRename.Value : settings.Import.RenameOnImport,
            });
        if (!result.Success)
            return new File.Location.RelocateResult
            {
                ID = fileLocation.VideoLocal_Place_ID,
                FileID = fileLocation.VideoLocalID,
                ErrorMessage = result.ErrorMessage,
                IsSuccess = false,
            };

        // Check if it was actually relocated, or if we landed on the same location as earlier.
        var relocated = !string.Equals(oldRelativePath, result.RelativePath, StringComparison.InvariantCultureIgnoreCase) || oldImportFolderId != result.ImportFolder.ImportFolderID;
        return new File.Location.RelocateResult
        {
            ID = fileLocation.VideoLocal_Place_ID,
            FileID = fileLocation.VideoLocalID,
            ScriptID = script.RenameScriptID,
            ImportFolderID = result.ImportFolder.ImportFolderID,
            RelativePath = result.RelativePath,
            IsSuccess = true,
            IsRelocated = relocated,
            IsPreview = body.Preview,
        };
    }

    /// <summary>
    /// Link one or more episodes to the same file.
    /// </summary>
    /// <param name="fileID">The file id.</param>
    /// <param name="body">The body.</param>
    /// <returns></returns>
    [HttpPost("{fileID}/Link")]
    public ActionResult LinkSingleEpisodeToFile([FromRoute] int fileID, [FromBody] File.Input.LinkEpisodesBody body)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        // Validate that we can manually link this file.
        CheckXRefsForFile(file, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Validate the episodes.
        var episodeList = body.EpisodeIDs
            .Select(episodeID =>
            {
                var episode = RepoFactory.AnimeEpisode.GetByID(episodeID);
                if (episode == null)
                    ModelState.AddModelError(nameof(body.EpisodeIDs), $"Unable to find shoko episode with id {episodeID}");
                return episode;
            })
            .Where(episode => episode != null)
            .ToList();
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Remove any old links and schedule the linking commands.
        RemoveXRefsForFile(file);
        foreach (var episode in episodeList)
        {
            _commandFactory.CreateAndSave<CommandRequest_LinkFileManually>(
                c =>
                {
                    c.VideoLocalID = fileID;
                    c.EpisodeID = episode.AnimeEpisodeID;
                }
            );
        }

        return Ok();
    }

    /// <summary>
    /// Link one or more episodes from a series to the same file.
    /// </summary>
    /// <param name="fileID">The file id.</param>
    /// <param name="body">The body.</param>
    /// <returns></returns>
    [HttpPost("{fileID}/LinkFromSeries")]
    public ActionResult LinkMultipleEpisodesToFile([FromRoute] int fileID, [FromBody] File.Input.LinkSeriesBody body)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        // Validate that we can manually link this file.
        CheckXRefsForFile(file, ModelState);
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Validate that the ranges are in a valid syntax and that the series exists.
        var series = RepoFactory.AnimeSeries.GetByID(body.SeriesID);
        if (series == null)
            ModelState.AddModelError(nameof(body.SeriesID), $"Unable to find series with id {body.SeriesID}.");

        var (rangeStart, startType, startErrorMessage) = Helpers.ModelHelper.GetEpisodeNumberAndTypeFromInput(body.RangeStart);
        if (!string.IsNullOrEmpty(startErrorMessage))
            ModelState.AddModelError(nameof(body.RangeStart), string.Format(startErrorMessage, nameof(body.RangeStart)));

        var (rangeEnd, endType, endErrorMessage) = Helpers.ModelHelper.GetEpisodeNumberAndTypeFromInput(body.RangeEnd);
        if (!string.IsNullOrEmpty(endErrorMessage))
            ModelState.AddModelError(nameof(body.RangeEnd), string.Format(endErrorMessage, nameof(body.RangeEnd)));

        if (startType != endType)
            ModelState.AddModelError(nameof(body.RangeEnd), "Unable to use different episode types in the `RangeStart` and `RangeEnd`.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Validate that the ranges are valid for the series.
        var episodeType = startType ?? EpisodeType.Episode;
        var totalEpisodes = Helpers.ModelHelper.GetTotalEpisodesForType(series.GetAnimeEpisodes(), episodeType);
        if (rangeStart < 1)
            ModelState.AddModelError(nameof(body.RangeStart), "`RangeStart` cannot be lower then 1.");

        if (rangeStart > totalEpisodes)
            ModelState.AddModelError(nameof(body.RangeStart), "`RangeStart` cannot be higher then the total number of episodes for the selected type.");

        if (rangeEnd < rangeStart)
            ModelState.AddModelError(nameof(body.RangeEnd), "`RangeEnd`cannot be lower then `RangeStart`.");

        if (rangeEnd > totalEpisodes)
            ModelState.AddModelError(nameof(body.RangeEnd), "`RangeEnd` cannot be higher than the total number of episodes for the selected type.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Validate the episodes.
        var episodeList = new List<SVR_AnimeEpisode>();
        for (int episodeNumber = rangeStart; episodeNumber <= rangeEnd; episodeNumber++)
        {
            var anidbEpisode = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeTypeNumber(series.AniDB_ID, episodeType, episodeNumber)[0];
            if (anidbEpisode == null)
            {
                ModelState.AddModelError("Episodes", $"Could not find the AniDB entry for the {episodeType.ToString().ToLowerInvariant()} episode {episodeNumber}.");
                continue;
            }

            var episode = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(anidbEpisode.EpisodeID);
            if (episode == null)
            {
                ModelState.AddModelError("Episodes", $"Could not find the Shoko entry for the {episodeType.ToString().ToLowerInvariant()} episode {episodeNumber}.");
                continue;
            }

            episodeList.Add(episode);
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Remove any old links and schedule the linking commands.
        RemoveXRefsForFile(file);
        foreach (var episode in episodeList)
        {
            _commandFactory.CreateAndSave<CommandRequest_LinkFileManually>(
                c =>
                {
                    c.VideoLocalID = fileID;
                    c.EpisodeID = episode.AnimeEpisodeID;
                }
            );
        }

        return Ok();
    }

    /// <summary>
    /// Unlink all the episodes if no body is given, or only the spesified episodes from the file.
    /// </summary>
    /// <param name="fileID">The file id.</param>
    /// <param name="body">Optional. The body.</param>
    /// <returns></returns>
    [HttpDelete("{fileID}/Link")]
    public ActionResult UnlinkMultipleEpisodesFromFile([FromRoute] int fileID, [FromBody] File.Input.UnlinkEpisodesBody body)
    {
        var file = RepoFactory.VideoLocal.GetByID(fileID);
        if (file == null)
            return NotFound(FileNotFoundWithFileID);

        // Validate that the cross-references are allowed to be removed.
        var all = body == null;
        var episodeIdSet = body?.EpisodeIDs?.ToHashSet() ?? new();
        var seriesIDs = new HashSet<int>();
        var episodeList = file.GetAnimeEpisodes()
            .Where(episode => all || episodeIdSet.Contains(episode.AniDB_EpisodeID))
            .Select(episode => (Episode: episode, XRef: RepoFactory.CrossRef_File_Episode.GetByHashAndEpisodeID(file.Hash, episode.AniDB_EpisodeID)))
            .Where(obj => obj.XRef != null)
            .ToList();
        foreach (var (episode, xref) in episodeList)
            if (xref.CrossRefSource == (int)CrossRefSource.AniDB)
                ModelState.AddModelError("CrossReferences", $"Unable to remove AniDB cross-reference to anidb episode with id {xref.EpisodeID} for file with id {file.VideoLocalID}.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Remove the cross-references, and take note of the series ids that
        // needs to be updated later.
        foreach (var (episode, xref) in episodeList)
        {
            seriesIDs.Add(episode.AnimeSeriesID);
            RepoFactory.CrossRef_File_Episode.Delete(xref.CrossRef_File_EpisodeID);
        }

        // Reset the import date.
        if (file.DateTimeImported.HasValue)
        {
            file.DateTimeImported = null;
            RepoFactory.VideoLocal.Save(file);
        }

        // Update any series affected by this unlinking.
        foreach (var seriesID in seriesIDs)
        {
            var series = RepoFactory.AnimeSeries.GetByID(seriesID);
            if (series != null)
                series.QueueUpdateStats();
        }

        return Ok();
    }

    /// <summary>
    /// Link multiple files to one or more episodes in a series.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns></returns>
    [HttpPost("LinkFromSeries")]
    public ActionResult LinkMultipleFiles([FromBody] File.Input.LinkSeriesMultipleBody body)
    {
        // Validate the file ids, series ids, and the range syntax.
        var files = body.FileIDs
            .Select(fileID =>
            {
                var file = RepoFactory.VideoLocal.GetByID(fileID);
                if (file == null)
                    ModelState.AddModelError(nameof(body.FileIDs), $"Unable to find a file with id {fileID}.");
                else
                    CheckXRefsForFile(file, ModelState);

                return file;
            })
            .Where(file => file != null)
            .ToList();
        if (body.FileIDs.Length == 0)
            ModelState.AddModelError(nameof(body.FileIDs), "`FileIDs` must contain at least one element.");

        var series = RepoFactory.AnimeSeries.GetByID(body.SeriesID);
        if (series == null)
            ModelState.AddModelError(nameof(body.SeriesID), $"Unable to find series with id {body.SeriesID}.");

        var (rangeStart, startType, startErrorMessage) = Helpers.ModelHelper.GetEpisodeNumberAndTypeFromInput(body.RangeStart);
        if (!string.IsNullOrEmpty(startErrorMessage))
            ModelState.AddModelError(nameof(body.RangeStart), string.Format(startErrorMessage, nameof(body.RangeStart)));

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Validate the range.
        var episodeType = startType ?? EpisodeType.Episode;
        var rangeEnd = rangeStart + files.Count - 1;
        var totalEpisodes = Helpers.ModelHelper.GetTotalEpisodesForType(series.GetAnimeEpisodes(), episodeType);
        if (rangeStart < 1)
            ModelState.AddModelError(nameof(body.RangeStart), "`RangeStart` cannot be lower then 1.");

        if (rangeStart > totalEpisodes)
            ModelState.AddModelError(nameof(body.RangeStart), "`RangeStart` cannot be higher then the total number of episodes for the selected type.");

        if (rangeEnd < rangeStart)
            ModelState.AddModelError("RangeEnd", "`RangeEnd`cannot be lower then `RangeStart`.");

        if (rangeEnd > totalEpisodes)
            ModelState.AddModelError("RangeEnd", "`RangeEnd` cannot be higher than the total number of episodes for the selected type.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Validate the episodes.
        var fileCount = 1;
        var singleEpisode = body.SingleEpisode;
        var episodeNumber = rangeStart;
        var episodeList = new List<(SVR_VideoLocal, SVR_AnimeEpisode)>();
        foreach (var file in files)
        {
            var anidbEpisode = RepoFactory.AniDB_Episode.GetByAnimeIDAndEpisodeTypeNumber(series.AniDB_ID, episodeType, episodeNumber)[0];
            if (anidbEpisode == null)
            {
                ModelState.AddModelError("Episodes", $"Could not find the AniDB entry for the {episodeType.ToString().ToLowerInvariant()} episode {episodeNumber}.");
                continue;
            }

            var episode = RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(anidbEpisode.EpisodeID);
            if (episode == null)
            {
                ModelState.AddModelError("Episodes", $"Could not find the Shoko entry for the {episodeType.ToString().ToLowerInvariant()} episode {episodeNumber}.");
                continue;
            }

            episodeList.Add((file, episode));
        }

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        // Remove any old links and schedule the linking commands.
        foreach (var (file, episode) in episodeList)
        {
            RemoveXRefsForFile(file);

            var command = _commandFactory.Create<CommandRequest_LinkFileManually>(
                c =>
                {
                    c.VideoLocalID = file.VideoLocalID;
                    c.EpisodeID = episode.AnimeEpisodeID;
                }
            );
            if (singleEpisode)
                command.Percentage = (int)Math.Round((double)(fileCount / files.Count * 100));
            else
                episodeNumber++;

            fileCount++;
            _commandFactory.Save(command);
        }

        return Ok();
    }

    /// <summary>
    /// Link multiple files to a single episode.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns></returns>
    [HttpPost("Link")]
    public ActionResult LinkMultipleFiles([FromBody] File.Input.LinkMultipleFilesBody body)
    {
        // Validate the file ids and episode id.
        var files = body.FileIDs
            .Select(fileID =>
            {
                var file = RepoFactory.VideoLocal.GetByID(fileID);
                if (file == null)
                    ModelState.AddModelError(nameof(body.FileIDs), $"Unable to find a file with id {fileID}.");
                else
                    CheckXRefsForFile(file, ModelState);

                return file;
            })
            .Where(file => file != null)
            .ToList();
        if (body.FileIDs.Length == 0)
            ModelState.AddModelError(nameof(body.FileIDs), "`FileIDs` must contain at least one element.");

        var episode = RepoFactory.AnimeEpisode.GetByID(body.EpisodeID);
        if (episode == null)
            ModelState.AddModelError(nameof(body.EpisodeID), $"Unable to find episode with id {body.EpisodeID}.");

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var anidbEpisode = episode.AniDB_Episode;
        if (anidbEpisode == null)
            return InternalError("Could not find the AniDB entry for episode");

        // Remove any old links and schedule the linking commands.
        var fileCount = 1;
        foreach (var file in files)
        {
            RemoveXRefsForFile(file);

            var command = _commandFactory.Create<CommandRequest_LinkFileManually>(
                c =>
                {
                    c.VideoLocalID = file.VideoLocalID;
                    c.EpisodeID = episode.AnimeEpisodeID;
                }
            );
            command.Percentage = (int)Math.Round((double)fileCount / files.Count * 100);

            fileCount++;
            _commandFactory.Save(command);
        }

        return Ok();
    }

    [NonAction]
    private void RemoveXRefsForFile(SVR_VideoLocal file)
    {
        foreach (var xref in RepoFactory.CrossRef_File_Episode.GetByHash(file.Hash))
        {
            if (xref.CrossRefSource == (int)CrossRefSource.AniDB)
                return;

            RepoFactory.CrossRef_File_Episode.Delete(xref.CrossRef_File_EpisodeID);
        }

        // Reset the import date.
        if (file.DateTimeImported.HasValue)
        {
            file.DateTimeImported = null;
            RepoFactory.VideoLocal.Save(file);
        }
    }

    [NonAction]
    private void CheckXRefsForFile(SVR_VideoLocal file, ModelStateDictionary modelState)
    {
        foreach (var xref in RepoFactory.CrossRef_File_Episode.GetByHash(file.Hash))
            if (xref.CrossRefSource == (int)CrossRefSource.AniDB)
                modelState.AddModelError("CrossReferences", $"Unable to remove AniDB cross-reference to anidb episode with id {xref.EpisodeID} for file with id {file.VideoLocalID}.");
    }

    /// <summary>
    /// Search for a file by path or name. Internally, it will convert forward
    /// slash (/) and backwards slash (\) to the system directory separator
    /// before matching.
    /// </summary>
    /// <param name="path">The path to search for.</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="limit">Limit the number of returned results.</param>
    /// <returns>A list of all files with a file location that ends with the given path.</returns>
    [HttpGet("PathEndsWith")]
    public ActionResult<List<File>> PathEndsWithQuery([FromQuery] string path, [FromQuery] bool includeXRefs = true,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [Range(0, 100)] int limit = 0)
        => PathEndsWithInternal(path, includeXRefs, includeDataFrom, limit);

    /// <summary>
    /// Search for a file by path or name. Internally, it will convert forward
    /// slash (/) and backwards slash (\) to the system directory separator
    /// before matching.
    /// </summary>
    /// <param name="path">The path to search for. URL encoded.</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="limit">Limit the number of returned results.</param>
    /// <returns>A list of all files with a file location that ends with the given path.</returns>
    [HttpGet("PathEndsWith/{*path}")]
    public ActionResult<List<File>> PathEndsWithPath([FromRoute] string path, [FromQuery] bool includeXRefs = true,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<DataSource> includeDataFrom = null,
        [Range(0, 100)] int limit = 0)
        => PathEndsWithInternal(Uri.UnescapeDataString(path), includeXRefs, includeDataFrom, limit);

    /// <summary>
    /// Search for a file by path or name. Internally, it will convert forward
    /// slash (/) and backwards slash (\) to the system directory separator
    /// before matching.
    /// </summary>
    /// <param name="path">The path to search for.</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <param name="includeDataFrom">Include data from selected <see cref="DataSource"/>s.</param>
    /// <param name="limit">Limit the number of returned results.</param>
    /// <returns>A list of all files with a file location that ends with the given path.</returns>
    internal ActionResult<List<File>> PathEndsWithInternal(string path, bool includeXRefs,
        HashSet<DataSource> includeDataFrom, int limit = 0)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new List<File>();

        var query = path
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var results = RepoFactory.VideoLocalPlace.GetAll()
            .AsParallel()
            .Where(location => location.FullServerPath?.EndsWith(query, StringComparison.OrdinalIgnoreCase) ?? false)
            .Select(location => location.VideoLocal)
            .Where(file =>
            {
                if (file == null)
                    return false;

                var xrefs = file.EpisodeCrossRefs;
                var series = xrefs.Count > 0 ? RepoFactory.AnimeSeries.GetByAnimeID(xrefs[0].AnimeID) : null;
                return series == null || User.AllowedSeries(series);
            })
            .DistinctBy(file => file.VideoLocalID);

        if (limit <= 0)
            return results
                .Select(a => new File(HttpContext, a, true, includeDataFrom))
                .ToList();

        return results
            .Take(limit)
            .Select(a => new File(HttpContext, a, true, includeDataFrom))
            .ToList();
    }

    /// <summary>
    /// Search for a file by path or name via regex. Internally, it will convert \/ to the system directory separator and match against the string
    /// </summary>
    /// <param name="path">a path to search for. URL Encoded</param>
    /// <returns></returns>
    [HttpGet("PathRegex/{*path}")]
    public ActionResult<List<File>> RegexSearchByPath([FromRoute] string path)
    {
        var query = path;
        if (query.Contains("%") || query.Contains("+")) query = Uri.UnescapeDataString(query);
        if (query.Contains("%")) query = Uri.UnescapeDataString(query);
        if (Path.DirectorySeparatorChar == '\\') query = query.Replace("\\/", "\\\\");
        Regex regex;

        try
        {
            regex = new Regex(query, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        }
        catch (RegexParseException e)
        {
            return ValidationProblem(e.Message, "path");
        }

        var results = RepoFactory.VideoLocalPlace.GetAll().AsParallel()
            .Where(a => regex.IsMatch(a.FullServerPath)).Select(a => a.VideoLocal)
            .Distinct()
            .Where(a =>
            {
                var ser = a?.GetAnimeEpisodes().FirstOrDefault()?.GetAnimeSeries();
                return ser == null || User.AllowedSeries(ser);
            }).Select(a => new File(HttpContext, a, true)).ToList();
        return results;
    }

    /// <summary>
    /// Search for a file by path or name via regex. Internally, it will convert \/ to the system directory separator and match against the string
    /// </summary>
    /// <param name="path">a path to search for. URL Encoded</param>
    /// <returns></returns>
    [HttpGet("FilenameRegex/{*path}")]
    public ActionResult<List<File>> RegexSearchByFileName([FromRoute] string path)
    {
        var query = path;
        if (query.Contains("%") || query.Contains("+")) query = Uri.UnescapeDataString(query);
        if (query.Contains("%")) query = Uri.UnescapeDataString(query);
        if (Path.DirectorySeparatorChar == '\\') query = query.Replace("\\/", "\\\\");
        Regex regex;

        try
        {
            regex = new Regex(query, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        }
        catch (RegexParseException e)
        {
            return ValidationProblem(e.Message, "path");
        }

        var results = RepoFactory.VideoLocalPlace.GetAll().AsParallel()
            .Where(a => regex.IsMatch(a.FileName)).Select(a => a.VideoLocal)
            .Distinct()
            .Where(a =>
            {
                var ser = a?.GetAnimeEpisodes().FirstOrDefault()?.GetAnimeSeries();
                return ser == null || User.AllowedSeries(ser);
            }).Select(a => new File(HttpContext, a, true)).ToList();
        return results;
    }

    /// <summary>
    /// Get recently added files.
    /// </summary>
    /// <returns></returns>
    [HttpGet("Recent/{limit:int?}")]
    [Obsolete("Use the universal file endpoint instead.")]
    public ActionResult<ListResult<File>> GetRecentFilesObselete([FromRoute] [Range(0, 1000)] int limit = 100)
        => GetRecentFiles(limit);

    /// <summary>
    /// Get recently added files.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeXRefs">Set to false to exclude series and episode cross-references.</param>
    /// <returns></returns>
    [Obsolete("Use the universal file endpoint instead.")]
    [HttpGet("Recent")]
    public ActionResult<ListResult<File>> GetRecentFiles([FromQuery] [Range(0, 1000)] int pageSize = 100, [FromQuery] [Range(1, int.MaxValue)] int page = 1, [FromQuery] bool includeXRefs = true)
    {
        return RepoFactory.VideoLocal.GetMostRecentlyAdded(-1, 0, User.JMMUserID)
            .ToListResult(file => new File(HttpContext, file, includeXRefs), page, pageSize);
    }

    /// <summary>
    /// Get ignored files.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [Obsolete("Use the universal file endpoint instead.")]
    [HttpGet("Ignored")]
    public ActionResult<ListResult<File>> GetIgnoredFiles([FromQuery] [Range(0, 1000)] int pageSize = 100, [FromQuery] [Range(1, int.MaxValue)] int page = 1)
    {
        return RepoFactory.VideoLocal.GetIgnoredVideos()
            .ToListResult(file => new File(HttpContext, file), page, pageSize);
    }

    /// <summary>
    /// Get files with more than one location.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeXRefs">Set to true to include series and episode cross-references.</param>
    /// <returns></returns>
    [Obsolete("Use the universal file endpoint instead.")]
    [HttpGet("Duplicates")]
    public ActionResult<ListResult<File>> GetExactDuplicateFiles([FromQuery] [Range(0, 1000)] int pageSize = 100, [FromQuery] [Range(1, int.MaxValue)] int page = 1, [FromQuery] bool includeXRefs = false)
    {
        return RepoFactory.VideoLocal.GetExactDuplicateVideos()
            .ToListResult(file => new File(HttpContext, file, includeXRefs), page, pageSize);
    }

    /// <summary>
    /// Get files with no cross-reference.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeXRefs">Set to false to exclude series and episode cross-references.</param>
    /// <returns></returns>
    [Obsolete("Use the universal file endpoint instead.")]
    [HttpGet("Linked")]
    public ActionResult<ListResult<File>> GetManuellyLinkedFiles([FromQuery] [Range(0, 1000)] int pageSize = 100, [FromQuery] [Range(1, int.MaxValue)] int page = 1, [FromQuery] bool includeXRefs = true)
    {
        return RepoFactory.VideoLocal.GetManuallyLinkedVideos()
            .ToListResult(file => new File(HttpContext, file, includeXRefs), page, pageSize);
    }

    /// <summary>
    /// Get all files with missing cross-references data.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <param name="includeXRefs">Set to false to exclude series and episode cross-references.</param>
    /// <returns></returns>
    [HttpGet("MissingCrossReferenceData")]
    public ActionResult<ListResult<File>> GetFilesWithMissingCrossReferenceData([FromQuery] [Range(0, 1000)] int pageSize = 100, [FromQuery] [Range(1, int.MaxValue)] int page = 1, [FromQuery] bool includeXRefs = true)
    {
        return RepoFactory.VideoLocal.GetVideosWithMissingCrossReferenceData()
            .ToListResult(
                file => new File(HttpContext, file)
                {
                    SeriesIDs = includeXRefs ? file.EpisodeCrossRefs
                        .GroupBy(xref => xref.AnimeID, xref => new File.CrossReferenceIDs { ID = 0, AniDB = xref.EpisodeID, TvDB = new() })
                        .Select(tuples => new File.SeriesCrossReference { SeriesID = new() { ID = 0, AniDB = tuples.Key, TvDB = new() }, EpisodeIDs = tuples.ToList() })
                        .ToList() : null
                },
                page,
                pageSize
            );
    }

    /// <summary>
    /// Get unrecognized files.
    /// Use pageSize and page (index 0) in the query to enable pagination.
    /// </summary>
    /// <param name="pageSize">Limits the number of results per page. Set to 0 to disable the limit.</param>
    /// <param name="page">Page number.</param>
    /// <returns></returns>
    [Obsolete("Use the universal file endpoint instead.")]
    [HttpGet("Unrecognized")]
    public ActionResult<ListResult<File>> GetUnrecognizedFiles([FromQuery] [Range(0, 1000)] int pageSize = 100, [FromQuery] [Range(1, int.MaxValue)] int page = 1)
    {
        return RepoFactory.VideoLocal.GetVideosWithoutEpisode()
            .ToListResult(file => new File(HttpContext, file), page, pageSize);
    }
}
