using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using Shoko.Commons.Utils;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Server.ImageDownload;
using Shoko.Server.Server;
using Shoko.Server.Settings.DI;
using Shoko.Server.Utilities;
using Constants = Shoko.Server.Server.Constants;
using Formatting = Newtonsoft.Json.Formatting;
using Legacy = Shoko.Server.Settings.Migration.ServerSettings_Legacy;

namespace Shoko.Server.Settings
{
    public class ServerSettings
    {
        private const string SettingsFilename = "settings-server.json";
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static readonly object SettingsLock = new object();

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

        public int CachingDatabaseTimeout { get; set; } = 180;

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

        public List<string> LanguagePreference { get; set; } = new List<string> { "x-jat", "en" };

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

        public static ServerSettings Instance { get; private set; } = new ServerSettings();
        public bool LogWebRequests { get; set; }

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
            LoadSettingsFromFile(path);
            Instance.SaveSettings();

            ShokoServer.SetTraceLogging(Instance.TraceLog);
        }

        private static ServerSettings LoadLegacySettings()
        {
            var legacy = Legacy.LoadSettingsFromFile();
            var settings = new ServerSettings
            {
                ImagesPath = legacy.ImagesPath,
                AnimeXmlDirectory = legacy.AnimeXmlDirectory,
                MyListDirectory = legacy.MyListDirectory,
                ServerPort = (ushort) legacy.JMMServerPort,
                PluginAutoWatchThreshold = double.Parse(legacy.PluginAutoWatchThreshold, CultureInfo.InvariantCulture),
                Culture = legacy.Culture,
                WebUI_Settings = legacy.WebUI_Settings,
                FirstRun = legacy.FirstRun,
                LogRotator =
                    new LogRotatorSettings
                    {
                        Enabled = legacy.RotateLogs,
                        Zip = legacy.RotateLogs_Zip,
                        Delete = legacy.RotateLogs_Delete,
                        Delete_Days = legacy.RotateLogs_Delete_Days
                    },
                AniDb = new AniDbSettings
                {
                    Username = legacy.AniDB_Username,
                    Password = legacy.AniDB_Password,
                    ServerAddress = legacy.AniDB_ServerAddress,
                    ServerPort = ushort.Parse(legacy.AniDB_ServerPort),
                    ClientPort = ushort.Parse(legacy.AniDB_ClientPort),
                    AVDumpKey = legacy.AniDB_AVDumpKey,
                    AVDumpClientPort = ushort.Parse(legacy.AniDB_AVDumpClientPort),
                    DownloadRelatedAnime = legacy.AniDB_DownloadRelatedAnime,
                    DownloadSimilarAnime = legacy.AniDB_DownloadSimilarAnime,
                    DownloadReviews = legacy.AniDB_DownloadReviews,
                    DownloadReleaseGroups = legacy.AniDB_DownloadReleaseGroups,
                    MyList_AddFiles = legacy.AniDB_MyList_AddFiles,
                    MyList_StorageState = legacy.AniDB_MyList_StorageState,
                    MyList_DeleteType = legacy.AniDB_MyList_DeleteType,
                    MyList_ReadUnwatched = legacy.AniDB_MyList_ReadUnwatched,
                    MyList_ReadWatched = legacy.AniDB_MyList_ReadWatched,
                    MyList_SetWatched = legacy.AniDB_MyList_SetWatched,
                    MyList_SetUnwatched = legacy.AniDB_MyList_SetUnwatched,
                    MyList_UpdateFrequency = legacy.AniDB_MyList_UpdateFrequency,
                    Calendar_UpdateFrequency = legacy.AniDB_Calendar_UpdateFrequency,
                    Anime_UpdateFrequency = legacy.AniDB_Anime_UpdateFrequency,
                    MyListStats_UpdateFrequency = legacy.AniDB_MyListStats_UpdateFrequency,
                    File_UpdateFrequency = legacy.AniDB_File_UpdateFrequency,
                    DownloadCharacters = legacy.AniDB_DownloadCharacters,
                    DownloadCreators = legacy.AniDB_DownloadCreators,
                    MaxRelationDepth = legacy.AniDB_MaxRelationDepth
                },
                WebCache = new WebCacheSettings
                {
                    Address = legacy.WebCache_Address,
                    XRefFileEpisode_Get = legacy.WebCache_XRefFileEpisode_Get,
                    XRefFileEpisode_Send = legacy.WebCache_XRefFileEpisode_Send,
                    TvDB_Get = legacy.WebCache_TvDB_Get,
                    TvDB_Send = legacy.WebCache_TvDB_Send,
                    Trakt_Get = legacy.WebCache_Trakt_Get,
                    Trakt_Send = legacy.WebCache_Trakt_Send,
                },
                TvDB =
                    new TvDBSettings
                    {
                        AutoLink = legacy.TvDB_AutoLink,
                        AutoFanart = legacy.TvDB_AutoFanart,
                        AutoFanartAmount = legacy.TvDB_AutoFanartAmount,
                        AutoWideBanners = legacy.TvDB_AutoWideBanners,
                        AutoWideBannersAmount = legacy.TvDB_AutoWideBannersAmount,
                        AutoPosters = legacy.TvDB_AutoPosters,
                        AutoPostersAmount = legacy.TvDB_AutoPostersAmount,
                        UpdateFrequency = legacy.TvDB_UpdateFrequency,
                        Language = legacy.TvDB_Language
                    },
                MovieDb =
                    new MovieDbSettings
                    {
                        AutoFanart = legacy.MovieDB_AutoFanart,
                        AutoFanartAmount = legacy.MovieDB_AutoFanartAmount,
                        AutoPosters = legacy.MovieDB_AutoPosters,
                        AutoPostersAmount = legacy.MovieDB_AutoPostersAmount
                    },
                Import =
                    new ImportSettings
                    {
                        VideoExtensions = legacy.VideoExtensions.Split(',').ToList(),
                        DefaultSeriesLanguage = legacy.DefaultSeriesLanguage,
                        DefaultEpisodeLanguage = legacy.DefaultEpisodeLanguage,
                        RunOnStart = legacy.RunImportOnStart,
                        ScanDropFoldersOnStart = legacy.ScanDropFoldersOnStart,
                        Hash_CRC32 = legacy.Hash_CRC32,
                        Hash_MD5 = legacy.Hash_MD5,
                        Hash_SHA1 = legacy.Hash_SHA1,
                        UseExistingFileWatchedStatus = legacy.Import_UseExistingFileWatchedStatus
                    },
                Plex =
                    new PlexSettings
                    {
                        ThumbnailAspects = legacy.PlexThumbnailAspects,
                        Libraries = legacy.Plex_Libraries.ToList(),
                        Token = legacy.Plex_Token,
                        Server = legacy.Plex_Server
                    },
                AutoGroupSeries = legacy.AutoGroupSeries,
                AutoGroupSeriesRelationExclusions = legacy.AutoGroupSeriesRelationExclusions,
                AutoGroupSeriesUseScoreAlgorithm = legacy.AutoGroupSeriesUseScoreAlgorithm,
                FileQualityFilterEnabled = legacy.FileQualityFilterEnabled,
                FileQualityPreferences = legacy.FileQualityFilterPreferences,
                LanguagePreference = legacy.LanguagePreference.Split(',').ToList(),
                EpisodeLanguagePreference = legacy.EpisodeLanguagePreference,
                LanguageUseSynonyms = legacy.LanguageUseSynonyms,
                CloudWatcherTime = legacy.CloudWatcherTime,
                EpisodeTitleSource = legacy.EpisodeTitleSource,
                SeriesDescriptionSource = legacy.SeriesDescriptionSource,
                SeriesNameSource = legacy.SeriesNameSource,
                TraktTv = new TraktSettings
                {
                    Enabled = legacy.Trakt_IsEnabled,
                    PIN = legacy.Trakt_PIN,
                    AuthToken = legacy.Trakt_AuthToken,
                    RefreshToken = legacy.Trakt_RefreshToken,
                    TokenExpirationDate = legacy.Trakt_TokenExpirationDate,
                    UpdateFrequency = legacy.Trakt_UpdateFrequency,
                    SyncFrequency = legacy.Trakt_SyncFrequency
                },
                UpdateChannel = legacy.UpdateChannel,
                Linux = new LinuxSettings
                {
                    UID = legacy.Linux_UID, GID = legacy.Linux_GID, Permission = legacy.Linux_Permission
                },
                TraceLog = legacy.TraceLog,
                Database = new DatabaseSettings
                {
                    MySqliteDirectory = legacy.MySqliteDirectory,
                    DatabaseBackupDirectory = legacy.DatabaseBackupDirectory,
                    Type = legacy.DatabaseType
                }
            };

            switch (legacy.DatabaseType)
            {
                case Constants.DatabaseType.MySQL:
                    settings.Database.Username = legacy.MySQL_Username;
                    settings.Database.Password = legacy.MySQL_Password;
                    settings.Database.Schema = legacy.MySQL_SchemaName;
                    settings.Database.Hostname = legacy.MySQL_Hostname;
                    break;
                case Constants.DatabaseType.SqlServer:
                    settings.Database.Username = legacy.DatabaseUsername;
                    settings.Database.Password = legacy.DatabasePassword;
                    settings.Database.Schema = legacy.DatabaseName;
                    settings.Database.Hostname = legacy.DatabaseServer;
                    break;
            }

            return settings;
        }

