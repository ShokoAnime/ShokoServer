using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Data.SqlClient;
using System.Data.SQLite;
using JMMServer.Entities;
using JMMServer.Repositories;
using NLog;
using System.Collections;

namespace JMMServer.Databases
{
	public class SQLite
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();
		public const string DefaultDBName = @"JMMServer.db3";

		public static string GetDatabasePath()
		{
			string appPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
			string dbPath = Path.Combine(appPath, "SQLite");
			return dbPath;
		}

		public static string GetDatabaseFilePath()
		{
			string dbName = Path.Combine(GetDatabasePath(), DefaultDBName);
			return dbName;
		}

		public static string GetConnectionString()
		{
			return string.Format(@"data source={0};useutf16encoding=True", GetDatabaseFilePath());
		}

		public static bool DatabaseAlreadyExists()
		{
			if (SQLite.GetDatabaseFilePath().Length == 0) return false;

			if (File.Exists(SQLite.GetDatabaseFilePath()))
				return true;
			else
				return false;

		}

		public static bool TestLogin(string serverName, string user, string password)
		{
			return true;
		}

		public static void CreateDatabase()
		{
			if (DatabaseAlreadyExists()) return;

			if (!Directory.Exists(GetDatabasePath()))
				Directory.CreateDirectory(GetDatabasePath());

			if (!File.Exists(GetDatabaseFilePath()))
				SQLiteConnection.CreateFile(GetDatabaseFilePath());

			ServerSettings.DatabaseFile = GetDatabaseFilePath();
		}

		public static ArrayList GetData(string sql)
		{
			ArrayList rowList = new ArrayList();
			try
			{
				SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
				myConn.Open();

				SQLiteCommand sqCommand = new SQLiteCommand(sql);
				sqCommand.Connection = myConn;
				SQLiteDataReader reader = sqCommand.ExecuteReader();

				while (reader.Read())
				{
					object[] values = new object[reader.FieldCount];
					reader.GetValues(values);
					rowList.Add(values);
				}

				myConn.Close();
				reader.Close();
			}
			catch (Exception ex)
			{
				logger.Error(sql + " - " + ex.Message);
			}
			return rowList;
		}

		#region Schema Updates

		public static void UpdateSchema()
		{

			VersionsRepository repVersions = new VersionsRepository();
			Versions ver = repVersions.GetByVersionType(Constants.DatabaseTypeKey);
			if (ver == null) return;

			int versionNumber = 0;
			int.TryParse(ver.VersionValue, out versionNumber);

			try
			{
				UpdateSchema_002(versionNumber);
				UpdateSchema_003(versionNumber);
				UpdateSchema_004(versionNumber);
				UpdateSchema_005(versionNumber);
				UpdateSchema_006(versionNumber);
				UpdateSchema_007(versionNumber);
				UpdateSchema_008(versionNumber);
				UpdateSchema_009(versionNumber);
				UpdateSchema_010(versionNumber);
				UpdateSchema_011(versionNumber);
				UpdateSchema_012(versionNumber);
				UpdateSchema_013(versionNumber);
				UpdateSchema_014(versionNumber);
				UpdateSchema_015(versionNumber);
				UpdateSchema_016(versionNumber);
				UpdateSchema_017(versionNumber);
				UpdateSchema_018(versionNumber);
				UpdateSchema_019(versionNumber);
				UpdateSchema_020(versionNumber);
				UpdateSchema_021(versionNumber);
				UpdateSchema_022(versionNumber);
				UpdateSchema_023(versionNumber);
				UpdateSchema_024(versionNumber);
				UpdateSchema_025(versionNumber);
				UpdateSchema_026(versionNumber);
				UpdateSchema_027(versionNumber);
				UpdateSchema_028(versionNumber);
				UpdateSchema_029(versionNumber);
				UpdateSchema_030(versionNumber);
                UpdateSchema_031(versionNumber);
                UpdateSchema_032(versionNumber);
                UpdateSchema_033(versionNumber);
                UpdateSchema_034(versionNumber);
                UpdateSchema_035(versionNumber);
                UpdateSchema_036(versionNumber);
                UpdateSchema_037(versionNumber);
            }
			catch (Exception ex)
			{
				logger.ErrorException("Error updating schema: " + ex.ToString(), ex);
			}

		}

