using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Extensions;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Commands;
using Shoko.Server.Commands.AniDB;
using Shoko.Server.Commands.Plex;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
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
    private readonly ICommandRequestFactory _commandFactory;
    private readonly TraktTVHelper _traktHelper;
    private readonly MovieDBHelper _movieDBHelper;
    private readonly IHttpConnectionHandler _httpHandler;

    public ActionController(ILogger<ActionController> logger, ICommandRequestFactory commandFactory,
        TraktTVHelper traktHelper, MovieDBHelper movieDBHelper, IHttpConnectionHandler httpHandler, ISettingsProvider settingsProvider) : base(settingsProvider)
    {
        _logger = logger;
        _commandFactory = commandFactory;
        _traktHelper = traktHelper;
        _movieDBHelper = movieDBHelper;
        _httpHandler = httpHandler;
    }

    #region Common Actions

    /// <summary>
    /// Run Import. This checks for new files, hashes them etc, scans Drop Folders, checks and scans for community site links (tvdb, trakt, moviedb, etc), and downloads missing images.
    /// </summary>
    /// <returns></returns>
    [HttpGet("RunImport")]
    public ActionResult RunImport()
    {
        ShokoServer.RunImport();
        return Ok();
    }

    /// <summary>
    /// Queues a task to import only new files found in the import folder
    /// </summary>
    /// <returns></returns>
    [HttpGet("ImportNewFiles")]
    public ActionResult ImportNewFiles()
    {
        Importer.RunImport_NewFiles();
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
    public ActionResult SyncVotes()
    {
        _commandFactory.Create<CommandRequest_SyncMyVotes>().Save();
        return Ok();
    }

    /// <summary>
    /// Sync Trakt states. Requires Trakt to be set up, obviously
    /// </summary>
    /// <returns></returns>
    [HttpGet("SyncTrakt")]
    public ActionResult SyncTrakt()
    {
        var settings = SettingsProvider.GetSettings().TraktTv;
        if (!settings.Enabled ||
            string.IsNullOrEmpty(settings.AuthToken))
        {
            return BadRequest();
        }

        _commandFactory.Create<CommandRequest_TraktSyncCollection>(c => c.ForceRefresh = true).Save();

        return Ok();
    }

    /// <summary>
    /// Remove Entries in the Shoko Database for Files that are no longer accessible
    /// </summary>
    /// <returns></returns>
    [HttpGet("RemoveMissingFiles/{removeFromMyList:bool?}")]
    public ActionResult RemoveMissingFiles(bool removeFromMyList = true)
    {
        ShokoServer.RemoveMissingFiles(removeFromMyList);
        return Ok();
    }

    /// <summary>
    /// Update All TvDB Series Info
    /// </summary>
    /// <returns></returns>
    [HttpGet("UpdateAllTvDBInfo")]
    public ActionResult UpdateAllTvDBInfo()
    {
        Importer.RunImport_UpdateTvDB(false);
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
    /// Updates All MovieDB Info
    /// </summary>
    /// <returns></returns>
    [HttpGet("UpdateAllMovieDBInfo")]
    public ActionResult UpdateAllMovieDBInfo()
    {
        Task.Factory.StartNew(() => _movieDBHelper.UpdateAllMovieInfo(true));
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
    /// Validates invalid images and redownloads them
    /// </summary>
    /// <returns></returns>
    [HttpGet("ValidateAllImages")]
    public ActionResult ValidateAllImages()
    {
        _commandFactory.Create<CommandRequest_ValidateAllImages>().Save();
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
    public ActionResult AVDumpMismatchedFiles()
    {
        var allvids = RepoFactory.VideoLocal.GetAll().Where(vid => !vid.IsEmpty() && vid.Media != null)
            .ToDictionary(a => a, a => a.GetAniDBFile());
        Task.Factory.StartNew(() =>
        {
            var list = allvids.Keys.Select(vid => new { vid, anidb = allvids[vid] })
                .Where(_tuple => _tuple.anidb != null)
                .Where(_tuple => !_tuple.anidb.IsDeprecated)
                .Where(_tuple => _tuple.vid.Media?.MenuStreams.Any() != _tuple.anidb.IsChaptered)
                .Select(_tuple => _tuple.vid.GetBestVideoLocalPlace(true)?.FullServerPath)
                .Where(path => !string.IsNullOrEmpty(path)).ToList();
            var index = 0;
            foreach (var path in list)
            {
                _logger.LogInformation($"AVDump Start {index + 1}/{list.Count}: {path}");
                AVDumpHelper.DumpFile(path);
                _logger.LogInformation($"AVDump Finished {index + 1}/{list.Count}: {path}");
                index++;
                _logger.LogInformation($"AVDump Progress: {list.Count - index} remaining");
            }
        });

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
    public ActionResult UpdateMissingAniDBXML()
    {
        // Check existing anime.
        int index = 0;
        var queuedCount = 0;
        var allAnime = RepoFactory.AniDB_Anime.GetAll()
            .Select(a => a.AnimeID)
            .OrderBy(a => a)
            .ToHashSet();
        _logger.LogInformation("Starting the check for {AllAnimeCount} anime XML files", allAnime.Count);
        foreach (var animeID in allAnime)
        {
            if (++index % 10 == 1)
            {
                _logger.LogInformation("Checking anime for XML file {I}/{AllAnimeCount}", index + 1, allAnime.Count);
            }

            var xmlUtils = HttpContext.RequestServices.GetRequiredService<HttpXmlUtils>();
            var rawXml = xmlUtils.LoadAnimeHTTPFromFile(animeID);

            if (rawXml != null)
            {
                continue;
            }

            Series.QueueAniDBRefresh(_commandFactory, _httpHandler, animeID, true, false, false);
            queuedCount++;
        }

        // Queue missing anime needed by existing files.
        index = 0;
        var missingAnime = RepoFactory.VideoLocal.GetVideosWithMissingCrossReferenceData()
            .SelectMany(file => file.EpisodeCrossRefs.Select(xRef => xRef.AnimeID))
            .Distinct()
            .Where(id => !allAnime.Contains(id))
            .ToHashSet();
        _logger.LogInformation("Queueing {MissingAnimeCount} anime XML files", missingAnime.Count);
        foreach (var animeID in missingAnime)
        {
            if (++index % 10 == 1)
            {
                _logger.LogInformation("Queuing missing anime {I}/{MissingAnimeCount}", index + 1, missingAnime.Count);
            }

            Series.QueueAniDBRefresh(_commandFactory, _httpHandler, animeID, true, true, true);
            queuedCount++;
        }

        return Ok();
    }

    /// <summary>
    /// Download any missing anidb release groups.
    /// </summary>
    [Authorize("admin")]
    [HttpGet("DownloadMissingAniDBReleaseGroups")]
    public ActionResult UpdateMissingAniDBReleaseGroups()
    {
        // Queue missing release groups.
        int index = 0;
        var queuedCount = 0;
        var anidbFiles = RepoFactory.AniDB_File.GetAll();
        var anidbReleaseGroupIDs = RepoFactory.AniDB_ReleaseGroup.GetAll()
            .Select(group => group.GroupID)
            .ToHashSet();

        _logger.LogInformation("Starting the check for {AllAniDBFileCount} anidb files for missing release groups", anidbFiles.Count);

        foreach (var anidbFile in anidbFiles)
        {
            if (++index % 10 == 1)
                _logger.LogInformation("Checking anidb files for missing release group {I}/{AllAniDBFileCount}", index + 1, anidbFiles.Count);

            if (anidbReleaseGroupIDs.Contains(anidbFile.GroupID))
                continue;
            
            var command = _commandFactory.Create<CommandRequest_GetReleaseGroup>(c =>
            {
                c.GroupID = anidbFile.GroupID;
                c.ForceRefresh = true;
            });
            command.Save();
        }

        _logger.LogInformation("Queued {UpdatedReleaseGroups} release groups", queuedCount);

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
            RepoFactory.AnimeSeries.GetAll().ToList().AsParallel().ForAll(animeseries =>
                TvDBLinkingHelper.GenerateTvDBEpisodeMatches(animeseries.AniDB_ID, true));
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message);
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
    public ActionResult SyncMyList()
    {
        ShokoServer.SyncMyList();
        return Ok();
    }

    /// <summary>
    /// Update All AniDB Series Info
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("UpdateAllAniDBInfo")]
    public ActionResult UpdateAllAniDBInfo()
    {
        Importer.RunImport_UpdateAllAniDB();
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
        ShokoServer.RefreshAllMediaInfo();
        return Ok();
    }

    /// <summary>
    /// Queues commands to Update All Series Stats and Force a Recalculation of All Group Filters
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("UpdateSeriesStats")]
    public ActionResult UpdateSeriesStats()
    {
        Importer.UpdateAllStats();
        return Ok();
    }

    /// <summary>
    /// Update the AniDB Calendar data for use on the dashboard.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("UpdateAniDBCalendar")]
    public ActionResult UpdateAniDBCalendarData()
    {
        Importer.CheckForCalendarUpdate(true);
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
        Task.Factory.StartNew(() => new AnimeGroupCreator().RecreateAllGroups()).ConfigureAwait(false);
        return Ok();
    }

    /// <summary>
    /// Sync watch states with plex.
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("PlexSyncAll")]
    public ActionResult PlexSyncAll()
    {
        _commandFactory.Create<CommandRequest_PlexSyncWatched>(c => c.User = HttpContext.GetUser()).Save();
        return Ok();
    }
    
    /// <summary>
    /// Forcibly runs AddToMyList commands for all manual links
    /// </summary>
    /// <returns></returns>
    [Authorize("admin")]
    [HttpGet("AddAllManualLinksToMyList")]
    public ActionResult AddAllManualLinksToMyList()
    {
        var cmds = RepoFactory.VideoLocal.GetManuallyLinkedVideos().Select(a => _commandFactory.Create<CommandRequest_AddFileToMyList>(c => c.Hash = a.Hash)).ToList();
        cmds.ForEach(a => a.Save());
        return Ok($"Saved {cmds.Count} AddToMyList Commands");
    }

    #endregion
}
