using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Shoko.Commons.Extensions;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Models.Server;
using Shoko.Server.API.Annotations;
using Shoko.Server.Extensions;
using Shoko.Server.Filters.Legacy;
using Shoko.Server.Plex;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling;
using Shoko.Server.Scheduling.Jobs.Actions;
using Shoko.Server.Scheduling.Jobs.AniDB;
using Shoko.Server.Scheduling.Jobs.Shoko;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Shoko.Server.Tasks;
using Shoko.Server.Utilities;

namespace Shoko.Server;

[EmitEmptyEnumerableInsteadOfNull]
[ApiController]
[Route("/v1")]
[ApiExplorerSettings(IgnoreApi = true)]
public partial class ShokoServiceImplementation : Controller, IShokoServer
{
    private readonly ILogger<ShokoServiceImplementation> _logger;
    private readonly AnimeGroupCreator _groupCreator;
    private readonly JobFactory _jobFactory;
    private readonly TraktTVHelper _traktHelper;
    private readonly TmdbLinkingService _tmdbLinkingService;
    private readonly TmdbMetadataService _tmdbMetadataService;
    private readonly TmdbSearchService _tmdbSearchService;
    private readonly ISettingsProvider _settingsProvider;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ActionService _actionService;
    private readonly AnimeEpisodeService _episodeService;
    private readonly VideoLocalService _videoLocalService;
    private readonly WatchedStatusService _watchedService;

    public ShokoServiceImplementation(
        TraktTVHelper traktHelper,
        TmdbLinkingService tmdbLinkingService,
        TmdbMetadataService tmdbMetadataService,
        TmdbSearchService tmdbSearchService,
        ISchedulerFactory schedulerFactory,
        ISettingsProvider settingsProvider,
        ILogger<ShokoServiceImplementation> logger,
        ActionService actionService,
        AnimeGroupCreator groupCreator,
        JobFactory jobFactory,
        AnimeEpisodeService episodeService,
        WatchedStatusService watchedService,
        VideoLocalService videoLocalService
    )
    {
        _traktHelper = traktHelper;
        _tmdbLinkingService = tmdbLinkingService;
        _tmdbMetadataService = tmdbMetadataService;
        _tmdbSearchService = tmdbSearchService;
        _schedulerFactory = schedulerFactory;
        _settingsProvider = settingsProvider;
        _logger = logger;
        _actionService = actionService;
        _groupCreator = groupCreator;
        _jobFactory = jobFactory;
        _episodeService = episodeService;
        _watchedService = watchedService;
        _videoLocalService = videoLocalService;
    }

    #region Bookmarks

    [HttpGet("Bookmark")]
    public List<CL_BookmarkedAnime> GetAllBookmarkedAnime()
    {
        return [];
    }

    [HttpPost("Bookmark")]
    public CL_Response<CL_BookmarkedAnime> SaveBookmarkedAnime(CL_BookmarkedAnime contract)
    {
        return new CL_Response<CL_BookmarkedAnime> { ErrorMessage = "No longer supported" };
    }

    [HttpDelete("Bookmark/{bookmarkedAnimeID}")]
    public string DeleteBookmarkedAnime(int bookmarkedAnimeID)
    {
        return "No longer supported";
    }

    [HttpGet("Bookmark/{bookmarkedAnimeID}")]
    public CL_BookmarkedAnime GetBookmarkedAnime(int bookmarkedAnimeID)
    {
        return null;
    }

    #endregion

    #region Status and Changes

