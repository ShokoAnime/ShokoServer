using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Video.Services;
using Shoko.QueueProcessor.Abstractions;
using Shoko.QueueProcessor.Scheduling;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Plex;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Tasks;

namespace Shoko.Server.API.v3.Controllers;

[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize]
public class ActionController : BaseController
{
    private readonly ILogger<ActionController> _logger;
    private readonly AnimeGroupCreator _groupCreator;
    private readonly ActionService _actionService;
    private readonly IShokoGroupManager _groupService;
    private readonly TmdbMetadataService _tmdbMetadataService;
    private readonly TmdbLinkingService _tmdbLinkingService;
    private readonly IVideoService _videoService;
    private readonly IVideoReleaseService _videoReleaseService;
    private readonly IQueueScheduler _scheduler;
    private readonly IImageManager _imageManager;
    private readonly VideoLocalRepository _videoLocals;
    private readonly JMMUserRepository _jmmUsers;

    public ActionController(
        ILogger<ActionController> logger,
        TmdbMetadataService tmdbMetadataService,
        TmdbLinkingService tmdbLinkingService,
        IQueueScheduler scheduler,
        IVideoService videoService,
        IVideoReleaseService videoReleaseService,
        ISettingsProvider settingsProvider,
        ActionService actionService,
        AnimeGroupCreator groupCreator,
        IShokoGroupManager groupService,
        IImageManager imageManager,
        VideoLocalRepository videoLocals,
        JMMUserRepository jmmUsers
    ) : base(settingsProvider)
    {
        _logger = logger;
        _tmdbMetadataService = tmdbMetadataService;
        _tmdbLinkingService = tmdbLinkingService;
        _videoService = videoService;
        _videoReleaseService = videoReleaseService;
        _scheduler = scheduler;
        _actionService = actionService;
        _groupCreator = groupCreator;
        _groupService = groupService;
        _imageManager = imageManager;
        _videoLocals = videoLocals;
        _jmmUsers = jmmUsers;
    }

    #region Common Actions

    /// <summary>
    /// Run Import. This checks for new files, hashes them etc, scans Drop Folders, checks and scans for community site links (tmdb, etc), and downloads missing images.
    /// </summary>
    /// <returns></returns>
    [HttpGet("RunImport")]
    public async Task<ActionResult> RunImport()
    {
        await _scheduler.StartJob<ImportJob>();
        return Ok();
    }

    /// <summary>
    /// Queues a task to import only new files found in the managed folders
    /// </summary>
    /// <returns></returns>
    [HttpGet("ImportNewFiles")]
    public async Task<ActionResult> ImportNewFiles()
    {
        await _videoService.ScheduleScanForManagedFolders(onlyNewFiles: true);
        return Ok();
    }

    /// <summary>
    /// This was for web cache hash syncing, and will be for perceptual hashing maybe eventually.
    /// </summary>
    /// <returns></returns>
    [HttpGet("SyncHashes")]
    public ActionResult SyncHashes()
    {
        return BadRequest();
    }

    /// <summary>
    /// Sync the votes from Shoko to AniDB.
    /// </summary>
    /// <returns></returns>
    [HttpGet("SyncVotes")]
    public async Task<ActionResult> SyncVotes([FromQuery] bool export = false, [FromQuery] bool import = false)
    {
        if (User.IsAniDBUser != 1)
            return BadRequest("User is not an AniDB user. Nothing to do.");

        if (export && import)
            return BadRequest("Cannot export and import at the same time.");
        await _scheduler.StartJob<SyncAniDBVotesJob>(c => (c.UserID, c.Export) = (User.JMMUserID, export));
        return Ok();
    }

    /// <summary>
    /// Remove Entries in the Shoko Database for Files that are no longer accessible
    /// </summary>
    /// <returns></returns>
    [HttpGet("RemoveMissingFiles/{removeFromMyList:bool?}")]
    public async Task<ActionResult> RemoveMissingFiles(bool removeFromMyList = true)
    {
        await _actionService.RemoveRecordsWithoutPhysicalFiles(removeFromMyList);
        return Ok();
    }

    /// <summary>
    /// Updates and Downloads Missing Images
    /// </summary>
    /// <returns></returns>
    [HttpGet("UpdateAllImages")]
    public ActionResult UpdateAllImages()
    {
        Task.Factory.StartNew(() => _imageManager.ScheduleAllAutoDownloads());
        return Ok();
    }

