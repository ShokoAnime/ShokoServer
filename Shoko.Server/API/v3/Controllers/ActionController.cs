using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Providers.AniDB;
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
    private readonly TmdbMetadataService _tmdbService;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly JobFactory _jobFactory;
    private readonly AnimeSeriesService _seriesService;

    public ActionController(ILogger<ActionController> logger, TraktTVHelper traktHelper, TmdbMetadataService tmdbService, ISchedulerFactory schedulerFactory,
        ISettingsProvider settingsProvider, JobFactory jobFactory, ActionService actionService, AnimeSeriesService seriesService, AnimeGroupCreator groupCreator, AnimeGroupService groupService) : base(settingsProvider)
    {
        _logger = logger;
        _traktHelper = traktHelper;
        _tmdbService = tmdbService;
        _schedulerFactory = schedulerFactory;
        _jobFactory = jobFactory;
        _actionService = actionService;
        _seriesService = seriesService;
        _groupCreator = groupCreator;
        _groupService = groupService;
    }

    #region Common Actions

    /// <summary>
    /// Run Import. This checks for new files, hashes them etc, scans Drop Folders, checks and scans for community site links (tvdb, trakt, tmdb, etc), and downloads missing images.
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
        await _actionService.RunImport_NewFiles();
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
    /// Update All TvDB Series Info
    /// </summary>
    /// <returns></returns>
    [HttpGet("UpdateAllTvDBInfo")]
    public async Task<ActionResult> UpdateAllTvDBInfo()
    {
        await _actionService.RunImport_UpdateTvDB(false);
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
    /// Updates All TMDB Movie Info.
    /// </summary>
    /// <returns></returns>
    [Obsolete("Use 'UpdateAllTMDBMovieInfo' instead.")]
    [HttpGet("UpdateAllMovieDBInfo")]
    public ActionResult UpdateAllMovieDBInfo()
    {
        Task.Factory.StartNew(() => _tmdbService.UpdateAllMovies(true, true));
        return Ok();
    }

    /// <summary>
    /// Updates all TMDB Movies in the local database.
    /// </summary>
    /// <returns></returns>
    [HttpGet("UpdateAllTmdbMovies")]
    public ActionResult UpdateAllTmdbMovies()
    {
        Task.Factory.StartNew(() => _tmdbService.UpdateAllMovies(true, true));
        return Ok();
    }

    /// <summary>
    /// Purge all unused TMDB Movies that are not linked to any AniDB anime.
    /// </summary>
    /// <returns></returns>
    [HttpGet("PurgeAllUnusedTmdbMovies")]
    public ActionResult PurgeAllUnusedTmdbMovies()
    {
        Task.Factory.StartNew(() => _tmdbService.PurgeAllUnusedMovies());
        return Ok();
    }

    /// <summary>
    /// Update all TMDB Shows in the local database.
    /// </summary>
    /// <returns></returns>
    [HttpGet("UpdateAllTmdbShows")]
    public ActionResult UpdateAllTmdbShows()
    {
        Task.Factory.StartNew(() => _tmdbService.UpdateAllShows(true, true));
        return Ok();
    }

    /// <summary>
    /// Purge all unused TMDB Shows that are not linked to any AniDB anime.
    /// </summary>
    /// <returns></returns>
    [HttpGet("PurgeAllUnusedTmdbShows")]
    public ActionResult PurgeAllUnusedTmdbShows()
    {
        Task.Factory.StartNew(() => _tmdbService.PurgeAllUnusedShows());
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

    private static readonly object _tvdbLock = new();

    private static bool _tvdbPurged = false;

    /// <summary>
    /// Purges all TVDB data, including images and episode/series links.
    /// This is a one-time action and will be blocked if ran again.
    /// <br/>
    /// This action is only accessible to admins.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpPost("PurgeAllOfTvDB")]
    public ActionResult PurgeAllTvdbData()
    {
        Task.Run(() =>
        {
            if (_tvdbPurged)
                return;
            lock (_tvdbLock)
            {
                if (_tvdbPurged)
                    return;
                _tvdbPurged = true;

                var startedAt = DateTime.Now;
                _logger.LogInformation("Starting TVDB purging at {StartedAt}", startedAt);

                _logger.LogInformation("Resetting all TvDB settings.");
                var settings = SettingsProvider.GetSettings();
                settings.TvDB.AutoLink = false;
                settings.TvDB.AutoFanart = false;
                settings.TvDB.AutoFanartAmount = 0;
                settings.TvDB.AutoPosters = false;
                settings.TvDB.AutoPostersAmount = 0;
                settings.TvDB.AutoWideBanners = false;
                settings.TvDB.AutoWideBannersAmount = 0;
                settings.TvDB.UpdateFrequency = Shoko.Models.Enums.ScheduledUpdateFrequency.Never;
                settings.TvDB.Language = "en";
                SettingsProvider.SaveSettings(settings);

                var tvdbPath = ImageUtils.GetBaseTvDBImagesPath();
                _logger.LogInformation("Deleting TVDB images directory at {Path}", tvdbPath);
                if (Directory.Exists(tvdbPath))
                    Directory.Delete(tvdbPath, true);

                var allTvdbEpisodes = RepoFactory.TvDB_Episode.GetAll();
                _logger.LogInformation("Deleting {Count} TvDB episodes", allTvdbEpisodes.Count);
                RepoFactory.TvDB_Episode.Delete(allTvdbEpisodes);

                var allTvdbSeries = RepoFactory.TvDB_Series.GetAll();
                _logger.LogInformation("Deleting {Count} TvDB series", allTvdbSeries.Count);
                RepoFactory.TvDB_Series.Delete(allTvdbSeries);

                var allTvdbSeriesXrefs = RepoFactory.CrossRef_AniDB_TvDB.GetAll();

                _logger.LogInformation("Deleting {Count} TvDB series xrefs", allTvdbSeriesXrefs.Count);
                RepoFactory.CrossRef_AniDB_TvDB.Delete(allTvdbSeriesXrefs);

                var allTvdbEpisodeXrefs = RepoFactory.CrossRef_AniDB_TvDB_Episode.GetAll();
                _logger.LogInformation("Deleting {Count} TvDB episode xrefs", allTvdbEpisodeXrefs.Count);
                RepoFactory.CrossRef_AniDB_TvDB_Episode.Delete(allTvdbEpisodeXrefs);

                var allTvdbEpisodeOverrideXrefs = RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetAll();
                _logger.LogInformation("Deleting {Count} TvDB episode override xrefs", allTvdbEpisodeOverrideXrefs.Count);
                RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.Delete(allTvdbEpisodeOverrideXrefs);

                var allTvdbImageFanart = RepoFactory.TvDB_ImageFanart.GetAll();
                _logger.LogInformation("Deleting {Count} TvDB image fanarts", allTvdbImageFanart.Count);
                RepoFactory.TvDB_ImageFanart.Delete(allTvdbImageFanart);

                var allTvdbImagePoster = RepoFactory.TvDB_ImagePoster.GetAll();
                _logger.LogInformation("Deleting {Count} TvDB image posters", allTvdbImagePoster.Count);
                RepoFactory.TvDB_ImagePoster.Delete(allTvdbImagePoster);

                var allTvdbImageWideBanner = RepoFactory.TvDB_ImageWideBanner.GetAll();
                _logger.LogInformation("Deleting {Count} TvDB image wide banners", allTvdbImageWideBanner.Count);
                RepoFactory.TvDB_ImageWideBanner.Delete(allTvdbImageWideBanner);

                _logger.LogInformation("Purged all TvDB data in {Elapsed}", DateTime.Now - startedAt);
            }
        });

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
            .Select(file => (Video: file, AniDB: file.AniDBFile))
            .Where(tuple => tuple.AniDB is { IsDeprecated: false } && tuple.Video.MediaInfo?.MenuStreams.Count != 0 != tuple.AniDB.IsChaptered)
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
    public async Task<ActionResult> UpdateMissingAnidbXml()
    {
        // Check existing anime.
        var index = 0;
        var queuedAnimeSet = new HashSet<int>();
        var localAnimeSet = RepoFactory.AniDB_Anime.GetAll()
            .Select(a => a.AnimeID)
            .OrderBy(a => a)
            .ToHashSet();
        _logger.LogInformation("Checking {AllAnimeCount} anime for missing XML files…", localAnimeSet.Count);
        foreach (var animeID in localAnimeSet)
        {
            if (++index % 10 == 1)
                _logger.LogInformation("Checking {AllAnimeCount} anime for missing XML files — {CurrentCount}/{AllAnimeCount}", localAnimeSet.Count, index + 1, localAnimeSet.Count);

            var xmlUtils = HttpContext.RequestServices.GetRequiredService<HttpXmlUtils>();
            var rawXml = await xmlUtils.LoadAnimeHTTPFromFile(animeID);

            if (rawXml != null)
                continue;

            await _seriesService.QueueAniDBRefresh(animeID, true, false, false);
            queuedAnimeSet.Add(animeID);
        }

        // Queue missing anime needed by existing files.
        index = 0;
        var localEpisodeSet = RepoFactory.AniDB_Episode.GetAll()
            .Select(episode => episode.EpisodeID)
            .ToHashSet();
        var missingAnimeSet = RepoFactory.VideoLocal.GetVideosWithMissingCrossReferenceData()
            .SelectMany(file => file.EpisodeCrossRefs)
            .Where(xref => !queuedAnimeSet.Contains(xref.AnimeID) && (!localAnimeSet.Contains(xref.AnimeID) || !localEpisodeSet.Contains(xref.EpisodeID)))
            .Select(xref => xref.AnimeID)
            .ToHashSet();
        _logger.LogInformation("Queueing {MissingAnimeCount} anime that needs an update…", missingAnimeSet.Count);
        foreach (var animeID in missingAnimeSet)
        {
            if (++index % 10 == 1)
                _logger.LogInformation("Queueing {MissingAnimeCount} anime that needs an update — {CurrentCount}/{MissingAnimeCount}", missingAnimeSet.Count, index + 1, missingAnimeSet.Count);

            await _seriesService.QueueAniDBRefresh(animeID, false, true, true);
            queuedAnimeSet.Add(animeID);
        }

        _logger.LogInformation("Queued {QueuedAnimeCount} anime for an online refresh", queuedAnimeSet.Count);
        return Ok();
    }

    /// <summary>
    /// Regenerate All Episode Matchings for TvDB. Generally, don't do this unless there was an error that was fixed.
    /// In those cases, you'd be told to.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("RegenerateAllTvDBEpisodeMatchings")]
    public ActionResult RegenerateAllEpisodeLinks()
    {
        try
        {
            RepoFactory.CrossRef_AniDB_TvDB_Episode.DeleteAllUnverifiedLinks();
            RepoFactory.AnimeSeries.GetAll().ToList().AsParallel().ForAll(animeSeries => TvDBLinkingHelper.GenerateTvDBEpisodeMatches(animeSeries.AniDB_ID, true));
        }
        catch (Exception e)
        {
            _logger.LogError(e, "{ex}", e.Message);
            return InternalError(e.Message);
        }

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
        await scheduler.StartJob<SyncAniDBMyListJob>();
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
    /// Update AniDB Files with missing file info, including with missing release
    /// groups and/or with out-of-date internal data versions.
    /// </summary>
    /// <param name="missingInfo">Update files with missing release group info</param>
    /// <param name="outOfDate">Update files with and out-of-date internal version.</param>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("UpdateMissingAniDBFileInfo")]
    public async Task<ActionResult> UpdateMissingAniDBFileInfo([FromQuery] bool missingInfo = true, [FromQuery] bool outOfDate = false)
    {
        await _actionService.UpdateAniDBFileData(missingInfo, outOfDate, false);
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
