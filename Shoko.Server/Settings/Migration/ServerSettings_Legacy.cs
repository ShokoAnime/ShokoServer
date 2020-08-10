using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NLog;
using Shoko.Commons.Properties;
using Shoko.Models;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Server.ImageDownload;
using Shoko.Server.Utilities;
using Formatting = Newtonsoft.Json.Formatting;
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedAutoPropertyAccessor.Local

namespace Shoko.Server.Settings.Migration
{
    public class ServerSettings_Legacy
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        //in this way, we could host two ShokoServers int the same machine
        private static string DefaultInstance { get; } =
            Assembly.GetEntryAssembly().GetName().Name;

        private static string ApplicationPath
        {
            get
            {
                if (Utils.IsLinux)
                    return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".shoko",
                        DefaultInstance);

                return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), DefaultInstance);
            }
        }

        private static string DefaultImagePath => Path.Combine(ApplicationPath, "images");

        /// <summary>
        /// Load setting from custom file - ex. read setting from backup
        /// </summary>
        public static ServerSettings_Legacy LoadSettingsFromFile()
        {
            try
            {
                string path = string.Empty;
                ServerSettings_Legacy settings = null;

                if (!string.IsNullOrEmpty(ApplicationPath))
                {
                    path = Path.Combine(ApplicationPath, "settings.json");
                }

                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    settings = JsonConvert.DeserializeObject<ServerSettings_Legacy>(File.ReadAllText(path));
                }

                if (settings == null) return null;
                if (Directory.Exists(BaseImagesPath) && string.IsNullOrEmpty(settings.ImagesPath))
                {
                    settings.ImagesPath = BaseImagesPath;
                }

                if (string.IsNullOrEmpty(settings.ImagesPath))
                    settings.ImagesPath = DefaultImagePath;

                return settings;
            }
            catch (Exception e)
            {
                logger.Error(e);
                return null;
            }
        }

        public string AnimeXmlDirectory { get; set; }


        public string MyListDirectory { get; set; }

        public string MySqliteDirectory { get; set; }

        public string DatabaseBackupDirectory { get; set; }

        public int JMMServerPort { get; set; }

        public string PluginAutoWatchThreshold { get; set; }

        public string PlexThumbnailAspects { get; set; }

        public string Culture { get; set; }

        public bool RotateLogs { get; set; }

        public bool RotateLogs_Zip { get; set; }

        public bool RotateLogs_Delete { get; set; }

        public string RotateLogs_Delete_Days { get; set; }

        /// <summary>
        /// Store json settings inside string
        /// </summary>
        public string WebUI_Settings { get; set; }

        /// <summary>
        /// FirstRun idicates if DB was configured or not, as it needed as backend for user authentication
        /// </summary>
        public bool FirstRun { get; set; }

        public string DatabaseType { get; set; }

        public string DatabaseServer { get; set; }

        public string DatabaseName { get; set; }

        public string DatabaseUsername { get; set; }

        public string DatabasePassword { get; set; }

        public string DatabaseFile { get; set; }

        public string MySQL_Hostname { get; set; }

        public string MySQL_SchemaName { get; set; }

        public string MySQL_Username { get; set; }

        public string MySQL_Password { get; set; }

        public string AniDB_Username { get; set; }

        public string AniDB_Password { get; set; }

        public string AniDB_ServerAddress { get; set; }

        public string AniDB_ServerPort { get; set; }

        public string AniDB_ClientPort { get; set; }

        public string AniDB_AVDumpKey { get; set; }

        public string AniDB_AVDumpClientPort { get; set; }

        public bool AniDB_DownloadRelatedAnime { get; set; }

        public bool AniDB_DownloadSimilarAnime { get; set; }

        public bool AniDB_DownloadReviews { get; set; }

        public bool AniDB_DownloadReleaseGroups { get; set; }

        public bool AniDB_MyList_AddFiles { get; set; }

        public AniDBFile_State AniDB_MyList_StorageState { get; set; }

        public AniDBFileDeleteType AniDB_MyList_DeleteType { get; set; }

        public bool AniDB_MyList_ReadUnwatched { get; set; }

        public bool AniDB_MyList_ReadWatched { get; set; }

        public bool AniDB_MyList_SetWatched { get; set; }

        public bool AniDB_MyList_SetUnwatched { get; set; }

        public ScheduledUpdateFrequency AniDB_MyList_UpdateFrequency { get; set; }

        public ScheduledUpdateFrequency AniDB_Calendar_UpdateFrequency { get; set; }

        public ScheduledUpdateFrequency AniDB_Anime_UpdateFrequency { get; set; }

        public ScheduledUpdateFrequency AniDB_MyListStats_UpdateFrequency { get; set; }

        public ScheduledUpdateFrequency AniDB_File_UpdateFrequency { get; set; }

        public bool AniDB_DownloadCharacters { get; set; }

        public bool AniDB_DownloadCreators { get; set; }

        public string WebCache_Address { get; set; }

        public bool WebCache_Anonymous { get; set; }

        public bool WebCache_XRefFileEpisode_Get { get; set; }

        public bool WebCache_XRefFileEpisode_Send { get; set; }

        public bool WebCache_TvDB_Get { get; set; }

        public bool WebCache_TvDB_Send { get; set; }

        public bool WebCache_Trakt_Get { get; set; }

        public bool WebCache_Trakt_Send { get; set; }

        public bool WebCache_UserInfo { get; set; }

        public bool TvDB_AutoLink { get; set; }

        public bool TvDB_AutoFanart { get; set; }

        public int TvDB_AutoFanartAmount { get; set; }

        public bool TvDB_AutoWideBanners { get; set; }

        public int TvDB_AutoWideBannersAmount { get; set; }

        public bool TvDB_AutoPosters { get; set; }

        public int TvDB_AutoPostersAmount { get; set; }

        public ScheduledUpdateFrequency TvDB_UpdateFrequency { get; set; }

        public string TvDB_Language { get; set; }
        public bool MovieDB_AutoFanart { get; set; }

        public int MovieDB_AutoFanartAmount { get; set; }

        public bool MovieDB_AutoPosters { get; set; }

        public int MovieDB_AutoPostersAmount { get; set; }

        public string VideoExtensions { get; set; }

        public RenamingLanguage DefaultSeriesLanguage { get; set; }

        public RenamingLanguage DefaultEpisodeLanguage { get; set; }

        public bool RunImportOnStart { get; set; }

        public bool ScanDropFoldersOnStart { get; set; }

        public bool Hash_CRC32 { get; set; }

        public bool Hash_MD5 { get; set; }

        public bool ExperimentalUPnP { get; set; }

        public bool Hash_SHA1 { get; set; }

        public bool Import_UseExistingFileWatchedStatus { get; set; }

        public bool AutoGroupSeries { get; set; }

        public string AutoGroupSeriesRelationExclusions { get; set; }

        public bool AutoGroupSeriesUseScoreAlgorithm { get; set; }

        public bool FileQualityFilterEnabled { get; set; }

        public FileQualityPreferences FileQualityFilterPreferences { get; set; }

        public string LanguagePreference { get; set; }

        public string EpisodeLanguagePreference { get; set; }

        public bool LanguageUseSynonyms { get; set; }

        public int CloudWatcherTime { get; set; }

        public DataSourceType EpisodeTitleSource { get; set; }

        public DataSourceType SeriesDescriptionSource { get; set; }

        public DataSourceType SeriesNameSource { get; set; }

        public string ImagesPath { get; set; }

        private static string BaseImagesPath { get; set; }

        public string VLCLocation { get; set; }

        public bool MinimizeOnStartup { get; set; }

        public bool Trakt_IsEnabled { get; set; }

        public string Trakt_PIN { get; set; }

        public string Trakt_AuthToken { get; set; }

        public string Trakt_RefreshToken { get; set; }

        public string Trakt_TokenExpirationDate { get; set; }

        public ScheduledUpdateFrequency Trakt_UpdateFrequency { get; set; }

        public ScheduledUpdateFrequency Trakt_SyncFrequency { get; set; }

        public string UpdateChannel { get; set; }

        public string WebCacheAuthKey { get; set; }

        //plex
        public int[] Plex_Libraries { get; set; }

        public string Plex_Token { get; set; }

        public string Plex_Server { get; set; }

        public int Linux_UID { get; set; }

        public int Linux_GID { get; set; }

        public int Linux_Permission { get; set; }

        public int AniDB_MaxRelationDepth { get; set; }

        public bool TraceLog { get; set; }

    }
}