        public static T Deserialize<T>(string json) where T : class
        {
            return Deserialize(typeof(T), json) as T;
        }

        public static object Deserialize(Type t, string json)
        {
            var serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new NullToDefaultValueResolver(),
                Converters = new List<JsonConverter>{new StringEnumConverter()},
                Error = (sender, args) => { args.ErrorContext.Handled = true; },
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                MissingMemberHandling = MissingMemberHandling.Ignore,
            };
            var result = JsonConvert.DeserializeObject(json, t, serializerSettings);
            if (result == null) return null;
            var context = new ValidationContext(result, serviceProvider: null, items: null);
            var results = new List<ValidationResult>();

            if (!Validator.TryValidateObject(result, context, results))
            {
                throw new ValidationException(string.Join("\n", results.Select(a => a.ErrorMessage)));
            }

            return result;
        }

        public static void LoadSettingsFromFile(string path, bool delete = false)
        {
            FixNonEmittedDefaults(path);
            try
            {
                Instance = Deserialize<ServerSettings>(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Logger.Error(e);
            }

            if (delete) File.Delete(path);
        }

        /// <summary>
        /// Fix the behavior of missing members in pre-4.0
        /// </summary>
        /// <param name="path"></param>
        private static void FixNonEmittedDefaults(string path)
        {
            var json = File.ReadAllText(path);
            if (json.Contains("\"FirstRun\":")) return;
            var serializerSettings = new JsonSerializerSettings
            {
                Converters = new List<JsonConverter>{new StringEnumConverter()},
                Error = (sender, args) => { args.ErrorContext.Handled = true; },
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                DefaultValueHandling = DefaultValueHandling.Populate
            };
            var result = JsonConvert.DeserializeObject<ServerSettings>(json, serializerSettings);
            string inCode = Serialize(result, true);
            File.WriteAllText(path, inCode);
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

            lock (SettingsLock)
            {
                string onDisk = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
                string inCode = Serialize(this, true);
                if (!onDisk.Equals(inCode, StringComparison.Ordinal))
                {
                    File.WriteAllText(path, inCode);
                    ShokoEventHandler.Instance.OnSettingsSaved();
                }
            }
        }

        public static string Serialize(object obj, bool indent = false)
        {
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                Formatting = indent ? Formatting.Indented : Formatting.None,
                DefaultValueHandling = DefaultValueHandling.Include,
                MissingMemberHandling = MissingMemberHandling.Ignore,
                Converters = new List<JsonConverter> {new StringEnumConverter()}
            };
            return JsonConvert.SerializeObject(obj, serializerSettings);
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
                FileQualityFilterPreferences = Serialize(FileQualityPreferences),
                Import_MoveOnImport = Import.MoveOnImport,
                Import_RenameOnImport = Import.RenameOnImport,
                Import_UseExistingFileWatchedStatus = Import.UseExistingFileWatchedStatus,
                RunImportOnStart = Import.RunOnStart,
                ScanDropFoldersOnStart = Import.ScanDropFoldersOnStart,
                Hash_CRC32 = Import.Hash_CRC32,
                Hash_MD5 = Import.Hash_MD5,
                Hash_SHA1 = Import.Hash_SHA1,
                SkipDiskSpaceChecks = Import.SkipDiskSpaceChecks,

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
            if (obj == null)
            {
                Logger.Info($"{path}: null");
                return;
            }
            foreach (var prop in obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                var type = prop.PropertyType;
                if (type.FullName.StartsWith("Shoko.Server") ||
                    type.FullName.StartsWith("Shoko.Models") ||
                    type.FullName.StartsWith("Shoko.Plugin"))
                {
                    DumpSettings(prop.GetValue(obj), path + $".{prop.Name}");
                    continue;
                }

                var value = prop.GetValue(obj);

                if (!IsPrimitive(type)) value = Serialize(value);
                if (prop.Name.ToLower().EndsWith("password")) value = "***HIDDEN***";

                Logger.Info($"{path}.{prop.Name}: {value}");
            }
        }

        private static bool IsPrimitive(Type type)
        {
            if (type.IsPrimitive) return true;
            if (type.IsValueType) return true;
            return false;
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
                string mediaInfoVersion = "**** MediaInfo Not found *****";

                string mediaInfoPath = Assembly.GetEntryAssembly().Location;
                FileInfo fi = new FileInfo(mediaInfoPath);
                mediaInfoPath = Path.Combine(fi.Directory.FullName, "MediaInfo", "MediaInfo.exe");

                if (File.Exists(mediaInfoPath))
                {
                    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(mediaInfoPath);
                    mediaInfoVersion =
                        $"MediaInfo {fvi.FileMajorPart}.{fvi.FileMinorPart}.{fvi.FileBuildPart}.{fvi.FilePrivatePart} ({mediaInfoPath})";
                }
                Logger.Info(mediaInfoVersion);

                string hasherInfoVersion = "**** Hasher - DLL NOT found *****";

                string fullHasherexepath = Assembly.GetEntryAssembly().Location;
                fi = new FileInfo(fullHasherexepath);
                fullHasherexepath = Path.Combine(fi.Directory.FullName, Environment.Is64BitProcess ? "x64" : "x86",
                    "librhash.dll");

                if (File.Exists(fullHasherexepath))
                {
                    FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(fullHasherexepath);
                    hasherInfoVersion =
                        $"RHash {fvi.FileMajorPart}.{fvi.FileMinorPart}.{fvi.FileBuildPart}.{fvi.FilePrivatePart} ({fullHasherexepath})";
                }
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

        public static void ConfigureServices(IServiceCollection services)
        {
            services.AddSettings(Instance.AniDb)
                .AddSettings(Instance)
                .AddSettings(Instance.Database)
                .AddSettings(Instance.FileQualityPreferences)
                .AddSettings(Instance.Import)
                .AddSettings(Instance.Linux)
                .AddSettings(Instance.LogRotator)
                .AddSettings(Instance.MovieDb)
                .AddSettings(Instance.Plex)
                .AddSettings(Instance.Plugins)
                .AddSettings(Instance.TraktTv)
                .AddSettings(Instance.TvDB)
                .AddSettings(Instance.WebCache);
        }
    }
}