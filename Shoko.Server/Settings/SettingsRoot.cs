using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.Configuration;
using Shoko.Server.Server;

namespace Shoko.Server.Settings
{
    public class SettingsRoot :IDefaultedConfig
    {
        public string AnimeXmlDirectory { get; set; } = Path.Combine(ServerSettings.ApplicationPath, "Anime_HTTP");

        public string MyListDirectory { get; set; } = Path.Combine(ServerSettings.ApplicationPath, "MyList");

        public ushort ServerPort { get; set; } = 8111;

        [Range(0, 1, ErrorMessage = "PluginAutoWatchThreshold must be between 0 and 1")]
        public double PluginAutoWatchThreshold { get; set; } = 0.89;

        public string Culture { get; set; } = "en";

        /// <summary>
        /// Store json settings inside string
        /// </summary>
        public string WebUI_Settings { get; set; } = "";

        /// <summary>
        /// FirstRun indicates if DB was configured or not, as it needed as backend for user authentication
        /// </summary>
        public bool FirstRun { get; set; } = true;

        public int LegacyRenamerMaxEpisodeLength { get; set; } = 33;

        public LogRotatorSettings LogRotator { get; set; } = new LogRotatorSettings();

        public DatabaseSettings Database { get; set; } = new DatabaseSettings();

        public AniDbSettings AniDb { get; set; } = new AniDbSettings();

        public WebCacheSettings WebCache { get; set; } = new WebCacheSettings();

        public TvDBSettings TvDB { get; set; } = new TvDBSettings();

        public MovieDbSettings MovieDb { get; set; } = new MovieDbSettings();

        public ImportSettings Import { get; set; } = new ImportSettings();

        public PlexSettings Plex { get; set; } = new PlexSettings();

        public PluginSettings Plugins { get; set; } = new PluginSettings();

        public bool AutoGroupSeries { get; set; }

        public string AutoGroupSeriesRelationExclusions { get; set; } = "same setting|character";

        public bool AutoGroupSeriesUseScoreAlgorithm { get; set; }

        public bool FileQualityFilterEnabled { get; set; }

        public FileQualityPreferences FileQualityPreferences { get; set; } = new FileQualityPreferences();

        public HashSet<string> LanguagePreference { get; set; } = new() { "x-jat", "en" };

        public string EpisodeLanguagePreference { get; set; } = string.Empty;

        public bool LanguageUseSynonyms { get; set; } = true;

        public int CloudWatcherTime { get; set; } = 3;

        public DataSourceType EpisodeTitleSource { get; set; } = DataSourceType.AniDB;
        public DataSourceType SeriesDescriptionSource { get; set; } = DataSourceType.AniDB;
        public DataSourceType SeriesNameSource { get; set; } = DataSourceType.AniDB;

        // [JsonIgnore]
        // public string _ImagesPath;

        public string ImagesPath { get; set; }
        // {
        //     get => _ImagesPath;
        //     set
        //     {
        //         _ImagesPath = value;
        //         ServerState.Instance.BaseImagePath = ImageUtils.GetBaseImagesPath();
        //     }
        // }

        public TraktSettings TraktTv { get; set; } = new TraktSettings();

        public string UpdateChannel { get; set; } = "Stable";

        public LinuxSettings Linux { get; set; } = new LinuxSettings();

        public bool TraceLog { get; set; }

        [JsonIgnore]
        public Guid GA_Client
        {
            get
            {
                if (Guid.TryParse(GA_ClientId, out var val)) return val;
                val = Guid.NewGuid();
                GA_ClientId = val.ToString();
                return val;
            }
            set => GA_ClientId = value.ToString();
        }

        public string GA_ClientId { get; set; }

        public bool GA_OptOutPlzDont { get; set; } = false;