    /// <summary>
    /// Schedule auto-downloads for all images across all entities, optionally
    /// filtered by image source, image type, and/or cross-reference source.
    /// </summary>
    /// <param name="imageSource">Optional. Filter to a specific image source.</param>
    /// <param name="imageType">Optional. Filter to a specific image type.</param>
    /// <param name="xrefSource">Optional. Filter to a specific cross-reference source.</param>
    /// <param name="force">Optional. Re-download even if images already exist locally.</param>
    /// <returns></returns>
    [HttpGet("DownloadAllImages")]
    public ActionResult DownloadAllImages(
        [FromQuery] DataSource? imageSource = null,
        [FromQuery] ImageEntityType? imageType = null,
        [FromQuery] DataSource? xrefSource = null,
        [FromQuery] bool force = false
    )
    {
        Task.Factory.StartNew(() => _imageManager.ScheduleAllAutoDownloads(imageSource, imageType, xrefSource, force));
        return Ok();
    }

    /// <summary>
    /// Scan for TMDB matches for all unlinked AniDB anime.
    /// </summary>
    /// <returns></returns>
    [HttpGet("SearchForTmdbMatches")]
    public ActionResult SearchForTmdbMatches()
    {
        Task.Factory.StartNew(() => _tmdbMetadataService.ScanForMatches());
        return Ok();
    }

    /// <summary>
    /// Updates all TMDB Movies in the local database.
    /// </summary>
    /// <returns></returns>
    [HttpGet("UpdateAllTmdbMovies")]
    public ActionResult UpdateAllTmdbMovies()
    {
        Task.Factory.StartNew(() => _tmdbMetadataService.UpdateAllMovies(true, true));
        return Ok();
    }

    /// <summary>
    /// Purge all unused TMDB Movies that are not linked to any AniDB anime.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("PurgeAllUnusedTmdbMovies")]
    public ActionResult PurgeAllUnusedTmdbMovies()
    {
        Task.Factory.StartNew(() => _tmdbMetadataService.PurgeAllUnusedMovies());
        return Ok();
    }

    /// <summary>
    /// Purge all TMDB Movie Collections.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("PurgeAllTmdbMovieCollections")]
    public ActionResult PurgeAllTmdbMovieCollections()
    {
        Task.Factory.StartNew(() => _tmdbMetadataService.PurgeAllMovieCollections());
        return Ok();
    }

    /// <summary>
    /// Update all TMDB Shows in the local database.
    /// </summary>
    /// <returns></returns>
    [HttpGet("UpdateAllTmdbShows")]
    public ActionResult UpdateAllTmdbShows()
    {
        Task.Factory.StartNew(() => _tmdbMetadataService.UpdateAllShows(true, true));
        return Ok();
    }

    /// <summary>
    /// Download any missing TMDB People.
    /// </summary>
    [HttpGet("DownloadMissingTmdbPeople")]
    public ActionResult DownloadMissingTmdbPeople()
    {
        Task.Factory.StartNew(() => _tmdbMetadataService.RepairMissingPeople());
        return Ok();
    }

    /// <summary>
    /// Purge all unused TMDB Images that are not linked to anything.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("PurgeAllUnusedTmdbImages")]
    public ActionResult PurgeAllUnusedTmdbImages()
    {
        Task.Factory.StartNew(() => _imageManager.SchedulePurgeOfOrphanedImages(0, DataSource.TMDB));
        return Ok();
    }

    /// <summary>
    /// Purge all unused TMDB Shows that are not linked to any AniDB anime.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("PurgeAllUnusedTmdbShows")]
    public ActionResult PurgeAllUnusedTmdbShows()
    {
        Task.Factory.StartNew(() => _tmdbMetadataService.PurgeAllUnusedShows());
        return Ok();
    }

    /// <summary>
    /// Purge all TMDB Show Alternate Orderings.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("PurgeAllTmdbShowAlternateOrderings")]
    public ActionResult PurgeAllTmdbShowAlternateOrderings()
    {
        Task.Factory.StartNew(_tmdbMetadataService.PurgeAllShowEpisodeGroups);
        return Ok();
    }

    /// <summary>
    /// Purge all AniDB-TMDB links, optionally removing the links and resetting the auto-linking state.
    /// </summary>
    /// <param name="removeShowLinks">Whether to remove show links.</param>
    /// <param name="removeMovieLinks">Whether to remove movie links.</param>
    /// <param name="resetAutoLinkingState">Whether to reset the auto-linking state.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("PurgeAllTmdbLinks")]
    public ActionResult PurgeAllTmdbLinks([FromQuery] bool removeShowLinks = true, [FromQuery] bool removeMovieLinks = true, [FromQuery] bool? resetAutoLinkingState = null)
    {
        Task.Run(() =>
        {
            if (removeShowLinks || removeMovieLinks)
                _tmdbLinkingService.RemoveAllLinks(removeShowLinks, removeMovieLinks);
            if (resetAutoLinkingState.HasValue)
                _tmdbLinkingService.ResetAutoLinkingState(resetAutoLinkingState.Value);
        });
        return Ok();
    }

