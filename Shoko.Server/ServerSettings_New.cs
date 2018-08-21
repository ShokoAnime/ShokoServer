using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Server.Databases;
using Shoko.Server.ImageDownload;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Shoko.Server
{
    public class ServerSettings
    {
        private const string SettingsFilename = "settings.json";
        private static Logger logger = LogManager.GetCurrentClassLogger();

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

        public string MySqliteDirectory { get; set; } = Path.Combine(ApplicationPath, "SQLite");
        public string DatabaseBackupDirectory { get; set; } = Path.Combine(ApplicationPath, "DatabaseBackup");

        public ushort JMMServerPort { get; set; } = 8111;


        public double PluginAutoWatchThreshold { get; set; } = 0.89;

        public string PlexThumbnailAspects { get; set; } = "Default, 0.6667, IOS, 1.0, Android, 1.3333";

        public string Culture { get; set; } = "en";


        #region LogRotator

        public bool RotateLogs { get; set; } = true;

        public bool RotateLogs_Zip { get; set; } = true;

        public bool RotateLogs_Delete { get; set; } = true;

        public string RotateLogs_Delete_Days { get; set; } = "";

        #endregion

        #region WebUI

        /// <summary>
        /// Store json settings inside string
        /// </summary>
        public string WebUI_Settings { get; set; } = "";

        /// <summary>
        /// FirstRun idicates if DB was configured or not, as it needed as backend for user authentication
        /// </summary>
        public bool FirstRun { get; set; } = true;

        #endregion

        #region Database

        public string DefaultUserUsername { get; set; } = "Default";
        public string DefaultUserPassword { get; set; } = string.Empty;

        public DatabaseTypes DatabaseType { get; set; } = DatabaseTypes.Sqlite;

        //Legacy wrapper.
        [JsonIgnore]
        [Obsolete("Use SQLServer_DatabaseServer")]
        public string DatabaseServer { get => SQLServer_DatabaseServer; set => SQLServer_DatabaseServer = value; }

        public string SQLServer_DatabaseServer { get; set; } = string.Empty;

        [JsonIgnore]
        [Obsolete("Use SQLServer_DatabaseName")]
        public string DatabaseName { get => SQLServer_DatabaseName; set => SQLServer_DatabaseName = value; }

        public string SQLServer_DatabaseName { get; set; } = string.Empty;

        public string DatabaseUsername { get => SQLServer_Username; set => SQLServer_Username = value; }

        public string SQLServer_Username { get; set; } = string.Empty;

        public string DatabasePassword { get => SQLServer_Password; set => SQLServer_Password = value; }
        public string SQLServer_Password { get; set; } = string.Empty;

        [JsonIgnore]
        [Obsolete("Use SQLite_DatabaseFile")]
        public string DatabaseFile { get => SQLite_DatabaseFile; set => SQLite_DatabaseFile = value; }

        public string SQLite_DatabaseFile { get; set; } = "JMMServer.db3";

        public string MySQL_Hostname { get; set; } = "localhost";

        public string MySQL_SchemaName { get; set; } = string.Empty;

        public string MySQL_Username { get; set; } = string.Empty;

        public string MySQL_Password { get; set; } = string.Empty;

        #endregion

        #region AniDB

        public string AniDB_Username { get; set; }
        public string AniDB_Password { get; set; }

        public string AniDB_ServerAddress { get; set; } = "api.anidb.info";

        public ushort AniDB_ServerPort { get; set; } = 9000;

        public ushort AniDB_ClientPort { get; set; } = 4556;

        public string AniDB_AVDumpKey { get; set; }

        public ushort AniDB_AVDumpClientPort { get; set; } = 4557;

        public bool AniDB_DownloadRelatedAnime { get; set; } = true;

        public bool AniDB_DownloadSimilarAnime { get; set; } = true;

        public bool AniDB_DownloadReviews { get; set; } = false;

        public bool AniDB_DownloadReleaseGroups { get; set; } = false;

        public bool AniDB_MyList_AddFiles { get; set; } = true;

        public AniDBFile_State AniDB_MyList_StorageState { get; set; } = AniDBFile_State.Disk;

        public AniDBFileDeleteType AniDB_MyList_DeleteType { get; set; } = AniDBFileDeleteType.MarkUnknown;

        public bool AniDB_MyList_ReadUnwatched { get; set; } = true;

        public bool AniDB_MyList_ReadWatched { get; set; } = true;

        public bool AniDB_MyList_SetWatched { get; set; } = true;

        public bool AniDB_MyList_SetUnwatched { get; set; } = true;
        public ScheduledUpdateFrequency AniDB_MyList_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.Never;

        public ScheduledUpdateFrequency AniDB_Calendar_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.HoursTwelve;

        public ScheduledUpdateFrequency AniDB_Anime_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.HoursTwelve;

        public ScheduledUpdateFrequency AniDB_MyListStats_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.Never;

        public ScheduledUpdateFrequency AniDB_File_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.Daily;

        public bool AniDB_DownloadCharacters { get; set; } = true;

        public bool AniDB_DownloadCreators { get; set; } = true;

        #endregion

        #region Web Cache

        public string WebCache_Address { get; set; } = "omm.hobbydb.net.leaf.arvixe.com";

        public bool WebCache_Anonymous { get; set; } = false;

        public bool WebCache_XRefFileEpisode_Get { get; set; } = true;

        public bool WebCache_XRefFileEpisode_Send { get; set; } = true;

        public bool WebCache_TvDB_Get { get; set; } = true;

        public bool WebCache_TvDB_Send { get; set; } = true;

        public bool WebCache_Trakt_Get { get; set; } = true;

        public bool WebCache_Trakt_Send { get; set; } = true;

        public bool WebCache_UserInfo { get; set; } = true;

        #endregion

        #region TvDB

        public bool TvDB_AutoLink { get; set; } = false;

        public bool TvDB_AutoFanart { get; set; } = true;

        public int TvDB_AutoFanartAmount { get; set; } = 10;

        public bool TvDB_AutoWideBanners { get; set; } = true;

        public int TvDB_AutoWideBannersAmount { get; set; } = 10;

        public bool TvDB_AutoPosters { get; set; } = true;

        public int TvDB_AutoPostersAmount { get; set; } = 10;

        public ScheduledUpdateFrequency TvDB_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.HoursTwelve;

        public string TvDB_Language { get; set; } = "en";

        #endregion

        #region MovieDB

        public bool MovieDB_AutoFanart { get; set; } = true;

        public int MovieDB_AutoFanartAmount { get; set; } = 10;

        public bool MovieDB_AutoPosters { get; set; } = true;

        public int MovieDB_AutoPostersAmount { get; set; } = 10;

        #endregion

        #region Import Settings

        public string[] VideoExtensions { get; set; } = new[] { "MKV", "AVI", "MP4", "MOV", "OGM", "WMV", "MPG", "MPEG", "MK3D", "M4V" };

        public RenamingLanguage DefaultSeriesLanguage { get; set; } = RenamingLanguage.Romaji;

        public RenamingLanguage DefaultEpisodeLanguage { get; set; } = RenamingLanguage.Romaji;

        public bool RunImportOnStart { get; set; } = false;

        public bool ScanDropFoldersOnStart { get; set; } = false;

        public bool Hash_CRC32 { get; set; } = false;
        public bool Hash_MD5 { get; set; } = false;
        public bool Hash_SHA1 { get; set; } = false;

        public bool Import_UseExistingFileWatchedStatus { get; set; } = true;
        #endregion

        public bool AutoGroupSeries { get; set; } = false;

        public string AutoGroupSeriesRelationExclusions { get; set; } = "same setting|character";

        public bool AutoGroupSeriesUseScoreAlgorithm { get; set; } = false;

        public bool FileQualityFilterEnabled { get; set; } = false;


        string _FileQualityFilterPreferences;
        public string FileQualityFilterPreferences
        {
            get
            {
                return _FileQualityFilterPreferences ?? JsonConvert.SerializeObject(FileQualityFilter.Settings, Formatting.None, new StringEnumConverter());
            }
            set
            {
                try
                {
                    FileQualityPreferences prefs = JsonConvert.DeserializeObject<FileQualityPreferences>(
                        value, new StringEnumConverter());
                    FileQualityFilter.Settings = prefs;
                    _FileQualityFilterPreferences = value;
                }
                catch
                {
                    logger.Error("Error Deserializing json into FileQualityPreferences. json was :" + value);
                }

            }
        }

        public string[] LanguagePreference { get; set; } = new[] { "x-jat", "en" };

        public string EpisodeLanguagePreference { get; set; } = string.Empty;

        public bool LanguageUseSynonyms { get; set; } = true;

        public int CloudWatcherTime { get; set; } = 3;

        public DataSourceType EpisodeTitleSource { get; set; } = DataSourceType.AniDB;
        public DataSourceType SeriesDescriptionSource { get; set; } = DataSourceType.AniDB;
        public DataSourceType SeriesNameSource { get; set; } = DataSourceType.AniDB;

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

        private static string BaseImagesPath { get; set; } = string.Empty;

        private static bool BaseImagesPathIsDefault { get; set; } = true;

        public string VLCLocation { get; set; } = string.Empty;

        public bool MinimizeOnStartup { get; set; } = false;
        #region Trakt

        public bool Trakt_IsEnabled { get; set; } = false;

        public string Trakt_PIN { get; set; } = string.Empty;

        public string Trakt_AuthToken { get; set; } = string.Empty;

        public string Trakt_RefreshToken { get; set; } = string.Empty;

        public string Trakt_TokenExpirationDate { get; set; } = string.Empty;

        public ScheduledUpdateFrequency Trakt_UpdateFrequency { get; set; } = ScheduledUpdateFrequency.Daily;

        public ScheduledUpdateFrequency Trakt_SyncFrequency { get; set; } = ScheduledUpdateFrequency.Daily;

        #endregion

        public string UpdateChannel { get; set; } = "Stable";

        public string WebCacheAuthKey { get; set; } = string.Empty;

        #region plex

        //plex
        public int[] Plex_Libraries { get; set; } = new int[0];

        public string Plex_Token { get; set; } = string.Empty;

        public string Plex_Server { get; set; } = string.Empty;

        #endregion

        public int Linux_UID { get; set; } = -1;
        public int Linux_GID { get; set; } = -1;
        public int Linux_Permission { get; set; } = 0;
        public int AniDB_MaxRelationDepth { get; set; } = 3;
        public bool TraceLog { get; set; } = false;

        public static ServerSettings Instance { get; private set; } = new ServerSettings();

        public static void LoadSettings()
        {
            if (!Directory.Exists(ApplicationPath)) Directory.CreateDirectory(ApplicationPath);
            var path = Path.Combine(ApplicationPath, SettingsFilename);
            if (!File.Exists(path))
            {
                Instance = new ServerSettings();
                Instance.SaveSettings();
                return;
            }
            LoadSettingsFromFile(path, false);
        }

        public static void LoadSettingsFromFile(string path, bool delete = false)
        {
            Instance = JsonConvert.DeserializeObject<ServerSettings>(File.ReadAllText(path));
            if(delete) File.Delete(path);
        }

        public void SaveSettings()
        {
            string path = Path.Combine(ApplicationPath, SettingsFilename);

            File.WriteAllText(path, JsonConvert.SerializeObject(Instance, Formatting.Indented, new StringEnumConverter() { AllowIntegerValues = true }));
        }


        public CL_ServerSettings ToContract()
        {
            return new CL_ServerSettings
            {
                AniDB_Username = AniDB_Username,
                AniDB_Password = AniDB_Password,
                AniDB_ServerAddress = AniDB_ServerAddress,
                AniDB_ServerPort = AniDB_ServerPort.ToString(),
                AniDB_ClientPort = AniDB_ClientPort.ToString(),
                AniDB_AVDumpClientPort = AniDB_AVDumpClientPort.ToString(),
                AniDB_AVDumpKey = AniDB_AVDumpKey,

                AniDB_DownloadRelatedAnime = AniDB_DownloadRelatedAnime,
                AniDB_DownloadSimilarAnime = AniDB_DownloadSimilarAnime,
                AniDB_DownloadReviews = AniDB_DownloadReviews,
                AniDB_DownloadReleaseGroups = AniDB_DownloadReleaseGroups,

                AniDB_MyList_AddFiles = AniDB_MyList_AddFiles,
                AniDB_MyList_StorageState = (int)AniDB_MyList_StorageState,
                AniDB_MyList_DeleteType = (int)AniDB_MyList_DeleteType,
                AniDB_MyList_ReadWatched = AniDB_MyList_ReadWatched,
                AniDB_MyList_ReadUnwatched = AniDB_MyList_ReadUnwatched,
                AniDB_MyList_SetWatched = AniDB_MyList_SetWatched,
                AniDB_MyList_SetUnwatched = AniDB_MyList_SetUnwatched,

                AniDB_MyList_UpdateFrequency = (int)AniDB_MyList_UpdateFrequency,
                AniDB_Calendar_UpdateFrequency = (int)AniDB_Calendar_UpdateFrequency,
                AniDB_Anime_UpdateFrequency = (int)AniDB_Anime_UpdateFrequency,
                AniDB_MyListStats_UpdateFrequency = (int)AniDB_MyListStats_UpdateFrequency,
                AniDB_File_UpdateFrequency = (int)AniDB_File_UpdateFrequency,

                AniDB_DownloadCharacters = AniDB_DownloadCharacters,
                AniDB_DownloadCreators = AniDB_DownloadCreators,
                AniDB_MaxRelationDepth = AniDB_MaxRelationDepth,

                // Web Cache
                WebCache_Address = WebCache_Address,
                WebCache_Anonymous = WebCache_Anonymous,
                WebCache_XRefFileEpisode_Get = WebCache_XRefFileEpisode_Get,
                WebCache_XRefFileEpisode_Send = WebCache_XRefFileEpisode_Send,
                WebCache_TvDB_Get = WebCache_TvDB_Get,
                WebCache_TvDB_Send = WebCache_TvDB_Send,
                WebCache_Trakt_Get = WebCache_Trakt_Get,
                WebCache_Trakt_Send = WebCache_Trakt_Send,
                WebCache_UserInfo = WebCache_UserInfo,

                // TvDB
                TvDB_AutoLink = TvDB_AutoLink,
                TvDB_AutoFanart = TvDB_AutoFanart,
                TvDB_AutoFanartAmount = TvDB_AutoFanartAmount,
                TvDB_AutoPosters = TvDB_AutoPosters,
                TvDB_AutoPostersAmount = TvDB_AutoPostersAmount,
                TvDB_AutoWideBanners = TvDB_AutoWideBanners,
                TvDB_AutoWideBannersAmount = TvDB_AutoWideBannersAmount,
                TvDB_UpdateFrequency = (int)TvDB_UpdateFrequency,
                TvDB_Language = TvDB_Language,

                // MovieDB
                MovieDB_AutoFanart = MovieDB_AutoFanart,
                MovieDB_AutoFanartAmount = MovieDB_AutoFanartAmount,
                MovieDB_AutoPosters = MovieDB_AutoPosters,
                MovieDB_AutoPostersAmount = MovieDB_AutoPostersAmount,

                // Import settings
                VideoExtensions = string.Join(",", VideoExtensions),
                AutoGroupSeries = AutoGroupSeries,
                AutoGroupSeriesUseScoreAlgorithm = AutoGroupSeriesUseScoreAlgorithm,
                AutoGroupSeriesRelationExclusions = AutoGroupSeriesRelationExclusions,
                FileQualityFilterEnabled = FileQualityFilterEnabled,
                FileQualityFilterPreferences = FileQualityFilterPreferences,
                Import_UseExistingFileWatchedStatus = Import_UseExistingFileWatchedStatus,
                RunImportOnStart = RunImportOnStart,
                ScanDropFoldersOnStart = ScanDropFoldersOnStart,
                Hash_CRC32 = Hash_CRC32,
                Hash_MD5 = Hash_MD5,
                Hash_SHA1 = Hash_SHA1,

                // Language
                LanguagePreference = string.Join(",", LanguagePreference),
                LanguageUseSynonyms = LanguageUseSynonyms,
                EpisodeTitleSource = (int)EpisodeTitleSource,
                SeriesDescriptionSource = (int)SeriesDescriptionSource,
                SeriesNameSource = (int)SeriesNameSource,

                // trakt
                Trakt_IsEnabled = Trakt_IsEnabled,
                Trakt_AuthToken = Trakt_AuthToken,
                Trakt_RefreshToken = Trakt_RefreshToken,
                Trakt_TokenExpirationDate = Trakt_TokenExpirationDate,
                Trakt_UpdateFrequency = (int)Trakt_UpdateFrequency,
                Trakt_SyncFrequency = (int)Trakt_SyncFrequency,

                // LogRotator
                RotateLogs = RotateLogs,
                RotateLogs_Delete = RotateLogs_Delete,
                RotateLogs_Delete_Days = RotateLogs_Delete_Days,
                RotateLogs_Zip = RotateLogs_Zip,

                //WebUI
                WebUI_Settings = WebUI_Settings,

                //Plex
                Plex_Sections = string.Join(",", Plex_Libraries),
                Plex_ServerHost = Plex_Server
            };
        }

        public void DebugSettingsToLog()
        {
            #region System Info

            logger.Info("-------------------- SYSTEM INFO -----------------------");

            Assembly a = Assembly.GetEntryAssembly();
            try
            {
                if (Utils.GetApplicationVersion(a) != null)
                    logger.Info($"Shoko Server Version: v{Utils.GetApplicationVersion(a)}");
            }
            catch (Exception ex)
            {
                logger.Warn($"Error in log (server version lookup): {ex}");
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
            logger.Info($"Operating System: {Utils.GetOSInfo()}");

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
                logger.Info(mediaInfoVersion);

                string hasherInfoVersion = "**** Hasher - DLL NOT found *****";

                string fullHasherexepath = Assembly.GetEntryAssembly().Location;
                fi = new FileInfo(fullHasherexepath);
                fullHasherexepath = Path.Combine(fi.Directory.FullName, Environment.Is64BitProcess ? "x64" : "x86",
                    "hasher.dll");

                if (File.Exists(fullHasherexepath))
                    hasherInfoVersion = $"Hasher DLL found at {fullHasherexepath}";
                logger.Info(hasherInfoVersion);
            }
            catch (Exception ex)
            {
                logger.Error("Error in log (hasher / info): {0}", ex.Message);
            }

            logger.Info("-------------------------------------------------------");

            #endregion

            logger.Info("----------------- SERVER SETTINGS ----------------------");

            logger.Info("DatabaseType: {0}", DatabaseType);
            logger.Info("MSSQL DatabaseServer: {0}", DatabaseServer);
            logger.Info("MSSQL DatabaseName: {0}", DatabaseName);
            logger.Info("MSSQL DatabaseUsername: {0}",
                string.IsNullOrEmpty(DatabaseUsername) ? "NOT SET" : "***HIDDEN***");
            logger.Info("MSSQL DatabasePassword: {0}",
                string.IsNullOrEmpty(DatabasePassword) ? "NOT SET" : "***HIDDEN***");

            logger.Info("SQLITE DatabaseFile: {0}", DatabaseFile);

            logger.Info("MySQL_Hostname: {0}", MySQL_Hostname);
            logger.Info("MySQL_SchemaName: {0}", MySQL_SchemaName);
            logger.Info("MySQL_Username: {0}", string.IsNullOrEmpty(MySQL_Username) ? "NOT SET" : "***HIDDEN***");
            logger.Info("MySQL_Password: {0}", string.IsNullOrEmpty(MySQL_Password) ? "NOT SET" : "***HIDDEN***");

            logger.Info("AniDB_Username: {0}", string.IsNullOrEmpty(AniDB_Username) ? "NOT SET" : "***HIDDEN***");
            logger.Info("AniDB_Password: {0}", string.IsNullOrEmpty(AniDB_Password) ? "NOT SET" : "***HIDDEN***");
            logger.Info("AniDB_ServerAddress: {0}", AniDB_ServerAddress);
            logger.Info("AniDB_ServerPort: {0}", AniDB_ServerPort);
            logger.Info("AniDB_ClientPort: {0}", AniDB_ClientPort);
            logger.Info("AniDB_AVDumpKey: {0}", string.IsNullOrEmpty(AniDB_AVDumpKey) ? "NOT SET" : "***HIDDEN***");
            logger.Info("AniDB_AVDumpClientPort: {0}", AniDB_AVDumpClientPort);
            logger.Info("AniDB_DownloadRelatedAnime: {0}", AniDB_DownloadRelatedAnime);
            logger.Info("AniDB_DownloadSimilarAnime: {0}", AniDB_DownloadSimilarAnime);
            logger.Info("AniDB_DownloadReviews: {0}", AniDB_DownloadReviews);
            logger.Info("AniDB_DownloadReleaseGroups: {0}", AniDB_DownloadReleaseGroups);
            logger.Info("AniDB_MyList_AddFiles: {0}", AniDB_MyList_AddFiles);
            logger.Info("AniDB_MyList_StorageState: {0}", AniDB_MyList_StorageState);
            logger.Info("AniDB_MyList_ReadUnwatched: {0}", AniDB_MyList_ReadUnwatched);
            logger.Info("AniDB_MyList_ReadWatched: {0}", AniDB_MyList_ReadWatched);
            logger.Info("AniDB_MyList_SetWatched: {0}", AniDB_MyList_SetWatched);
            logger.Info("AniDB_MyList_SetUnwatched: {0}", AniDB_MyList_SetUnwatched);
            logger.Info("AniDB_MyList_UpdateFrequency: {0}", AniDB_MyList_UpdateFrequency);
            logger.Info("AniDB_Calendar_UpdateFrequency: {0}", AniDB_Calendar_UpdateFrequency);
            logger.Info("AniDB_Anime_UpdateFrequency: {0}", AniDB_Anime_UpdateFrequency);
            logger.Info($"{nameof(AniDB_MaxRelationDepth)}: {AniDB_MaxRelationDepth}");


            logger.Info("WebCache_Address: {0}", WebCache_Address);
            logger.Info("WebCache_Anonymous: {0}", WebCache_Anonymous);
            logger.Info("WebCache_XRefFileEpisode_Get: {0}", WebCache_XRefFileEpisode_Get);
            logger.Info("WebCache_XRefFileEpisode_Send: {0}", WebCache_XRefFileEpisode_Send);
            logger.Info("WebCache_TvDB_Get: {0}", WebCache_TvDB_Get);
            logger.Info("WebCache_TvDB_Send: {0}", WebCache_TvDB_Send);

            logger.Info("TvDB_AutoFanart: {0}", TvDB_AutoFanart);
            logger.Info("TvDB_AutoFanartAmount: {0}", TvDB_AutoFanartAmount);
            logger.Info("TvDB_AutoWideBanners: {0}", TvDB_AutoWideBanners);
            logger.Info("TvDB_AutoPosters: {0}", TvDB_AutoPosters);
            logger.Info("TvDB_UpdateFrequency: {0}", TvDB_UpdateFrequency);
            logger.Info("TvDB_Language: {0}", TvDB_Language);

            logger.Info("MovieDB_AutoFanart: {0}", MovieDB_AutoFanart);
            logger.Info("MovieDB_AutoFanartAmount: {0}", MovieDB_AutoFanartAmount);
            logger.Info("MovieDB_AutoPosters: {0}", MovieDB_AutoPosters);

            logger.Info("VideoExtensions: {0}", VideoExtensions);
            logger.Info("DefaultSeriesLanguage: {0}", DefaultSeriesLanguage);
            logger.Info("DefaultEpisodeLanguage: {0}", DefaultEpisodeLanguage);
            logger.Info("RunImportOnStart: {0}", RunImportOnStart);
            logger.Info("Hash_CRC32: {0}", Hash_CRC32);
            logger.Info("Hash_MD5: {0}", Hash_MD5);
            logger.Info("Hash_SHA1: {0}", Hash_SHA1);
            logger.Info("Import_UseExistingFileWatchedStatus: {0}", Import_UseExistingFileWatchedStatus);

            logger.Info("Trakt_IsEnabled: {0}", Trakt_IsEnabled);
            logger.Info("Trakt_AuthToken: {0}", string.IsNullOrEmpty(Trakt_AuthToken) ? "NOT SET" : "***HIDDEN***");
            logger.Info("Trakt_RefreshToken: {0}",
                string.IsNullOrEmpty(Trakt_RefreshToken) ? "NOT SET" : "***HIDDEN***");
            logger.Info("Trakt_UpdateFrequency: {0}", Trakt_UpdateFrequency);
            logger.Info("Trakt_SyncFrequency: {0}", Trakt_SyncFrequency);

            logger.Info("AutoGroupSeries: {0}", AutoGroupSeries);
            logger.Info("AutoGroupSeriesRelationExclusions: {0}", AutoGroupSeriesRelationExclusions);
            logger.Info("FileQualityFilterEnabled: {0}", FileQualityFilterEnabled);
            logger.Info("FileQualityFilterPreferences: {0}", FileQualityFilterPreferences);
            logger.Info("LanguagePreference: {0}", LanguagePreference);
            logger.Info("LanguageUseSynonyms: {0}", LanguageUseSynonyms);
            logger.Info("EpisodeTitleSource: {0}", EpisodeTitleSource);
            logger.Info("SeriesDescriptionSource: {0}", SeriesDescriptionSource);
            logger.Info("SeriesNameSource: {0}", SeriesNameSource);
            logger.Info("BaseImagesPath: {0}", BaseImagesPath);
            logger.Info("BaseImagesPathIsDefault: {0}", BaseImagesPathIsDefault);


            logger.Info("-------------------------------------------------------");
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