        public CL_ServerSettings ToContract()
        {
            return new CL_ServerSettings
            {
                AniDB_Username = AniDb.Username,
                AniDB_Password = AniDb.Password,
                AniDB_ServerAddress = AniDb.ServerAddress,
                AniDB_ServerPort = AniDb.ServerPort.ToString(),
                AniDB_ClientPort = AniDb.ClientPort.ToString(),
                AniDB_AVDumpClientPort = AniDb.AVDumpClientPort.ToString(),
                AniDB_AVDumpKey = AniDb.AVDumpKey,

                AniDB_DownloadRelatedAnime = AniDb.DownloadRelatedAnime,
                AniDB_DownloadSimilarAnime = AniDb.DownloadSimilarAnime,
                AniDB_DownloadReviews = AniDb.DownloadReviews,
                AniDB_DownloadReleaseGroups = AniDb.DownloadReleaseGroups,

                AniDB_MyList_AddFiles = AniDb.MyList_AddFiles,
                AniDB_MyList_StorageState = (int)AniDb.MyList_StorageState,
                AniDB_MyList_DeleteType = (int)AniDb.MyList_DeleteType,
                AniDB_MyList_ReadWatched = AniDb.MyList_ReadWatched,
                AniDB_MyList_ReadUnwatched = AniDb.MyList_ReadUnwatched,
                AniDB_MyList_SetWatched = AniDb.MyList_SetWatched,
                AniDB_MyList_SetUnwatched = AniDb.MyList_SetUnwatched,

                AniDB_MyList_UpdateFrequency = (int)AniDb.MyList_UpdateFrequency,
                AniDB_Calendar_UpdateFrequency = (int)AniDb.Calendar_UpdateFrequency,
                AniDB_Anime_UpdateFrequency = (int)AniDb.Anime_UpdateFrequency,
                AniDB_MyListStats_UpdateFrequency = (int)AniDb.MyListStats_UpdateFrequency,
                AniDB_File_UpdateFrequency = (int)AniDb.File_UpdateFrequency,

                AniDB_DownloadCharacters = AniDb.DownloadCharacters,
                AniDB_DownloadCreators = AniDb.DownloadCreators,
                AniDB_MaxRelationDepth = AniDb.MaxRelationDepth,

                // Web Cache
                WebCache_Address = WebCache.Address,
                WebCache_XRefFileEpisode_Get = WebCache.XRefFileEpisode_Get,
                WebCache_XRefFileEpisode_Send = WebCache.XRefFileEpisode_Send,
                WebCache_TvDB_Get = WebCache.TvDB_Get,
                WebCache_TvDB_Send = WebCache.TvDB_Send,
                WebCache_Trakt_Get = WebCache.Trakt_Get,
                WebCache_Trakt_Send = WebCache.Trakt_Send,

                // TvDB
                TvDB_AutoLink = TvDB.AutoLink,
                TvDB_AutoFanart = TvDB.AutoFanart,
                TvDB_AutoFanartAmount = TvDB.AutoFanartAmount,
                TvDB_AutoPosters = TvDB.AutoPosters,
                TvDB_AutoPostersAmount = TvDB.AutoPostersAmount,
                TvDB_AutoWideBanners = TvDB.AutoWideBanners,
                TvDB_AutoWideBannersAmount = TvDB.AutoWideBannersAmount,
                TvDB_UpdateFrequency = (int)TvDB.UpdateFrequency,
                TvDB_Language = TvDB.Language,

                // MovieDB
                MovieDB_AutoFanart = MovieDb.AutoFanart,
                MovieDB_AutoFanartAmount = MovieDb.AutoFanartAmount,
                MovieDB_AutoPosters = MovieDb.AutoPosters,
                MovieDB_AutoPostersAmount = MovieDb.AutoPostersAmount,

                // Import settings
                VideoExtensions = string.Join(",", Import.VideoExtensions),
                AutoGroupSeries = AutoGroupSeries,
                AutoGroupSeriesUseScoreAlgorithm = AutoGroupSeriesUseScoreAlgorithm,
                AutoGroupSeriesRelationExclusions = AutoGroupSeriesRelationExclusions,
                FileQualityFilterEnabled = FileQualityFilterEnabled,
                FileQualityFilterPreferences = ServerSettings.Serialize(FileQualityPreferences),
                Import_MoveOnImport = Import.MoveOnImport,
                Import_RenameOnImport = Import.RenameOnImport,
                Import_UseExistingFileWatchedStatus = Import.UseExistingFileWatchedStatus,
                RunImportOnStart = Import.RunOnStart,
                ScanDropFoldersOnStart = Import.ScanDropFoldersOnStart,
                Hash_CRC32 = Import.Hash_CRC32,
                Hash_MD5 = Import.Hash_MD5,
                Hash_SHA1 = Import.Hash_SHA1,

                // Language
                LanguagePreference = string.Join(",", LanguagePreference),
                LanguageUseSynonyms = LanguageUseSynonyms,
                EpisodeTitleSource = (int)EpisodeTitleSource,
                SeriesDescriptionSource = (int)SeriesDescriptionSource,
                SeriesNameSource = (int)SeriesNameSource,

                // trakt
                Trakt_IsEnabled = TraktTv.Enabled,
                Trakt_AuthToken = TraktTv.AuthToken,
                Trakt_RefreshToken = TraktTv.RefreshToken,
                Trakt_TokenExpirationDate = TraktTv.TokenExpirationDate,
                Trakt_UpdateFrequency = (int)TraktTv.UpdateFrequency,
                Trakt_SyncFrequency = (int)TraktTv.SyncFrequency,

                // LogRotator
                RotateLogs = LogRotator.Enabled,
                RotateLogs_Delete = LogRotator.Delete,
                RotateLogs_Delete_Days = LogRotator.Delete_Days,
                RotateLogs_Zip = LogRotator.Zip,

                //WebUI
                WebUI_Settings = WebUI_Settings,

                //Plex
                Plex_Sections = string.Join(",", Plex.Libraries),
                Plex_ServerHost = Plex.Server
            };
        }

        [Obsolete("Use IWritableOptions<T>")]
        public void SaveSettings()
        {
            string path = Path.Combine(ServerSettings.ApplicationPath, ServerSettings.SettingsFilename);

            var context = new ValidationContext(ServerSettings.Instance, serviceProvider: null, items: null);
            var results = new List<ValidationResult>();
            var logger = ShokoServer.ServiceContainer.GetService<ILogger<ServerSettings>>();

            if (!Validator.TryValidateObject(ServerSettings.Instance, context, results))
            {
                results.ForEach(s => logger.LogError(s.ErrorMessage));
                throw new ValidationException();
            }

            lock (ServerSettings.SettingsLock)
            {
                string onDisk = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
                string inCode = ServerSettings.Serialize(this, true);
                if (!onDisk.Equals(inCode, StringComparison.Ordinal)) File.WriteAllText(path, inCode);
            }
        }

        public void SetDefaults()
        {
            LogRotator.SetDefaults();
            Database.SetDefaults();
            AniDb.SetDefaults();
            WebCache.SetDefaults();
            TvDB.SetDefaults();
            MovieDb.SetDefaults();
            Import.SetDefaults();
            Plex.SetDefaults();
            Plugins.SetDefaults();
            FileQualityPreferences.SetDefaults();
        }
    }
}
