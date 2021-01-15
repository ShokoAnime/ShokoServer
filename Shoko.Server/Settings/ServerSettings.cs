using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using Shoko.Commons.Utils;
using Shoko.Models;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Shoko.Plugin.Abstractions.Configuration;
using Shoko.Server.Settings.Source;
using Constants = Shoko.Server.Server.Constants;
using Formatting = Newtonsoft.Json.Formatting;
using Legacy = Shoko.Server.Settings.Migration.ServerSettings_Legacy;

namespace Shoko.Server.Settings
{
    public class ServerSettings
    {
        internal const string SettingsFilename = "settings-server.json";
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        internal static readonly object SettingsLock = new object();


        private static IConfigurationRoot _configuration;

        internal static IConfiguration Configuration =>
            _configuration ??= new ConfigurationBuilder()
                // .Add(new JsonProvider(new ))
                .AddNewtonsoftJsonFile(src =>
                {
                    src.Path = Path.Combine(ApplicationPath, SettingsFilename);
                    src.Optional = true;
                    src.ReloadOnChange = true;
                    src.ResolveFileProvider();
                })
                .AddEnvironmentVariables()
                .Build();

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
        
        public static SettingsRoot Instance => ShokoServer.ServiceContainer.GetRequiredService<IWritableOptions<SettingsRoot>>().Value;

        public static void LoadSettings()
        {
            // if (!Directory.Exists(ApplicationPath)) Directory.CreateDirectory(ApplicationPath);
            // var path = Path.Combine(ApplicationPath, SettingsFilename);
            // if (!File.Exists(path))
            // {
            //     Instance = File.Exists(Path.Combine(ApplicationPath, "settings.json")) ? LoadLegacySettings() : new ServerSettings();
            //     Instance.SaveSettings();
            //     return;
            // }
            // LoadSettingsFromFile(path);
            // Instance.SaveSettings();
            // Instance.SaveSettings();

            // ShokoServer.SetTraceLogging(Instance.TraceLog);
        }

        private static SettingsRoot LoadLegacySettings()
        {
            var legacy = Legacy.LoadSettingsFromFile();
            var settings = new SettingsRoot
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
                        VideoExtensions = legacy.VideoExtensions.Split(',').ToHashSet(),
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
                        Libraries = legacy.Plex_Libraries,
                        Token = legacy.Plex_Token,
                        Server = legacy.Plex_Server
                    },
                AutoGroupSeries = legacy.AutoGroupSeries,
                AutoGroupSeriesRelationExclusions = legacy.AutoGroupSeriesRelationExclusions,
                AutoGroupSeriesUseScoreAlgorithm = legacy.AutoGroupSeriesUseScoreAlgorithm,
                FileQualityFilterEnabled = legacy.FileQualityFilterEnabled,
                FileQualityPreferences = legacy.FileQualityFilterPreferences,
                LanguagePreference = legacy.LanguagePreference.Split(',').ToHashSet(),
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
            return;
            // FixNonEmittedDefaults(path);
            // try
            // {
            //     Instance = Deserialize<ServerSettings>(File.ReadAllText(path));
            // }
            // catch (Exception e)
            // {
            //     Logger.Error(e);
            // }
            //
            // if (delete) File.Delete(path);
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

            DumpSettings(Instance, "Settings");

            Logger.Info("-------------------------------------------------------");
        }

        internal static void ConfigureDi(IServiceCollection services)
        {
            var config = (IConfigurationRoot) Configuration;
            
            // services.Configure<IConfiguration>(config);
            services.AddSingleton<IConfiguration>(config);

            services.ConfigureWritable<LogRotatorSettings>(config.GetSection("LogRotator"));
            services.ConfigureWritable<DatabaseSettings>(config.GetSection("Database"));
            services.ConfigureWritable<WebCacheSettings>(config.GetSection("WebCache"));
            services.ConfigureWritable<TvDBSettings>(config.GetSection("TvDB"));
            services.ConfigureWritable<MovieDbSettings>(config.GetSection("MovieDb"));
            services.ConfigureWritable<ImportSettings>(config.GetSection("Import"));
            services.ConfigureWritable<PlexSettings>(config.GetSection("Plex"));
            services.ConfigureWritable<PluginSettings>(config.GetSection("Plugins"));
            services.ConfigureWritable<FileQualityPreferences>(config.GetSection("FileQualityPreferences"));
            services.ConfigureWritable<TraktSettings>(config.GetSection("TraktTv"));
            services.ConfigureWritable<LinuxSettings>(config.GetSection("Linux"));

            services.ConfigureWritable<SettingsRoot>(config);
        }

        public static event EventHandler<ReasonedEventArgs> ServerShutdown;
        //public static event EventHandler<ReasonedEventArgs> ServerError;
        public static void DoServerShutdown(ReasonedEventArgs args)
        {
            ServerShutdown?.Invoke(null, args);
        }

        public static IWritableOptions<T> Settings<T>() where T : class, IDefaultedConfig, new()
        {
            return ShokoServer.ServiceContainer.GetRequiredService<IWritableOptions<T>>();
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
