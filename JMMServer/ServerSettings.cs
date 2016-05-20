using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Specialized;
using System.Configuration;
using AniDBAPI;
using JMMContracts;
using System.IO;
using JMMServer.ImageDownload;
using NLog;
using System.Diagnostics;
using System.Threading;
using JMMServer.Repositories;
using JMMServer.Entities;

namespace JMMServer
{
	public class ServerSettings
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public static void CreateDefaultConfig()
		{
			System.Reflection.Assembly assm = System.Reflection.Assembly.GetExecutingAssembly();
			// check if the app config file exists

			string appConfigPath = assm.Location + ".config";
			string defaultConfigPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(assm.Location), "default.config");
			if (!File.Exists(appConfigPath) && File.Exists(defaultConfigPath))
			{
				File.Copy(defaultConfigPath, appConfigPath);
			}
		}

		public static string JMMServerPort
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;

				string serverPort = appSettings["JMMServerPort"];
				if (string.IsNullOrEmpty(serverPort))
				{
					serverPort = "8111";
					UpdateSetting("JMMServerPort", serverPort); 
				}

				return serverPort;
			}
			set
			{
				UpdateSetting("JMMServerPort", value);
			}
		}

		public static string JMMServerFilePort
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;

				string serverPort = appSettings["JMMServerFilePort"];
				if (string.IsNullOrEmpty(serverPort))
				{
					serverPort = "8112";
					UpdateSetting("JMMServerFilePort", serverPort);
				}

				return serverPort;
			}
			set
			{
				UpdateSetting("JMMServerFilePort", value);
			}
		}

	    public static string PlexThumbnailAspects
	    {
            get
            {
                NameValueCollection appSettings = ConfigurationManager.AppSettings;
                string thumbaspect = appSettings["PlexThumbnailAspects"];
                if (string.IsNullOrEmpty(thumbaspect))
                {
                    thumbaspect = "Default, 0.6667, IOS, 1.0, Android, 1.3333";
                    UpdateSetting("PlexThumbnailAspects", thumbaspect);
                }

                return thumbaspect;
            }
            set
            {
                UpdateSetting("PlexThumbnailAspect", value);
            }
        }

        public static string Culture
        {
            get
            {
                NameValueCollection appSettings = ConfigurationManager.AppSettings;

                string cult = appSettings["Culture"];
                if (string.IsNullOrEmpty(cult))
                {
                    // default value
                    cult = "en";
                    UpdateSetting("Culture", cult);
                }
                return cult;
            }
            set
            {
                UpdateSetting("Culture", value);
            }
        }

        #region Database

        public static string DatabaseType
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["DatabaseType"];
			}
			set
			{
				UpdateSetting("DatabaseType", value);
			}
		}

		public static string DatabaseServer
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["SQLServer_DatabaseServer"];
			}
			set
			{
				UpdateSetting("SQLServer_DatabaseServer", value);
			}
		}

		public static string DatabaseName
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["SQLServer_DatabaseName"];
			}
			set
			{
				UpdateSetting("SQLServer_DatabaseName", value);
			}
		}

		public static string DatabaseUsername
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["SQLServer_Username"];
			}
			set
			{
				UpdateSetting("SQLServer_Username", value);
			}
		}

		public static string DatabasePassword
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["SQLServer_Password"];
			}
			set
			{
				UpdateSetting("SQLServer_Password", value);
			}
		}

		public static string DatabaseFile
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["SQLite_DatabaseFile"];
			}
			set
			{
				UpdateSetting("SQLite_DatabaseFile", value);
			}
		}

		public static string MySQL_Hostname
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["MySQL_Hostname"];
			}
			set
			{
				UpdateSetting("MySQL_Hostname", value);
			}
		}

		public static string MySQL_SchemaName
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["MySQL_SchemaName"];
			}
			set
			{
				UpdateSetting("MySQL_SchemaName", value);
			}
		}

		public static string MySQL_Username
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["MySQL_Username"];
			}
			set
			{
				UpdateSetting("MySQL_Username", value);
			}
		}

		public static string MySQL_Password
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["MySQL_Password"];
			}
			set
			{
				UpdateSetting("MySQL_Password", value);
			}
		}

		#endregion

		#region AniDB

		public static string AniDB_Username
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["AniDB_Username"];
			}
			set
			{
				UpdateSetting("AniDB_Username", value);
			}
		}

		public static string AniDB_Password
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["AniDB_Password"];
			}
			set
			{
				UpdateSetting("AniDB_Password", value);
			}
		}

		public static string AniDB_ServerAddress
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["AniDB_ServerAddress"];
			}
			set
			{
				UpdateSetting("AniDB_ServerAddress", value);
			}
		}

		public static string AniDB_ServerPort
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["AniDB_ServerPort"];
			}
			set
			{
				UpdateSetting("AniDB_ServerPort", value);
			}
		}

		public static string AniDB_ClientPort
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["AniDB_ClientPort"];
			}
			set
			{
				UpdateSetting("AniDB_ClientPort", value);
			}
		}

		public static string AniDB_AVDumpKey
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["AniDB_AVDumpKey"];
			}
			set
			{
				UpdateSetting("AniDB_AVDumpKey", value);
			}
		}

		public static string AniDB_AVDumpClientPort
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["AniDB_AVDumpClientPort"];
			}
			set
			{
				UpdateSetting("AniDB_AVDumpClientPort", value);
			}
		}

		public static bool AniDB_DownloadRelatedAnime
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool download = false;
				bool.TryParse(appSettings["AniDB_DownloadRelatedAnime"], out download);
				return download;
			}
			set
			{
				UpdateSetting("AniDB_DownloadRelatedAnime", value.ToString());
			}
		}

		public static bool AniDB_DownloadSimilarAnime
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool download = false;
				bool.TryParse(appSettings["AniDB_DownloadSimilarAnime"], out download);
				return download;
			}
			set
			{
				UpdateSetting("AniDB_DownloadSimilarAnime", value.ToString());
			}
		}

		public static bool AniDB_DownloadReviews
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool download = false;
				bool.TryParse(appSettings["AniDB_DownloadReviews"], out download);
				return download;
			}
			set
			{
				UpdateSetting("AniDB_DownloadReviews", value.ToString());
			}
		}

		public static bool AniDB_DownloadReleaseGroups
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool download = false;
				bool.TryParse(appSettings["AniDB_DownloadReleaseGroups"], out download);
				return download;
			}
			set
			{
				UpdateSetting("AniDB_DownloadReleaseGroups", value.ToString());
			}
		}

		public static bool AniDB_MyList_AddFiles
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = false;
				bool.TryParse(appSettings["AniDB_MyList_AddFiles"], out val);
				return val;
			}
			set
			{
				UpdateSetting("AniDB_MyList_AddFiles", value.ToString());
			}
		}

		public static AniDBFileStatus AniDB_MyList_StorageState
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				int val = 1;
				int.TryParse(appSettings["AniDB_MyList_StorageState"], out val);

				return (AniDBFileStatus)val;
			}
			set
			{
				UpdateSetting("AniDB_MyList_StorageState", ((int)value).ToString());
			}
		}

		public static AniDBFileDeleteType AniDB_MyList_DeleteType
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				int val = 0;
				int.TryParse(appSettings["AniDB_MyList_DeleteType"], out val);

				return (AniDBFileDeleteType)val;
			}
			set
			{
				UpdateSetting("AniDB_MyList_DeleteType", ((int)value).ToString());
			}
		}

		public static bool AniDB_MyList_ReadUnwatched
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = false;
				bool.TryParse(appSettings["AniDB_MyList_ReadUnwatched"], out val);
				return val;
			}
			set
			{
				UpdateSetting("AniDB_MyList_ReadUnwatched", value.ToString());
			}
		}

		public static bool AniDB_MyList_ReadWatched
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = false;
				bool.TryParse(appSettings["AniDB_MyList_ReadWatched"], out val);
				return val;
			}
			set
			{
				UpdateSetting("AniDB_MyList_ReadWatched", value.ToString());
			}
		}

		public static bool AniDB_MyList_SetWatched
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = false;
				bool.TryParse(appSettings["AniDB_MyList_SetWatched"], out val);
				return val;
			}
			set
			{
				UpdateSetting("AniDB_MyList_SetWatched", value.ToString());
			}
		}

		public static bool AniDB_MyList_SetUnwatched
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = false;
				bool.TryParse(appSettings["AniDB_MyList_SetUnwatched"], out val);
				return val;
			}
			set
			{
				UpdateSetting("AniDB_MyList_SetUnwatched", value.ToString());
			}
		}

		public static ScheduledUpdateFrequency AniDB_MyList_UpdateFrequency
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				int val = 1;
				if (int.TryParse(appSettings["AniDB_MyList_UpdateFrequency"], out val))
					return (ScheduledUpdateFrequency)val;
				else
					return ScheduledUpdateFrequency.Never; // default value
			}
			set
			{
				UpdateSetting("AniDB_MyList_UpdateFrequency", ((int)value).ToString());
			}
		}

		public static ScheduledUpdateFrequency AniDB_Calendar_UpdateFrequency
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				int val = 1;
				if (int.TryParse(appSettings["AniDB_Calendar_UpdateFrequency"], out val))
					return (ScheduledUpdateFrequency)val;
				else
					return ScheduledUpdateFrequency.HoursTwelve; // default value
			}
			set
			{
				UpdateSetting("AniDB_Calendar_UpdateFrequency", ((int)value).ToString());
			}
		}

		public static ScheduledUpdateFrequency AniDB_Anime_UpdateFrequency
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				int val = 1;
				if (int.TryParse(appSettings["AniDB_Anime_UpdateFrequency"], out val))
					return (ScheduledUpdateFrequency)val;
				else
					return ScheduledUpdateFrequency.HoursTwelve; // default value
			}
			set
			{
				UpdateSetting("AniDB_Anime_UpdateFrequency", ((int)value).ToString());
			}
		}

		public static ScheduledUpdateFrequency AniDB_MyListStats_UpdateFrequency
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				int val = 1;
				if (int.TryParse(appSettings["AniDB_MyListStats_UpdateFrequency"], out val))
					return (ScheduledUpdateFrequency)val;
				else
					return ScheduledUpdateFrequency.Never; // default value
			}
			set
			{
				UpdateSetting("AniDB_MyListStats_UpdateFrequency", ((int)value).ToString());
			}
		}

		public static ScheduledUpdateFrequency AniDB_File_UpdateFrequency
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				int val = 1;
				if (int.TryParse(appSettings["AniDB_File_UpdateFrequency"], out val))
					return (ScheduledUpdateFrequency)val;
				else
					return ScheduledUpdateFrequency.Daily; // default value
			}
			set
			{
				UpdateSetting("AniDB_File_UpdateFrequency", ((int)value).ToString());
			}
		}

		public static bool AniDB_DownloadCharacters
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = true;
				if (!bool.TryParse(appSettings["AniDB_DownloadCharacters"], out val))
					val = true; // default
				return val;
			}
			set
			{
				UpdateSetting("AniDB_DownloadCharacters", value.ToString());
			}
		}

		public static bool AniDB_DownloadCreators
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = true;
				if (!bool.TryParse(appSettings["AniDB_DownloadCreators"], out val))
					val = true; // default
				return val;
			}
			set
			{
				UpdateSetting("AniDB_DownloadCreators", value.ToString());
			}
		}

		#endregion

		#region Web Cache

		public static string WebCache_Address
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["WebCache_Address"];
			}
			set
			{
				UpdateSetting("WebCache_Address", value);
			}
		}

		public static bool WebCache_Anonymous
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = false;
				bool.TryParse(appSettings["WebCache_Anonymous"], out val);
				return val;
			}
			set
			{
				UpdateSetting("WebCache_Anonymous", value.ToString());
			}
		}

		public static bool WebCache_XRefFileEpisode_Get
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool usecache = false;
				bool.TryParse(appSettings["WebCache_XRefFileEpisode_Get"], out usecache);
				return usecache;
			}
			set
			{
				UpdateSetting("WebCache_XRefFileEpisode_Get", value.ToString());
			}
		}

		public static bool WebCache_XRefFileEpisode_Send
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool usecache = false;
				bool.TryParse(appSettings["WebCache_XRefFileEpisode_Send"], out usecache);
				return usecache;
			}
			set
			{
				UpdateSetting("WebCache_XRefFileEpisode_Send", value.ToString());
			}
		}

		public static bool WebCache_TvDB_Get
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool usecache = true;
				if (bool.TryParse(appSettings["WebCache_TvDB_Get"], out usecache))
				    return usecache;
                else
                    return true; // default
            }
			set
			{
				UpdateSetting("WebCache_TvDB_Get", value.ToString());
			}
		}

		public static bool WebCache_TvDB_Send
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool usecache = true;
				if (bool.TryParse(appSettings["WebCache_TvDB_Send"], out usecache))
				    return usecache;
                else
                    return true; // default
            }
			set
			{
				UpdateSetting("WebCache_TvDB_Send", value.ToString());
			}
		}

        public static bool WebCache_Trakt_Get
        {
            get
            {
                NameValueCollection appSettings = ConfigurationManager.AppSettings;
                bool usecache = true;
                if (bool.TryParse(appSettings["WebCache_Trakt_Get"], out usecache))
                    return usecache;
                else
                    return true; // default
            }
            set
            {
                UpdateSetting("WebCache_Trakt_Get", value.ToString());
            }
        }

        public static bool WebCache_Trakt_Send
        {
            get
            {
                NameValueCollection appSettings = ConfigurationManager.AppSettings;
                bool usecache = true;
                if (bool.TryParse(appSettings["WebCache_Trakt_Send"], out usecache))
                    return usecache;
                else
                    return true; // default
            }
            set
            {
                UpdateSetting("WebCache_Trakt_Send", value.ToString());
            }
        }

		public static bool WebCache_MAL_Get
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool usecache = true;
				if (bool.TryParse(appSettings["WebCache_MAL_Get"], out usecache))
					return usecache;
				else
					return true; // default
			}
			set
			{
				UpdateSetting("WebCache_MAL_Get", value.ToString());
			}
		}

		public static bool WebCache_MAL_Send
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool usecache = true;
				if (bool.TryParse(appSettings["WebCache_MAL_Send"], out usecache))
					return usecache;
				else
					return true; // default
			}
			set
			{
				UpdateSetting("WebCache_MAL_Send", value.ToString());
			}
		}

        public static bool WebCache_UserInfo
        {
            get
            {
                NameValueCollection appSettings = ConfigurationManager.AppSettings;
                bool usecache = false;
                if (bool.TryParse(appSettings["WebCache_UserInfo"], out usecache))
                    return usecache;
                else
                    return true; // default
            }
            set
            {
                UpdateSetting("WebCache_UserInfo", value.ToString());
            }
        }

		#endregion

		#region TvDB

		public static bool TvDB_AutoFanart
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = false;
				bool.TryParse(appSettings["TvDB_AutoFanart"], out val);
				return val;
			}
			set
			{
				UpdateSetting("TvDB_AutoFanart", value.ToString());
			}
		}

		public static int TvDB_AutoFanartAmount
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				int val = 0;
				int.TryParse(appSettings["TvDB_AutoFanartAmount"], out val);
				return val;
			}
			set
			{
				UpdateSetting("TvDB_AutoFanartAmount", value.ToString());
			}
		}

		public static bool TvDB_AutoWideBanners
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = false;
				bool.TryParse(appSettings["TvDB_AutoWideBanners"], out val);
				return val;
			}
			set
			{
				UpdateSetting("TvDB_AutoWideBanners", value.ToString());
			}
		}

		public static int TvDB_AutoWideBannersAmount
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				int val = 0;
				if (!int.TryParse(appSettings["TvDB_AutoWideBannersAmount"], out val))
					val = 10; // default
				return val;
			}
			set
			{
				UpdateSetting("TvDB_AutoWideBannersAmount", value.ToString());
			}
		}

		public static bool TvDB_AutoPosters
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = false;
				bool.TryParse(appSettings["TvDB_AutoPosters"], out val);
				return val;
			}
			set
			{
				UpdateSetting("TvDB_AutoPosters", value.ToString());
			}
		}

		public static int TvDB_AutoPostersAmount
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				int val = 0;
				if (!int.TryParse(appSettings["TvDB_AutoPostersAmount"], out val))
					val = 10; // default
				return val;
			}
			set
			{
				UpdateSetting("TvDB_AutoPostersAmount", value.ToString());
			}
		}

		public static ScheduledUpdateFrequency TvDB_UpdateFrequency
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				int val = 1;
				if (int.TryParse(appSettings["TvDB_UpdateFrequency"], out val))
					return (ScheduledUpdateFrequency)val;
				else
					return ScheduledUpdateFrequency.HoursTwelve; // default value
			}
			set
			{
				UpdateSetting("TvDB_UpdateFrequency", ((int)value).ToString());
			}
		}

		public static string TvDB_Language
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				string language = appSettings["TvDB_Language"];
				if (string.IsNullOrEmpty(language))
					return "en";
				else
					return language;
			}
			set
			{
				UpdateSetting("TvDB_Language", value);
			}
		}

		#endregion

		#region MovieDB

		public static bool MovieDB_AutoFanart
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = false;
				bool.TryParse(appSettings["MovieDB_AutoFanart"], out val);
				return val;
			}
			set
			{
				UpdateSetting("MovieDB_AutoFanart", value.ToString());
			}
		}

		public static int MovieDB_AutoFanartAmount
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				int val = 0;
				int.TryParse(appSettings["MovieDB_AutoFanartAmount"], out val);
				return val;
			}
			set
			{
				UpdateSetting("MovieDB_AutoFanartAmount", value.ToString());
			}
		}

		public static bool MovieDB_AutoPosters
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = false;
				bool.TryParse(appSettings["MovieDB_AutoPosters"], out val);
				return val;
			}
			set
			{
				UpdateSetting("MovieDB_AutoPosters", value.ToString());
			}
		}

		public static int MovieDB_AutoPostersAmount
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				int val = 0;
				if (!int.TryParse(appSettings["MovieDB_AutoPostersAmount"], out val))
					val = 10; // default
				return val;
			}
			set
			{
				UpdateSetting("MovieDB_AutoPostersAmount", value.ToString());
			}
		}

		#endregion

		#region Import Settings

		public static string VideoExtensions
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["VideoExtensions"];
			}
			set
			{
				UpdateSetting("VideoExtensions", value);
			}
		}

		public static RenamingLanguage DefaultSeriesLanguage
		{
			get
			{
				RenamingLanguage rl = RenamingLanguage.Romaji;
				NameValueCollection appSettings = ConfigurationManager.AppSettings;

				string rls = appSettings["DefaultSeriesLanguage"];
				if (string.IsNullOrEmpty(rls)) return rl;

				rl = (RenamingLanguage)int.Parse(rls);

				return rl;
			}
			set
			{
				UpdateSetting("DefaultSeriesLanguage", ((int)value).ToString());
			}
		}

		public static RenamingLanguage DefaultEpisodeLanguage
		{
			get
			{
				RenamingLanguage rl = RenamingLanguage.Romaji;
				NameValueCollection appSettings = ConfigurationManager.AppSettings;

				string rls = appSettings["DefaultEpisodeLanguage"];
				if (string.IsNullOrEmpty(rls)) return rl;

				rl = (RenamingLanguage)int.Parse(rls);

				return rl;
			}
			set
			{
				UpdateSetting("DefaultEpisodeLanguage", ((int)value).ToString());
			}
		}

		public static bool RunImportOnStart
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = false;
				bool.TryParse(appSettings["RunImportOnStart"], out val);
				return val;
			}
			set
			{
				UpdateSetting("RunImportOnStart", value.ToString());
			}
		}

		public static bool ScanDropFoldersOnStart
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = false;
				bool.TryParse(appSettings["ScanDropFoldersOnStart"], out val);
				return val;
			}
			set
			{
				UpdateSetting("ScanDropFoldersOnStart", value.ToString());
			}
		}

		public static bool Hash_CRC32
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool bval = false;
				bool.TryParse(appSettings["Hash_CRC32"], out bval);
				return bval;
			}
			set
			{
				UpdateSetting("Hash_CRC32", value.ToString());
			}
		}

		public static bool Hash_MD5
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool bval = false;
				bool.TryParse(appSettings["Hash_MD5"], out bval);
				return bval;
			}
			set
			{
				UpdateSetting("Hash_MD5", value.ToString());
			}
		}
        public static bool ExperimentalUPnP
        {
            get
            {
                NameValueCollection appSettings = ConfigurationManager.AppSettings;
                bool bval = false;
                bool.TryParse(appSettings["ExperimentalUPnP"], out bval);
                return bval;
            }
            set
            {
                UpdateSetting("ExperimentalUPnP", value.ToString());
            }
        }
        public static bool Hash_SHA1
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool bval = false;
				bool.TryParse(appSettings["Hash_SHA1"], out bval);
				return bval;
			}
			set
			{
				UpdateSetting("Hash_SHA1", value.ToString());
			}
		}

		public static bool Import_UseExistingFileWatchedStatus
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool bval = false;
				bool.TryParse(appSettings["Import_UseExistingFileWatchedStatus"], out bval);
				return bval;
			}
			set
			{
				UpdateSetting("Import_UseExistingFileWatchedStatus", value.ToString());
			}
		}

		#endregion

		public static bool AutoGroupSeries
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = false;
				bool.TryParse(appSettings["AutoGroupSeries"], out val);
				return val;
			}
			set
			{
				UpdateSetting("AutoGroupSeries", value.ToString());
			}
		}

        public static string AutoGroupSeriesTypeExclusions
        {
            get
            {
                NameValueCollection appSettings = ConfigurationManager.AppSettings;
                string val = "same setting|alternative setting|character|other";
                try
                {
                    val = appSettings["AutoGroupSeriesTypeExclusions"];
                }
                catch (Exception e) { }
                return val;
            }
            set
            {
                UpdateSetting("AutoGroupSeriesTypeExclusions", value);
            }
        }

        public static string LanguagePreference
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["LanguagePreference"];
			}
			set
			{
				UpdateSetting("LanguagePreference", value);
			}
		}

		public static bool LanguageUseSynonyms
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = false;
				bool.TryParse(appSettings["LanguageUseSynonyms"], out val);
				return val;
			}
			set
			{
				UpdateSetting("LanguageUseSynonyms", value.ToString());
			}
		}

		public static DataSourceType EpisodeTitleSource
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				int val = 0;
				int.TryParse(appSettings["EpisodeTitleSource"], out val);
				if (val <= 0)
					return DataSourceType.AniDB;
				else
					return (DataSourceType)val;
			}
			set
			{
				UpdateSetting("EpisodeTitleSource", ((int)value).ToString());
			}
		}

        public static DataSourceType SeriesDescriptionSource
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				int val = 0;
				int.TryParse(appSettings["SeriesDescriptionSource"], out val);
				if (val <= 0)
					return DataSourceType.AniDB;
				else
					return (DataSourceType)val;
			}
			set
			{
				UpdateSetting("SeriesDescriptionSource", ((int)value).ToString());
			}
		}

		public static DataSourceType SeriesNameSource
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				int val = 0;
				int.TryParse(appSettings["SeriesNameSource"], out val);
				if (val <= 0)
					return DataSourceType.AniDB;
				else
					return (DataSourceType)val;
			}
			set
			{
				UpdateSetting("SeriesNameSource", ((int)value).ToString());
			}
		}

		public static string BaseImagesPath
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
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
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				string basePath = appSettings["BaseImagesPathIsDefault"];
				if (!string.IsNullOrEmpty(basePath))
				{
					bool val = true;
					bool.TryParse(basePath, out val);
					return val;
				}
				else return true;
				
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
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
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
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = false;
				bool.TryParse(appSettings["MinimizeOnStartup"], out val);
				return val;
			}
			set
			{
				UpdateSetting("MinimizeOnStartup", value.ToString());
			}
		}

		public static bool AllowMultipleInstances
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = false;
				if (!bool.TryParse(appSettings["AllowMultipleInstances"], out val))
					val = false;
				return val;
			}
			set
			{
				UpdateSetting("AllowMultipleInstances", value.ToString());
			}
		}

		#region Trakt

        public static bool Trakt_IsEnabled
        {
            get
            {
                NameValueCollection appSettings = ConfigurationManager.AppSettings;
                bool val = true;
                if (!bool.TryParse(appSettings["Trakt_IsEnabled"], out val))
                    val = true;
                return val;
            }
            set
            {
                UpdateSetting("Trakt_IsEnabled", value.ToString());
            }
        }

        public static string Trakt_AuthToken
        {
            get
            {
                NameValueCollection appSettings = ConfigurationManager.AppSettings;
                return appSettings["Trakt_AuthToken"];
            }
            set
            {
                UpdateSetting("Trakt_AuthToken", value);
            }
        }

        public static string Trakt_RefreshToken
        {
            get
            {
                NameValueCollection appSettings = ConfigurationManager.AppSettings;
                return appSettings["Trakt_RefreshToken"];
            }
            set
            {
                UpdateSetting("Trakt_RefreshToken", value);
            }
        }

        public static string Trakt_TokenExpirationDate
        {
            get
            {
                NameValueCollection appSettings = ConfigurationManager.AppSettings;
                return appSettings["Trakt_TokenExpirationDate"];
            }
            set
            {
                UpdateSetting("Trakt_TokenExpirationDate", value);
            }
        }

		public static ScheduledUpdateFrequency Trakt_UpdateFrequency
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				int val = 1;
				if (int.TryParse(appSettings["Trakt_UpdateFrequency"], out val))
					return (ScheduledUpdateFrequency)val;
				else
					return ScheduledUpdateFrequency.Daily; // default value
			}
			set
			{
				UpdateSetting("Trakt_UpdateFrequency", ((int)value).ToString());
			}
		}

		public static ScheduledUpdateFrequency Trakt_SyncFrequency
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				int val = 1;
				if (int.TryParse(appSettings["Trakt_SyncFrequency"], out val))
					return (ScheduledUpdateFrequency)val;
				else
					return ScheduledUpdateFrequency.Never; // default value
			}
			set
			{
				UpdateSetting("Trakt_SyncFrequency", ((int)value).ToString());
			}
		}

		public static bool Trakt_DownloadFanart
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = true;
				if (!bool.TryParse(appSettings["Trakt_DownloadFanart"], out val))
					val = true; // default
				return val;
			}
			set
			{
				UpdateSetting("Trakt_DownloadFanart", value.ToString());
			}
		}

		public static bool Trakt_DownloadPosters
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = true;
				if (!bool.TryParse(appSettings["Trakt_DownloadPosters"], out val))
					val = true; // default
				return val;
			}
			set
			{
				UpdateSetting("Trakt_DownloadPosters", value.ToString());
			}
		}

		public static bool Trakt_DownloadEpisodes
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = true;
				if (!bool.TryParse(appSettings["Trakt_DownloadEpisodes"], out val))
					val = true; // default
				return val;
			}
			set
			{
				UpdateSetting("Trakt_DownloadEpisodes", value.ToString());
			}
		}

		#endregion

		#region MAL

		public static string MAL_Username
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["MAL_Username"];
			}
			set
			{
				UpdateSetting("MAL_Username", value);
			}
		}

		public static string MAL_Password
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["MAL_Password"];
			}
			set
			{
				UpdateSetting("MAL_Password", value);
			}
		}

		public static ScheduledUpdateFrequency MAL_UpdateFrequency
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				int val = 1;
				if (int.TryParse(appSettings["MAL_UpdateFrequency"], out val))
					return (ScheduledUpdateFrequency)val;
				else
					return ScheduledUpdateFrequency.Daily; // default value
			}
			set
			{
				UpdateSetting("MAL_UpdateFrequency", ((int)value).ToString());
			}
		}

		public static bool MAL_NeverDecreaseWatchedNums
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				string wtchNum = appSettings["MAL_NeverDecreaseWatchedNums"];
				if (!string.IsNullOrEmpty(wtchNum))
				{
					bool val = true;
					bool.TryParse(wtchNum, out val);
					return val;
				}
				else return true;

			}
			set
			{
				UpdateSetting("MAL_NeverDecreaseWatchedNums", value.ToString());
			}
		}

		#endregion


		public static string WebCacheAuthKey
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["WebCacheAuthKey"];
			}
			set
			{
				UpdateSetting("WebCacheAuthKey", value);
			}
		}

        public static bool EnablePlex
        {
            get
            {
                NameValueCollection appSettings = ConfigurationManager.AppSettings;
                string basePath = appSettings["EnablePlex"];
                if (!string.IsNullOrEmpty(basePath))
                {
                    bool val = true;
                    bool.TryParse(basePath, out val);
                    return val;
                }
                else return true;

            }
            set
            {
                UpdateSetting("EnablePlex", value.ToString());
            }
        }

        public static bool EnableKodi
        {
            get
            {
                NameValueCollection appSettings = ConfigurationManager.AppSettings;
                string basePath = appSettings["EnableKodi"];
                if (!string.IsNullOrEmpty(basePath))
                {
                    bool val = true;
                    bool.TryParse(basePath, out val);
                    return val;
                }
                else return true;

            }
            set
            {
                UpdateSetting("EnableKodi", value.ToString());
            }
        }

        public static void UpdateSetting(string key, string value)
		{
			System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

			if (config.AppSettings.Settings.AllKeys.Contains(key))
				config.AppSettings.Settings[key].Value = value;
			else
				config.AppSettings.Settings.Add(key, value);

			config.Save();
			ConfigurationManager.RefreshSection("appSettings");
		}

		public static Contract_ServerSettings ToContract()
		{
			Contract_ServerSettings contract = new Contract_ServerSettings();

			contract.AniDB_Username = ServerSettings.AniDB_Username;
			contract.AniDB_Password = ServerSettings.AniDB_Password;
			contract.AniDB_ServerAddress = ServerSettings.AniDB_ServerAddress;
			contract.AniDB_ServerPort = ServerSettings.AniDB_ServerPort;
			contract.AniDB_ClientPort = ServerSettings.AniDB_ClientPort;
			contract.AniDB_AVDumpClientPort = ServerSettings.AniDB_AVDumpClientPort;
			contract.AniDB_AVDumpKey = ServerSettings.AniDB_AVDumpKey;

			contract.AniDB_DownloadRelatedAnime = ServerSettings.AniDB_DownloadRelatedAnime;
			contract.AniDB_DownloadSimilarAnime = ServerSettings.AniDB_DownloadSimilarAnime;
			contract.AniDB_DownloadReviews = ServerSettings.AniDB_DownloadReviews;
			contract.AniDB_DownloadReleaseGroups = ServerSettings.AniDB_DownloadReleaseGroups;

			contract.AniDB_MyList_AddFiles = ServerSettings.AniDB_MyList_AddFiles;
			contract.AniDB_MyList_StorageState = (int)ServerSettings.AniDB_MyList_StorageState;
			contract.AniDB_MyList_DeleteType = (int)ServerSettings.AniDB_MyList_DeleteType;
			contract.AniDB_MyList_ReadWatched = ServerSettings.AniDB_MyList_ReadWatched;
			contract.AniDB_MyList_ReadUnwatched = ServerSettings.AniDB_MyList_ReadUnwatched;
			contract.AniDB_MyList_SetWatched = ServerSettings.AniDB_MyList_SetWatched;
			contract.AniDB_MyList_SetUnwatched = ServerSettings.AniDB_MyList_SetUnwatched;

			contract.AniDB_MyList_UpdateFrequency = (int)ServerSettings.AniDB_MyList_UpdateFrequency;
			contract.AniDB_Calendar_UpdateFrequency = (int)ServerSettings.AniDB_Calendar_UpdateFrequency;
			contract.AniDB_Anime_UpdateFrequency = (int)ServerSettings.AniDB_Anime_UpdateFrequency;
			contract.AniDB_MyListStats_UpdateFrequency = (int)ServerSettings.AniDB_MyListStats_UpdateFrequency;
			contract.AniDB_File_UpdateFrequency = (int)ServerSettings.AniDB_File_UpdateFrequency;

			contract.AniDB_DownloadCharacters = ServerSettings.AniDB_DownloadCharacters;
			contract.AniDB_DownloadCreators = ServerSettings.AniDB_DownloadCreators;

			// Web Cache
			contract.WebCache_Address = ServerSettings.WebCache_Address;
			contract.WebCache_Anonymous = ServerSettings.WebCache_Anonymous;
			contract.WebCache_XRefFileEpisode_Get = ServerSettings.WebCache_XRefFileEpisode_Get;
			contract.WebCache_XRefFileEpisode_Send = ServerSettings.WebCache_XRefFileEpisode_Send;
			contract.WebCache_TvDB_Get = ServerSettings.WebCache_TvDB_Get;
			contract.WebCache_TvDB_Send = ServerSettings.WebCache_TvDB_Send;
            contract.WebCache_Trakt_Get = ServerSettings.WebCache_Trakt_Get;
            contract.WebCache_Trakt_Send = ServerSettings.WebCache_Trakt_Send;
			contract.WebCache_MAL_Get = ServerSettings.WebCache_MAL_Get;
			contract.WebCache_MAL_Send = ServerSettings.WebCache_MAL_Send;
            contract.WebCache_UserInfo = ServerSettings.WebCache_UserInfo;

			// TvDB
			contract.TvDB_AutoFanart = ServerSettings.TvDB_AutoFanart;
			contract.TvDB_AutoFanartAmount = ServerSettings.TvDB_AutoFanartAmount;
			contract.TvDB_AutoPosters = ServerSettings.TvDB_AutoPosters;
			contract.TvDB_AutoPostersAmount = ServerSettings.TvDB_AutoPostersAmount;
			contract.TvDB_AutoWideBanners = ServerSettings.TvDB_AutoWideBanners;
			contract.TvDB_AutoWideBannersAmount = ServerSettings.TvDB_AutoWideBannersAmount;
			contract.TvDB_UpdateFrequency = (int)ServerSettings.TvDB_UpdateFrequency;
			contract.TvDB_Language = ServerSettings.TvDB_Language;

			// MovieDB
			contract.MovieDB_AutoFanart = ServerSettings.MovieDB_AutoFanart;
			contract.MovieDB_AutoFanartAmount = ServerSettings.MovieDB_AutoFanartAmount;
			contract.MovieDB_AutoPosters = ServerSettings.MovieDB_AutoPosters;
			contract.MovieDB_AutoPostersAmount = ServerSettings.MovieDB_AutoPostersAmount;

			// Import settings
			contract.VideoExtensions = ServerSettings.VideoExtensions;
			contract.AutoGroupSeries = ServerSettings.AutoGroupSeries;
			contract.Import_UseExistingFileWatchedStatus = ServerSettings.Import_UseExistingFileWatchedStatus;
			contract.RunImportOnStart = ServerSettings.RunImportOnStart;
			contract.ScanDropFoldersOnStart = ServerSettings.ScanDropFoldersOnStart;
			contract.Hash_CRC32 = ServerSettings.Hash_CRC32;
			contract.Hash_MD5 = ServerSettings.Hash_MD5;
			contract.Hash_SHA1 = ServerSettings.Hash_SHA1;

			// Language
			contract.LanguagePreference = ServerSettings.LanguagePreference;
			contract.LanguageUseSynonyms = ServerSettings.LanguageUseSynonyms;
			contract.EpisodeTitleSource = (int)ServerSettings.EpisodeTitleSource;
			contract.SeriesDescriptionSource = (int)ServerSettings.SeriesDescriptionSource;
			contract.SeriesNameSource = (int)ServerSettings.SeriesNameSource;

			// trakt
            contract.Trakt_IsEnabled = ServerSettings.Trakt_IsEnabled;
            contract.Trakt_AuthToken = ServerSettings.Trakt_AuthToken;
            contract.Trakt_RefreshToken = ServerSettings.Trakt_RefreshToken;
            contract.Trakt_TokenExpirationDate = ServerSettings.Trakt_TokenExpirationDate;
			contract.Trakt_UpdateFrequency = (int)ServerSettings.Trakt_UpdateFrequency;
			contract.Trakt_SyncFrequency = (int)ServerSettings.Trakt_SyncFrequency;
			contract.Trakt_DownloadEpisodes = ServerSettings.Trakt_DownloadEpisodes;
			contract.Trakt_DownloadFanart = ServerSettings.Trakt_DownloadFanart;
			contract.Trakt_DownloadPosters = ServerSettings.Trakt_DownloadPosters;

			// MAL
			contract.MAL_Username = ServerSettings.MAL_Username;
			contract.MAL_Password = ServerSettings.MAL_Password;
			contract.MAL_UpdateFrequency = (int)ServerSettings.MAL_UpdateFrequency;
			contract.MAL_NeverDecreaseWatchedNums = ServerSettings.MAL_NeverDecreaseWatchedNums;

			

			return contract;
		}

		public static void DebugSettingsToLog()
		{
			#region System Info
			logger.Info("-------------------- SYSTEM INFO -----------------------");

			System.Reflection.Assembly a = System.Reflection.Assembly.GetExecutingAssembly();
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
				VersionsRepository repVersions = new VersionsRepository();
				Versions ver = repVersions.GetByVersionType(Constants.DatabaseTypeKey);
				if (ver != null)
					logger.Info(string.Format("Database Version: {0}", ver.VersionValue));
			}
			catch (Exception ex)
			{
				// oopps, can't create file
				logger.Warn("Error in log: {0}", ex.Message);
			}

			logger.Info(string.Format("Operating System: {0}", Utils.GetOSInfo()));

			string screenSize = System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width.ToString() + "x" +
				System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height.ToString();
			logger.Info(string.Format("Screen Size: {0}", screenSize));


			
			
			try
			{
				string mediaInfoVersion = "**** MediaInfo - DLL Not found *****";

                string mediaInfoPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                FileInfo fi = new FileInfo(mediaInfoPath);
                mediaInfoPath = Path.Combine(fi.Directory.FullName, Environment.Is64BitProcess ? "x64" : "x86", "MediaInfo.dll");

				if (File.Exists(mediaInfoPath))
				{
					FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(mediaInfoPath);
                    mediaInfoVersion = string.Format("MediaInfo DLL {0}.{1}.{2}.{3} ({4})", fvi.FileMajorPart, fvi.FileMinorPart, fvi.FileBuildPart, fvi.FilePrivatePart, mediaInfoPath);
				}
				logger.Info(mediaInfoVersion);

				string hasherInfoVersion = "**** Hasher - DLL NOT found *****";

                string fullHasherexepath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                fi = new FileInfo(fullHasherexepath);
                fullHasherexepath = Path.Combine(fi.Directory.FullName, Environment.Is64BitProcess ? "x64" : "x86", "hasher.dll");

                if (File.Exists(fullHasherexepath))
					hasherInfoVersion = string.Format("Hasher DLL found at {0}", fullHasherexepath);
				logger.Info(hasherInfoVersion);
			}
			catch { }

			logger.Info("-------------------------------------------------------");
			#endregion

			logger.Info("----------------- SERVER SETTINGS ----------------------");

			logger.Info("DatabaseType: {0}", DatabaseType);
			logger.Info("MSSQL DatabaseServer: {0}", DatabaseServer);
			logger.Info("MSSQL DatabaseName: {0}", DatabaseName);
			logger.Info("MSSQL DatabaseUsername: {0}", string.IsNullOrEmpty(DatabaseUsername) ? "NOT SET" : "***HIDDEN***");
			logger.Info("MSSQL DatabasePassword: {0}", string.IsNullOrEmpty(DatabasePassword) ? "NOT SET" : "***HIDDEN***");

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
	}
}