    /// <summary>
    /// Clears the current release for all known videos.
    /// </summary>
    /// <param name="skipEvents">
    ///   Set to <c>false</c> to skip provider-specific post-clear state sync (e.g. removing the release from a tracking list).
    /// </param>
    /// <param name="providerNames">
    ///   The names of the providers to clear. If null, all providers will be cleared.
    /// </param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("PurgeAllUsedReleases")]
    public ActionResult PurgeAllUsedReleases(
        [FromQuery] bool skipEvents = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<string>? providerNames = null
    )
    {
        Task.Run(() => _videoReleaseService.PurgeUsedReleases(providerNames, skipEvents));
        return Ok();
    }

    /// <summary>
    /// Purges all unused releases not linked to any videos from the database.
    /// </summary>
    /// <param name="skipEvents">
    ///   Set to <c>false</c> to skip provider-specific post-clear state sync (e.g. removing the release from a tracking list).
    /// </param>
    /// <param name="providerNames">
    ///   The names of the providers to clear. If null, all providers will be cleared.
    /// </param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("PurgeAllUnusedReleases")]
    public ActionResult PurgeAllUnusedReleases(
        [FromQuery] bool skipEvents = false,
        [FromQuery, ModelBinder(typeof(CommaDelimitedModelBinder))] HashSet<string>? providerNames = null
    )
    {
        Task.Run(() => _videoReleaseService.PurgeUnusedReleases(providerNames, skipEvents));
        return Ok();
    }

    /// <summary>
    /// Validates invalid images and re-downloads them
    /// </summary>
    /// <returns></returns>
    [HttpGet("ValidateAllImages")]
    public async Task<ActionResult> ValidateAllImages()
    {
        await _imageManager.ScheduleValidateAllImages(prioritize: true);
        return Ok();
    }

    #endregion

    #region Admin Actions