		private static void UpdateSchema_002(int currentVersionNumber)
		{
			int thisVersion = 2;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE IgnoreAnime( " +
				" IgnoreAnimeID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" JMMUserID int NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" IgnoreType int NOT NULL)");

			cmds.Add("CREATE UNIQUE INDEX UIX_IgnoreAnime_User_AnimeID ON IgnoreAnime(JMMUserID, AnimeID, IgnoreType);");


			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_003(int currentVersionNumber)
		{
			int thisVersion = 3;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE Trakt_Friend( " +
				" Trakt_FriendID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" Username text NOT NULL, " +
				" FullName text NULL, " +
				" Gender text NULL, " +
				" Age text NULL, " +
				" Location text NULL, " +
				" About text NULL, " +
				" Joined int NOT NULL, " +
				" Avatar text NULL, " +
				" Url text NULL, " +
				" LastAvatarUpdate timestamp NOT NULL)");

			cmds.Add("CREATE UNIQUE INDEX UIX_Trakt_Friend_Username ON Trakt_Friend(Username);");


			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_004(int currentVersionNumber)
		{
			int thisVersion = 4;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();
			cmds.Add("ALTER TABLE AnimeGroup ADD DefaultAnimeSeriesID int NULL");

			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_005(int currentVersionNumber)
		{
			int thisVersion = 5;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();
			cmds.Add("ALTER TABLE JMMUser ADD CanEditServerSettings int NULL");

			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_006(int currentVersionNumber)
		{
			int thisVersion = 6;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			DatabaseHelper.FixDuplicateTvDBLinks();
			DatabaseHelper.FixDuplicateTraktLinks();

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();
			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB_Season ON CrossRef_AniDB_TvDB(TvDBID, TvDBSeasonNumber);");
			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB_AnimeID ON CrossRef_AniDB_TvDB(AnimeID);");

			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Trakt_Season ON CrossRef_AniDB_Trakt(TraktID, TraktSeasonNumber);");
			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Trakt_Anime ON CrossRef_AniDB_Trakt(AnimeID);");

			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_007(int currentVersionNumber)
		{
			int thisVersion = 7;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();
			cmds.Add("ALTER TABLE VideoInfo ADD VideoBitDepth text NULL");

			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_008(int currentVersionNumber)
		{
			int thisVersion = 8;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_009(int currentVersionNumber)
		{
			int thisVersion = 9;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();
			cmds.Add("ALTER TABLE ImportFolder ADD IsWatched int NOT NULL DEFAULT 1");

			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_010(int currentVersionNumber)
		{
			int thisVersion = 10;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE CrossRef_AniDB_MAL( " +
				" CrossRef_AniDB_MALID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeID int NOT NULL, " +
				" MALID int NOT NULL, " +
				" MALTitle text, " +
				" CrossRefSource int NOT NULL " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_MAL_AnimeID ON CrossRef_AniDB_MAL(AnimeID);");
			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_MAL_MALID ON CrossRef_AniDB_MAL(MALID);");


			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_011(int currentVersionNumber)
		{
			int thisVersion = 11;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();

			cmds.Add("DROP TABLE CrossRef_AniDB_MAL;");

			cmds.Add("CREATE TABLE CrossRef_AniDB_MAL( " +
				" CrossRef_AniDB_MALID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeID int NOT NULL, " +
				" MALID int NOT NULL, " +
				" MALTitle text, " +
				" StartEpisodeType int NOT NULL, " +
				" StartEpisodeNumber int NOT NULL, " +
				" CrossRefSource int NOT NULL " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_MAL_MALID ON CrossRef_AniDB_MAL(MALID);");
			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_MAL_Anime ON CrossRef_AniDB_MAL(AnimeID, StartEpisodeType, StartEpisodeNumber);");


			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_012(int currentVersionNumber)
		{
			int thisVersion = 12;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE Playlist( " +
				" PlaylistID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" PlaylistName text, " +
				" PlaylistItems text, " +
				" DefaultPlayOrder int NOT NULL, " +
				" PlayWatched int NOT NULL, " +
				" PlayUnwatched int NOT NULL " +
				" ); ");


			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_013(int currentVersionNumber)
		{
			int thisVersion = 13;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();
			cmds.Add("ALTER TABLE AnimeSeries ADD SeriesNameOverride text");

			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_014(int currentVersionNumber)
		{
			int thisVersion = 14;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE BookmarkedAnime( " +
				" BookmarkedAnimeID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeID int NOT NULL, " +
				" Priority int NOT NULL, " +
				" Notes text, " +
				" Downloading int NOT NULL " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_BookmarkedAnime_AnimeID ON BookmarkedAnime(BookmarkedAnimeID)");


			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_015(int currentVersionNumber)
		{
			int thisVersion = 15;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();
			cmds.Add("ALTER TABLE VideoLocal ADD DateTimeCreated timestamp NULL");
			cmds.Add("UPDATE VideoLocal SET DateTimeCreated = DateTimeUpdated");

			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_016(int currentVersionNumber)
		{
			int thisVersion = 16;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE CrossRef_AniDB_TvDB_Episode( " +
				" CrossRef_AniDB_TvDB_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeID int NOT NULL, " +
				" AniDBEpisodeID int NOT NULL, " +
				" TvDBEpisodeID int NOT NULL " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB_Episode_AniDBEpisodeID ON CrossRef_AniDB_TvDB_Episode(AniDBEpisodeID);");

			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_017(int currentVersionNumber)
		{
			int thisVersion = 17;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE AniDB_MylistStats( " +
				" AniDB_MylistStatsID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" Animes int NOT NULL, " +
				" Episodes int NOT NULL, " +
				" Files int NOT NULL, " +
				" SizeOfFiles INTEGER NOT NULL, " +
				" AddedAnimes int NOT NULL, " +
				" AddedEpisodes int NOT NULL, " +
				" AddedFiles int NOT NULL, " +
				" AddedGroups int NOT NULL, " +
				" LeechPct int NOT NULL, " +
				" GloryPct int NOT NULL, " +
				" ViewedPct int NOT NULL, " +
				" MylistPct int NOT NULL, " +
				" ViewedMylistPct int NOT NULL, " +
				" EpisodesViewed int NOT NULL, " +
				" Votes int NOT NULL, " +
				" Reviews int NOT NULL, " +
				" ViewiedLength int NOT NULL " +
				" ); ");


			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_018(int currentVersionNumber)
		{
			int thisVersion = 18;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE FileFfdshowPreset( " +
				" FileFfdshowPresetID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" Hash int NOT NULL, " +
				" FileSize INTEGER NOT NULL, " +
				" Preset text " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_FileFfdshowPreset_Hash ON FileFfdshowPreset(Hash, FileSize);");


			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_019(int currentVersionNumber)
		{
			int thisVersion = 19;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();
			cmds.Add("ALTER TABLE AniDB_Anime ADD DisableExternalLinksFlag int NULL");
			cmds.Add("UPDATE AniDB_Anime SET DisableExternalLinksFlag = 0");

			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_020(int currentVersionNumber)
		{
			int thisVersion = 20;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();
			cmds.Add("ALTER TABLE AniDB_File ADD FileVersion int NULL");
			cmds.Add("UPDATE AniDB_File SET FileVersion = 1");

			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_021(int currentVersionNumber)
		{
			int thisVersion = 21;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE RenameScript( " +
				" RenameScriptID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" ScriptName text, " +
				" Script text, " +
				" IsEnabledOnImport int NOT NULL " +
				" ); ");


			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_022(int currentVersionNumber)
		{
			int thisVersion = 22;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();
			cmds.Add("ALTER TABLE AniDB_File ADD IsCensored int NULL");
			cmds.Add("ALTER TABLE AniDB_File ADD IsDeprecated int NULL");

			try
			{
				foreach (string cmdTable in cmds)
				{
					SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
					sqCommand.Connection = myConn;
					sqCommand.ExecuteNonQuery();
				}
			}
			catch { }

			cmds.Clear();
			cmds.Add("ALTER TABLE AniDB_File ADD InternalVersion int NULL");

			cmds.Add("UPDATE AniDB_File SET IsCensored = 0");
			cmds.Add("UPDATE AniDB_File SET IsDeprecated = 0");
			cmds.Add("UPDATE AniDB_File SET InternalVersion = 1");

			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_023(int currentVersionNumber)
		{
			int thisVersion = 23;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();

			cmds.Add("UPDATE JMMUser SET CanEditServerSettings = 1");

			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_024(int currentVersionNumber)
		{
			int thisVersion = 24;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();
			cmds.Add("ALTER TABLE VideoLocal ADD IsVariation int NULL");
			cmds.Add("UPDATE VideoLocal SET IsVariation = 0");

			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_025(int currentVersionNumber)
		{
			int thisVersion = 25;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE AniDB_Recommendation( " +
				" AniDB_RecommendationID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeID int NOT NULL, " +
				" UserID int NOT NULL, " +
				" RecommendationType int NOT NULL, " +
				" RecommendationText text " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Recommendation ON AniDB_Recommendation(AnimeID, UserID);");


			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_026(int currentVersionNumber)
		{
			int thisVersion = 26;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();

			cmds.Add("CREATE INDEX IX_CrossRef_File_Episode_Hash ON CrossRef_File_Episode(Hash);");
			cmds.Add("CREATE INDEX IX_CrossRef_File_Episode_EpisodeID ON CrossRef_File_Episode(EpisodeID);");


			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_027(int currentVersionNumber)
		{
			int thisVersion = 27;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();

			cmds.Add("update CrossRef_File_Episode SET CrossRefSource=1 WHERE Hash IN (Select Hash from ANIDB_File) AND CrossRefSource=2;");


			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_028(int currentVersionNumber)
		{
			int thisVersion = 28;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE LogMessage( " +
				" LogMessageID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" LogType text, " +
				" LogContent text, " +
				" LogDate timestamp NOT NULL " +
				" ); ");


			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);
		}

		private static void UpdateSchema_029(int currentVersionNumber)
		{
			int thisVersion = 29;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE CrossRef_AniDB_TvDBV2( " +
				" CrossRef_AniDB_TvDBV2ID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeID int NOT NULL, " +
				" AniDBStartEpisodeType int NOT NULL, " +
				" AniDBStartEpisodeNumber int NOT NULL, " +
				" TvDBID int NOT NULL, " +
				" TvDBSeasonNumber int NOT NULL, " +
				" TvDBStartEpisodeNumber int NOT NULL, " +
				" TvDBTitle text, " +
				" CrossRefSource int NOT NULL " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDBV2 ON CrossRef_AniDB_TvDBV2(AnimeID, TvDBID, TvDBSeasonNumber, TvDBStartEpisodeNumber, AniDBStartEpisodeType, AniDBStartEpisodeNumber);");

			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();

			UpdateDatabaseVersion(thisVersion);

			// Now do the migratiuon
			DatabaseHelper.MigrateTvDBLinks_V1_to_V2();
		}

		private static void UpdateSchema_030(int currentVersionNumber)
		{
			int thisVersion = 30;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			List<string> cmds = new List<string>();
			cmds.Add("ALTER TABLE GroupFilter ADD Locked int NULL");

			ExecuteSQLCommands(cmds);

			UpdateDatabaseVersion(thisVersion);
		}
		private static void UpdateSchema_031(int currentVersionNumber)
		{
			int thisVersion = 31;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			List<string> cmds = new List<string>();
            cmds.Add("ALTER TABLE VideoInfo ADD FullInfo text NULL");

			ExecuteSQLCommands(cmds);

			UpdateDatabaseVersion(thisVersion);
		}

        private static void UpdateSchema_032(int currentVersionNumber)
        {
            int thisVersion = 32;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
            myConn.Open();

            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE CrossRef_AniDB_TraktV2( " +
                " CrossRef_AniDB_TraktV2ID INTEGER PRIMARY KEY AUTOINCREMENT, " +
                " AnimeID int NOT NULL, " +
                " AniDBStartEpisodeType int NOT NULL, " +
                " AniDBStartEpisodeNumber int NOT NULL, " +
                " TraktID text, " +
                " TraktSeasonNumber int NOT NULL, " +
                " TraktStartEpisodeNumber int NOT NULL, " +
                " TraktTitle text, " +
                " CrossRefSource int NOT NULL " +
                " ); ");

            cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TraktV2 ON CrossRef_AniDB_TraktV2(AnimeID, TraktSeasonNumber, TraktStartEpisodeNumber, AniDBStartEpisodeType, AniDBStartEpisodeNumber);");

            foreach (string cmdTable in cmds)
            {
                SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
                sqCommand.Connection = myConn;
                sqCommand.ExecuteNonQuery();
            }

            myConn.Close();

            UpdateDatabaseVersion(thisVersion);

            // Now do the migratiuon
            DatabaseHelper.MigrateTraktLinks_V1_to_V2();
        }


        private static void UpdateSchema_033(int currentVersionNumber)
        {
            int thisVersion = 33;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
            myConn.Open();

            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE CrossRef_AniDB_Trakt_Episode( " +
                " CrossRef_AniDB_Trakt_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, " +
                " AnimeID int NOT NULL, " +
                " AniDBEpisodeID int NOT NULL, " +
                 "TraktID text, " +
                " Season int NOT NULL, " +
                " EpisodeNumber int NOT NULL " +
                " ); ");

            cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Trakt_Episode_AniDBEpisodeID ON CrossRef_AniDB_Trakt_Episode(AniDBEpisodeID);");

            foreach (string cmdTable in cmds)
            {
                SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
                sqCommand.Connection = myConn;
                sqCommand.ExecuteNonQuery();
            }

            myConn.Close();

            UpdateDatabaseVersion(thisVersion);
        }


        private static void UpdateSchema_034(int currentVersionNumber)
        {
            int thisVersion = 34;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            UpdateDatabaseVersion(thisVersion);

            // Now do the migration
            DatabaseHelper.RemoveOldMovieDBImageRecords();
        }

        private static void UpdateSchema_035(int currentVersionNumber)
        {
            int thisVersion = 35;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
            myConn.Open();

            List<string> cmds = new List<string>();

            cmds.Add("CREATE TABLE CustomTag( " +
                " CustomTagID INTEGER PRIMARY KEY AUTOINCREMENT, " +
                " TagName text, " +
                " TagDescription text " +
                " ); ");

            cmds.Add("CREATE TABLE CrossRef_CustomTag( " +
                " CrossRef_CustomTagID INTEGER PRIMARY KEY AUTOINCREMENT, " +
                " CustomTagID int NOT NULL, " +
                " CrossRefID int NOT NULL, " +
                " CrossRefType int NOT NULL " +
                " ); ");


            foreach (string cmdTable in cmds)
            {
                SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
                sqCommand.Connection = myConn;
                sqCommand.ExecuteNonQuery();
            }

            myConn.Close();

            UpdateDatabaseVersion(thisVersion);

            DatabaseHelper.CreateInitialCustomTags();
        }

        private static void UpdateSchema_036(int currentVersionNumber)
        {
            int thisVersion = 36;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            List<string> cmds = new List<string>();
            cmds.Add("ALTER TABLE AniDB_Anime_Tag ADD Weight int NULL");

            ExecuteSQLCommands(cmds);

            UpdateDatabaseVersion(thisVersion);
        }

        private static void UpdateSchema_037(int currentVersionNumber)
        {
            int thisVersion = 37;
            if (currentVersionNumber >= thisVersion) return;

            logger.Info("Updating schema to VERSION: {0}", thisVersion);

            UpdateDatabaseVersion(thisVersion);

            DatabaseHelper.CreateInitialCustomTags();
        }

		private static void ExecuteSQLCommands(List<string> cmds)
		{
			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open();

			foreach (string cmdTable in cmds)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable);
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery();
			}

			myConn.Close();
		}

		private static void UpdateDatabaseVersion(int versionNumber)
		{
			VersionsRepository repVersions = new VersionsRepository();
			Versions ver = repVersions.GetByVersionType(Constants.DatabaseTypeKey);
			if (ver == null) return;

			ver.VersionValue = versionNumber.ToString();
			repVersions.Save(ver);
		}

		#endregion

		#region Create Initial Schema

		public static void CreateInitialSchema()
		{
			SQLiteConnection myConn = new SQLiteConnection(GetConnectionString());
			myConn.Open(); 

			string cmd = string.Format("SELECT count(*) as NumTables FROM sqlite_master WHERE name='Versions'");
			SQLiteCommand sqCommandCheck = new SQLiteCommand(cmd);
			sqCommandCheck.Connection = myConn;
			long count = long.Parse(sqCommandCheck.ExecuteScalar().ToString());

			// if the Versions already exists, it means we have done this already
			if (count > 0) return;

			//Create all the commands to be executed
			List<string> commands = new List<string>();
			commands.AddRange(CreateTableString_Versions());
			commands.AddRange(CreateTableString_AniDB_Anime());
			commands.AddRange(CreateTableString_AniDB_Anime_Category());
			commands.AddRange(CreateTableString_AniDB_Anime_Character());
			commands.AddRange(CreateTableString_AniDB_Anime_Relation());
			commands.AddRange(CreateTableString_AniDB_Anime_Review());
			commands.AddRange(CreateTableString_AniDB_Anime_Similar());
			commands.AddRange(CreateTableString_AniDB_Anime_Tag());
			commands.AddRange(CreateTableString_AniDB_Anime_Title());
			commands.AddRange(CreateTableString_AniDB_Category());
			commands.AddRange(CreateTableString_AniDB_Character());
			commands.AddRange(CreateTableString_AniDB_Character_Seiyuu());
			commands.AddRange(CreateTableString_AniDB_Seiyuu());
			commands.AddRange(CreateTableString_AniDB_Episode());
			commands.AddRange(CreateTableString_AniDB_File());
			commands.AddRange(CreateTableString_AniDB_GroupStatus());
			commands.AddRange(CreateTableString_AniDB_ReleaseGroup());
			commands.AddRange(CreateTableString_AniDB_Review());
			commands.AddRange(CreateTableString_AniDB_Tag());
			commands.AddRange(CreateTableString_AnimeEpisode());
			commands.AddRange(CreateTableString_AnimeGroup());
			commands.AddRange(CreateTableString_AnimeSeries());
			commands.AddRange(CreateTableString_CommandRequest());
			commands.AddRange(CreateTableString_CrossRef_AniDB_Other());
			commands.AddRange(CreateTableString_CrossRef_AniDB_TvDB());
			commands.AddRange(CreateTableString_CrossRef_File_Episode());
			commands.AddRange(CreateTableString_CrossRef_Languages_AniDB_File());
			commands.AddRange(CreateTableString_CrossRef_Subtitles_AniDB_File());
			commands.AddRange(CreateTableString_FileNameHash());
			commands.AddRange(CreateTableString_Language());
			commands.AddRange(CreateTableString_ImportFolder());
			commands.AddRange(CreateTableString_ScheduledUpdate());
			commands.AddRange(CreateTableString_VideoInfo());
			commands.AddRange(CreateTableString_VideoLocal());
			commands.AddRange(CreateTableString_DuplicateFile());
			commands.AddRange(CreateTableString_GroupFilter());
			commands.AddRange(CreateTableString_GroupFilterCondition());
			commands.AddRange(CreateTableString_AniDB_Vote());
			commands.AddRange(CreateTableString_TvDB_ImageFanart());
			commands.AddRange(CreateTableString_TvDB_ImageWideBanner());
			commands.AddRange(CreateTableString_TvDB_ImagePoster());
			commands.AddRange(CreateTableString_TvDB_Episode());
			commands.AddRange(CreateTableString_TvDB_Series());
			commands.AddRange(CreateTableString_AniDB_Anime_DefaultImage());
			commands.AddRange(CreateTableString_MovieDB_Movie());
			commands.AddRange(CreateTableString_MovieDB_Poster());
			commands.AddRange(CreateTableString_MovieDB_Fanart());
			commands.AddRange(CreateTableString_JMMUser());
			commands.AddRange(CreateTableString_Trakt_Episode());
			commands.AddRange(CreateTableString_Trakt_ImagePoster());
			commands.AddRange(CreateTableString_Trakt_ImageFanart());
			commands.AddRange(CreateTableString_Trakt_Show());
			commands.AddRange(CreateTableString_Trakt_Season());
			commands.AddRange(CreateTableString_CrossRef_AniDB_Trakt());

			commands.AddRange(CreateTableString_AnimeEpisode_User());
			commands.AddRange(CreateTableString_AnimeSeries_User());
			commands.AddRange(CreateTableString_AnimeGroup_User());
			commands.AddRange(CreateTableString_VideoLocal_User());

			foreach (string cmdTable in commands)
			{
				SQLiteCommand sqCommand = new SQLiteCommand(cmdTable); 
				sqCommand.Connection = myConn;
				sqCommand.ExecuteNonQuery(); 
			}

			myConn.Close();

			logger.Trace("Creating version...");
			Versions ver1 = new Versions();
			ver1.VersionType = Constants.DatabaseTypeKey;
			ver1.VersionValue = "1";

			VersionsRepository repVer = new VersionsRepository();
			repVer.Save(ver1);
			
		}

		public static List<string> CreateTableString_Versions()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE Versions ( " +
				" VersionsID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" VersionType Text NOT NULL, " +
				" VersionValue Text NOT NULL)");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Anime ( " +
				" AniDB_AnimeID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeID int NOT NULL, " +
				" EpisodeCount int NOT NULL, " +
				" AirDate timestamp NULL, " +
				" EndDate timestamp NULL, " +
				" URL text NULL, " +
				" Picname text NULL, " +
				" BeginYear int NOT NULL, " +
				" EndYear int NOT NULL, " +
				" AnimeType int NOT NULL, " +
				" MainTitle text NOT NULL, " +
				" AllTitles text NOT NULL, " +
				" AllCategories text NOT NULL, " +
				" AllTags text NOT NULL, " +
				" Description text NOT NULL, " +
				" EpisodeCountNormal int NOT NULL, " +
				" EpisodeCountSpecial int NOT NULL, " +
				" Rating int NOT NULL, " +
				" VoteCount int NOT NULL, " +
				" TempRating int NOT NULL, " +
				" TempVoteCount int NOT NULL, " +
				" AvgReviewRating int NOT NULL, " +
				" ReviewCount int NOT NULL, " +
				" DateTimeUpdated timestamp NOT NULL, " +
				" DateTimeDescUpdated timestamp NOT NULL, " +
				" ImageEnabled int NOT NULL, " +
				" AwardList text NOT NULL, " +
				" Restricted int NOT NULL, " +
				" AnimePlanetID int NULL, " +
				" ANNID int NULL, " +
				" AllCinemaID int NULL, " +
				" AnimeNfo int NULL, " +
				" LatestEpisodeNumber int NULL " +
				" );");

			cmds.Add("CREATE UNIQUE INDEX [UIX_AniDB_Anime_AnimeID] ON [AniDB_Anime] ([AnimeID]);");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_Category()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Anime_Category ( " +
				" AniDB_Anime_CategoryID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeID int NOT NULL, " +
       			" CategoryID int NOT NULL, " +
       			" Weighting int NOT NULL " +
				" ); ");

			cmds.Add("CREATE INDEX IX_AniDB_Anime_Category_AnimeID on AniDB_Anime_Category(AnimeID);");
			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Anime_Category_AnimeID_CategoryID ON AniDB_Anime_Category (AnimeID, CategoryID);");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_Character()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Anime_Character ( " +
				" AniDB_Anime_CharacterID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeID int NOT NULL, " +
				" CharID int NOT NULL, " +
				" CharType text NOT NULL, " +
				" EpisodeListRaw text NOT NULL " +
				" ); ");

			cmds.Add("CREATE INDEX IX_AniDB_Anime_Character_AnimeID on AniDB_Anime_Character(AnimeID);");
			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Anime_Character_AnimeID_CharID ON AniDB_Anime_Character(AnimeID, CharID);");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_Relation()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Anime_Relation ( " +
				" AniDB_Anime_RelationID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeID int NOT NULL, " +
				" RelatedAnimeID int NOT NULL, " +
				" RelationType text NOT NULL " +
				" ); ");

			cmds.Add("CREATE INDEX IX_AniDB_Anime_Relation_AnimeID on AniDB_Anime_Relation(AnimeID);");
			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Anime_Relation_AnimeID_RelatedAnimeID ON AniDB_Anime_Relation(AnimeID, RelatedAnimeID);");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_Review()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Anime_Review ( " +
				" AniDB_Anime_ReviewID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeID int NOT NULL, " +
				" ReviewID int NOT NULL " +
				" ); ");

			cmds.Add("CREATE INDEX IX_AniDB_Anime_Review_AnimeID on AniDB_Anime_Review(AnimeID);");
			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Anime_Review_AnimeID_ReviewID ON AniDB_Anime_Review(AnimeID, ReviewID);");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_Similar()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Anime_Similar ( " +
				" AniDB_Anime_SimilarID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeID int NOT NULL, " +
				" SimilarAnimeID int NOT NULL, " +
				" Approval int NOT NULL, " +
				" Total int NOT NULL " +
				" ); ");

			cmds.Add("CREATE INDEX IX_AniDB_Anime_Similar_AnimeID on AniDB_Anime_Similar(AnimeID);");
			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Anime_Similar_AnimeID_SimilarAnimeID ON AniDB_Anime_Similar(AnimeID, SimilarAnimeID);");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_Tag()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Anime_Tag ( " +
				" AniDB_Anime_TagID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeID int NOT NULL, " +
				" TagID int NOT NULL, " +
				" Approval int NOT NULL " +
				" ); ");

			cmds.Add("CREATE INDEX IX_AniDB_Anime_Tag_AnimeID on AniDB_Anime_Tag(AnimeID);");
			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Anime_Tag_AnimeID_TagID ON AniDB_Anime_Tag(AnimeID, TagID);");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_Title()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Anime_Title ( " +
				" AniDB_Anime_TitleID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeID int NOT NULL, " +
				" TitleType text NOT NULL, " +
				" Language text NOT NULL, " +
				" Title text NULL " +
				" ); ");

			cmds.Add("CREATE INDEX IX_AniDB_Anime_Title_AnimeID on AniDB_Anime_Title(AnimeID);");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Category()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Category ( " +
				" AniDB_CategoryID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" CategoryID int NOT NULL, " +
       			" ParentID int NOT NULL, " +
       			" IsHentai int NOT NULL, " +
       			" CategoryName text NOT NULL, " +
				" CategoryDescription text NOT NULL  " +  
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Category_CategoryID ON AniDB_Category(CategoryID);");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Character()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Character ( " +
				" AniDB_CharacterID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" CharID int NOT NULL, " +
				" CharName text NOT NULL, " +
				" PicName text NOT NULL, " +
				" CharKanjiName text NOT NULL, " +
				" CharDescription text NOT NULL, " +
				" CreatorListRaw text NOT NULL " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Character_CharID ON AniDB_Character(CharID);");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Character_Seiyuu()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE AniDB_Character_Seiyuu ( " +
				" AniDB_Character_SeiyuuID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" CharID int NOT NULL, " +
				" SeiyuuID int NOT NULL " +
				" ); ");

			cmds.Add("CREATE INDEX IX_AniDB_Character_Seiyuu_CharID on AniDB_Character_Seiyuu(CharID);");
			cmds.Add("CREATE INDEX IX_AniDB_Character_Seiyuu_SeiyuuID on AniDB_Character_Seiyuu(SeiyuuID);");
			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Character_Seiyuu_CharID_SeiyuuID ON AniDB_Character_Seiyuu(CharID, SeiyuuID);");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Seiyuu()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE AniDB_Seiyuu ( " +
				" AniDB_SeiyuuID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" SeiyuuID int NOT NULL, " +
				" SeiyuuName text NOT NULL, " +
				" PicName text NOT NULL " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Seiyuu_SeiyuuID ON AniDB_Seiyuu(SeiyuuID);");


			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Episode()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Episode ( " +
				" AniDB_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" EpisodeID int NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" LengthSeconds int NOT NULL, " +
				" Rating text NOT NULL, " +
				" Votes text NOT NULL, " +
				" EpisodeNumber int NOT NULL, " +
				" EpisodeType int NOT NULL, " +
				" RomajiName text NOT NULL, " +
				" EnglishName text NOT NULL, " +
				" AirDate int NOT NULL, " +
				" DateTimeUpdated timestamp NOT NULL " +
				" ); ");

			cmds.Add("CREATE INDEX IX_AniDB_Episode_AnimeID on AniDB_Episode(AnimeID);");
			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Episode_EpisodeID ON AniDB_Episode(EpisodeID);");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_File()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_File ( " +
				" AniDB_FileID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" FileID int NOT NULL, " +
				" Hash text NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" GroupID int NOT NULL, " +
				" File_Source text NOT NULL, " +
				" File_AudioCodec text NOT NULL, " +
				" File_VideoCodec text NOT NULL, " +
				" File_VideoResolution text NOT NULL, " +
				" File_FileExtension text NOT NULL, " +
				" File_LengthSeconds int NOT NULL, " +
				" File_Description text NOT NULL, " +
				" File_ReleaseDate int NOT NULL, " +
				" Anime_GroupName text NOT NULL, " +
				" Anime_GroupNameShort text NOT NULL, " +
				" Episode_Rating int NOT NULL, " +
				" Episode_Votes int NOT NULL, " +
				" DateTimeUpdated timestamp NOT NULL, " +
				" IsWatched int NOT NULL, " +
				" WatchedDate timestamp NULL, " +
				" CRC text NOT NULL, " +
				" MD5 text NOT NULL, " +
				" SHA1 text NOT NULL, " +
				" FileName text NOT NULL, " +
				" FileSize INTEGER NOT NULL " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_File_Hash on AniDB_File(Hash);");
			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_File_FileID ON AniDB_File(FileID);");
			cmds.Add("CREATE INDEX IX_AniDB_File_File_Source on AniDB_File(File_Source);");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_GroupStatus()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_GroupStatus ( " +
				" AniDB_GroupStatusID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeID int NOT NULL, " +
				" GroupID int NOT NULL, " +
				" GroupName text NOT NULL, " +
				" CompletionState int NOT NULL, " +
				" LastEpisodeNumber int NOT NULL, " +
				" Rating int NOT NULL, " +
				" Votes int NOT NULL, " +
				" EpisodeRange text NOT NULL " +
				" ); ");

			cmds.Add("CREATE INDEX IX_AniDB_GroupStatus_AnimeID on AniDB_GroupStatus(AnimeID);");
			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_GroupStatus_AnimeID_GroupID ON AniDB_GroupStatus(AnimeID, GroupID);");


			return cmds;
		}

		public static List<string> CreateTableString_AniDB_ReleaseGroup()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_ReleaseGroup ( " +
				" AniDB_ReleaseGroupID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" GroupID int NOT NULL, " +
				" Rating int NOT NULL, " +
				" Votes int NOT NULL, " +
				" AnimeCount int NOT NULL, " +
				" FileCount int NOT NULL, " +
				" GroupName text NOT NULL, " +
				" GroupNameShort text NOT NULL, " +
				" IRCChannel text NOT NULL, " +
				" IRCServer text NOT NULL, " +
				" URL text NOT NULL, " +
				" Picname text NOT NULL " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_ReleaseGroup_GroupID ON AniDB_ReleaseGroup(GroupID);");


			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Review()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Review ( " +
				" AniDB_ReviewID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" ReviewID int NOT NULL, " +
				" AuthorID int NOT NULL, " +
				" RatingAnimation int NOT NULL, " +
				" RatingSound int NOT NULL, " +
				" RatingStory int NOT NULL, " +
				" RatingCharacter int NOT NULL, " +
				" RatingValue int NOT NULL, " +
				" RatingEnjoyment int NOT NULL, " +
				" ReviewText text NOT NULL " +
				" ); ");


			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Review_ReviewID ON AniDB_Review(ReviewID);");


			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Tag()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Tag ( " +
				" AniDB_TagID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" TagID int NOT NULL, " +
       			" Spoiler int NOT NULL, " +
       			" LocalSpoiler int NOT NULL, " +
       			" GlobalSpoiler int NOT NULL, " +
       			" TagName text NOT NULL, " +
       			" TagCount int NOT NULL, " +
				" TagDescription text NOT NULL " +   
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Tag_TagID ON AniDB_Tag(TagID);");

			return cmds;
		}

		public static List<string> CreateTableString_AnimeEpisode()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE [AnimeEpisode]( " +
				" AnimeEpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeSeriesID int NOT NULL, " +
				" AniDB_EpisodeID int NOT NULL, " +
				" DateTimeUpdated timestamp NOT NULL, " +
				" DateTimeCreated timestamp NOT NULL " +
				" );");

			cmds.Add("CREATE UNIQUE INDEX UIX_AnimeEpisode_AniDB_EpisodeID ON AnimeEpisode(AniDB_EpisodeID);");
			cmds.Add("CREATE INDEX IX_AnimeEpisode_AnimeSeriesID on AnimeEpisode(AnimeSeriesID);");

			return cmds;
		}

		public static List<string> CreateTableString_AnimeEpisode_User()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AnimeEpisode_User( " +
				" AnimeEpisode_UserID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" JMMUserID int NOT NULL, " +
				" AnimeEpisodeID int NOT NULL, " +
				" AnimeSeriesID int NOT NULL, " + // we only have this column to improve performance
				" WatchedDate timestamp NULL, " +
				" PlayedCount int NOT NULL, " +
				" WatchedCount int NOT NULL, " +
				" StoppedCount int NOT NULL " +
				" );");

			cmds.Add("CREATE UNIQUE INDEX UIX_AnimeEpisode_User_User_EpisodeID ON AnimeEpisode_User(JMMUserID, AnimeEpisodeID);");
			cmds.Add("CREATE INDEX IX_AnimeEpisode_User_User_AnimeSeriesID on AnimeEpisode_User(JMMUserID, AnimeSeriesID);");

			return cmds;
		}

		public static List<string> CreateTableString_AnimeGroup()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AnimeGroup ( " +
				" AnimeGroupID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeGroupParentID int NULL, " +
				" GroupName text NOT NULL, " +
				" Description text NULL, " +
				" IsManuallyNamed int NOT NULL, " +
				" DateTimeUpdated timestamp NOT NULL, " +
				" DateTimeCreated timestamp NOT NULL, " +
				" SortName text NOT NULL, " +
				" MissingEpisodeCount int NOT NULL, " +
				" MissingEpisodeCountGroups int NOT NULL, " +
				" OverrideDescription int NOT NULL, " +
				" EpisodeAddedDate timestamp NULL " +
				" ); ");

			return cmds;
		}

		public static List<string> CreateTableString_AnimeGroup_User()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AnimeGroup_User( " +
				" AnimeGroup_UserID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" JMMUserID int NOT NULL, " +
				" AnimeGroupID int NOT NULL, " +
				" IsFave int NOT NULL, " +
				" UnwatchedEpisodeCount int NOT NULL, " +
				" WatchedEpisodeCount int NOT NULL, " +
				" WatchedDate timestamp NULL, " +
				" PlayedCount int NOT NULL, " +
				" WatchedCount int NOT NULL, " +
				" StoppedCount int NOT NULL " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_AnimeGroup_User_User_GroupID ON AnimeGroup_User(JMMUserID, AnimeGroupID);");

			return cmds;
		}

		public static List<string> CreateTableString_AnimeSeries()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AnimeSeries ( " +
					" AnimeSeriesID INTEGER PRIMARY KEY AUTOINCREMENT, " +
					" AnimeGroupID int NOT NULL, " +
					" AniDB_ID int NOT NULL, " +
					" DateTimeUpdated timestamp NOT NULL, " +
					" DateTimeCreated timestamp NOT NULL, " +
					" DefaultAudioLanguage text NULL, " +
					" DefaultSubtitleLanguage text NULL, " +
					" MissingEpisodeCount int NOT NULL, " +
					" MissingEpisodeCountGroups int NOT NULL, " +
					" LatestLocalEpisodeNumber int NOT NULL, " +
					" EpisodeAddedDate timestamp NULL " +
					" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_AnimeSeries_AniDB_ID ON AnimeSeries(AniDB_ID);");

			return cmds;
		}

		public static List<string> CreateTableString_AnimeSeries_User()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AnimeSeries_User( " +
				" AnimeSeries_UserID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" JMMUserID int NOT NULL, " +
				" AnimeSeriesID int NOT NULL, " +
				" UnwatchedEpisodeCount int NOT NULL, " +
				" WatchedEpisodeCount int NOT NULL, " +
				" WatchedDate timestamp NULL, " +
				" PlayedCount int NOT NULL, " +
				" WatchedCount int NOT NULL, " +
				" StoppedCount int NOT NULL " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_AnimeSeries_User_User_SeriesID ON AnimeSeries_User(JMMUserID, AnimeSeriesID);");

			return cmds;
		}

		public static List<string> CreateTableString_CommandRequest()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE CommandRequest ( " +
				" CommandRequestID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" Priority int NOT NULL, " +
				" CommandType int NOT NULL, " +
				" CommandID text NOT NULL, " +
				" CommandDetails text NOT NULL, " +
				" DateTimeUpdated timestamp NOT NULL " +
				" ); ");

			return cmds;
		}

		public static List<string> CreateTableString_CrossRef_AniDB_TvDB()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE CrossRef_AniDB_TvDB( " +
				" CrossRef_AniDB_TvDBID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeID int NOT NULL, " +
				" TvDBID int NOT NULL, " +
				" TvDBSeasonNumber int NOT NULL, " +
				" CrossRefSource int NOT NULL " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB ON CrossRef_AniDB_TvDB(AnimeID, TvDBID, TvDBSeasonNumber, CrossRefSource);");

			return cmds;
		}

		public static List<string> CreateTableString_CrossRef_AniDB_Other()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE CrossRef_AniDB_Other( " +
				" CrossRef_AniDB_OtherID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeID int NOT NULL, " +
				" CrossRefID text NOT NULL, " +
				" CrossRefSource int NOT NULL, " +
				" CrossRefType int NOT NULL " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Other ON CrossRef_AniDB_Other(AnimeID, CrossRefID, CrossRefSource, CrossRefType);");

			return cmds;
		}

		public static List<string> CreateTableString_CrossRef_File_Episode()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE CrossRef_File_Episode ( " +
				" CrossRef_File_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" Hash text NULL, " +
				" FileName text NOT NULL, " +
				" FileSize INTEGER NOT NULL, " +
				" CrossRefSource int NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" EpisodeID int NOT NULL, " +
				" Percentage int NOT NULL, " +
				" EpisodeOrder int NOT NULL " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_File_Episode_Hash_EpisodeID ON CrossRef_File_Episode(Hash, EpisodeID);");

			return cmds;
		}

		public static List<string> CreateTableString_CrossRef_Languages_AniDB_File()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE CrossRef_Languages_AniDB_File ( " +
				" CrossRef_Languages_AniDB_FileID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" FileID int NOT NULL, " +
				" LanguageID int NOT NULL " +
				" ); ");

			return cmds;
		}

		public static List<string> CreateTableString_CrossRef_Subtitles_AniDB_File()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE CrossRef_Subtitles_AniDB_File ( " +
				" CrossRef_Subtitles_AniDB_FileID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" FileID int NOT NULL, " +
				" LanguageID int NOT NULL " +
				" ); ");

			return cmds;
		}

		public static List<string> CreateTableString_FileNameHash()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE FileNameHash ( " +
				" FileNameHashID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" FileName text NOT NULL, " +
				" FileSize INTEGER NOT NULL, " +
				" Hash text NOT NULL, " +
				" DateTimeUpdated timestamp NOT NULL " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_FileNameHash ON FileNameHash(FileName, FileSize, Hash);");

			return cmds;
		}

		public static List<string> CreateTableString_Language()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE Language ( " +
				" LanguageID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" LanguageName text NOT NULL " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_Language_LanguageName ON Language(LanguageName);");

			return cmds;
		}

		public static List<string> CreateTableString_ImportFolder()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE ImportFolder ( " +
				" ImportFolderID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" ImportFolderType int NOT NULL, " +
				" ImportFolderName text NOT NULL, " +
				" ImportFolderLocation text NOT NULL, " +
				" IsDropSource int NOT NULL, " +
				" IsDropDestination int NOT NULL " +
				" ); ");

			return cmds;
		}

		public static List<string> CreateTableString_ScheduledUpdate()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE ScheduledUpdate( " +
				" ScheduledUpdateID INTEGER PRIMARY KEY AUTOINCREMENT,  " +
				" UpdateType int NOT NULL, " +
				" LastUpdate timestamp NOT NULL, " +
				" UpdateDetails text NOT NULL " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_ScheduledUpdate_UpdateType ON ScheduledUpdate(UpdateType);");

			return cmds;
		}

		public static List<string> CreateTableString_VideoInfo()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE VideoInfo ( " +
				" VideoInfoID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" Hash text NOT NULL, " +
				" FileSize INTEGER NOT NULL, " +
				" FileName text NOT NULL, " +
				" DateTimeUpdated timestamp NOT NULL, " +
				" VideoCodec text NOT NULL, " +
				" VideoBitrate text NOT NULL, " +
				" VideoFrameRate text NOT NULL, " +
				" VideoResolution text NOT NULL, " +
				" AudioCodec text NOT NULL, " +
				" AudioBitrate text NOT NULL, " +
				" Duration INTEGER NOT NULL " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_VideoInfo_Hash on VideoInfo(Hash);");

			return cmds;
		}

		public static List<string> CreateTableString_VideoLocal()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE VideoLocal ( " +
				" VideoLocalID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" FilePath text NOT NULL, " +
				" ImportFolderID int NOT NULL, " +
				" Hash text NOT NULL, " +
				" CRC32 text NULL, " +
				" MD5 text NULL, " +
				" SHA1 text NULL, " +
				" HashSource int NOT NULL, " +
				" FileSize INTEGER NOT NULL, " +
				" IsIgnored int NOT NULL, " +
				" DateTimeUpdated timestamp NOT NULL " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_VideoLocal_Hash on VideoLocal(Hash)");

			return cmds;
		}

		public static List<string> CreateTableString_VideoLocal_User()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE VideoLocal_User( " +
				" VideoLocal_UserID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" JMMUserID int NOT NULL, " +
				" VideoLocalID int NOT NULL, " +
				" WatchedDate timestamp NOT NULL " +
				" ); ");

			cmds.Add("CREATE UNIQUE INDEX UIX_VideoLocal_User_User_VideoLocalID ON VideoLocal_User(JMMUserID, VideoLocalID);");

			return cmds;
		}

		public static List<string> CreateTableString_DuplicateFile()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE DuplicateFile ( " +
				" DuplicateFileID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" FilePathFile1 text NOT NULL, " +
				" FilePathFile2 text NOT NULL, " +
				" ImportFolderIDFile1 int NOT NULL, " +
				" ImportFolderIDFile2 int NOT NULL, " +
				" Hash text NOT NULL, " +
				" DateTimeUpdated timestamp NOT NULL " +
				" ); ");

			return cmds;
		}

		public static List<string> CreateTableString_GroupFilter()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE GroupFilter( " +
				" GroupFilterID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" GroupFilterName text NOT NULL, " +
				" ApplyToSeries int NOT NULL, " +
				" BaseCondition int NOT NULL, " +
				" SortingCriteria text " +
				" ); ");

			return cmds;
		}

		public static List<string> CreateTableString_GroupFilterCondition()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE GroupFilterCondition( " +
				" GroupFilterConditionID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" GroupFilterID int NOT NULL, " +
				" ConditionType int NOT NULL, " +
				" ConditionOperator int NOT NULL, " +
				" ConditionParameter text NOT NULL " +
				" ); ");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Vote()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Vote ( " +
				" AniDB_VoteID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" EntityID int NOT NULL, " +
				" VoteValue int NOT NULL, " +
				" VoteType int NOT NULL " +
				" ); ");

			return cmds;
		}

		public static List<string> CreateTableString_TvDB_ImageFanart()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE TvDB_ImageFanart ( " +
				" TvDB_ImageFanartID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" Id integer NOT NULL, " +
				" SeriesID integer NOT NULL, " +
				" BannerPath text, " +
				" BannerType text, " +
				" BannerType2 text, " +
				" Colors text, " +
				" Language text, " +
				" ThumbnailPath text, " +
				" VignettePath text, " +
				" Enabled integer NOT NULL, " +
				" Chosen INTEGER NULL)");

			cmds.Add("CREATE UNIQUE INDEX UIX_TvDB_ImageFanart_Id ON TvDB_ImageFanart(Id)");

			return cmds;
		}

		public static List<string> CreateTableString_TvDB_ImageWideBanner()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE TvDB_ImageWideBanner ( " +
				" TvDB_ImageWideBannerID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" Id integer NOT NULL, " +
				" SeriesID integer NOT NULL, " +
				" BannerPath text, " +
				" BannerType text, " +
				" BannerType2 text, " +
				" Language text, " +
				" Enabled integer NOT NULL, " +
				" SeasonNumber integer)");

			cmds.Add("CREATE UNIQUE INDEX UIX_TvDB_ImageWideBanner_Id ON TvDB_ImageWideBanner(Id);");

			return cmds;
		}

		public static List<string> CreateTableString_TvDB_ImagePoster()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE TvDB_ImagePoster ( " +
				" TvDB_ImagePosterID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" Id integer NOT NULL, " +
				" SeriesID integer NOT NULL, " +
				" BannerPath text, " +
				" BannerType text, " +
				" BannerType2 text, " +
				" Language text, " +
				" Enabled integer NOT NULL, " +
				" SeasonNumber integer)");

			cmds.Add("CREATE UNIQUE INDEX UIX_TvDB_ImagePoster_Id ON TvDB_ImagePoster(Id)");

			return cmds;
		}

		public static List<string> CreateTableString_TvDB_Episode()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE TvDB_Episode ( " +
				" TvDB_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" Id integer NOT NULL, " +
				" SeriesID integer NOT NULL, " +
				" SeasonID integer NOT NULL, " +
				" SeasonNumber integer NOT NULL, " +
				" EpisodeNumber integer NOT NULL, " +
				" EpisodeName text, " +
				" Overview text, " +
				" Filename text, " +
				" EpImgFlag integer NOT NULL, " +
				" FirstAired text, " +
				" AbsoluteNumber integer, " +
				" AirsAfterSeason integer, " +
				" AirsBeforeEpisode integer, " +
				" AirsBeforeSeason integer)");

			cmds.Add("CREATE UNIQUE INDEX UIX_TvDB_Episode_Id ON TvDB_Episode(Id);");

			return cmds;
		}

		public static List<string> CreateTableString_TvDB_Series()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE TvDB_Series( " +
				" TvDB_SeriesID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" SeriesID integer NOT NULL, " +
				" Overview text, " +
				" SeriesName text, " +
				" Status text, " +
				" Banner text, " +
				" Fanart text, " +
				" Poster text, " +
				" Lastupdated text)");

			cmds.Add("CREATE UNIQUE INDEX UIX_TvDB_Series_Id ON TvDB_Series(SeriesID);");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_DefaultImage()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE AniDB_Anime_DefaultImage ( " +
				" AniDB_Anime_DefaultImageID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeID int NOT NULL, " +
				" ImageParentID int NOT NULL, " +
				" ImageParentType int NOT NULL, " +
				" ImageType int NOT NULL " +
				" );");

			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Anime_DefaultImage_ImageType ON AniDB_Anime_DefaultImage(AnimeID, ImageType)");

			return cmds;
		}

		public static List<string> CreateTableString_MovieDB_Movie()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE MovieDB_Movie( " +
				" MovieDB_MovieID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" MovieId int NOT NULL, " +
				" MovieName text, " +
				" OriginalName text, " +
				" Overview text " +
				" );");

			cmds.Add("CREATE UNIQUE INDEX UIX_MovieDB_Movie_Id ON MovieDB_Movie(MovieId)");

			return cmds;
		}

		public static List<string> CreateTableString_MovieDB_Poster()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE MovieDB_Poster( " +
				" MovieDB_PosterID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" ImageID text, " +
				" MovieId int NOT NULL, " +
				" ImageType text, " +
				" ImageSize text,  " +
				" URL text,  " +
				" ImageWidth int NOT NULL,  " +
				" ImageHeight int NOT NULL,  " +
				" Enabled int NOT NULL " +
				" );");

			return cmds;
		}

		public static List<string> CreateTableString_MovieDB_Fanart()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE MovieDB_Fanart( " +
				" MovieDB_FanartID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" ImageID text, " +
				" MovieId int NOT NULL, " +
				" ImageType text, " +
				" ImageSize text,  " +
				" URL text,  " +
				" ImageWidth int NOT NULL,  " +
				" ImageHeight int NOT NULL,  " +
				" Enabled int NOT NULL " +
				" );");

			return cmds;
		}

		public static List<string> CreateTableString_JMMUser()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE JMMUser( " +
				" JMMUserID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" Username text, " +
				" Password text, " +
				" IsAdmin int NOT NULL, " +
				" IsAniDBUser int NOT NULL, " +
				" IsTraktUser int NOT NULL, " +
				" HideCategories text " +
				" );");

			return cmds;
		}

		public static List<string> CreateTableString_Trakt_Episode()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE Trakt_Episode( " +
				" Trakt_EpisodeID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" Trakt_ShowID int NOT NULL, " +
				" Season int NOT NULL, " +
				" EpisodeNumber int NOT NULL, " +
				" Title text, " +
				" URL text, " +
				" Overview text, " +
				" EpisodeImage text " +
				" );");

			return cmds;
		}

		public static List<string> CreateTableString_Trakt_ImagePoster()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE Trakt_ImagePoster( " +
				" Trakt_ImagePosterID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" Trakt_ShowID int NOT NULL, " +
				" Season int NOT NULL, " +
				" ImageURL text, " +
				" Enabled int NOT NULL " +
				" );");

			return cmds;
		}

