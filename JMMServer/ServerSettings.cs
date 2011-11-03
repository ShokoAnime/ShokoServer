using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.Specialized;
using System.Configuration;
using AniDBAPI;
using JMMContracts;

namespace JMMServer
{
	public class ServerSettings
	{
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
					return ScheduledUpdateFrequency.Daily; // default value
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

		public static bool WebCache_FileHashes_Get
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool usecache = false;
				bool.TryParse(appSettings["WebCache_FileHashes_Get"], out usecache);
				return usecache;
			}
			set
			{
				UpdateSetting("WebCache_FileHashes_Get", value.ToString());
			}
		}

		public static bool WebCache_FileHashes_Send
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool usecache = false;
				bool.TryParse(appSettings["WebCache_FileHashes_Send"], out usecache);
				return usecache;
			}
			set
			{
				UpdateSetting("WebCache_FileHashes_Send", value.ToString());
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
				bool usecache = false;
				bool.TryParse(appSettings["WebCache_TvDB_Get"], out usecache);
				return usecache;
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
				bool usecache = false;
				bool.TryParse(appSettings["WebCache_TvDB_Send"], out usecache);
				return usecache;
			}
			set
			{
				UpdateSetting("WebCache_TvDB_Send", value.ToString());
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

		public static bool WatchForNewFiles
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				bool val = false;
				bool.TryParse(appSettings["WatchForNewFiles"], out val);
				return val;
			}
			set
			{
				UpdateSetting("WatchForNewFiles", value.ToString());
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

		public static string Trakt_Username
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["Trakt_Username"];
			}
			set
			{
				UpdateSetting("Trakt_Username", value);
			}
		}

		public static string Trakt_Password
		{
			get
			{
				NameValueCollection appSettings = ConfigurationManager.AppSettings;
				return appSettings["Trakt_Password"];
			}
			set
			{
				UpdateSetting("Trakt_Password", value);
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
					return ScheduledUpdateFrequency.Daily; // default value
			}
			set
			{
				UpdateSetting("Trakt_SyncFrequency", ((int)value).ToString());
			}
		}

		public static void UpdateSetting(string key, string value)
		{
			System.Configuration.Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);

			if (config.AppSettings.Settings.AllKeys.Contains(key))
				config.AppSettings.Settings[key].Value = value;
			else
				config.AppSettings.Settings.Add(key, value);

			config.Save(ConfigurationSaveMode.Modified);
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

			contract.AniDB_DownloadRelatedAnime = ServerSettings.AniDB_DownloadRelatedAnime;
			contract.AniDB_DownloadSimilarAnime = ServerSettings.AniDB_DownloadSimilarAnime;
			contract.AniDB_DownloadReviews = ServerSettings.AniDB_DownloadReviews;
			contract.AniDB_DownloadReleaseGroups = ServerSettings.AniDB_DownloadReleaseGroups;

			contract.AniDB_MyList_AddFiles = ServerSettings.AniDB_MyList_AddFiles;
			contract.AniDB_MyList_StorageState = (int)ServerSettings.AniDB_MyList_StorageState;
			contract.AniDB_MyList_ReadWatched = ServerSettings.AniDB_MyList_ReadWatched;
			contract.AniDB_MyList_ReadUnwatched = ServerSettings.AniDB_MyList_ReadUnwatched;
			contract.AniDB_MyList_SetWatched = ServerSettings.AniDB_MyList_SetWatched;
			contract.AniDB_MyList_SetUnwatched = ServerSettings.AniDB_MyList_SetUnwatched;

			contract.AniDB_MyList_UpdateFrequency = (int)ServerSettings.AniDB_MyList_UpdateFrequency;
			contract.AniDB_Calendar_UpdateFrequency = (int)ServerSettings.AniDB_Calendar_UpdateFrequency;
			contract.AniDB_Anime_UpdateFrequency = (int)ServerSettings.AniDB_Anime_UpdateFrequency;

			// Web Cache
			contract.WebCache_Address = ServerSettings.WebCache_Address;
			contract.WebCache_Anonymous = ServerSettings.WebCache_Anonymous;
			contract.WebCache_FileHashes_Get = ServerSettings.WebCache_FileHashes_Get;
			contract.WebCache_FileHashes_Send = ServerSettings.WebCache_FileHashes_Send;
			contract.WebCache_XRefFileEpisode_Get = ServerSettings.WebCache_XRefFileEpisode_Get;
			contract.WebCache_XRefFileEpisode_Send = ServerSettings.WebCache_XRefFileEpisode_Send;
			contract.WebCache_TvDB_Get = ServerSettings.WebCache_TvDB_Get;
			contract.WebCache_TvDB_Send = ServerSettings.WebCache_TvDB_Send;

			// TvDB
			contract.TvDB_AutoFanart = ServerSettings.TvDB_AutoFanart;
			contract.TvDB_AutoFanartAmount = ServerSettings.TvDB_AutoFanartAmount;
			contract.TvDB_AutoPosters = ServerSettings.TvDB_AutoPosters;
			contract.TvDB_AutoWideBanners = ServerSettings.TvDB_AutoWideBanners;
			contract.TvDB_UpdateFrequency = (int)ServerSettings.TvDB_UpdateFrequency;

			// MovieDB
			contract.MovieDB_AutoFanart = ServerSettings.MovieDB_AutoFanart;
			contract.MovieDB_AutoFanartAmount = ServerSettings.MovieDB_AutoFanartAmount;
			contract.MovieDB_AutoPosters = ServerSettings.MovieDB_AutoPosters;

			// Import settings
			contract.VideoExtensions = ServerSettings.VideoExtensions;
			contract.WatchForNewFiles = ServerSettings.WatchForNewFiles;
			contract.AutoGroupSeries = ServerSettings.AutoGroupSeries;
			contract.Import_UseExistingFileWatchedStatus = ServerSettings.Import_UseExistingFileWatchedStatus;
			contract.RunImportOnStart = ServerSettings.RunImportOnStart;
			contract.Hash_CRC32 = ServerSettings.Hash_CRC32;
			contract.Hash_MD5 = ServerSettings.Hash_MD5;
			contract.Hash_SHA1 = ServerSettings.Hash_SHA1;

			// Language
			contract.LanguagePreference = ServerSettings.LanguagePreference;
			contract.LanguageUseSynonyms = ServerSettings.LanguageUseSynonyms;

			// trakt
			contract.Trakt_Username = ServerSettings.Trakt_Username;
			contract.Trakt_Password = ServerSettings.Trakt_Password;
			contract.Trakt_UpdateFrequency = (int)ServerSettings.Trakt_UpdateFrequency;
			contract.Trakt_SyncFrequency = (int)ServerSettings.Trakt_SyncFrequency;

			return contract;
		}
	}
}
