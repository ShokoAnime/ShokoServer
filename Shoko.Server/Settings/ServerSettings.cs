using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Server.ImageDownload;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Legacy = Shoko.Server.Settings.Migration.ServerSettings_Legacy;
using System.Globalization;

namespace Shoko.Server.Settings
{
    public class ServerSettings
    {
        private const string SettingsFilename = "settings-server.json";
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        //in this way, we could host two ShokoServers int the same machine
        [JsonIgnore]
        public static string DefaultInstance { get; set; } = Assembly.GetEntryAssembly().GetName().Name;
        [JsonIgnore]
        public static string DefaultImagePath => Path.Combine(ApplicationPath, "images");

        [JsonIgnore]
        public static string ApplicationPath
        {
            get
            {
                if (Utils.IsLinux)
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".shoko", DefaultInstance);

                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), DefaultInstance);
            }
        }

        public string AnimeXmlDirectory { get; set; } = Path.Combine(ApplicationPath, "Anime_HTTP");

        public string MyListDirectory { get; set; } = Path.Combine(ApplicationPath, "MyList");

        public ushort ServerPort { get; set; } = 8111;

        [Range(0, 1, ErrorMessage = "PluginAutoWatchThreshold must be between 0 and 1")]
        public double PluginAutoWatchThreshold { get; set; } = 0.89;

        public string Culture { get; set; } = "en";

        /// <summary>
        /// Store json settings inside string
        /// </summary>
        public string WebUI_Settings { get; set; } = "";

        /// <summary>
        /// FirstRun idicates if DB was configured or not, as it needed as backend for user authentication
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

        public bool AutoGroupSeries { get; set; } = false;

        public string AutoGroupSeriesRelationExclusions { get; set; } = "same setting|character";

        public bool AutoGroupSeriesUseScoreAlgorithm { get; set; } = false;

        public bool FileQualityFilterEnabled { get; set; } = false;

        public FileQualityPreferences FileQualityFilterPreferences { get; set; } = new FileQualityPreferences();

        public string[] LanguagePreference { get; set; } = new[] { "x-jat", "en" };

        public string EpisodeLanguagePreference { get; set; } = string.Empty;

        public bool LanguageUseSynonyms { get; set; } = true;

        public int CloudWatcherTime { get; set; } = 3;

        public DataSourceType EpisodeTitleSource { get; set; } = DataSourceType.AniDB;
        public DataSourceType SeriesDescriptionSource { get; set; } = DataSourceType.AniDB;
        public DataSourceType SeriesNameSource { get; set; } = DataSourceType.AniDB;

        [JsonIgnore]
        public string _ImagesPath;

        public string ImagesPath
        {
            get => _ImagesPath;
            set
            {
                _ImagesPath = value;
                ServerState.Instance.BaseImagePath = ImageUtils.GetBaseImagesPath();
            }
        }

        public string VLCLocation { get; set; } = string.Empty;

        public bool MinimizeOnStartup { get; set; } = false;

        public TraktSettings TraktTv { get; set; } = new TraktSettings();

        public string UpdateChannel { get; set; } = "Stable";

        public LinuxSettings Linux { get; set; } = new LinuxSettings();

        public bool TraceLog { get; set; } = false;

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

        public static ServerSettings Instance { get; private set; } = new ServerSettings();

        public static void LoadSettings()
        {
            if (!Directory.Exists(ApplicationPath)) Directory.CreateDirectory(ApplicationPath);
            var path = Path.Combine(ApplicationPath, SettingsFilename);
            if (!File.Exists(path))
            {
                Instance = File.Exists(Path.Combine(ApplicationPath, "settings.json")) ? LoadLegacySettings() : new ServerSettings();
                Instance.SaveSettings();
                return;
            }
            LoadSettingsFromFile(path, false);

            ShokoServer.SetTraceLogging(Instance.TraceLog);
        }

        private static ServerSettings LoadLegacySettings()
        {
            Legacy.LoadSettings();
            var settings = new ServerSettings()
            {
                ImagesPath = Legacy.ImagesPath,
                AnimeXmlDirectory = Legacy.AnimeXmlDirectory,
                MyListDirectory = Legacy.MyListDirectory,
                ServerPort = (ushort)Legacy.JMMServerPort,
                PluginAutoWatchThreshold = double.Parse(Legacy.PluginAutoWatchThreshold, CultureInfo.InvariantCulture),
                Culture = Legacy.Culture,
                WebUI_Settings = Legacy.WebUI_Settings,
                FirstRun = Legacy.FirstRun,
                LogRotator = new LogRotatorSettings()
                {
                    Enabled = Legacy.RotateLogs,
                    Zip = Legacy.RotateLogs_Zip,
                    Delete = Legacy.RotateLogs_Delete,
                    Delete_Days = Legacy.RotateLogs_Delete_Days
                },
                AniDb = new AniDbSettings()
                {
                    Username = Legacy.AniDB_Username,
                    Password = Legacy.AniDB_Password,
                    ServerAddress = Legacy.AniDB_ServerAddress,
                    ServerPort = ushort.Parse(Legacy.AniDB_ServerPort),
                    ClientPort = ushort.Parse(Legacy.AniDB_ClientPort),
                    AVDumpKey = Legacy.AniDB_AVDumpKey,
                    AVDumpClientPort = ushort.Parse(Legacy.AniDB_AVDumpClientPort),
                    DownloadRelatedAnime = Legacy.AniDB_DownloadRelatedAnime,
                    DownloadSimilarAnime = Legacy.AniDB_DownloadSimilarAnime,
                    DownloadReviews = Legacy.AniDB_DownloadReviews,
                    DownloadReleaseGroups = Legacy.AniDB_DownloadReleaseGroups,
                    MyList_AddFiles = Legacy.AniDB_MyList_AddFiles,
                    MyList_StorageState = Legacy.AniDB_MyList_StorageState,
                    MyList_DeleteType = Legacy.AniDB_MyList_DeleteType,
                    MyList_ReadUnwatched = Legacy.AniDB_MyList_ReadUnwatched,
                    MyList_ReadWatched = Legacy.AniDB_MyList_ReadWatched,
                    MyList_SetWatched = Legacy.AniDB_MyList_SetWatched,
                    MyList_SetUnwatched = Legacy.AniDB_MyList_SetUnwatched,
                    MyList_UpdateFrequency = Legacy.AniDB_MyList_UpdateFrequency,
                    Calendar_UpdateFrequency = Legacy.AniDB_Calendar_UpdateFrequency,
                    Anime_UpdateFrequency = Legacy.AniDB_Anime_UpdateFrequency,
                    MyListStats_UpdateFrequency = Legacy.AniDB_MyListStats_UpdateFrequency,
                    File_UpdateFrequency = Legacy.AniDB_File_UpdateFrequency,
                    DownloadCharacters = Legacy.AniDB_DownloadCharacters,
                    DownloadCreators = Legacy.AniDB_DownloadCreators,
                    MaxRelationDepth = Legacy.AniDB_MaxRelationDepth
                },
                WebCache = new WebCacheSettings()
                {
                    Address = Legacy.WebCache_Address,
                    XRefFileEpisode_Get = Legacy.WebCache_XRefFileEpisode_Get,
                    XRefFileEpisode_Send = Legacy.WebCache_XRefFileEpisode_Send,
                    TvDB_Get = Legacy.WebCache_TvDB_Get,
                    TvDB_Send = Legacy.WebCache_TvDB_Send,
                    Trakt_Get = Legacy.WebCache_Trakt_Get,
                    Trakt_Send = Legacy.WebCache_Trakt_Send,
                },
                TvDB = new TvDBSettings()
                {
                    AutoLink = Legacy.TvDB_AutoLink,
                    AutoFanart = Legacy.TvDB_AutoFanart,
                    AutoFanartAmount = Legacy.TvDB_AutoFanartAmount,
                    AutoWideBanners = Legacy.TvDB_AutoWideBanners,
                    AutoWideBannersAmount = Legacy.TvDB_AutoWideBannersAmount,
                    AutoPosters = Legacy.TvDB_AutoPosters,
                    AutoPostersAmount = Legacy.TvDB_AutoPostersAmount,
                    UpdateFrequency = Legacy.TvDB_UpdateFrequency,
                    Language = Legacy.TvDB_Language
                },
                MovieDb = new MovieDbSettings()
                {
                    AutoFanart = Legacy.MovieDB_AutoFanart,
                    AutoFanartAmount = Legacy.MovieDB_AutoFanartAmount,
                    AutoPosters = Legacy.MovieDB_AutoPosters,
                    AutoPostersAmount = Legacy.MovieDB_AutoPostersAmount
                },
                Import = new ImportSettings()
                {
                    VideoExtensions = Legacy.VideoExtensions.Split(','),
                    DefaultSeriesLanguage = Legacy.DefaultSeriesLanguage,
                    DefaultEpisodeLanguage = Legacy.DefaultEpisodeLanguage,
                    RunOnStart = Legacy.RunImportOnStart,
                    ScanDropFoldersOnStart = Legacy.ScanDropFoldersOnStart,
                    Hash_CRC32 = Legacy.Hash_CRC32,
                    Hash_MD5 = Legacy.Hash_MD5,
                    Hash_SHA1 = Legacy.Hash_SHA1,
                    UseExistingFileWatchedStatus = Legacy.Import_UseExistingFileWatchedStatus
                },
                Plex = new PlexSettings()
                {
                    ThumbnailAspects = Legacy.PlexThumbnailAspects,
                    Libraries = Legacy.Plex_Libraries,
                    Token = Legacy.Plex_Token,
                    Server = Legacy.Plex_Server
                },
                AutoGroupSeries = Legacy.AutoGroupSeries,
                AutoGroupSeriesRelationExclusions = Legacy.AutoGroupSeriesRelationExclusions,
                AutoGroupSeriesUseScoreAlgorithm = Legacy.AutoGroupSeriesUseScoreAlgorithm,
                FileQualityFilterEnabled = Legacy.FileQualityFilterEnabled,
                FileQualityFilterPreferences = JsonConvert.DeserializeObject<FileQualityPreferences>(Legacy.FileQualityFilterPreferences),
                LanguagePreference = Legacy.LanguagePreference.Split(','),
                EpisodeLanguagePreference = Legacy.EpisodeLanguagePreference,
                LanguageUseSynonyms = Legacy.LanguageUseSynonyms,
                CloudWatcherTime = Legacy.CloudWatcherTime,
                EpisodeTitleSource = Legacy.EpisodeTitleSource,
                SeriesDescriptionSource = Legacy.SeriesDescriptionSource,
                SeriesNameSource = Legacy.SeriesNameSource,
                VLCLocation = Legacy.VLCLocation,
                MinimizeOnStartup = Legacy.MinimizeOnStartup,
                TraktTv = new TraktSettings()
                {
                    Enabled = Legacy.Trakt_IsEnabled,
                    PIN = Legacy.Trakt_PIN,
                    AuthToken = Legacy.Trakt_AuthToken,
                    RefreshToken = Legacy.Trakt_RefreshToken,
                    TokenExpirationDate = Legacy.Trakt_TokenExpirationDate,
                    UpdateFrequency = Legacy.Trakt_UpdateFrequency,
                    SyncFrequency = Legacy.Trakt_SyncFrequency
                },
                UpdateChannel = Legacy.UpdateChannel,
                Linux = new LinuxSettings()
                {
                    UID = Legacy.Linux_UID,
                    GID = Legacy.Linux_GID,
                    Permission = Legacy.Linux_Permission
                },
                TraceLog = Legacy.TraceLog
            };

            settings.Database = new DatabaseSettings()
            {
                MySqliteDirectory = Legacy.MySqliteDirectory,
                DatabaseBackupDirectory = Legacy.DatabaseBackupDirectory
            };
            settings.Database.Type = Legacy.DatabaseType;
            switch (Legacy.DatabaseType)
            {
                case Constants.DatabaseType.MySQL:
                    settings.Database.Username = Legacy.MySQL_Username;
                    settings.Database.Password = Legacy.MySQL_Password;
                    settings.Database.Schema = Legacy.MySQL_SchemaName;
                    settings.Database.Hostname = Legacy.MySQL_Hostname;
                    break;
                case Constants.DatabaseType.SqlServer:
                    settings.Database.Username = Legacy.DatabaseUsername;
                    settings.Database.Password = Legacy.DatabasePassword;
                    settings.Database.Schema = Legacy.DatabaseName;
                    settings.Database.Hostname = Legacy.DatabaseServer;
                    break;
            }

            return settings;
        }

        public static void LoadSettingsFromFile(string path, bool delete = false)
        {
            Instance = JsonConvert.DeserializeObject<ServerSettings>(File.ReadAllText(path));
            var context = new ValidationContext(Instance, serviceProvider: null, items: null);
            var results = new List<ValidationResult>();

            if (!Validator.TryValidateObject(Instance, context, results))
            {
                results.ForEach(s => Logger.Error(s.ErrorMessage));
                throw new ValidationException();
            }
            if (delete) File.Delete(path);
        }

        public void SaveSettings()
        {
            string path = Path.Combine(ApplicationPath, SettingsFilename);

            var context = new ValidationContext(Instance, serviceProvider: null, items: null);
            var results = new List<ValidationResult>();

            if (!Validator.TryValidateObject(Instance, context, results))
            {
                results.ForEach(s => Logger.Error(s.ErrorMessage));
                throw new ValidationException();
            }

            File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented, new StringEnumConverter { AllowIntegerValues = true }));
        }

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
                FileQualityFilterPreferences = JsonConvert.SerializeObject(FileQualityFilterPreferences),
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

        private static void DumpSettings(object obj, string path = "")
        {
            foreach (var prop in obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (prop.PropertyType.FullName.StartsWith("Shoko.Server")) DumpSettings(prop.GetValue(obj), path + $".{prop.Name}");
                var value = prop.GetValue(obj);

                if (prop.PropertyType.IsArray) value = string.Join(", ", ToEnum((Array) value));
                if (prop.Name.ToLower().EndsWith("password")) value = "***HIDDEN***";

                Logger.Info($"{path}.{prop.Name}: {value}");
            }
        }

        static IEnumerable<object> ToEnum(Array a)
        {
            for (int i = 0; i < a.Length; i++) { yield return a.GetValue(i); }
        }

        public void DebugSettingsToLog()
        {
            #region System Info

            Logger.Info("-------------------- SYSTEM INFO -----------------------");

            Assembly a = Assembly.GetEntryAssembly();
            try
            {
                if (Utils.GetApplicationVersion(a) != null)
                    Logger.Info($"Shoko Server Version: v{Utils.GetApplicationVersion(a)}");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Error in log (server version lookup): {ex}");
            }
            /*
            try
            {
                if (DatabaseFactory.Instance != null)
                    logger.Info($"Database Version: {DatabaseFactory.Instance.GetDatabaseVersion()}");
            }
            catch (Exception ex)
            {
                // oopps, can't create file
                logger.Warn("Error in log (database version lookup: {0}", ex.Message);
            }
            */
            Logger.Info($"Operating System: {Utils.GetOSInfo()}");

            try
            {
                string mediaInfoVersion = "**** MediaInfo - DLL Not found *****";

                string mediaInfoPath = Assembly.GetEntryAssembly().Location;
                FileInfo fi = new FileInfo(mediaInfoPath);
                mediaInfoPath = Path.Combine(fi.Directory.FullName, Environment.Is64BitProcess ? "x64" : "x86",
                    "MediaInfo.dll");

                if (File.Exists(mediaInfoPath))
                {
                    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(mediaInfoPath);
                    mediaInfoVersion =
                        $"MediaInfo DLL {fvi.FileMajorPart}.{fvi.FileMinorPart}.{fvi.FileBuildPart}.{fvi.FilePrivatePart} ({mediaInfoPath})";
                }
                Logger.Info(mediaInfoVersion);

                string hasherInfoVersion = "**** Hasher - DLL NOT found *****";

                string fullHasherexepath = Assembly.GetEntryAssembly().Location;
                fi = new FileInfo(fullHasherexepath);
                fullHasherexepath = Path.Combine(fi.Directory.FullName, Environment.Is64BitProcess ? "x64" : "x86",
                    "hasher.dll");

                if (File.Exists(fullHasherexepath))
                    hasherInfoVersion = $"Hasher DLL found at {fullHasherexepath}";
                Logger.Info(hasherInfoVersion);
            }
            catch (Exception ex)
            {
                Logger.Error("Error in log (hasher / info): {0}", ex.Message);
            }

            Logger.Info("-------------------------------------------------------");

            #endregion

            Logger.Info("----------------- SERVER SETTINGS ----------------------");

            DumpSettings(this, "Settings");

            Logger.Info("-------------------------------------------------------");
        }

        public static event EventHandler<ReasonedEventArgs> ServerShutdown;
        //public static event EventHandler<ReasonedEventArgs> ServerError;
        public static void DoServerShutdown(ReasonedEventArgs args)
        {
            ServerShutdown?.Invoke(null, args);
        }

        public class ReasonedEventArgs : EventArgs
        {
            // ReSharper disable once UnusedAutoPropertyAccessor.Global
            public string Reason { get; set; }
            // ReSharper disable once UnusedAutoPropertyAccessor.Global
            public Exception Exception { get; set; }
        }
    }
}