    [HttpGet("Changes/{date}/{userID}")]
    public CL_MainChanges GetAllChanges(DateTime date, int userID)
    {
        var c = new CL_MainChanges();
        try
        {
            var changes = ChangeTracker<int>.GetChainedChanges(
            [
                RepoFactory.AnimeGroup.GetChangeTracker(),
                RepoFactory.AnimeGroup_User.GetChangeTracker(userID),
                RepoFactory.AnimeSeries.GetChangeTracker(),
                RepoFactory.AnimeSeries_User.GetChangeTracker(userID)
            ], date);
            var legacyConverter = HttpContext.RequestServices.GetRequiredService<LegacyFilterConverter>();
            c.Filters = new CL_Changes<CL_GroupFilter>
            {
                ChangedItems = legacyConverter.ToClient(RepoFactory.FilterPreset.GetAll(), userID)
                    .Where(a => a != null)
                    .ToList(),
                RemovedItems = [],
                LastChange = DateTime.Now
            };

            c.Groups = new CL_Changes<CL_AnimeGroup_User>();
            changes[0].ChangedItems.UnionWith(changes[1].ChangedItems);
            changes[0].ChangedItems.UnionWith(changes[1].RemovedItems);
            if (changes[1].LastChange > changes[0].LastChange)
            {
                changes[0].LastChange = changes[1].LastChange;
            }

            var groupService = Utils.ServiceContainer.GetRequiredService<AnimeGroupService>();
            c.Groups.ChangedItems = changes[0]
                .ChangedItems.Select(a => RepoFactory.AnimeGroup.GetByID(a))
                .Where(a => a != null)
                .Select(a => groupService.GetV1Contract(a, userID))
                .ToList();


            c.Groups.RemovedItems = changes[0].RemovedItems.ToList();
            c.Groups.LastChange = changes[0].LastChange;
            c.Series = new CL_Changes<CL_AnimeSeries_User>();
            changes[2].ChangedItems.UnionWith(changes[3].ChangedItems);
            changes[2].ChangedItems.UnionWith(changes[3].RemovedItems);
            if (changes[3].LastChange > changes[2].LastChange)
            {
                changes[2].LastChange = changes[3].LastChange;
            }

            var seriesService = Utils.ServiceContainer.GetRequiredService<AnimeSeriesService>();
            c.Series.ChangedItems = changes[2]
                .ChangedItems.Select(a => RepoFactory.AnimeSeries.GetByID(a))
                .Where(a => a != null)
                .Select(a => seriesService.GetV1UserContract(a, userID))
                .ToList();
            c.Series.RemovedItems = changes[2].RemovedItems.ToList();
            c.Series.LastChange = changes[2].LastChange;
            c.LastChange = c.Filters.LastChange;
            if (c.Groups.LastChange > c.LastChange)
            {
                c.LastChange = c.Groups.LastChange;
            }

            if (c.Series.LastChange > c.LastChange)
            {
                c.LastChange = c.Series.LastChange;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return c;
    }

    [HttpGet("GroupFilter/Changes/{date}")]
    public CL_Changes<CL_GroupFilter> GetGroupFilterChanges(DateTime date)
    {
        var c = new CL_Changes<CL_GroupFilter>();
        try
        {
            var legacyConverter = HttpContext.RequestServices.GetRequiredService<LegacyFilterConverter>();
            c.ChangedItems = legacyConverter.ToClient(RepoFactory.FilterPreset.GetAll())
                .Where(a => a != null)
                .ToList();
            c.RemovedItems = [];
            c.LastChange = DateTime.Now;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return c;
    }

    [DatabaseBlockedExempt]
    [HttpGet("Server")]
    public CL_ServerStatus GetServerStatus()
    {
        var contract = new CL_ServerStatus();

        try
        {
            contract.HashQueueCount = 0;
            contract.HashQueueMessage = string.Empty;
            contract.HashQueueState = string.Empty; // Deprecated since 3.6.0.0
            contract.HashQueueStateId = 0;
            contract.HashQueueStateParams = [];
            contract.GeneralQueueCount = 0;
            contract.GeneralQueueMessage = string.Empty;
            contract.GeneralQueueState = string.Empty; // Deprecated since 3.6.0.0
            contract.GeneralQueueStateId = 0;
            contract.GeneralQueueStateParams = [];
            contract.ImagesQueueCount = 0;
            contract.ImagesQueueMessage = string.Empty;
            contract.ImagesQueueState = string.Empty; // Deprecated since 3.6.0.0
            contract.ImagesQueueStateId = 0;
            contract.ImagesQueueStateParams = [];

            var udp = HttpContext.RequestServices.GetRequiredService<IUDPConnectionHandler>();
            var http = HttpContext.RequestServices.GetRequiredService<IHttpConnectionHandler>();
            if (http.IsBanned)
            {
                contract.IsBanned = true;
                contract.BanReason = http.BanTime?.ToString();
                contract.BanOrigin = @"HTTP";
            }
            else if (udp.IsBanned)
            {
                contract.IsBanned = true;
                contract.BanReason = udp.BanTime?.ToString();
                contract.BanOrigin = @"UDP";
            }
            else
            {
                contract.IsBanned = false;
                contract.BanReason = string.Empty;
                contract.BanOrigin = string.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return contract;
    }

    [HttpGet("Server/Versions")]
    public CL_AppVersions GetAppVersions()
    {
        try
        {
            return new CL_AppVersions();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return null;
    }

    #endregion

    [HttpGet("Years")]
    public List<string> GetAllYears()
    {
        return RepoFactory.AnimeSeries.GetAllYears().Select(a => a.ToString()).ToList();
    }

    [HttpGet("Seasons")]
    public List<string> GetAllSeasons()
    {
        return RepoFactory.AnimeSeries.GetAllSeasons().Select(a => a.Season + " " + a).ToList();
    }

    [HttpGet("Tags")]
    public List<string> GetAllTagNames()
    {
        var allTagNames = new List<string>();

        try
        {
            var start = DateTime.Now;

            foreach (var tag in RepoFactory.AniDB_Tag.GetAll())
            {
                allTagNames.Add(tag.TagName);
            }

            allTagNames.Sort();

            var ts = DateTime.Now - start;
            _logger.LogInformation("GetAllTagNames  in {Time} ms", ts.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return allTagNames;
    }

    [HttpPost("CloudAccount/Directory")]
    public List<string> DirectoriesFromImportFolderPath([FromForm] string path)
    {
        if (path == null)
        {
            return [];
        }

        try
        {
            return !Directory.Exists(path)
                ? []
                : new DirectoryInfo(path).EnumerateDirectories().Select(a => a.FullName).OrderByNatural(a => a)
                    .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return [];
    }

    #region Settings

    [HttpPost("Server/Settings")]
    public CL_Response SaveServerSettings(CL_ServerSettings contractIn)
    {
        var contract = new CL_Response { ErrorMessage = string.Empty };
        try
        {
            var settings = _settingsProvider.GetSettings();
            // validate the settings
            var anidbSettingsChanged = false;
            if (ushort.TryParse(contractIn.AniDB_ClientPort, out var newAniDB_ClientPort) &&
                newAniDB_ClientPort != settings.AniDb.ClientPort)
            {
                anidbSettingsChanged = true;
                contract.ErrorMessage += "AniDB Client Port must be numeric and greater than 0" +
                                         Environment.NewLine;
            }

            if (contractIn.AniDB_Username != settings.AniDb.Username)
            {
                anidbSettingsChanged = true;
                if (string.IsNullOrEmpty(contractIn.AniDB_Username))
                {
                    contract.ErrorMessage += "AniDB User Name must have a value" + Environment.NewLine;
                }
            }

            if (contractIn.AniDB_Password != settings.AniDb.Password)
            {
                anidbSettingsChanged = true;
                if (string.IsNullOrEmpty(contractIn.AniDB_Password))
                {
                    contract.ErrorMessage += "AniDB Password must have a value" + Environment.NewLine;
                }
            }

            if (!ushort.TryParse(contractIn.AniDB_AVDumpClientPort, out var newAniDB_AVDumpClientPort))
            {
                contract.ErrorMessage += "AniDB AVDump port must be a valid port" + Environment.NewLine;
            }


            if (contract.ErrorMessage.Length > 0)
            {
                return contract;
            }

            settings.AniDb.ClientPort = newAniDB_ClientPort;
            settings.AniDb.Password = contractIn.AniDB_Password;
            settings.AniDb.Username = contractIn.AniDB_Username;
            settings.AniDb.AVDumpClientPort = newAniDB_AVDumpClientPort;
            settings.AniDb.AVDumpKey = contractIn.AniDB_AVDumpKey;

            settings.AniDb.DownloadRelatedAnime = contractIn.AniDB_DownloadRelatedAnime;
            settings.AniDb.DownloadReleaseGroups = contractIn.AniDB_DownloadReleaseGroups;
            settings.AniDb.DownloadReviews = contractIn.AniDB_DownloadReviews;

            settings.AniDb.MyList_AddFiles = contractIn.AniDB_MyList_AddFiles;
            settings.AniDb.MyList_ReadUnwatched = contractIn.AniDB_MyList_ReadUnwatched;
            settings.AniDb.MyList_ReadWatched = contractIn.AniDB_MyList_ReadWatched;
            settings.AniDb.MyList_SetUnwatched = contractIn.AniDB_MyList_SetUnwatched;
            settings.AniDb.MyList_SetWatched = contractIn.AniDB_MyList_SetWatched;
            settings.AniDb.MyList_StorageState = (AniDBFile_State)contractIn.AniDB_MyList_StorageState;
            settings.AniDb.MyList_DeleteType = (AniDBFileDeleteType)contractIn.AniDB_MyList_DeleteType;
            //settings.AniDb.MaxRelationDepth = contractIn.AniDB_MaxRelationDepth;

            settings.AniDb.MyList_UpdateFrequency =
                (ScheduledUpdateFrequency)contractIn.AniDB_MyList_UpdateFrequency;
            settings.AniDb.Calendar_UpdateFrequency =
                (ScheduledUpdateFrequency)contractIn.AniDB_Calendar_UpdateFrequency;
            settings.AniDb.Anime_UpdateFrequency =
                (ScheduledUpdateFrequency)contractIn.AniDB_Anime_UpdateFrequency;
            settings.AniDb.File_UpdateFrequency =
                (ScheduledUpdateFrequency)contractIn.AniDB_File_UpdateFrequency;

            settings.AniDb.DownloadCharacters = contractIn.AniDB_DownloadCharacters;
            settings.AniDb.DownloadCreators = contractIn.AniDB_DownloadCreators;

            // TMDB
            settings.TMDB.AutoDownloadBackdrops = contractIn.MovieDB_AutoFanart;
            settings.TMDB.MaxAutoBackdrops = contractIn.MovieDB_AutoFanartAmount;
            settings.TMDB.AutoDownloadPosters = contractIn.MovieDB_AutoPosters;
            settings.TMDB.MaxAutoPosters = contractIn.MovieDB_AutoPostersAmount;

            // Import settings
            settings.Import.VideoExtensions = contractIn.VideoExtensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            settings.Import.UseExistingFileWatchedStatus =
                contractIn.Import_UseExistingFileWatchedStatus;
            settings.AutoGroupSeries = contractIn.AutoGroupSeries;
            settings.AutoGroupSeriesUseScoreAlgorithm = contractIn.AutoGroupSeriesUseScoreAlgorithm;
            settings.AutoGroupSeriesRelationExclusions = contractIn.AutoGroupSeriesRelationExclusions.Replace("alternate", "alternative", StringComparison.InvariantCultureIgnoreCase).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            settings.FileQualityFilterEnabled = contractIn.FileQualityFilterEnabled;
            if (!string.IsNullOrEmpty(contractIn.FileQualityFilterPreferences))
            {
                settings.FileQualityPreferences =
                    SettingsProvider.Deserialize<FileQualityPreferences>(contractIn.FileQualityFilterPreferences);
            }

            settings.Import.RunOnStart = contractIn.RunImportOnStart;
            settings.Import.ScanDropFoldersOnStart = contractIn.ScanDropFoldersOnStart;
            settings.Import.Hasher.CRC = contractIn.Hash_CRC32;
            settings.Import.Hasher.MD5 = contractIn.Hash_MD5;
            settings.Import.Hasher.SHA1 = contractIn.Hash_SHA1;
            settings.Plugins.Renamer.RenameOnImport = contractIn.Import_RenameOnImport;
            settings.Plugins.Renamer.MoveOnImport = contractIn.Import_MoveOnImport;
            settings.Import.SkipDiskSpaceChecks = contractIn.SkipDiskSpaceChecks;

            // Language
            settings.Language.SeriesTitleLanguageOrder = contractIn.LanguagePreference.Split(',').ToList();
            settings.Language.UseSynonyms = contractIn.LanguageUseSynonyms;
            settings.Language.EpisodeTitleSourceOrder = [(DataSourceType)contractIn.EpisodeTitleSource];
            settings.Language.DescriptionSourceOrder = [(DataSourceType)contractIn.SeriesDescriptionSource];
            settings.Language.SeriesTitleSourceOrder = [(DataSourceType)contractIn.SeriesNameSource];

            // Trakt
            settings.TraktTv.Enabled = contractIn.Trakt_IsEnabled;
            settings.TraktTv.AuthToken = contractIn.Trakt_AuthToken;
            settings.TraktTv.RefreshToken = contractIn.Trakt_RefreshToken;
            settings.TraktTv.TokenExpirationDate = contractIn.Trakt_TokenExpirationDate;
            settings.TraktTv.UpdateFrequency =
                (ScheduledUpdateFrequency)contractIn.Trakt_UpdateFrequency;
            settings.TraktTv.SyncFrequency = (ScheduledUpdateFrequency)contractIn.Trakt_SyncFrequency;

            //Plex
            settings.Plex.Server = contractIn.Plex_ServerHost;
            settings.Plex.Libraries = contractIn.Plex_Sections.Length > 0
                ? contractIn.Plex_Sections.Split(',').Select(int.Parse).ToList()
                : [];

            // SAVE!
            _settingsProvider.SaveSettings();

            if (anidbSettingsChanged)
            {
                var handler = HttpContext.RequestServices.GetRequiredService<IUDPConnectionHandler>();

                handler.ForceLogout();
                handler.CloseConnections();

                Thread.Sleep(1000);
                handler.Init(settings.AniDb.Username, settings.AniDb.Password,
                    settings.AniDb.UDPServerAddress,
                    settings.AniDb.UDPServerPort, settings.AniDb.ClientPort);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Save server settings exception:\\n {Ex}", ex);
            contract.ErrorMessage = ex.Message;
        }

        return contract;
    }

    [HttpGet("Server/Settings")]
    public CL_ServerSettings GetServerSettings()
    {
        var contract = new CL_ServerSettings();

        try
        {
            var settings = _settingsProvider.GetSettings();
            return settings.ToContract();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return contract;
    }

    #endregion

    #region Actions

    [HttpPost("Folder/Import")]
    public async void RunImport()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.StartJob<ImportJob>();
    }

    [HttpPost("File/Hashes/Sync")]
    public void SyncHashes()
    {
    }

    [HttpPost("Folder/Scan")]
    public void ScanDropFolders()
    {
        Utils.ServiceContainer.GetRequiredService<ActionService>().RunImport_DropFolders().GetAwaiter().GetResult();
    }

    [HttpPost("Folder/Scan/{importFolderID}")]
    public void ScanFolder(int importFolderID)
    {
        var scheduler = _schedulerFactory.GetScheduler().GetAwaiter().GetResult();
        scheduler.StartJob<ScanFolderJob>(a => a.ImportFolderID = importFolderID).GetAwaiter().GetResult();
    }

    [HttpPost("Folder/RemoveMissing")]
    public void RemoveMissingFiles()
    {
        _actionService.RemoveRecordsWithoutPhysicalFiles().GetAwaiter().GetResult();
    }

    [HttpPost("Folder/RefreshMediaInfo")]
    public void RefreshAllMediaInfo()
    {
        Utils.ShokoServer.RefreshAllMediaInfo();
    }

    [HttpPost("AniDB/MyList/Sync")]
    public void SyncMyList()
    {
        var scheduler = _schedulerFactory.GetScheduler().GetAwaiter().GetResult();
        scheduler.StartJobNow<SyncAniDBMyListJob>(c => c.ForceRefresh = true).GetAwaiter().GetResult();
    }

    [HttpPost("AniDB/Vote/Sync")]
    public void SyncVotes()
    {
        var scheduler = _schedulerFactory.GetScheduler().GetAwaiter().GetResult();
        scheduler.StartJob<SyncAniDBMyListJob>().GetAwaiter().GetResult();
    }

    #endregion

    #region Queue Actions

    [HttpPost("CommandQueue/Hasher/{paused}")]
    public void SetCommandProcessorHasherPaused(bool paused)
    {
    }

    [HttpPost("CommandQueue/General/{paused}")]
    public void SetCommandProcessorGeneralPaused(bool paused)
    {
    }

    [HttpPost("CommandQueue/Images/{paused}")]
    public void SetCommandProcessorImagesPaused(bool paused)
    {
    }

    [HttpDelete("CommandQueue/Hasher")]
    public void ClearHasherQueue()
    {
        try
        {

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }
    }

    [HttpDelete("CommandQueue/Images")]
    public void ClearImagesQueue()
    {
        try
        {

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }
    }

    [HttpDelete("CommandQueue/General")]
    public void ClearGeneralQueue()
    {
        try
        {

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }
    }

    #endregion

    [HttpPost("AniDB/Status")]
    public string TestAniDBConnection()
    {
        var log = string.Empty;
        try
        {
            var handler = HttpContext.RequestServices.GetRequiredService<IUDPConnectionHandler>();
            log += "Disposing..." + Environment.NewLine;
            handler.ForceLogout();
            handler.CloseConnections();

            log += "Init..." + Environment.NewLine;
            var settings = _settingsProvider.GetSettings();
            handler.Init(settings.AniDb.Username, settings.AniDb.Password,
                settings.AniDb.UDPServerAddress,
                settings.AniDb.UDPServerPort, settings.AniDb.ClientPort);

            log += "Login..." + Environment.NewLine;
            if (handler.Login().Result)
            {
                log += "Login Success!" + Environment.NewLine;
                log += "Logout..." + Environment.NewLine;
                handler.ForceLogout();
                log += "Logged out" + Environment.NewLine;
            }
            else
            {
                log += "Login FAILED!" + Environment.NewLine;
            }

            return log;
        }
        catch (Exception ex)
        {
            log += ex.Message + Environment.NewLine;
        }

        return log;
    }

    [HttpGet("MediaInfo/Quality")]
    public List<string> GetAllUniqueVideoQuality()
    {
        try
        {
            return RepoFactory.AniDB_File.GetAll().Select(a => a.File_Source).Distinct().OrderBy(a => a).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return [];
        }
    }

    [HttpGet("MediaInfo/AudioLanguages")]
    public List<string> GetAllUniqueAudioLanguages()
    {
        try
        {
            return RepoFactory.CrossRef_Languages_AniDB_File.GetAll().Select(a => a.LanguageName).Distinct()
                .OrderBy(a => a).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return [];
        }
    }

    [HttpGet("MediaInfo/SubtitleLanguages")]
    public List<string> GetAllUniqueSubtitleLanguages()
    {
        try
        {
            return RepoFactory.CrossRef_Subtitles_AniDB_File.GetAll().Select(a => a.LanguageName).Distinct()
                .OrderBy(a => a).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return [];
        }
    }

    #region Plex

    [HttpGet("User/Plex/LoginUrl/{userID}")]
    public string LoginUrl(int userID)
    {
        JMMUser user = RepoFactory.JMMUser.GetByID(userID);
        return PlexHelper.GetForUser(user).LoginUrl;
    }

    [HttpGet("User/Plex/Authenticated/{userID}")]
    public bool IsPlexAuthenticated(int userID)
    {
        JMMUser user = RepoFactory.JMMUser.GetByID(userID);
        return PlexHelper.GetForUser(user).IsAuthenticated;
    }

    [HttpGet("User/Plex/Remove/{userID}")]
    public bool RemovePlexAuth(int userID)
    {
        JMMUser user = RepoFactory.JMMUser.GetByID(userID);
        PlexHelper.GetForUser(user).InvalidateToken();
        return true;
    }

    #endregion

    [HttpPost("Image/Enable/{enabled}/{imageID}/{imageType}")]
    public string EnableDisableImage(bool enabled, int imageID, int imageType)
    {
        try
        {
            var it = (CL_ImageEntityType)imageType;
            if (!ImageUtils.SetEnabled(it.ToServerSource(), it.ToServerType(), imageID, enabled))
                return "Could not find image";

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }
    }

    [HttpPost("Image/Default/{isDefault}/{animeID}/{imageID}/{imageType}/{imageSizeType}")]
    public string SetDefaultImage(bool isDefault, int animeID, int imageID, int imageType, int imageSizeType)
    {
        try
        {
            var imageEntityType = ((CL_ImageEntityType)imageType).ToServerType();
            var dataSource = ((CL_ImageEntityType)imageType).ToServerSource();

            // Reset the image preference.
            if (!isDefault)
            {
                var defaultImage = RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(animeID, imageEntityType);
                if (defaultImage != null)
                    RepoFactory.AniDB_Anime_PreferredImage.Delete(defaultImage);
            }
            // Mark the image as the preferred/default for it's type.
            else
            {
                var defaultImage = RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(animeID, imageEntityType) ?? new(animeID, imageEntityType);
                defaultImage.ImageID = imageID;
                defaultImage.ImageSource = dataSource;
                RepoFactory.AniDB_Anime_PreferredImage.Save(defaultImage);
            }

            if (animeID != 0)
            {
                var scheduler = _schedulerFactory.GetScheduler().ConfigureAwait(false).GetAwaiter().GetResult();
                scheduler.StartJob<RefreshAnimeStatsJob>(a => a.AnimeID = animeID).GetAwaiter().GetResult();
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
            return ex.Message;
        }
    }

    #region Calendar (Dashboard)

    [HttpGet("AniDB/Anime/Calendar/{userID}/{numberOfDays}")]
    public List<CL_AniDB_Anime> GetMiniCalendar(int userID, int numberOfDays)
    {
        // get all the series
        var animeList = new List<CL_AniDB_Anime>();

        try
        {
            var aniDBAnimeService = Utils.ServiceContainer.GetRequiredService<AniDB_AnimeService>();
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user == null)
            {
                return animeList;
            }

            var animes = RepoFactory.AniDB_Anime.GetForDate(
                DateTime.Today.AddDays(0 - numberOfDays),
                DateTime.Today.AddDays(numberOfDays));
            foreach (var anime in animes)
            {
                if (!user.AllowedAnime(anime)) continue;
                animeList.Add(aniDBAnimeService.GetV1Contract(anime));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return animeList;
    }

    [HttpGet("AniDB/Anime/ForMonth/{userID}/{month}/{year}")]
    public List<CL_AniDB_Anime> GetAnimeForMonth(int userID, int month, int year)
    {
        // get all the series
        var animeList = new List<CL_AniDB_Anime>();

        try
        {
            var aniDBAnimeService = Utils.ServiceContainer.GetRequiredService<AniDB_AnimeService>();
            var user = RepoFactory.JMMUser.GetByID(userID);
            if (user == null)
            {
                return animeList;
            }

            var startDate = new DateTime(year, month, 1, 0, 0, 0);
            var endDate = startDate.AddMonths(1);
            endDate = endDate.AddMinutes(-10);

            var animes = RepoFactory.AniDB_Anime.GetForDate(startDate, endDate);
            foreach (var anime in animes)
            {
                if (!user.AllowedAnime(anime)) continue;
                animeList.Add(aniDBAnimeService.GetV1Contract(anime));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return animeList;
    }

    [HttpPost("AniDB/Anime/Calendar/Update")]
    public string UpdateCalendarData()
    {
        try
        {
            Utils.ServiceContainer.GetRequiredService<ActionService>().CheckForCalendarUpdate(true).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return string.Empty;
    }

    /*public List<Contract_AniDBAnime> GetMiniCalendar(int numberOfDays)
    {
        AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
        JMMUserRepository repUsers = new JMMUserRepository();

        // get all the series
        List<Contract_AniDBAnime> animeList = new List<Contract_AniDBAnime>();

        try
        {

            List<AniDB_Anime> animes = repAnime.GetForDate(DateTime.Today.AddDays(0 - numberOfDays), DateTime.Today.AddDays(numberOfDays));
            foreach (AniDB_Anime anime in animes)
            {

                    animeList.Add(anime.ToContract());
            }

        }
        catch (Exception ex)
        {
            logger.LogError( ex,ex.ToString());
        }
        return animeList;
    }*/

    #endregion

    /// <summary>
    /// Returns a list of recommendations based on the users votes
    /// </summary>
    /// <param name="maxResults"></param>
    /// <param name="userID"></param>
    /// <param name="recommendationType">1 = to watch, 2 = to download</param>
    [HttpGet("Recommendation/{maxResults}/{userID}/{recommendationType}")]
    public List<CL_Recommendation> GetRecommendations(int maxResults, int userID, int recommendationType)
    {
        var recs = new List<CL_Recommendation>();

        try
        {
            var aniDBAnimeService = Utils.ServiceContainer.GetRequiredService<AniDB_AnimeService>();
            var seriesService = Utils.ServiceContainer.GetRequiredService<AnimeSeriesService>();
            var juser = RepoFactory.JMMUser.GetByID(userID);
            if (juser == null)
            {
                return recs;
            }

            // get all the anime the user has chosen to ignore
            var ignoreType = 1;
            switch (recommendationType)
            {
                case 1:
                    ignoreType = 1;
                    break;
                case 2:
                    ignoreType = 2;
                    break;
            }

            var ignored = RepoFactory.IgnoreAnime.GetByUserAndType(userID, ignoreType);
            var dictIgnored = new Dictionary<int, IgnoreAnime>();
            foreach (var ign in ignored)
            {
                dictIgnored[ign.AnimeID] = ign;
            }


            // find all the series which the user has rated
            var allVotes = RepoFactory.AniDB_Vote.GetAll()
                .OrderByDescending(a => a.VoteValue)
                .ToList();
            if (allVotes.Count == 0)
            {
                return recs;
            }


            var dictRecs = new Dictionary<int, CL_Recommendation>();

            foreach (var vote in allVotes)
            {
                if (vote.VoteType != (int)AniDBVoteType.Anime &&
                    vote.VoteType != (int)AniDBVoteType.AnimeTemp)
                {
                    continue;
                }

                if (dictIgnored.ContainsKey(vote.EntityID))
                {
                    continue;
                }

                // check if the user has this anime
                var anime = RepoFactory.AniDB_Anime.GetByAnimeID(vote.EntityID);
                if (anime == null)
                {
                    continue;
                }

                // get similar anime
                var simAnime = anime.SimilarAnime
                    .OrderByDescending(a => a.GetApprovalPercentage())
                    .ToList();
                // sort by the highest approval

                foreach (var link in simAnime)
                {
                    if (dictIgnored.ContainsKey(link.SimilarAnimeID))
                    {
                        continue;
                    }

                    var animeLink = RepoFactory.AniDB_Anime.GetByAnimeID(link.SimilarAnimeID);
                    if (animeLink != null)
                    {
                        if (!juser.AllowedAnime(animeLink))
                        {
                            continue;
                        }
                    }

                    // don't recommend to watch anime that the user doesn't have
                    if (animeLink == null && recommendationType == 1)
                    {
                        continue;
                    }

                    // don't recommend to watch series that the user doesn't have
                    var ser = RepoFactory.AnimeSeries.GetByAnimeID(link.SimilarAnimeID);
                    if (ser == null && recommendationType == 1)
                    {
                        continue;
                    }


                    if (ser != null)
                    {
                        // don't recommend to watch series that the user has already started watching
                        var userRecord = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(userID, ser.AnimeSeriesID);
                        if (userRecord != null)
                        {
                            if (userRecord.WatchedEpisodeCount > 0 && recommendationType == 1)
                            {
                                continue;
                            }
                        }

                        // don't recommend to download anime that the user has files for
                        if (ser.LatestLocalEpisodeNumber > 0 && recommendationType == 2)
                        {
                            continue;
                        }
                    }

                    var rec = new CL_Recommendation
                    {
                        BasedOnAnimeID = anime.AnimeID,
                        RecommendedAnimeID = link.SimilarAnimeID,
                    };

                    // if we don't have the anime locally. lets assume the anime has a high rating
                    decimal animeRating = 850;
                    if (animeLink != null)
                    {
                        animeRating = animeLink.GetAniDBRating();
                    }

                    rec.Score =
                        CalculateRecommendationScore(vote.VoteValue, link.GetApprovalPercentage(), animeRating);
                    rec.BasedOnVoteValue = vote.VoteValue;
                    rec.RecommendedApproval = link.GetApprovalPercentage();

                    // check if we have added this recommendation before
                    // this might happen where animes are recommended based on different votes
                    // and could end up with different scores
                    if (dictRecs.TryGetValue(rec.RecommendedAnimeID, out var recommendation))
                    {
                        if (rec.Score < recommendation.Score)
                        {
                            continue;
                        }
                    }

                    rec.Recommended_AniDB_Anime = null;
                    if (animeLink != null)
                    {
                        rec.Recommended_AniDB_Anime = aniDBAnimeService.GetV1Contract(animeLink);
                    }

                    rec.BasedOn_AniDB_Anime = aniDBAnimeService.GetV1Contract(anime);

                    rec.Recommended_AnimeSeries = null;
                    if (ser != null)
                    {
                        rec.Recommended_AnimeSeries = seriesService.GetV1UserContract(ser, userID);
                    }

                    var serBasedOn = RepoFactory.AnimeSeries.GetByAnimeID(anime.AnimeID);
                    if (serBasedOn == null)
                    {
                        continue;
                    }

                    rec.BasedOn_AnimeSeries = seriesService.GetV1UserContract(serBasedOn, userID);

                    dictRecs[rec.RecommendedAnimeID] = rec;
                }
            }

            var tempRecs = new List<CL_Recommendation>();
            foreach (var rec in dictRecs.Values)
            {
                tempRecs.Add(rec);
            }

            // sort by the highest score

            var numRecs = 0;
            foreach (var rec in tempRecs.OrderByDescending(a => a.Score))
            {
                if (numRecs == maxResults)
                {
                    break;
                }

                recs.Add(rec);
                numRecs++;
            }

            if (recs.Count == 0)
            {
                return recs;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return recs;
    }

    private double CalculateRecommendationScore(int userVoteValue, double approvalPercentage, decimal animeRating)
    {
        double score = userVoteValue;

        score = score + approvalPercentage;

        if (approvalPercentage > 90)
        {
            score = score + 100;
        }

        if (approvalPercentage > 80)
        {
            score = score + 100;
        }

        if (approvalPercentage > 70)
        {
            score = score + 100;
        }

        if (approvalPercentage > 60)
        {
            score = score + 100;
        }

        if (approvalPercentage > 50)
        {
            score = score + 100;
        }

        if (animeRating > 900)
        {
            score = score + 100;
        }

        if (animeRating > 800)
        {
            score = score + 100;
        }

        if (animeRating > 700)
        {
            score = score + 100;
        }

        if (animeRating > 600)
        {
            score = score + 100;
        }

        if (animeRating > 500)
        {
            score = score + 100;
        }

        return score;
    }

    [HttpGet("AniDB/ReleaseGroup/{animeID}")]
    public List<CL_AniDB_GroupStatus> GetReleaseGroupsForAnime(int animeID)
    {
        var relGroups = new List<CL_AniDB_GroupStatus>();

        try
        {
            var series = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
            if (series == null)
            {
                return relGroups;
            }

            // get a list of all the release groups the user is collecting
            //List<int> userReleaseGroups = new List<int>();
            var userReleaseGroups = new Dictionary<int, int>();
            foreach (var ep in series.AllAnimeEpisodes)
            {
                var vids = ep.VideoLocals;
                var hashes = vids.Where(a => !string.IsNullOrEmpty(a.Hash)).Select(a => a.Hash).ToList();
                foreach (var h in hashes)
                {
                    var vid = vids.First(a => a.Hash == h);
                    AniDB_File anifile = vid.AniDBFile;
                    if (anifile != null)
                    {
                        if (!userReleaseGroups.ContainsKey(anifile.GroupID))
                        {
                            userReleaseGroups[anifile.GroupID] = 0;
                        }

                        userReleaseGroups[anifile.GroupID] = userReleaseGroups[anifile.GroupID] + 1;
                    }
                }
            }

            // get all the release groups for this series
            var grpStatuses = RepoFactory.AniDB_GroupStatus.GetByAnimeID(animeID);
            foreach (var gs in grpStatuses)
            {
                var cl = gs.ToClient();
                if (userReleaseGroups.ContainsKey(gs.GroupID))
                {
                    cl.UserCollecting = true;
                    cl.FileCount = userReleaseGroups[gs.GroupID];
                }
                else
                {
                    cl.UserCollecting = false;
                    cl.FileCount = 0;
                }

                relGroups.Add(cl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return relGroups;
    }

    [HttpGet("AniDB/Character/{animeID}")]
    public List<CL_AniDB_Character> GetCharactersForAnime(int animeID)
    {
        var chars = new List<CL_AniDB_Character>();

        try
        {
            var aniDBAnimeService = Utils.ServiceContainer.GetRequiredService<AniDB_AnimeService>();
            var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
            return aniDBAnimeService.GetCharactersContract(anime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return chars;
    }

    [HttpGet("AniDB/Character/FromSeiyuu/{seiyuuID}")]
    public List<CL_AniDB_Character> GetCharactersForSeiyuu(int seiyuuID)
    {
        var chars = new List<CL_AniDB_Character>();

        try
        {
            var seiyuu = RepoFactory.AniDB_Creator.GetByID(seiyuuID);
            if (seiyuu == null)
            {
                return chars;
            }

            var links = RepoFactory.AniDB_Character_Creator.GetByCreatorID(seiyuu.CreatorID);

            foreach (var chrSei in links)
            {
                var chr = RepoFactory.AniDB_Character.GetByID(chrSei.CharacterID);
                if (chr != null)
                {
                    var aniChars =
                        RepoFactory.AniDB_Anime_Character.GetByCharID(chr.CharID);
                    if (aniChars.Count > 0)
                    {
                        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(aniChars[0].AnimeID);
                        if (anime != null)
                        {
                            var cl = chr.ToClient();
                            chars.Add(cl);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return chars;
    }

    [HttpGet("AniDB/Seiyuu/{seiyuuID}")]
    public CL_AniDB_Seiyuu GetAniDBSeiyuu(int seiyuuID)
    {
        try
        {
            return RepoFactory.AniDB_Creator.GetByCreatorID(seiyuuID)?.ToClient();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Ex}", ex);
        }

        return null;
    }
}
