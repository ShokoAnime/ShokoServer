using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Scheduling.Jobs.Trakt;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Tasks;
using Shoko.Server.Utilities;

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
    private readonly AnimeGroupService _groupService;
    private readonly TraktTVHelper _traktHelper;
    private readonly TmdbMetadataService _tmdbMetadataService;
    private readonly TmdbLinkingService _tmdbLinkingService;
    private readonly TmdbImageService _tmdbImageService;
    private readonly ISchedulerFactory _schedulerFactory;

    public ActionController(
        ILogger<ActionController> logger,
        TraktTVHelper traktHelper,
        TmdbMetadataService tmdbMetadataService,
        TmdbLinkingService tmdbLinkingService,
        TmdbImageService tmdbImageService,
        ISchedulerFactory schedulerFactory,
        ISettingsProvider settingsProvider,
        ActionService actionService,
        AnimeGroupCreator groupCreator,
        AnimeGroupService groupService
    ) : base(settingsProvider)
    {
        _logger = logger;
        _traktHelper = traktHelper;
        _tmdbMetadataService = tmdbMetadataService;
        _tmdbLinkingService = tmdbLinkingService;
        _tmdbImageService = tmdbImageService;
        _schedulerFactory = schedulerFactory;
        _actionService = actionService;
        _groupCreator = groupCreator;
        _groupService = groupService;
    }

    #region Common Actions

    /// <summary>
    /// Run Import. This checks for new files, hashes them etc, scans Drop Folders, checks and scans for community site links (tmdb, trakt, etc), and downloads missing images.
    /// </summary>
    /// <returns></returns>
    [HttpGet("RunImport")]
    public async Task<ActionResult> RunImport()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<ImportJob>();
        return Ok();
    }

    /// <summary>
    /// Queues a task to import only new files found in the import folder
    /// </summary>
    /// <returns></returns>
    [HttpGet("ImportNewFiles")]
    public async Task<ActionResult> ImportNewFiles()
    {
        await _actionService.RunImport_DetectFiles(onlyNewFiles: true);
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
    public async Task<ActionResult> SyncVotes()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<SyncAniDBVotesJob>();
        return Ok();
    }

    /// <summary>
    /// Sync Trakt states. Requires Trakt to be set up, obviously
    /// </summary>
    /// <returns></returns>
    [HttpGet("SyncTrakt")]
    public async Task<ActionResult> SyncTrakt()
    {
        var settings = SettingsProvider.GetSettings().TraktTv;
        if (!settings.Enabled ||
            string.IsNullOrEmpty(settings.AuthToken))
        {
            return BadRequest();
        }

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJobNow<SyncTraktCollectionJob>(c => c.ForceRefresh = true);

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
        Utils.ShokoServer.DownloadAllImages();
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
        Task.Factory.StartNew(() => _tmdbImageService.PurgeAllUnusedImages());
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
        Task.Factory.StartNew(() => _tmdbMetadataService.PurgeAllShowEpisodeGroups());
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
    /// Update all Trakt info. Right now, that's not much.
    /// </summary>
    /// <returns></returns>
    [HttpGet("UpdateAllTraktInfo")]
    public ActionResult UpdateTraktInfo()
    {
        var settings = SettingsProvider.GetSettings().TraktTv;
        if (!settings.Enabled ||
            string.IsNullOrEmpty(settings.AuthToken))
        {
            return BadRequest();
        }

        _traktHelper.UpdateAllInfo();
        return Ok();
    }

    /// <summary>
    /// Validates invalid images and re-downloads them
    /// </summary>
    /// <returns></returns>
    [HttpGet("ValidateAllImages")]
    public async Task<ActionResult> ValidateAllImages()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJobNow<ValidateAllImagesJob>();
        return Ok();
    }

    #endregion

    #region Admin Actions

    /// <summary>
    /// Purges all TVDB data, including images and episode/series links.
    /// This is a one-time action and will be blocked if ran again.
    /// <br/>
    /// This action is only accessible to admins.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("PurgeAllOfTvDB")]
    [HttpPost("PurgeAllOfTvDB")]
    public ActionResult PurgeAllTvdbData()
    {
        return Ok();
    }

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

        var mismatchedFiles = RepoFactory.VideoLocal.GetAll()
            .Where(file => !file.IsEmpty() && file.MediaInfo != null)
            .Select(file => (Video: file, AniDB: file.ReleaseInfo))
            .Where(tuple => tuple.AniDB is { ProviderName: "AniDB", IsCorrupted: false } && tuple.Video.MediaInfo?.MenuStreams.Count != 0 != tuple.AniDB.IsChaptered)
            .Select(tuple => (Path: tuple.Video.FirstResolvedPlace?.FullServerPath, tuple.Video))
            .Where(tuple => !string.IsNullOrEmpty(tuple.Path))
            .ToDictionary(tuple => tuple.Video.VideoLocalID, tuple => tuple.Path);
        var scheduler = await _schedulerFactory.GetScheduler();
        foreach (var (fileId, filePath) in mismatchedFiles)
            await scheduler.StartJob<AVDumpFilesJob>(a => a.Videos = new() { { fileId, filePath } });

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
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<SyncAniDBMyListJob>(c => c.ForceRefresh = true);
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
    public ActionResult UpdateAllMediaInfo()
    {
        Utils.ShokoServer.RefreshAllMediaInfo();
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
        await _actionService.CheckForCalendarUpdate(true);
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
        Task.Factory.StartNew(_groupService.RenameAllGroups).ConfigureAwait(false);
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
        await Utils.ShokoServer.SyncPlex();
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
        var scheduler = await _schedulerFactory.GetScheduler();
        var files = RepoFactory.VideoLocal.GetManuallyLinkedVideos();
        foreach (var vl in files)
        {
            await scheduler.StartJob<AddFileToMyListJob>(c => c.Hash = vl.Hash);
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
        await _actionService.CheckForUnreadNotifications(true);
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

    #endregion
}