    /// <summary>
    /// Gets files whose data does not match AniDB
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("AVDumpMismatchedFiles")]
    public async Task<ActionResult> AVDumpMismatchedFiles()
    {
        var settings = SettingsProvider.GetSettings();
        if (string.IsNullOrWhiteSpace(settings.AniDb.AVDumpKey))
            return ValidationProblem("Missing AVDump API key.", "Settings");

        var mismatchedFiles = _videoLocals.GetAll()
            .Where(file => !file.IsEmpty() && file.MediaInfo != null)
            .Select(file => (Video: file, AniDB: file.ReleaseInfo))
            .Where(tuple => tuple.AniDB is { ProviderName: "AniDB", IsCorrupted: false } && tuple.Video.MediaInfo?.MenuStreams.Count != 0 != tuple.AniDB.IsChaptered)
            .Select(tuple => (Path: tuple.Video.FirstResolvedPlace?.Path, tuple.Video))
            .Where(tuple => !string.IsNullOrEmpty(tuple.Path))
            .ToDictionary(tuple => tuple.Video.VideoLocalID, tuple => tuple.Path);
        foreach (var (fileId, filePath) in mismatchedFiles)
            await _scheduler.StartJob<AVDumpFilesJob>(a => a.Videos = new() { { fileId, filePath } });

        _logger.LogInformation("Queued {QueuedAnimeCount} files for avdumping", mismatchedFiles.Count);

        return Ok();
    }

    /// <summary>
    /// This Downloads XML data from AniDB where there is none. This should only happen:
    /// A. If someone deleted or corrupted them.
    /// B. If the server closed unexpectedly at the wrong time (it'll only be one).
    /// C. If there was a catastrophic error.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("DownloadMissingAniDBAnimeData")]
    public ActionResult UpdateMissingAnidbXml()
    {
        Task.Run(_actionService.DownloadMissingAnidbAnimeXmls);
        Task.Run(_actionService.ScheduleMissingAnidbAnimeForFiles);

        return Ok();
    }

    /// <summary>
    /// Downloads all missing or partially missing AniDB creators over the UDP
    /// API. Will do nothing if downloading creator data is set to
    /// <see langword="false" />.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("DownloadMissingAniDBCreators")]
    public ActionResult ScheduleMissingAniDBCreators()
    {
        Task.Run(_actionService.ScheduleMissingAnidbCreators);
        return Ok();
    }

    /// <summary>
    /// BEWARE this is a dangerous command!
    /// It syncs all of the states in Shoko's library to AniDB.
    /// ONE WAY. THIS CAN ERASE ANIDB DATA IRREVERSIBLY
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("SyncMyList")]
    public async Task<ActionResult> SyncMyList()
    {
        await _scheduler.StartJob<SyncAniDBMyListJob>(c => c.ForceRefresh = true);
        return Ok();
    }

    /// <summary>
    /// Update All AniDB Series Info
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("UpdateAllAniDBInfo")]
    public async Task<ActionResult> UpdateAllAniDBInfo()
    {
        await _actionService.RunImport_UpdateAllAniDB();
        return Ok();
    }

    /// <summary>
    /// Queues a task to Update all media info
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("UpdateAllMediaInfo")]
    public async Task<ActionResult> UpdateAllMediaInfo()
    {
        await _scheduler.StartJob<MediaInfoAllFilesJob>();
        return Ok();
    }

    /// <summary>
    /// Queues commands to Update All Series Stats and Force a Recalculation of All Group Filters
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("UpdateSeriesStats")]
    public async Task<ActionResult> UpdateSeriesStats()
    {
        await _actionService.UpdateAllStats();
        return Ok();
    }

    /// <summary>
    /// Update AniDB Releases with missing group info, including with missing release
    /// groups.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("UpdateMissingAniDBFileInfo")]
    public async Task<ActionResult> UpdateMissingAniDBFileInfo()
    {
        await _actionService.UpdateAnidbReleaseInfo();
        return Ok();
    }

    /// <summary>
    /// Update the AniDB Calendar data for use on the dashboard.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("UpdateAniDBCalendar")]
    public async Task<ActionResult> UpdateAniDBCalendarData()
    {
        await _scheduler.StartJob<GetAniDBCalendarJob>(c => c.ForceRefresh = true);
        return Ok();
    }

    /// <summary>
    /// Recreate all <see cref="Group"/>s. This will delete any and all existing groups.
    /// </summary>
    /// <remarks>
    /// This action requires an admin account because it's a destructive action.
    /// </remarks>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("RecreateAllGroups")]
    public ActionResult RecreateAllGroups()
    {
        Task.Factory.StartNew(() => _groupCreator.RecreateAllGroups()).ConfigureAwait(false);
        return Ok();
    }

    /// <summary>
    /// Rename al <see cref="Group"/>s. This won't recreate the whole library,
    /// only rename any groups without a custom name set based on the current
    /// language preference.
    /// </summary>
    /// <remarks>
    /// This action requires an admin account because it affects all groups.
    /// </remarks>
    [Authorize("admin")]
    [HttpGet("RenameAllGroups")]
    public ActionResult RenameAllGroups()
    {
        Task.Factory.StartNew(_groupService.RenameAllGroups, TaskCreationOptions.LongRunning).ConfigureAwait(false);
        return Ok();
    }

    /// <summary>
    /// Add all series that have data and files, but no series. This helps if you've deleted a series, and it's stuck in limbo.
    /// </summary>
    /// <remarks>
    /// This action requires an admin account because it affects the collection.
    /// </remarks>
    [Authorize("admin")]
    [HttpGet("CreateMissingSeries")]
    public async Task<ActionResult> CreateMissingSeries()
    {
        await _actionService.CreateMissingSeries();
        return Ok();
    }

    /// <summary>
    /// Sync watch states with plex.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("PlexSyncAll")]
    public async Task<ActionResult> PlexSyncAll()
    {
        foreach (var user in _jmmUsers.GetAll())
        {
            if (string.IsNullOrEmpty(user.PlexToken)) continue;
            await _scheduler.StartJob<SyncPlexWatchedStatesJob>(c => c.User = user);
        }
        return Ok();
    }

    /// <summary>
    /// Forcibly runs AddToMyList commands for all manual links
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("AddAllManualLinksToMyList")]
    public async Task<ActionResult> AddAllManualLinksToMyList()
    {
        var files = _videoLocals.GetManuallyLinkedVideos();
        foreach (var vl in files)
        {
            await _scheduler.StartJob<AddFileToMyListJob>(c => c.Hash = vl.Hash);
        }

        return Ok($"Saved {files.Count} AddToMyList Commands");
    }

    /// <summary>
    /// Fetch unread notifications and messages from AniDB
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("GetAniDBNotifications")]
    public async Task<ActionResult> GetAniDBNotifications()
    {
        await _scheduler.StartJob<CheckAniDBNotificationsJob>(c => c.ForceRefresh = true);
        return Ok();
    }

    /// <summary>
    /// Process file moved messages from AniDB. This will force an update on the affected files.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("RefreshAniDBMovedFiles")]
    public async Task<ActionResult> RefreshAniDBMovedFiles()
    {
        await _actionService.RefreshAniDBMovedFiles(true);
        return Ok();
    }

    /// <summary>
    /// Verify all unverified AniDB relations by fetching the correct data via UDP.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("VerifyAllRelations")]
    public async Task<ActionResult> VerifyAllRelations()
    {
        var count = await _actionService.VerifyAllUnverifiedRelations();
        return Ok(new { ScheduledJobs = count });
    }

    #endregion
}
