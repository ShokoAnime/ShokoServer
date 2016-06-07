using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using AniDBAPI;
using JMMContracts;
using JMMServer.ImageDownload;
using JMMServer.Repositories;
using NLog;

namespace JMMServer
{
    public class ServerSettings
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public static string JMMServerPort
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;

                var serverPort = appSettings["JMMServerPort"];
                if (string.IsNullOrEmpty(serverPort))
                {
                    serverPort = "8111";
                    UpdateSetting("JMMServerPort", serverPort);
                }

                return serverPort;
            }
            set { UpdateSetting("JMMServerPort", value); }
        }

        public static string JMMServerFilePort
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;

                var serverPort = appSettings["JMMServerFilePort"];
                if (string.IsNullOrEmpty(serverPort))
                {
                    serverPort = "8112";
                    UpdateSetting("JMMServerFilePort", serverPort);
                }

                return serverPort;
            }
            set { UpdateSetting("JMMServerFilePort", value); }
        }

        public static string PlexThumbnailAspects
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var thumbaspect = appSettings["PlexThumbnailAspects"];
                if (string.IsNullOrEmpty(thumbaspect))
                {
                    thumbaspect = "Default, 0.6667, IOS, 1.0, Android, 1.3333";
                    UpdateSetting("PlexThumbnailAspects", thumbaspect);
                }

                return thumbaspect;
            }
            set { UpdateSetting("PlexThumbnailAspect", value); }
        }

        public static string Culture
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;

                var cult = appSettings["Culture"];
                if (string.IsNullOrEmpty(cult))
                {
                    // default value
                    cult = "en";
                    UpdateSetting("Culture", cult);
                }
                return cult;
            }
            set { UpdateSetting("Culture", value); }
        }

        public static bool AutoGroupSeries
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = false;
                bool.TryParse(appSettings["AutoGroupSeries"], out val);
                return val;
            }
            set { UpdateSetting("AutoGroupSeries", value.ToString()); }
        }

        public static string LanguagePreference
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["LanguagePreference"];
            }
            set { UpdateSetting("LanguagePreference", value); }
        }

        public static bool LanguageUseSynonyms
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = false;
                bool.TryParse(appSettings["LanguageUseSynonyms"], out val);
                return val;
            }
            set { UpdateSetting("LanguageUseSynonyms", value.ToString()); }
        }

        public static DataSourceType EpisodeTitleSource
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = 0;
                int.TryParse(appSettings["EpisodeTitleSource"], out val);
                if (val <= 0)
                    return DataSourceType.AniDB;
                return (DataSourceType)val;
            }
            set { UpdateSetting("EpisodeTitleSource", ((int)value).ToString()); }
        }

        public static DataSourceType SeriesDescriptionSource
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = 0;
                int.TryParse(appSettings["SeriesDescriptionSource"], out val);
                if (val <= 0)
                    return DataSourceType.AniDB;
                return (DataSourceType)val;
            }
            set { UpdateSetting("SeriesDescriptionSource", ((int)value).ToString()); }
        }

        public static DataSourceType SeriesNameSource
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = 0;
                int.TryParse(appSettings["SeriesNameSource"], out val);
                if (val <= 0)
                    return DataSourceType.AniDB;
                return (DataSourceType)val;
            }
            set { UpdateSetting("SeriesNameSource", ((int)value).ToString()); }
        }

        public static string BaseImagesPath
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["BaseImagesPath"];
            }
            set
            {
                UpdateSetting("BaseImagesPath", value);
                ServerState.Instance.BaseImagePath = ImageUtils.GetBaseImagesPath();
            }
        }


        public static bool BaseImagesPathIsDefault
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var basePath = appSettings["BaseImagesPathIsDefault"];
                if (!string.IsNullOrEmpty(basePath))
                {
                    var val = true;
                    bool.TryParse(basePath, out val);
                    return val;
                }
                return true;
            }
            set
            {
                UpdateSetting("BaseImagesPathIsDefault", value.ToString());
                ServerState.Instance.BaseImagePath = ImageUtils.GetBaseImagesPath();
            }
        }

        public static string VLCLocation
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["VLCLocation"];
            }
            set
            {
                UpdateSetting("VLCLocation", value);
                ServerState.Instance.VLCLocation = value;
            }
        }

        public static bool MinimizeOnStartup
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = false;
                bool.TryParse(appSettings["MinimizeOnStartup"], out val);
                return val;
            }
            set { UpdateSetting("MinimizeOnStartup", value.ToString()); }
        }

        public static bool AllowMultipleInstances
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = false;
                if (!bool.TryParse(appSettings["AllowMultipleInstances"], out val))
                    val = false;
                return val;
            }
            set { UpdateSetting("AllowMultipleInstances", value.ToString()); }
        }


        public static string WebCacheAuthKey
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["WebCacheAuthKey"];
            }
            set { UpdateSetting("WebCacheAuthKey", value); }
        }

        public static bool EnablePlex
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var basePath = appSettings["EnablePlex"];
                if (!string.IsNullOrEmpty(basePath))
                {
                    var val = true;
                    bool.TryParse(basePath, out val);
                    return val;
                }
                return true;
            }
            set { UpdateSetting("EnablePlex", value.ToString()); }
        }

        public static bool EnableKodi
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var basePath = appSettings["EnableKodi"];
                if (!string.IsNullOrEmpty(basePath))
                {
                    var val = true;
                    bool.TryParse(basePath, out val);
                    return val;
                }
                return true;
            }
            set { UpdateSetting("EnableKodi", value.ToString()); }
        }

        public static void CreateDefaultConfig()
        {
            var assm = Assembly.GetExecutingAssembly();
            // check if the app config file exists

            var appConfigPath = assm.Location + ".config";
            var defaultConfigPath = Path.Combine(Path.GetDirectoryName(assm.Location), "default.config");
            if (!File.Exists(appConfigPath) && File.Exists(defaultConfigPath))
            {
                File.Copy(defaultConfigPath, appConfigPath);
            }
        }

        public static void UpdateSetting(string key, string value)
        {
            var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

            if (config.AppSettings.Settings.AllKeys.Contains(key))
                config.AppSettings.Settings[key].Value = value;
            else
                config.AppSettings.Settings.Add(key, value);

            config.Save();
            ConfigurationManager.RefreshSection("appSettings");
        }

        public static Contract_ServerSettings ToContract()
        {
            var contract = new Contract_ServerSettings();

            contract.AniDB_Username = AniDB_Username;
            contract.AniDB_Password = AniDB_Password;
            contract.AniDB_ServerAddress = AniDB_ServerAddress;
            contract.AniDB_ServerPort = AniDB_ServerPort;
            contract.AniDB_ClientPort = AniDB_ClientPort;
            contract.AniDB_AVDumpClientPort = AniDB_AVDumpClientPort;
            contract.AniDB_AVDumpKey = AniDB_AVDumpKey;

            contract.AniDB_DownloadRelatedAnime = AniDB_DownloadRelatedAnime;
            contract.AniDB_DownloadSimilarAnime = AniDB_DownloadSimilarAnime;
            contract.AniDB_DownloadReviews = AniDB_DownloadReviews;
            contract.AniDB_DownloadReleaseGroups = AniDB_DownloadReleaseGroups;

            contract.AniDB_MyList_AddFiles = AniDB_MyList_AddFiles;
            contract.AniDB_MyList_StorageState = (int)AniDB_MyList_StorageState;
            contract.AniDB_MyList_DeleteType = (int)AniDB_MyList_DeleteType;
            contract.AniDB_MyList_ReadWatched = AniDB_MyList_ReadWatched;
            contract.AniDB_MyList_ReadUnwatched = AniDB_MyList_ReadUnwatched;
            contract.AniDB_MyList_SetWatched = AniDB_MyList_SetWatched;
            contract.AniDB_MyList_SetUnwatched = AniDB_MyList_SetUnwatched;

            contract.AniDB_MyList_UpdateFrequency = (int)AniDB_MyList_UpdateFrequency;
            contract.AniDB_Calendar_UpdateFrequency = (int)AniDB_Calendar_UpdateFrequency;
            contract.AniDB_Anime_UpdateFrequency = (int)AniDB_Anime_UpdateFrequency;
            contract.AniDB_MyListStats_UpdateFrequency = (int)AniDB_MyListStats_UpdateFrequency;
            contract.AniDB_File_UpdateFrequency = (int)AniDB_File_UpdateFrequency;

            contract.AniDB_DownloadCharacters = AniDB_DownloadCharacters;
            contract.AniDB_DownloadCreators = AniDB_DownloadCreators;

            // Web Cache
            contract.WebCache_Address = WebCache_Address;
            contract.WebCache_Anonymous = WebCache_Anonymous;
            contract.WebCache_XRefFileEpisode_Get = WebCache_XRefFileEpisode_Get;
            contract.WebCache_XRefFileEpisode_Send = WebCache_XRefFileEpisode_Send;
            contract.WebCache_TvDB_Get = WebCache_TvDB_Get;
            contract.WebCache_TvDB_Send = WebCache_TvDB_Send;
            contract.WebCache_Trakt_Get = WebCache_Trakt_Get;
            contract.WebCache_Trakt_Send = WebCache_Trakt_Send;
            contract.WebCache_MAL_Get = WebCache_MAL_Get;
            contract.WebCache_MAL_Send = WebCache_MAL_Send;
            contract.WebCache_UserInfo = WebCache_UserInfo;

            // TvDB
            contract.TvDB_AutoFanart = TvDB_AutoFanart;
            contract.TvDB_AutoFanartAmount = TvDB_AutoFanartAmount;
            contract.TvDB_AutoPosters = TvDB_AutoPosters;
            contract.TvDB_AutoPostersAmount = TvDB_AutoPostersAmount;
            contract.TvDB_AutoWideBanners = TvDB_AutoWideBanners;
            contract.TvDB_AutoWideBannersAmount = TvDB_AutoWideBannersAmount;
            contract.TvDB_UpdateFrequency = (int)TvDB_UpdateFrequency;
            contract.TvDB_Language = TvDB_Language;

            // MovieDB
            contract.MovieDB_AutoFanart = MovieDB_AutoFanart;
            contract.MovieDB_AutoFanartAmount = MovieDB_AutoFanartAmount;
            contract.MovieDB_AutoPosters = MovieDB_AutoPosters;
            contract.MovieDB_AutoPostersAmount = MovieDB_AutoPostersAmount;

            // Import settings
            contract.VideoExtensions = VideoExtensions;
            contract.AutoGroupSeries = AutoGroupSeries;
            contract.Import_UseExistingFileWatchedStatus = Import_UseExistingFileWatchedStatus;
            contract.RunImportOnStart = RunImportOnStart;
            contract.ScanDropFoldersOnStart = ScanDropFoldersOnStart;
            contract.Hash_CRC32 = Hash_CRC32;
            contract.Hash_MD5 = Hash_MD5;
            contract.Hash_SHA1 = Hash_SHA1;

            // Language
            contract.LanguagePreference = LanguagePreference;
            contract.LanguageUseSynonyms = LanguageUseSynonyms;
            contract.EpisodeTitleSource = (int)EpisodeTitleSource;
            contract.SeriesDescriptionSource = (int)SeriesDescriptionSource;
            contract.SeriesNameSource = (int)SeriesNameSource;

            // trakt
            contract.Trakt_IsEnabled = Trakt_IsEnabled;
            contract.Trakt_AuthToken = Trakt_AuthToken;
            contract.Trakt_RefreshToken = Trakt_RefreshToken;
            contract.Trakt_TokenExpirationDate = Trakt_TokenExpirationDate;
            contract.Trakt_UpdateFrequency = (int)Trakt_UpdateFrequency;
            contract.Trakt_SyncFrequency = (int)Trakt_SyncFrequency;
            contract.Trakt_DownloadEpisodes = Trakt_DownloadEpisodes;
            contract.Trakt_DownloadFanart = Trakt_DownloadFanart;
            contract.Trakt_DownloadPosters = Trakt_DownloadPosters;

            // MAL
            contract.MAL_Username = MAL_Username;
            contract.MAL_Password = MAL_Password;
            contract.MAL_UpdateFrequency = (int)MAL_UpdateFrequency;
            contract.MAL_NeverDecreaseWatchedNums = MAL_NeverDecreaseWatchedNums;


            return contract;
        }

        public static void DebugSettingsToLog()
        {
            #region System Info

            logger.Info("-------------------- SYSTEM INFO -----------------------");

            var a = Assembly.GetExecutingAssembly();
            try
            {
                if (a != null)
                {
                    logger.Info(string.Format("JMM Server Version: v{0}", Utils.GetApplicationVersion(a)));
                }
            }
            catch (Exception ex)
            {
                // oopps, can't create file
                logger.Warn("Error in log: {0}", ex.ToString());
            }

            try
            {
                var repVersions = new VersionsRepository();
                var ver = repVersions.GetByVersionType(Constants.DatabaseTypeKey);
                if (ver != null)
                    logger.Info(string.Format("Database Version: {0}", ver.VersionValue));
            }
            catch (Exception ex)
            {
                // oopps, can't create file
                logger.Warn("Error in log: {0}", ex.Message);
            }

            logger.Info(string.Format("Operating System: {0}", Utils.GetOSInfo()));

            var screenSize = Screen.PrimaryScreen.Bounds.Width + "x" +
                             Screen.PrimaryScreen.Bounds.Height;
            logger.Info(string.Format("Screen Size: {0}", screenSize));


            try
            {
                var mediaInfoVersion = "**** MediaInfo - DLL Not found *****";

                var mediaInfoPath = Assembly.GetExecutingAssembly().Location;
                var fi = new FileInfo(mediaInfoPath);
                mediaInfoPath = Path.Combine(fi.Directory.FullName, Environment.Is64BitProcess ? "x64" : "x86",
                    "MediaInfo.dll");

                if (File.Exists(mediaInfoPath))
                {
                    var fvi = FileVersionInfo.GetVersionInfo(mediaInfoPath);
                    mediaInfoVersion = string.Format("MediaInfo DLL {0}.{1}.{2}.{3} ({4})", fvi.FileMajorPart,
                        fvi.FileMinorPart, fvi.FileBuildPart, fvi.FilePrivatePart, mediaInfoPath);
                }
                logger.Info(mediaInfoVersion);

                var hasherInfoVersion = "**** Hasher - DLL NOT found *****";

                var fullHasherexepath = Assembly.GetExecutingAssembly().Location;
                fi = new FileInfo(fullHasherexepath);
                fullHasherexepath = Path.Combine(fi.Directory.FullName, Environment.Is64BitProcess ? "x64" : "x86",
                    "hasher.dll");

                if (File.Exists(fullHasherexepath))
                    hasherInfoVersion = string.Format("Hasher DLL found at {0}", fullHasherexepath);
                logger.Info(hasherInfoVersion);
            }
            catch
            {
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


            logger.Info("WebCache_Address: {0}", WebCache_Address);
            logger.Info("WebCache_Anonymous: {0}", WebCache_Anonymous);
            logger.Info("WebCache_XRefFileEpisode_Get: {0}", WebCache_XRefFileEpisode_Get);
            logger.Info("WebCache_XRefFileEpisode_Send: {0}", WebCache_XRefFileEpisode_Send);
            logger.Info("WebCache_TvDB_Get: {0}", WebCache_TvDB_Get);
            logger.Info("WebCache_TvDB_Send: {0}", WebCache_TvDB_Send);
            logger.Info("WebCache_MAL_Get: {0}", WebCache_MAL_Get);
            logger.Info("WebCache_MAL_Send: {0}", WebCache_MAL_Send);

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
            logger.Info("Trakt_RefreshToken: {0}", string.IsNullOrEmpty(Trakt_RefreshToken) ? "NOT SET" : "***HIDDEN***");
            logger.Info("Trakt_UpdateFrequency: {0}", Trakt_UpdateFrequency);
            logger.Info("Trakt_SyncFrequency: {0}", Trakt_SyncFrequency);

            logger.Info("AutoGroupSeries: {0}", AutoGroupSeries);
            logger.Info("LanguagePreference: {0}", LanguagePreference);
            logger.Info("LanguageUseSynonyms: {0}", LanguageUseSynonyms);
            logger.Info("EpisodeTitleSource: {0}", EpisodeTitleSource);
            logger.Info("SeriesDescriptionSource: {0}", SeriesDescriptionSource);
            logger.Info("SeriesNameSource: {0}", SeriesNameSource);
            logger.Info("BaseImagesPath: {0}", BaseImagesPath);
            logger.Info("BaseImagesPathIsDefault: {0}", BaseImagesPathIsDefault);


            logger.Info("-------------------------------------------------------");
        }

        #region Database

        public static string DatabaseType
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["DatabaseType"];
            }
            set { UpdateSetting("DatabaseType", value); }
        }

        public static string DatabaseServer
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["SQLServer_DatabaseServer"];
            }
            set { UpdateSetting("SQLServer_DatabaseServer", value); }
        }

        public static string DatabaseName
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["SQLServer_DatabaseName"];
            }
            set { UpdateSetting("SQLServer_DatabaseName", value); }
        }

        public static string DatabaseUsername
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["SQLServer_Username"];
            }
            set { UpdateSetting("SQLServer_Username", value); }
        }

        public static string DatabasePassword
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["SQLServer_Password"];
            }
            set { UpdateSetting("SQLServer_Password", value); }
        }

        public static string DatabaseFile
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["SQLite_DatabaseFile"];
            }
            set { UpdateSetting("SQLite_DatabaseFile", value); }
        }

        public static string MySQL_Hostname
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["MySQL_Hostname"];
            }
            set { UpdateSetting("MySQL_Hostname", value); }
        }

        public static string MySQL_SchemaName
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["MySQL_SchemaName"];
            }
            set { UpdateSetting("MySQL_SchemaName", value); }
        }

        public static string MySQL_Username
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["MySQL_Username"];
            }
            set { UpdateSetting("MySQL_Username", value); }
        }

        public static string MySQL_Password
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["MySQL_Password"];
            }
            set { UpdateSetting("MySQL_Password", value); }
        }

        #endregion

        #region AniDB

        public static string AniDB_Username
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["AniDB_Username"];
            }
            set { UpdateSetting("AniDB_Username", value); }
        }

        public static string AniDB_Password
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["AniDB_Password"];
            }
            set { UpdateSetting("AniDB_Password", value); }
        }

        public static string AniDB_ServerAddress
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["AniDB_ServerAddress"];
            }
            set { UpdateSetting("AniDB_ServerAddress", value); }
        }

        public static string AniDB_ServerPort
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["AniDB_ServerPort"];
            }
            set { UpdateSetting("AniDB_ServerPort", value); }
        }

        public static string AniDB_ClientPort
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["AniDB_ClientPort"];
            }
            set { UpdateSetting("AniDB_ClientPort", value); }
        }

        public static string AniDB_AVDumpKey
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["AniDB_AVDumpKey"];
            }
            set { UpdateSetting("AniDB_AVDumpKey", value); }
        }

        public static string AniDB_AVDumpClientPort
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["AniDB_AVDumpClientPort"];
            }
            set { UpdateSetting("AniDB_AVDumpClientPort", value); }
        }

        public static bool AniDB_DownloadRelatedAnime
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var download = false;
                bool.TryParse(appSettings["AniDB_DownloadRelatedAnime"], out download);
                return download;
            }
            set { UpdateSetting("AniDB_DownloadRelatedAnime", value.ToString()); }
        }

        public static bool AniDB_DownloadSimilarAnime
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var download = false;
                bool.TryParse(appSettings["AniDB_DownloadSimilarAnime"], out download);
                return download;
            }
            set { UpdateSetting("AniDB_DownloadSimilarAnime", value.ToString()); }
        }

        public static bool AniDB_DownloadReviews
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var download = false;
                bool.TryParse(appSettings["AniDB_DownloadReviews"], out download);
                return download;
            }
            set { UpdateSetting("AniDB_DownloadReviews", value.ToString()); }
        }

        public static bool AniDB_DownloadReleaseGroups
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var download = false;
                bool.TryParse(appSettings["AniDB_DownloadReleaseGroups"], out download);
                return download;
            }
            set { UpdateSetting("AniDB_DownloadReleaseGroups", value.ToString()); }
        }

        public static bool AniDB_MyList_AddFiles
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = false;
                bool.TryParse(appSettings["AniDB_MyList_AddFiles"], out val);
                return val;
            }
            set { UpdateSetting("AniDB_MyList_AddFiles", value.ToString()); }
        }

        public static AniDBFileStatus AniDB_MyList_StorageState
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = 1;
                int.TryParse(appSettings["AniDB_MyList_StorageState"], out val);

                return (AniDBFileStatus)val;
            }
            set { UpdateSetting("AniDB_MyList_StorageState", ((int)value).ToString()); }
        }

        public static AniDBFileDeleteType AniDB_MyList_DeleteType
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = 0;
                int.TryParse(appSettings["AniDB_MyList_DeleteType"], out val);

                return (AniDBFileDeleteType)val;
            }
            set { UpdateSetting("AniDB_MyList_DeleteType", ((int)value).ToString()); }
        }

        public static bool AniDB_MyList_ReadUnwatched
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = false;
                bool.TryParse(appSettings["AniDB_MyList_ReadUnwatched"], out val);
                return val;
            }
            set { UpdateSetting("AniDB_MyList_ReadUnwatched", value.ToString()); }
        }

        public static bool AniDB_MyList_ReadWatched
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = false;
                bool.TryParse(appSettings["AniDB_MyList_ReadWatched"], out val);
                return val;
            }
            set { UpdateSetting("AniDB_MyList_ReadWatched", value.ToString()); }
        }

        public static bool AniDB_MyList_SetWatched
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = false;
                bool.TryParse(appSettings["AniDB_MyList_SetWatched"], out val);
                return val;
            }
            set { UpdateSetting("AniDB_MyList_SetWatched", value.ToString()); }
        }

        public static bool AniDB_MyList_SetUnwatched
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = false;
                bool.TryParse(appSettings["AniDB_MyList_SetUnwatched"], out val);
                return val;
            }
            set { UpdateSetting("AniDB_MyList_SetUnwatched", value.ToString()); }
        }

        public static ScheduledUpdateFrequency AniDB_MyList_UpdateFrequency
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = 1;
                if (int.TryParse(appSettings["AniDB_MyList_UpdateFrequency"], out val))
                    return (ScheduledUpdateFrequency)val;
                return ScheduledUpdateFrequency.Never; // default value
            }
            set { UpdateSetting("AniDB_MyList_UpdateFrequency", ((int)value).ToString()); }
        }

        public static ScheduledUpdateFrequency AniDB_Calendar_UpdateFrequency
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = 1;
                if (int.TryParse(appSettings["AniDB_Calendar_UpdateFrequency"], out val))
                    return (ScheduledUpdateFrequency)val;
                return ScheduledUpdateFrequency.HoursTwelve; // default value
            }
            set { UpdateSetting("AniDB_Calendar_UpdateFrequency", ((int)value).ToString()); }
        }

        public static ScheduledUpdateFrequency AniDB_Anime_UpdateFrequency
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = 1;
                if (int.TryParse(appSettings["AniDB_Anime_UpdateFrequency"], out val))
                    return (ScheduledUpdateFrequency)val;
                return ScheduledUpdateFrequency.HoursTwelve; // default value
            }
            set { UpdateSetting("AniDB_Anime_UpdateFrequency", ((int)value).ToString()); }
        }

        public static ScheduledUpdateFrequency AniDB_MyListStats_UpdateFrequency
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = 1;
                if (int.TryParse(appSettings["AniDB_MyListStats_UpdateFrequency"], out val))
                    return (ScheduledUpdateFrequency)val;
                return ScheduledUpdateFrequency.Never; // default value
            }
            set { UpdateSetting("AniDB_MyListStats_UpdateFrequency", ((int)value).ToString()); }
        }

        public static ScheduledUpdateFrequency AniDB_File_UpdateFrequency
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = 1;
                if (int.TryParse(appSettings["AniDB_File_UpdateFrequency"], out val))
                    return (ScheduledUpdateFrequency)val;
                return ScheduledUpdateFrequency.Daily; // default value
            }
            set { UpdateSetting("AniDB_File_UpdateFrequency", ((int)value).ToString()); }
        }

        public static bool AniDB_DownloadCharacters
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = true;
                if (!bool.TryParse(appSettings["AniDB_DownloadCharacters"], out val))
                    val = true; // default
                return val;
            }
            set { UpdateSetting("AniDB_DownloadCharacters", value.ToString()); }
        }

        public static bool AniDB_DownloadCreators
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = true;
                if (!bool.TryParse(appSettings["AniDB_DownloadCreators"], out val))
                    val = true; // default
                return val;
            }
            set { UpdateSetting("AniDB_DownloadCreators", value.ToString()); }
        }

        #endregion

        #region Web Cache

        public static string WebCache_Address
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["WebCache_Address"];
            }
            set { UpdateSetting("WebCache_Address", value); }
        }

        public static bool WebCache_Anonymous
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = false;
                bool.TryParse(appSettings["WebCache_Anonymous"], out val);
                return val;
            }
            set { UpdateSetting("WebCache_Anonymous", value.ToString()); }
        }

        public static bool WebCache_XRefFileEpisode_Get
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var usecache = false;
                bool.TryParse(appSettings["WebCache_XRefFileEpisode_Get"], out usecache);
                return usecache;
            }
            set { UpdateSetting("WebCache_XRefFileEpisode_Get", value.ToString()); }
        }

        public static bool WebCache_XRefFileEpisode_Send
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var usecache = false;
                bool.TryParse(appSettings["WebCache_XRefFileEpisode_Send"], out usecache);
                return usecache;
            }
            set { UpdateSetting("WebCache_XRefFileEpisode_Send", value.ToString()); }
        }

        public static bool WebCache_TvDB_Get
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var usecache = true;
                if (bool.TryParse(appSettings["WebCache_TvDB_Get"], out usecache))
                    return usecache;
                return true; // default
            }
            set { UpdateSetting("WebCache_TvDB_Get", value.ToString()); }
        }

        public static bool WebCache_TvDB_Send
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var usecache = true;
                if (bool.TryParse(appSettings["WebCache_TvDB_Send"], out usecache))
                    return usecache;
                return true; // default
            }
            set { UpdateSetting("WebCache_TvDB_Send", value.ToString()); }
        }

        public static bool WebCache_Trakt_Get
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var usecache = true;
                if (bool.TryParse(appSettings["WebCache_Trakt_Get"], out usecache))
                    return usecache;
                return true; // default
            }
            set { UpdateSetting("WebCache_Trakt_Get", value.ToString()); }
        }

        public static bool WebCache_Trakt_Send
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var usecache = true;
                if (bool.TryParse(appSettings["WebCache_Trakt_Send"], out usecache))
                    return usecache;
                return true; // default
            }
            set { UpdateSetting("WebCache_Trakt_Send", value.ToString()); }
        }

        public static bool WebCache_MAL_Get
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var usecache = true;
                if (bool.TryParse(appSettings["WebCache_MAL_Get"], out usecache))
                    return usecache;
                return true; // default
            }
            set { UpdateSetting("WebCache_MAL_Get", value.ToString()); }
        }

        public static bool WebCache_MAL_Send
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var usecache = true;
                if (bool.TryParse(appSettings["WebCache_MAL_Send"], out usecache))
                    return usecache;
                return true; // default
            }
            set { UpdateSetting("WebCache_MAL_Send", value.ToString()); }
        }

        public static bool WebCache_UserInfo
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var usecache = false;
                if (bool.TryParse(appSettings["WebCache_UserInfo"], out usecache))
                    return usecache;
                return true; // default
            }
            set { UpdateSetting("WebCache_UserInfo", value.ToString()); }
        }

        #endregion

        #region TvDB

        public static bool TvDB_AutoFanart
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = false;
                bool.TryParse(appSettings["TvDB_AutoFanart"], out val);
                return val;
            }
            set { UpdateSetting("TvDB_AutoFanart", value.ToString()); }
        }

        public static int TvDB_AutoFanartAmount
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = 0;
                int.TryParse(appSettings["TvDB_AutoFanartAmount"], out val);
                return val;
            }
            set { UpdateSetting("TvDB_AutoFanartAmount", value.ToString()); }
        }

        public static bool TvDB_AutoWideBanners
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = false;
                bool.TryParse(appSettings["TvDB_AutoWideBanners"], out val);
                return val;
            }
            set { UpdateSetting("TvDB_AutoWideBanners", value.ToString()); }
        }

        public static int TvDB_AutoWideBannersAmount
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = 0;
                if (!int.TryParse(appSettings["TvDB_AutoWideBannersAmount"], out val))
                    val = 10; // default
                return val;
            }
            set { UpdateSetting("TvDB_AutoWideBannersAmount", value.ToString()); }
        }

        public static bool TvDB_AutoPosters
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = false;
                bool.TryParse(appSettings["TvDB_AutoPosters"], out val);
                return val;
            }
            set { UpdateSetting("TvDB_AutoPosters", value.ToString()); }
        }

        public static int TvDB_AutoPostersAmount
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = 0;
                if (!int.TryParse(appSettings["TvDB_AutoPostersAmount"], out val))
                    val = 10; // default
                return val;
            }
            set { UpdateSetting("TvDB_AutoPostersAmount", value.ToString()); }
        }

        public static ScheduledUpdateFrequency TvDB_UpdateFrequency
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = 1;
                if (int.TryParse(appSettings["TvDB_UpdateFrequency"], out val))
                    return (ScheduledUpdateFrequency)val;
                return ScheduledUpdateFrequency.HoursTwelve; // default value
            }
            set { UpdateSetting("TvDB_UpdateFrequency", ((int)value).ToString()); }
        }

        public static string TvDB_Language
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var language = appSettings["TvDB_Language"];
                if (string.IsNullOrEmpty(language))
                    return "en";
                return language;
            }
            set { UpdateSetting("TvDB_Language", value); }
        }

        #endregion

        #region MovieDB

        public static bool MovieDB_AutoFanart
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = false;
                bool.TryParse(appSettings["MovieDB_AutoFanart"], out val);
                return val;
            }
            set { UpdateSetting("MovieDB_AutoFanart", value.ToString()); }
        }

        public static int MovieDB_AutoFanartAmount
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = 0;
                int.TryParse(appSettings["MovieDB_AutoFanartAmount"], out val);
                return val;
            }
            set { UpdateSetting("MovieDB_AutoFanartAmount", value.ToString()); }
        }

        public static bool MovieDB_AutoPosters
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = false;
                bool.TryParse(appSettings["MovieDB_AutoPosters"], out val);
                return val;
            }
            set { UpdateSetting("MovieDB_AutoPosters", value.ToString()); }
        }

        public static int MovieDB_AutoPostersAmount
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = 0;
                if (!int.TryParse(appSettings["MovieDB_AutoPostersAmount"], out val))
                    val = 10; // default
                return val;
            }
            set { UpdateSetting("MovieDB_AutoPostersAmount", value.ToString()); }
        }

        #endregion

        #region Import Settings

        public static string VideoExtensions
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["VideoExtensions"];
            }
            set { UpdateSetting("VideoExtensions", value); }
        }

        public static RenamingLanguage DefaultSeriesLanguage
        {
            get
            {
                var rl = RenamingLanguage.Romaji;
                var appSettings = ConfigurationManager.AppSettings;

                var rls = appSettings["DefaultSeriesLanguage"];
                if (string.IsNullOrEmpty(rls)) return rl;

                rl = (RenamingLanguage)int.Parse(rls);

                return rl;
            }
            set { UpdateSetting("DefaultSeriesLanguage", ((int)value).ToString()); }
        }

        public static RenamingLanguage DefaultEpisodeLanguage
        {
            get
            {
                var rl = RenamingLanguage.Romaji;
                var appSettings = ConfigurationManager.AppSettings;

                var rls = appSettings["DefaultEpisodeLanguage"];
                if (string.IsNullOrEmpty(rls)) return rl;

                rl = (RenamingLanguage)int.Parse(rls);

                return rl;
            }
            set { UpdateSetting("DefaultEpisodeLanguage", ((int)value).ToString()); }
        }

        public static bool RunImportOnStart
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = false;
                bool.TryParse(appSettings["RunImportOnStart"], out val);
                return val;
            }
            set { UpdateSetting("RunImportOnStart", value.ToString()); }
        }

        public static bool ScanDropFoldersOnStart
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = false;
                bool.TryParse(appSettings["ScanDropFoldersOnStart"], out val);
                return val;
            }
            set { UpdateSetting("ScanDropFoldersOnStart", value.ToString()); }
        }

        public static bool Hash_CRC32
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var bval = false;
                bool.TryParse(appSettings["Hash_CRC32"], out bval);
                return bval;
            }
            set { UpdateSetting("Hash_CRC32", value.ToString()); }
        }

        public static bool Hash_MD5
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var bval = false;
                bool.TryParse(appSettings["Hash_MD5"], out bval);
                return bval;
            }
            set { UpdateSetting("Hash_MD5", value.ToString()); }
        }

        public static bool ExperimentalUPnP
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var bval = false;
                bool.TryParse(appSettings["ExperimentalUPnP"], out bval);
                return bval;
            }
            set { UpdateSetting("ExperimentalUPnP", value.ToString()); }
        }

        public static bool Hash_SHA1
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var bval = false;
                bool.TryParse(appSettings["Hash_SHA1"], out bval);
                return bval;
            }
            set { UpdateSetting("Hash_SHA1", value.ToString()); }
        }

        public static bool Import_UseExistingFileWatchedStatus
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var bval = false;
                bool.TryParse(appSettings["Import_UseExistingFileWatchedStatus"], out bval);
                return bval;
            }
            set { UpdateSetting("Import_UseExistingFileWatchedStatus", value.ToString()); }
        }

        #endregion

        #region Trakt

        public static bool Trakt_IsEnabled
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = true;
                if (!bool.TryParse(appSettings["Trakt_IsEnabled"], out val))
                    val = true;
                return val;
            }
            set { UpdateSetting("Trakt_IsEnabled", value.ToString()); }
        }

        public static string Trakt_AuthToken
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["Trakt_AuthToken"];
            }
            set { UpdateSetting("Trakt_AuthToken", value); }
        }

        public static string Trakt_RefreshToken
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["Trakt_RefreshToken"];
            }
            set { UpdateSetting("Trakt_RefreshToken", value); }
        }

        public static string Trakt_TokenExpirationDate
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["Trakt_TokenExpirationDate"];
            }
            set { UpdateSetting("Trakt_TokenExpirationDate", value); }
        }

        public static ScheduledUpdateFrequency Trakt_UpdateFrequency
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = 1;
                if (int.TryParse(appSettings["Trakt_UpdateFrequency"], out val))
                    return (ScheduledUpdateFrequency)val;
                return ScheduledUpdateFrequency.Daily; // default value
            }
            set { UpdateSetting("Trakt_UpdateFrequency", ((int)value).ToString()); }
        }

        public static ScheduledUpdateFrequency Trakt_SyncFrequency
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = 1;
                if (int.TryParse(appSettings["Trakt_SyncFrequency"], out val))
                    return (ScheduledUpdateFrequency)val;
                return ScheduledUpdateFrequency.Never; // default value
            }
            set { UpdateSetting("Trakt_SyncFrequency", ((int)value).ToString()); }
        }

        public static bool Trakt_DownloadFanart
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = true;
                if (!bool.TryParse(appSettings["Trakt_DownloadFanart"], out val))
                    val = true; // default
                return val;
            }
            set { UpdateSetting("Trakt_DownloadFanart", value.ToString()); }
        }

        public static bool Trakt_DownloadPosters
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = true;
                if (!bool.TryParse(appSettings["Trakt_DownloadPosters"], out val))
                    val = true; // default
                return val;
            }
            set { UpdateSetting("Trakt_DownloadPosters", value.ToString()); }
        }

        public static bool Trakt_DownloadEpisodes
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = true;
                if (!bool.TryParse(appSettings["Trakt_DownloadEpisodes"], out val))
                    val = true; // default
                return val;
            }
            set { UpdateSetting("Trakt_DownloadEpisodes", value.ToString()); }
        }

        #endregion

        #region MAL

        public static string MAL_Username
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["MAL_Username"];
            }
            set { UpdateSetting("MAL_Username", value); }
        }

        public static string MAL_Password
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                return appSettings["MAL_Password"];
            }
            set { UpdateSetting("MAL_Password", value); }
        }

        public static ScheduledUpdateFrequency MAL_UpdateFrequency
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var val = 1;
                if (int.TryParse(appSettings["MAL_UpdateFrequency"], out val))
                    return (ScheduledUpdateFrequency)val;
                return ScheduledUpdateFrequency.Daily; // default value
            }
            set { UpdateSetting("MAL_UpdateFrequency", ((int)value).ToString()); }
        }

        public static bool MAL_NeverDecreaseWatchedNums
        {
            get
            {
                var appSettings = ConfigurationManager.AppSettings;
                var wtchNum = appSettings["MAL_NeverDecreaseWatchedNums"];
                if (!string.IsNullOrEmpty(wtchNum))
                {
                    var val = true;
                    bool.TryParse(wtchNum, out val);
                    return val;
                }
                return true;
            }
            set { UpdateSetting("MAL_NeverDecreaseWatchedNums", value.ToString()); }
        }

        #endregion
    }
}