		public static List<string> CreateTableString_Trakt_ImageFanart()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE Trakt_ImageFanart( " +
				" Trakt_ImageFanartID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" Trakt_ShowID int NOT NULL, " +
				" Season int NOT NULL, " +
				" ImageURL text, " +
				" Enabled int NOT NULL " +
				" );");

			return cmds;
		}

		public static List<string> CreateTableString_Trakt_Show()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE Trakt_Show( " +
				" Trakt_ShowID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" TraktID text, " +
				" Title text, " +
				" Year text, " +
				" URL text, " +
				" Overview text, " +
				" TvDB_ID int NULL " +
				" );");

			return cmds;
		}

		public static List<string> CreateTableString_Trakt_Season()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE Trakt_Season( " +
				" Trakt_SeasonID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" Trakt_ShowID int NOT NULL, " +
				" Season int NOT NULL, " +
				" URL text " +
				" );");

			return cmds;
		}

		public static List<string> CreateTableString_CrossRef_AniDB_Trakt()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE CrossRef_AniDB_Trakt( " +
				" CrossRef_AniDB_TraktID INTEGER PRIMARY KEY AUTOINCREMENT, " +
				" AnimeID int NOT NULL, " +
				" TraktID text, " +
				" TraktSeasonNumber int NOT NULL, " +
				" CrossRefSource int NOT NULL " +
				" );");

			return cmds;
		}

		#endregion

		
	}
}
