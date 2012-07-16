using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.IO;
using Microsoft.Win32;
using JMMServer.Entities;
using JMMServer.Repositories;
using NLog;
using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Common;

namespace JMMServer.Databases
{
	public class SQLServer
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public static string GetConnectionString()
		{
			return string.Format("Server={0};Database={1};UID={2};PWD={3};",
					ServerSettings.DatabaseServer, ServerSettings.DatabaseName, ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword);
		}

		public static bool DatabaseAlreadyExists()
		{
			int count = 0;
			string cmd = string.Format("Select count(*) from sysdatabases where name = '{0}'", ServerSettings.DatabaseName);
			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, "master")))
			{
				using (SqlCommand command = new SqlCommand(cmd, tmpConn))
				{
					tmpConn.Open();
					object result = command.ExecuteScalar();
					count = int.Parse(result.ToString());
				}
			}

			// if the Versions already exists, it means we have done this already
			if (count > 0) return true;

			return false;
		}

		public static bool TestLogin()
		{
			return true;
		}

		public static void CreateDatabaseOld()
		{
			if (DatabaseAlreadyExists()) return;

			SQLServerDatabase db = new SQLServerDatabase();

			string dataPath = GetDatabasePath(ServerSettings.DatabaseServer);

			db.DatabaseName = ServerSettings.DatabaseName;
			db.MdfFileName = ServerSettings.DatabaseName;
			db.MdfFilePath = Path.Combine(dataPath, ServerSettings.DatabaseName + ".mdf");
			db.MdfFileSize = "3072KB";
			db.MdfMaxFileSize = "UNLIMITED";
			db.MdfFileGrowth = "1024KB";
			db.LdfFileName = ServerSettings.DatabaseName + "_log";
			db.LdfFilePath = Path.Combine(dataPath, ServerSettings.DatabaseName + ".ldf");
			db.LdfFileSize = "3072KB";
			db.LdfMaxFileSize = "2048GB";
			db.LdfFileGrowth = "1024KB";


			StringBuilder sb = new StringBuilder();
			sb.AppendFormat("CREATE DATABASE [{0}] ON PRIMARY ", db.DatabaseName);
			sb.AppendFormat("( NAME = N'{0}', FILENAME = N'{1}' , SIZE = ", db.MdfFileName, db.MdfFilePath);
			sb.AppendFormat("{0} , MAXSIZE = {1}, FILEGROWTH = {2}", db.MdfFileSize, db.MdfMaxFileSize, db.MdfFileGrowth);
			sb.Append(" )");
			sb.Append("    LOG ON ");
			sb.AppendFormat("( NAME = N'{0}', FILENAME = N'{1}' , SIZE = ", db.LdfFileName, db.LdfFilePath);
			sb.AppendFormat("{0} , MAXSIZE = {1}, FILEGROWTH = {2}", db.LdfFileSize, db.LdfMaxFileSize, db.LdfFileGrowth);
			sb.Append(" ) ");

			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database=master", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword)))
			{
				using (SqlCommand command = new SqlCommand(sb.ToString(), tmpConn))
				{
					tmpConn.Open();
					command.ExecuteNonQuery();

					Console.WriteLine("Database created successfully!");
				}

			}
		}

		public static void CreateDatabase()
		{
			if (DatabaseAlreadyExists()) return;

			ServerConnection conn = new ServerConnection(ServerSettings.DatabaseServer, ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword);
			Server srv = new Server(conn);
			
			Database db = new Database(srv, ServerSettings.DatabaseName);
			db.Create();
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

			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE IgnoreAnime( " +
				" IgnoreAnimeID int IDENTITY(1,1) NOT NULL, " +
				" JMMUserID int NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" IgnoreType int NOT NULL, " +
				" CONSTRAINT [PK_IgnoreAnime] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" IgnoreAnimeID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY]");

			cmds.Add("CREATE UNIQUE INDEX UIX_IgnoreAnime_User_AnimeID ON IgnoreAnime(JMMUserID, AnimeID, IgnoreType)");

			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				tmpConn.Open();
				foreach (string cmdTable in cmds)
				{
					using (SqlCommand command = new SqlCommand(cmdTable, tmpConn))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_003(int currentVersionNumber)
		{
			int thisVersion = 3;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE Trakt_Friend( " +
				" Trakt_FriendID int IDENTITY(1,1) NOT NULL, " +
				" Username nvarchar(100) NOT NULL, " +
				" FullName nvarchar(100) NULL, " +
				" Gender nvarchar(100) NULL, " +
				" Age nvarchar(100) NULL, " +
				" Location nvarchar(100) NULL, " +
				" About nvarchar(MAX) NULL, " +
				" Joined int NOT NULL, " +
				" Avatar nvarchar(MAX) NULL, " +
				" Url nvarchar(MAX) NULL, " +
				" LastAvatarUpdate datetime NOT NULL, " +
				" CONSTRAINT [PK_Trakt_Friend] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" Trakt_FriendID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY]");

			cmds.Add("CREATE UNIQUE INDEX UIX_Trakt_Friend_Username ON Trakt_Friend(Username)");

			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				tmpConn.Open();
				foreach (string cmdTable in cmds)
				{
					using (SqlCommand command = new SqlCommand(cmdTable, tmpConn))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			UpdateDatabaseVersion(thisVersion);

		}


		private static void UpdateSchema_004(int currentVersionNumber)
		{
			int thisVersion = 4;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			List<string> cmds = new List<string>();

			cmds.Add("ALTER TABLE AnimeGroup ADD DefaultAnimeSeriesID int NULL");

			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				tmpConn.Open();
				foreach (string cmdTable in cmds)
				{
					using (SqlCommand command = new SqlCommand(cmdTable, tmpConn))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_005(int currentVersionNumber)
		{
			int thisVersion = 5;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			List<string> cmds = new List<string>();

			cmds.Add("ALTER TABLE JMMUser ADD CanEditServerSettings int NULL");

			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				tmpConn.Open();
				foreach (string cmdTable in cmds)
				{
					using (SqlCommand command = new SqlCommand(cmdTable, tmpConn))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_006(int currentVersionNumber)
		{
			int thisVersion = 6;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			List<string> cmds = new List<string>();

			cmds.Add("ALTER TABLE VideoInfo ADD VideoBitDepth varchar(max) NULL");

			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				tmpConn.Open();
				foreach (string cmdTable in cmds)
				{
					using (SqlCommand command = new SqlCommand(cmdTable, tmpConn))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_007(int currentVersionNumber)
		{
			int thisVersion = 7;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);


			DatabaseHelper.FixDuplicateTvDBLinks();
			DatabaseHelper.FixDuplicateTraktLinks();
			

			List<string> cmds = new List<string>();

			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB_Season ON CrossRef_AniDB_TvDB(TvDBID, TvDBSeasonNumber)");

			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Trakt_Season ON CrossRef_AniDB_Trakt(TraktID, TraktSeasonNumber)");
			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Trakt_Anime ON CrossRef_AniDB_Trakt(AnimeID)");

			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				tmpConn.Open();
				foreach (string cmdTable in cmds)
				{
					using (SqlCommand command = new SqlCommand(cmdTable, tmpConn))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_008(int currentVersionNumber)
		{
			int thisVersion = 8;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			DatabaseHelper.FixDuplicateTvDBLinks();
			DatabaseHelper.FixDuplicateTraktLinks();

			List<string> cmds = new List<string>();

			cmds.Add("ALTER TABLE jmmuser ALTER COLUMN Password NVARCHAR(150) NULL");

			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				tmpConn.Open();
				foreach (string cmdTable in cmds)
				{
					using (SqlCommand command = new SqlCommand(cmdTable, tmpConn))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_009(int currentVersionNumber)
		{
			int thisVersion = 9;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			List<string> cmds = new List<string>();

			cmds.Add("ALTER TABLE ImportFolder ADD IsWatched int NULL");
			cmds.Add("UPDATE ImportFolder SET IsWatched = 1");
			cmds.Add("ALTER TABLE ImportFolder ALTER COLUMN IsWatched int NOT NULL");

			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				tmpConn.Open();
				foreach (string cmdTable in cmds)
				{
					using (SqlCommand command = new SqlCommand(cmdTable, tmpConn))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_010(int currentVersionNumber)
		{
			int thisVersion = 10;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE CrossRef_AniDB_MAL( " +
				" CrossRef_AniDB_MALID int IDENTITY(1,1) NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" MALID int NOT NULL, " +
				" MALTitle nvarchar(500), " +
				" CrossRefSource int NOT NULL, " +
				" CONSTRAINT [PK_CrossRef_AniDB_MAL] PRIMARY KEY CLUSTERED " +
				" ( " +
				" CrossRef_AniDB_MALID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_MAL_AnimeID ON CrossRef_AniDB_MAL(AnimeID)");
			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_MAL_MALID ON CrossRef_AniDB_MAL(MALID)");

			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				tmpConn.Open();
				foreach (string cmdTable in cmds)
				{
					using (SqlCommand command = new SqlCommand(cmdTable, tmpConn))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_011(int currentVersionNumber)
		{
			int thisVersion = 11;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			List<string> cmds = new List<string>();

			cmds.Add("DROP INDEX [UIX_CrossRef_AniDB_MAL_AnimeID] ON [dbo].[CrossRef_AniDB_MAL] WITH ( ONLINE = OFF )");
			cmds.Add("DROP INDEX [UIX_CrossRef_AniDB_MAL_MALID] ON [dbo].[CrossRef_AniDB_MAL] WITH ( ONLINE = OFF )");
			cmds.Add("DROP TABLE [dbo].[CrossRef_AniDB_MAL]");

			cmds.Add("CREATE TABLE CrossRef_AniDB_MAL( " +
				" CrossRef_AniDB_MALID int IDENTITY(1,1) NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" MALID int NOT NULL, " +
				" MALTitle nvarchar(500), " +
				" StartEpisodeType int NOT NULL, " +
				" StartEpisodeNumber int NOT NULL, " +
				" CrossRefSource int NOT NULL, " +
				" CONSTRAINT [PK_CrossRef_AniDB_MAL] PRIMARY KEY CLUSTERED " +
				" ( " +
				" CrossRef_AniDB_MALID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_MAL_MALID ON CrossRef_AniDB_MAL(MALID)");
			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_MAL_Anime ON CrossRef_AniDB_MAL(AnimeID, StartEpisodeType, StartEpisodeNumber)");

			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				tmpConn.Open();
				foreach (string cmdTable in cmds)
				{
					using (SqlCommand command = new SqlCommand(cmdTable, tmpConn))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_012(int currentVersionNumber)
		{
			int thisVersion = 12;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			List<string> cmds = new List<string>();


			cmds.Add("CREATE TABLE Playlist( " +
				" PlaylistID int IDENTITY(1,1) NOT NULL, " +
				" PlaylistName nvarchar(MAX) NULL, " +
				" PlaylistItems varchar(MAX) NULL, " +
				" DefaultPlayOrder int NOT NULL, " +
				" PlayWatched int NOT NULL, " +
				" PlayUnwatched int NOT NULL, " +
				" CONSTRAINT [PK_Playlist] PRIMARY KEY CLUSTERED " +
				" ( " +
				" PlaylistID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				tmpConn.Open();
				foreach (string cmdTable in cmds)
				{
					using (SqlCommand command = new SqlCommand(cmdTable, tmpConn))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_013(int currentVersionNumber)
		{
			int thisVersion = 13;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			List<string> cmds = new List<string>();

			cmds.Add("ALTER TABLE AnimeSeries ADD SeriesNameOverride nvarchar(500) NULL");

			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				tmpConn.Open();
				foreach (string cmdTable in cmds)
				{
					using (SqlCommand command = new SqlCommand(cmdTable, tmpConn))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_014(int currentVersionNumber)
		{
			int thisVersion = 14;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			List<string> cmds = new List<string>();


			cmds.Add("CREATE TABLE BookmarkedAnime( " +
				" BookmarkedAnimeID int IDENTITY(1,1) NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" Priority int NOT NULL, " +
				" Notes nvarchar(MAX) NULL, " +
				" Downloading int NOT NULL, " +
				" CONSTRAINT [PK_BookmarkedAnime] PRIMARY KEY CLUSTERED " +
				" ( " +
				" BookmarkedAnimeID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_BookmarkedAnime_AnimeID ON BookmarkedAnime(BookmarkedAnimeID)");

			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				tmpConn.Open();
				foreach (string cmdTable in cmds)
				{
					using (SqlCommand command = new SqlCommand(cmdTable, tmpConn))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_015(int currentVersionNumber)
		{
			int thisVersion = 15;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			List<string> cmds = new List<string>();

			cmds.Add("ALTER TABLE VideoLocal ADD DateTimeCreated datetime NULL");
			cmds.Add("UPDATE VideoLocal SET DateTimeCreated = DateTimeUpdated");
			cmds.Add("ALTER TABLE VideoLocal ALTER COLUMN DateTimeCreated datetime NOT NULL");

			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				tmpConn.Open();
				foreach (string cmdTable in cmds)
				{
					using (SqlCommand command = new SqlCommand(cmdTable, tmpConn))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_016(int currentVersionNumber)
		{
			int thisVersion = 16;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE CrossRef_AniDB_TvDB_Episode( " +
				" CrossRef_AniDB_TvDB_EpisodeID int IDENTITY(1,1) NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" AniDBEpisodeID int NOT NULL, " +
				" TvDBEpisodeID int NOT NULL, " +
				" CONSTRAINT [PK_CrossRef_AniDB_TvDB_Episode] PRIMARY KEY CLUSTERED " +
				" ( " +
				" CrossRef_AniDB_TvDB_EpisodeID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB_Episode_AniDBEpisodeID ON CrossRef_AniDB_TvDB_Episode(AniDBEpisodeID)");

			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				tmpConn.Open();
				foreach (string cmdTable in cmds)
				{
					using (SqlCommand command = new SqlCommand(cmdTable, tmpConn))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_017(int currentVersionNumber)
		{
			int thisVersion = 17;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE AniDB_MylistStats( " +
				" AniDB_MylistStatsID int IDENTITY(1,1) NOT NULL, " +
				" Animes int NOT NULL, " +
				" Episodes int NOT NULL, " +
				" Files int NOT NULL, " +
				" SizeOfFiles bigint NOT NULL, " +
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
				" ViewiedLength int NOT NULL, " +
				" CONSTRAINT [PK_AniDB_MylistStats] PRIMARY KEY CLUSTERED " +
				" ( " +
				" AniDB_MylistStatsID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");


			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				tmpConn.Open();
				foreach (string cmdTable in cmds)
				{
					using (SqlCommand command = new SqlCommand(cmdTable, tmpConn))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_018(int currentVersionNumber)
		{
			int thisVersion = 18;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE FileFfdshowPreset( " +
				" FileFfdshowPresetID int IDENTITY(1,1) NOT NULL, " +
				" Hash varchar(50) NOT NULL, " +
				" FileSize bigint NOT NULL, " +
				" Preset nvarchar(MAX) NULL, " +
				" CONSTRAINT [PK_FileFfdshowPreset] PRIMARY KEY CLUSTERED " +
				" ( " +
				" FileFfdshowPresetID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_FileFfdshowPreset_Hash ON FileFfdshowPreset(Hash, FileSize)");


			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				tmpConn.Open();
				foreach (string cmdTable in cmds)
				{
					using (SqlCommand command = new SqlCommand(cmdTable, tmpConn))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_019(int currentVersionNumber)
		{
			int thisVersion = 19;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			List<string> cmds = new List<string>();

			cmds.Add("ALTER TABLE AniDB_Anime ADD DisableExternalLinksFlag int NULL");
			cmds.Add("UPDATE AniDB_Anime SET DisableExternalLinksFlag = 0");
			cmds.Add("ALTER TABLE AniDB_Anime ALTER COLUMN DisableExternalLinksFlag int NOT NULL");

			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				tmpConn.Open();
				foreach (string cmdTable in cmds)
				{
					using (SqlCommand command = new SqlCommand(cmdTable, tmpConn))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			UpdateDatabaseVersion(thisVersion);

		}

		private static void UpdateSchema_020(int currentVersionNumber)
		{
			int thisVersion = 20;
			if (currentVersionNumber >= thisVersion) return;

			logger.Info("Updating schema to VERSION: {0}", thisVersion);

			List<string> cmds = new List<string>();

			cmds.Add("ALTER TABLE AniDB_File ADD FileVersion int NULL");
			cmds.Add("UPDATE AniDB_File SET FileVersion = 1");
			cmds.Add("ALTER TABLE AniDB_File ALTER COLUMN FileVersion int NOT NULL");

			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				tmpConn.Open();
				foreach (string cmdTable in cmds)
				{
					using (SqlCommand command = new SqlCommand(cmdTable, tmpConn))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			UpdateDatabaseVersion(thisVersion);

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
			

			int count = 0;
			string cmd = string.Format("Select count(*) from sysobjects where name = 'Versions'");
			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				using (SqlCommand command = new SqlCommand(cmd, tmpConn))
				{
					tmpConn.Open();
					object result = command.ExecuteScalar();
					count = int.Parse(result.ToString());
				}
			}

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
			

			//commands.AddRange(CreateTableString_CrossRef_AnimeEpisode_Hash());

			using (SqlConnection tmpConn = new SqlConnection(string.Format("Server={0};User ID={1};Password={2};database={3}", ServerSettings.DatabaseServer,
				ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword, ServerSettings.DatabaseName)))
			{
				tmpConn.Open();
				foreach (string cmdTable in commands)
				{
					using (SqlCommand command = new SqlCommand(cmdTable, tmpConn))
					{
						command.ExecuteNonQuery();
					}
				}
			}

			Console.WriteLine("Creating version...");
			Versions ver1 = new Versions();
			ver1.VersionType = Constants.DatabaseTypeKey;
			ver1.VersionValue = "1";

			VersionsRepository repVer = new VersionsRepository();
			repVer.Save(ver1);
		}


		public static List<string> CreateTableString_Versions()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE [Versions]( " +
				" [VersionsID] [int] IDENTITY(1,1) NOT NULL, " +
				" [VersionType] [varchar](100) NOT NULL, " +
				" [VersionValue] [varchar](100) NOT NULL,  " +
				" CONSTRAINT [PK_Versions] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" [VersionsID] ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_Versions_VersionType ON Versions(VersionType)");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Anime( " +
				" AniDB_AnimeID int IDENTITY(1,1) NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" EpisodeCount int NOT NULL, " +
				" AirDate datetime NULL, " +
				" EndDate datetime NULL, " +
				" URL varchar(max) NULL, " +
				" Picname varchar(max) NULL, " +
				" BeginYear int NOT NULL, " +
				" EndYear int NOT NULL, " +
				" AnimeType int NOT NULL, " +
				" MainTitle nvarchar(500) NOT NULL, " +
				" AllTitles nvarchar(1500) NOT NULL, " +
				" AllCategories nvarchar(MAX) NOT NULL, " +
				" AllTags nvarchar(MAX) NOT NULL, " +
				" Description varchar(max) NOT NULL, " +
				" EpisodeCountNormal int NOT NULL, " +
				" EpisodeCountSpecial int NOT NULL, " +
				" Rating int NOT NULL, " +
				" VoteCount int NOT NULL, " +
				" TempRating int NOT NULL, " +
				" TempVoteCount int NOT NULL, " +
				" AvgReviewRating int NOT NULL, " +
				" ReviewCount int NOT NULL, " +
				" DateTimeUpdated datetime NOT NULL, " +
				" DateTimeDescUpdated datetime NOT NULL, " +
				" ImageEnabled int NOT NULL, " +
				" AwardList varchar(max) NOT NULL, " +
				" Restricted int NOT NULL, " +
				" AnimePlanetID int NULL, " +
				" ANNID int NULL, " +
				" AllCinemaID int NULL, " +
				" AnimeNfo int NULL, " +
				" [LatestEpisodeNumber] [int] NULL, " +
				" CONSTRAINT [PK_AniDB_Anime] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" [AniDB_AnimeID] ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Anime_AnimeID ON AniDB_Anime(AnimeID)");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_Category()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Anime_Category ( " +
				" AniDB_Anime_CategoryID int IDENTITY(1,1) NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" CategoryID int NOT NULL, " +
				" Weighting int NOT NULL, " +
				" CONSTRAINT [PK_AniDB_Anime_Category] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AniDB_Anime_CategoryID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE INDEX IX_AniDB_Anime_Category_AnimeID on AniDB_Anime_Category(AnimeID)");
			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Anime_Category_AnimeID_CategoryID ON AniDB_Anime_Category(AnimeID, CategoryID)");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_Character()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Anime_Character ( " +
				" AniDB_Anime_CharacterID int IDENTITY(1,1) NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" CharID int NOT NULL, " +
				" CharType varchar(100) NOT NULL, " +
				" EpisodeListRaw varchar(max) NULL, " +
				" CONSTRAINT [PK_AniDB_Anime_Character] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AniDB_Anime_CharacterID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE INDEX IX_AniDB_Anime_Character_AnimeID on AniDB_Anime_Character(AnimeID)");
			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Anime_Character_AnimeID_CharID ON AniDB_Anime_Character(AnimeID, CharID)");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_Relation()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Anime_Relation ( " +
				" AniDB_Anime_RelationID int IDENTITY(1,1) NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" RelatedAnimeID int NOT NULL, " +
				" RelationType varchar(100) NOT NULL, " +
				" CONSTRAINT [PK_AniDB_Anime_Relation] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AniDB_Anime_RelationID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE INDEX IX_AniDB_Anime_Relation_AnimeID on AniDB_Anime_Relation(AnimeID)");
			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Anime_Relation_AnimeID_RelatedAnimeID ON AniDB_Anime_Relation(AnimeID, RelatedAnimeID)");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_Review()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Anime_Review ( " +
				" AniDB_Anime_ReviewID int IDENTITY(1,1) NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" ReviewID int NOT NULL, " +
				" CONSTRAINT [PK_AniDB_Anime_Review] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AniDB_Anime_ReviewID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE INDEX IX_AniDB_Anime_Review_AnimeID on AniDB_Anime_Review(AnimeID)");
			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Anime_Review_AnimeID_ReviewID ON AniDB_Anime_Review(AnimeID, ReviewID)");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_Similar()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Anime_Similar ( " +
				" AniDB_Anime_SimilarID int IDENTITY(1,1) NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" SimilarAnimeID int NOT NULL, " +
				" Approval int NOT NULL, " +
				" Total int NOT NULL, " +
				" CONSTRAINT [PK_AniDB_Anime_Similar] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AniDB_Anime_SimilarID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE INDEX IX_AniDB_Anime_Similar_AnimeID on AniDB_Anime_Similar(AnimeID)");
			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Anime_Similar_AnimeID_SimilarAnimeID ON AniDB_Anime_Similar(AnimeID, SimilarAnimeID)");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_Tag()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Anime_Tag ( " +
				" AniDB_Anime_TagID int IDENTITY(1,1) NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" TagID int NOT NULL, " +
				" Approval int NOT NULL, " +
				" CONSTRAINT [PK_AniDB_Anime_Tag] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AniDB_Anime_TagID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE INDEX IX_AniDB_Anime_Tag_AnimeID on AniDB_Anime_Tag(AnimeID)");
			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Anime_Tag_AnimeID_TagID ON AniDB_Anime_Tag(AnimeID, TagID)");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_Title()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE [AniDB_Anime_Title]( " +
				" AniDB_Anime_TitleID int IDENTITY(1,1) NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" TitleType varchar(50) NOT NULL, " +
				" Language nvarchar(50) NOT NULL, " +
				" Title nvarchar(500) NOT NULL, " +
				" CONSTRAINT [PK_AniDB_Anime_Title] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AniDB_Anime_TitleID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE INDEX IX_AniDB_Anime_Title_AnimeID on AniDB_Anime_Title(AnimeID)");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Category()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Category ( " +
				" AniDB_CategoryID int IDENTITY(1,1) NOT NULL, " +
				" CategoryID int NOT NULL, " +
				" ParentID int NOT NULL, " +
				" IsHentai int NOT NULL, " +
				" CategoryName varchar(50) NOT NULL, " +
				" CategoryDescription varchar(max) NOT NULL, " +
				" CONSTRAINT [PK_AniDB_Category] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AniDB_CategoryID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Category_CategoryID ON AniDB_Category(CategoryID)");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Character()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Character ( " +
				" AniDB_CharacterID int IDENTITY(1,1) NOT NULL, " +
				" CharID int NOT NULL, " +
				" CharName nvarchar(200) NOT NULL, " +
				" PicName varchar(100) NOT NULL, " +
				" CharKanjiName nvarchar(max) NOT NULL, " +
				" CharDescription nvarchar(max) NOT NULL, " +
				" CreatorListRaw varchar(max) NOT NULL, " +
				" CONSTRAINT [PK_AniDB_Character] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AniDB_CharacterID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Character_CharID ON AniDB_Character(CharID)");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Character_Seiyuu()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Character_Seiyuu ( " +
				" AniDB_Character_SeiyuuID int IDENTITY(1,1) NOT NULL, " +
				" CharID int NOT NULL, " +
				" SeiyuuID int NOT NULL " +
				" CONSTRAINT [PK_AniDB_Character_Seiyuu] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AniDB_Character_SeiyuuID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE INDEX IX_AniDB_Character_Seiyuu_CharID on AniDB_Character_Seiyuu(CharID)");
			cmds.Add("CREATE INDEX IX_AniDB_Character_Seiyuu_SeiyuuID on AniDB_Character_Seiyuu(SeiyuuID)");
			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Character_Seiyuu_CharID_SeiyuuID ON AniDB_Character_Seiyuu(CharID, SeiyuuID)");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Seiyuu()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Seiyuu ( " +
				" AniDB_SeiyuuID int IDENTITY(1,1) NOT NULL, " +
				" SeiyuuID int NOT NULL, " +
				" SeiyuuName nvarchar(200) NOT NULL, " +
				" PicName varchar(100) NOT NULL, " +
				" CONSTRAINT [PK_AniDB_Seiyuu] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AniDB_SeiyuuID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Seiyuu_SeiyuuID ON AniDB_Seiyuu(SeiyuuID)");


			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Episode()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Episode( " +
				" AniDB_EpisodeID int IDENTITY(1,1) NOT NULL, " +
				" EpisodeID int NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" LengthSeconds int NOT NULL, " +
				" Rating varchar(max) NOT NULL, " +
				" Votes varchar(max) NOT NULL, " +
				" EpisodeNumber int NOT NULL, " +
				" EpisodeType int NOT NULL, " +
				" RomajiName varchar(max) NOT NULL, " +
				" EnglishName varchar(max) NOT NULL, " +
				" AirDate int NOT NULL, " +
				" DateTimeUpdated datetime NOT NULL, " +
				" CONSTRAINT [PK_AniDB_Episode] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AniDB_EpisodeID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE INDEX IX_AniDB_Episode_AnimeID on AniDB_Episode(AnimeID)");
			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Episode_EpisodeID ON AniDB_Episode(EpisodeID)");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_File()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_File( " +
				" AniDB_FileID int IDENTITY(1,1) NOT NULL, " +
				" FileID int NOT NULL, " +
				" Hash varchar(50) NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" GroupID int NOT NULL, " +
				" File_Source varchar(max) NOT NULL, " +
				" File_AudioCodec varchar(max) NOT NULL, " +
				" File_VideoCodec varchar(max) NOT NULL, " +
				" File_VideoResolution varchar(max) NOT NULL, " +
				" File_FileExtension varchar(max) NOT NULL, " +
				" File_LengthSeconds int NOT NULL, " +
				" File_Description varchar(max) NOT NULL, " +
				" File_ReleaseDate int NOT NULL, " +
				" Anime_GroupName nvarchar(max) NOT NULL, " +
				" Anime_GroupNameShort nvarchar(max) NOT NULL, " +
				" Episode_Rating int NOT NULL, " +
				" Episode_Votes int NOT NULL, " +
				" DateTimeUpdated datetime NOT NULL, " +
				" IsWatched int NOT NULL, " +
				" WatchedDate datetime NULL, " +
				" CRC varchar(max) NOT NULL, " +
				" MD5 varchar(max) NOT NULL, " +
				" SHA1 varchar(max) NOT NULL, " +
				" FileName nvarchar(max) NOT NULL, " +
				" FileSize bigint NOT NULL, " +
				" CONSTRAINT [PK_AniDB_File] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AniDB_FileID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_File_Hash on AniDB_File(Hash)");
			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_File_FileID ON AniDB_File(FileID)");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_GroupStatus()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_GroupStatus ( " +
				" AniDB_GroupStatusID int IDENTITY(1,1) NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" GroupID int NOT NULL, " +
				" GroupName nvarchar(200) NOT NULL, " +
				" CompletionState int NOT NULL, " +
				" LastEpisodeNumber int NOT NULL, " +
				" Rating int NOT NULL, " +
				" Votes int NOT NULL, " +
				" EpisodeRange nvarchar(200) NOT NULL, " +
				" CONSTRAINT [PK_AniDB_GroupStatus] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AniDB_GroupStatusID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE INDEX IX_AniDB_GroupStatus_AnimeID on AniDB_GroupStatus(AnimeID)");
			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_GroupStatus_AnimeID_GroupID ON AniDB_GroupStatus(AnimeID, GroupID)");


			return cmds;
		}

		public static List<string> CreateTableString_AniDB_ReleaseGroup()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_ReleaseGroup ( " +
				" AniDB_ReleaseGroupID int IDENTITY(1,1) NOT NULL, " +
				" GroupID int NOT NULL, " +
				" Rating int NOT NULL, " +
				" Votes int NOT NULL, " +
				" AnimeCount int NOT NULL, " +
				" FileCount int NOT NULL, " +
				" GroupName nvarchar(MAX) NOT NULL, " +
				" GroupNameShort nvarchar(200) NOT NULL, " +
				" IRCChannel nvarchar(200) NOT NULL, " +
				" IRCServer nvarchar(200) NOT NULL, " +
				" URL nvarchar(200) NOT NULL, " +
				" Picname nvarchar(200) NOT NULL, " +
				" CONSTRAINT [PK_AniDB_ReleaseGroup] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AniDB_ReleaseGroupID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_ReleaseGroup_GroupID ON AniDB_ReleaseGroup(GroupID)");


			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Review()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Review ( " +
				" AniDB_ReviewID int IDENTITY(1,1) NOT NULL, " +
				" ReviewID int NOT NULL, " +
				" AuthorID int NOT NULL, " +
				" RatingAnimation int NOT NULL, " +
				" RatingSound int NOT NULL, " +
				" RatingStory int NOT NULL, " +
				" RatingCharacter int NOT NULL, " +
				" RatingValue int NOT NULL, " +
				" RatingEnjoyment int NOT NULL, " +
				" ReviewText nvarchar(MAX) NOT NULL, " +
				" CONSTRAINT [PK_AniDB_Review] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AniDB_ReviewID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Review_ReviewID ON AniDB_Review(ReviewID)");


			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Tag()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Tag ( " +
				" AniDB_TagID int IDENTITY(1,1) NOT NULL, " +
				" TagID int NOT NULL, " +
				" Spoiler int NOT NULL, " +
				" LocalSpoiler int NOT NULL, " +
				" GlobalSpoiler int NOT NULL, " +
				" TagName nvarchar(150) NOT NULL, " +
				" TagCount int NOT NULL, " +
				" TagDescription nvarchar(max) NOT NULL, " +
				" CONSTRAINT [PK_AniDB_Tag] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AniDB_TagID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Tag_TagID ON AniDB_Tag(TagID)");

			return cmds;
		}

		public static List<string> CreateTableString_AnimeEpisode()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AnimeEpisode( " +
				" AnimeEpisodeID int IDENTITY(1,1) NOT NULL, " +
				" AnimeSeriesID int NOT NULL, " +
				" AniDB_EpisodeID int NOT NULL, " +
				" DateTimeUpdated datetime NOT NULL, " +
				" DateTimeCreated datetime NOT NULL, " +
				" CONSTRAINT [PK_AnimeEpisode] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AnimeEpisodeID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY]");

			cmds.Add("CREATE UNIQUE INDEX UIX_AnimeEpisode_AniDB_EpisodeID ON AnimeEpisode(AniDB_EpisodeID)");
			cmds.Add("CREATE INDEX IX_AnimeEpisode_AnimeSeriesID on AnimeEpisode(AnimeSeriesID)");

			return cmds;
		}

		public static List<string> CreateTableString_AnimeEpisode_User()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AnimeEpisode_User( " +
				" AnimeEpisode_UserID int IDENTITY(1,1) NOT NULL, " +
				" JMMUserID int NOT NULL, " +
				" AnimeEpisodeID int NOT NULL, " +
				" AnimeSeriesID int NOT NULL, " + // we only have this column to improve performance
				" WatchedDate datetime NULL, " +
				" PlayedCount int NOT NULL, " +
				" WatchedCount int NOT NULL, " +
				" StoppedCount int NOT NULL, " +
				" CONSTRAINT [PK_AnimeEpisode_User] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AnimeEpisode_UserID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY]");

			cmds.Add("CREATE UNIQUE INDEX UIX_AnimeEpisode_User_User_EpisodeID ON AnimeEpisode_User(JMMUserID, AnimeEpisodeID)");
			cmds.Add("CREATE INDEX IX_AnimeEpisode_User_User_AnimeSeriesID on AnimeEpisode_User(JMMUserID, AnimeSeriesID)");

			return cmds;
		}

		public static List<string> CreateTableString_VideoLocal()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE VideoLocal( " +
				" VideoLocalID int IDENTITY(1,1) NOT NULL, " +
				" FilePath nvarchar(max) NOT NULL, " +
				" ImportFolderID int NOT NULL, " +
				" Hash varchar(50) NOT NULL, " +
				" CRC32 varchar(50) NULL, " +
				" MD5 varchar(50) NULL, " +
				" SHA1 varchar(50) NULL, " +
				" HashSource int NOT NULL, " +
				" FileSize bigint NOT NULL, " +
				" IsIgnored int NOT NULL, " +
				" DateTimeUpdated datetime NOT NULL, " +
				" CONSTRAINT [PK_VideoLocal] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" VideoLocalID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_VideoLocal_Hash on VideoLocal(Hash)");

			return cmds;
		}

		public static List<string> CreateTableString_VideoLocal_User()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE VideoLocal_User( " +
				" VideoLocal_UserID int IDENTITY(1,1) NOT NULL, " +
				" JMMUserID int NOT NULL, " +
				" VideoLocalID int NOT NULL, " +
				" WatchedDate datetime NOT NULL, " +
				" CONSTRAINT [PK_VideoLocal_User] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" VideoLocal_UserID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_VideoLocal_User_User_VideoLocalID ON VideoLocal_User(JMMUserID, VideoLocalID)");

			return cmds;
		}

		public static List<string> CreateTableString_AnimeGroup()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AnimeGroup( " +
				" AnimeGroupID int IDENTITY(1,1) NOT NULL, " +
				" AnimeGroupParentID int NULL, " +
				" GroupName nvarchar(max) NOT NULL, " +
				" Description nvarchar(max) NULL, " +
				" IsManuallyNamed int NOT NULL, " +
				" DateTimeUpdated datetime NOT NULL, " +
				" DateTimeCreated datetime NOT NULL, " +		
				" SortName varchar(max) NOT NULL, " +
				" MissingEpisodeCount int NOT NULL, " +
				" MissingEpisodeCountGroups int NOT NULL, " +
				" OverrideDescription int NOT NULL, " +
				" EpisodeAddedDate datetime NULL, " +
				" CONSTRAINT [PK_AnimeGroup] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" [AnimeGroupID] ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			return cmds;
		}

		public static List<string> CreateTableString_AnimeGroup_User()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AnimeGroup_User( " +
				" AnimeGroup_UserID int IDENTITY(1,1) NOT NULL, " +
				" JMMUserID int NOT NULL, " +
				" AnimeGroupID int NOT NULL, " +
				" IsFave int NOT NULL, " +
				" UnwatchedEpisodeCount int NOT NULL, " +
				" WatchedEpisodeCount int NOT NULL, " +
				" WatchedDate datetime NULL, " +
				" PlayedCount int NOT NULL, " +
				" WatchedCount int NOT NULL, " +
				" StoppedCount int NOT NULL, " +
				" CONSTRAINT [PK_AnimeGroup_User] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AnimeGroup_UserID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY]");

			cmds.Add("CREATE UNIQUE INDEX UIX_AnimeGroup_User_User_GroupID ON AnimeGroup_User(JMMUserID, AnimeGroupID)");

			return cmds;
		}

		public static List<string> CreateTableString_AnimeSeries()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AnimeSeries ( " +
				" AnimeSeriesID int IDENTITY(1,1) NOT NULL, " +
				" AnimeGroupID int NOT NULL, " +
				" AniDB_ID int NOT NULL, " +
				" DateTimeUpdated datetime NOT NULL, " +
				" DateTimeCreated datetime NOT NULL, " +
				" DefaultAudioLanguage varchar(max) NULL, " +
				" DefaultSubtitleLanguage varchar(max) NULL, " +
				" MissingEpisodeCount int NOT NULL, " +
				" MissingEpisodeCountGroups int NOT NULL, " +
				" LatestLocalEpisodeNumber int NOT NULL, " +
				" EpisodeAddedDate datetime NULL, " +
				" CONSTRAINT [PK_AnimeSeries] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AnimeSeriesID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_AnimeSeries_AniDB_ID ON AnimeSeries(AniDB_ID)");

			return cmds;
		}

		public static List<string> CreateTableString_AnimeSeries_User()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AnimeSeries_User( " +
				" AnimeSeries_UserID int IDENTITY(1,1) NOT NULL, " +
				" JMMUserID int NOT NULL, " +
				" AnimeSeriesID int NOT NULL, " +
				" UnwatchedEpisodeCount int NOT NULL, " +
				" WatchedEpisodeCount int NOT NULL, " +
				" WatchedDate datetime NULL, " +
				" PlayedCount int NOT NULL, " +
				" WatchedCount int NOT NULL, " +
				" StoppedCount int NOT NULL, " +
				" CONSTRAINT [PK_AnimeSeries_User] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AnimeSeries_UserID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY]");

			cmds.Add("CREATE UNIQUE INDEX UIX_AnimeSeries_User_User_SeriesID ON AnimeSeries_User(JMMUserID, AnimeSeriesID)");

			return cmds;
		}

		public static List<string> CreateTableString_CommandRequest()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE CommandRequest( " +
				" CommandRequestID int IDENTITY(1,1) NOT NULL, " +
				" Priority int NOT NULL, " +
				" CommandType int NOT NULL, " +
				" CommandID nvarchar(max) NOT NULL, " +
				" CommandDetails nvarchar(max) NOT NULL, " +
				" DateTimeUpdated datetime NOT NULL, " +
				" CONSTRAINT [PK_CommandRequest] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" CommandRequestID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			return cmds;
		}

		

		public static List<string> CreateTableString_CrossRef_AniDB_TvDB()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE CrossRef_AniDB_TvDB( " +
				" CrossRef_AniDB_TvDBID int IDENTITY(1,1) NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" TvDBID int NOT NULL, " +
				" TvDBSeasonNumber int NOT NULL, " +
				" CrossRefSource int NOT NULL, " +
				" CONSTRAINT [PK_CrossRef_AniDB_TvDB] PRIMARY KEY CLUSTERED " +
				" ( " +
				" CrossRef_AniDB_TvDBID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB ON CrossRef_AniDB_TvDB(AnimeID, TvDBID, TvDBSeasonNumber, CrossRefSource)");
			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_TvDB_AnimeID ON CrossRef_AniDB_TvDB(AnimeID)");

			return cmds;
		}

		public static List<string> CreateTableString_CrossRef_AniDB_Other()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE CrossRef_AniDB_Other( " +
				" CrossRef_AniDB_OtherID int IDENTITY(1,1) NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" CrossRefID nvarchar(500) NOT NULL, " +
				" CrossRefSource int NOT NULL, " +
				" CrossRefType int NOT NULL, " +
				" CONSTRAINT [PK_CrossRef_AniDB_Other] PRIMARY KEY CLUSTERED " +
				" ( " +
				" CrossRef_AniDB_OtherID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_AniDB_Other ON CrossRef_AniDB_Other(AnimeID, CrossRefID, CrossRefSource, CrossRefType)");

			return cmds;
		}

		public static List<string> CreateTableString_CrossRef_File_Episode()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE CrossRef_File_Episode( " +
				" CrossRef_File_EpisodeID int IDENTITY(1,1) NOT NULL, " +
				" Hash varchar(50) NULL, " +
				" FileName nvarchar(500) NOT NULL, " +
				" FileSize bigint NOT NULL, " +
				" CrossRefSource int NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" EpisodeID int NOT NULL, " +
				" Percentage int NOT NULL, " +
				" EpisodeOrder int NOT NULL, " +
				" CONSTRAINT [PK_CrossRef_File_Episode] PRIMARY KEY CLUSTERED " +
				" ( " +
				" CrossRef_File_EpisodeID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_CrossRef_File_Episode_Hash_EpisodeID ON CrossRef_File_Episode(Hash, EpisodeID)");

			return cmds;
		}

		public static List<string> CreateTableString_CrossRef_Languages_AniDB_File()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE CrossRef_Languages_AniDB_File( " +
				" CrossRef_Languages_AniDB_FileID int IDENTITY(1,1) NOT NULL, " +
				" FileID int NOT NULL, " +
				" LanguageID int NOT NULL, " +
				" CONSTRAINT [PK_CrossRef_Languages_AniDB_File] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" CrossRef_Languages_AniDB_FileID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");;

			return cmds;
		}

		public static List<string> CreateTableString_CrossRef_Subtitles_AniDB_File()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE CrossRef_Subtitles_AniDB_File( " +
				" CrossRef_Subtitles_AniDB_FileID int IDENTITY(1,1) NOT NULL, " +
				" FileID int NOT NULL, " +
				" LanguageID int NOT NULL, " +
				" CONSTRAINT [PK_CrossRef_Subtitles_AniDB_File] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" CrossRef_Subtitles_AniDB_FileID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			return cmds;
		}

		public static List<string> CreateTableString_FileNameHash()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE FileNameHash ( " +
				" FileNameHashID int IDENTITY(1,1) NOT NULL, " +
				" FileName nvarchar(500) NOT NULL, " +
				" FileSize bigint NOT NULL, " +
				" Hash varchar(50) NOT NULL, " +
				" DateTimeUpdated datetime NOT NULL, " +
				" CONSTRAINT [PK_FileNameHash] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" FileNameHashID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_FileNameHash ON FileNameHash(FileName, FileSize, Hash)");

			return cmds;
		}

		public static List<string> CreateTableString_Language()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE Language( " +
				" LanguageID int IDENTITY(1,1) NOT NULL, " +
				" LanguageName varchar(100) NOT NULL, " +
				" CONSTRAINT [PK_Language] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" LanguageID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_Language_LanguageName ON Language(LanguageName)");

			return cmds;
		}

		public static List<string> CreateTableString_ImportFolder()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE ImportFolder( " +
				" ImportFolderID int IDENTITY(1,1) NOT NULL, " +
				" ImportFolderType int NOT NULL, " +
				" ImportFolderName nvarchar(max) NOT NULL, " +
				" ImportFolderLocation nvarchar(max) NOT NULL, " +
				" IsDropSource int NOT NULL, " +
				" IsDropDestination int NOT NULL, " +
				" CONSTRAINT [PK_ImportFolder] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" ImportFolderID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			return cmds;
		}

		public static List<string> CreateTableString_ScheduledUpdate()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE ScheduledUpdate( " +
				" ScheduledUpdateID int IDENTITY(1,1) NOT NULL, " +
				" UpdateType int NOT NULL, " +
				" LastUpdate datetime NOT NULL, " +
				" UpdateDetails nvarchar(max) NOT NULL, " +
				" CONSTRAINT [PK_ScheduledUpdate] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" ScheduledUpdateID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_ScheduledUpdate_UpdateType ON ScheduledUpdate(UpdateType)");

			return cmds;
		}

		public static List<string> CreateTableString_VideoInfo()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE VideoInfo ( " +
				" VideoInfoID int IDENTITY(1,1) NOT NULL, " +
				" Hash varchar(50) NOT NULL, " +
				" FileSize bigint NOT NULL, " +
				" FileName nvarchar(max) NOT NULL, " +
				" DateTimeUpdated datetime NOT NULL, " +
				" VideoCodec varchar(max) NOT NULL, " +
				" VideoBitrate varchar(max) NOT NULL, " +
				" VideoFrameRate varchar(max) NOT NULL, " +
				" VideoResolution varchar(max) NOT NULL, " +
				" AudioCodec varchar(max) NOT NULL, " +
				" AudioBitrate varchar(max) NOT NULL, " +
				" Duration bigint NOT NULL, " +
				" CONSTRAINT [PK_VideoInfo] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" VideoInfoID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_VideoInfo_Hash on VideoInfo(Hash)");

			return cmds;
		}

		

		public static List<string> CreateTableString_DuplicateFile()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE DuplicateFile( " +
				" DuplicateFileID int IDENTITY(1,1) NOT NULL, " +
				" FilePathFile1 nvarchar(max) NOT NULL, " +
				" FilePathFile2 nvarchar(max) NOT NULL, " +
				" ImportFolderIDFile1 int NOT NULL, " +
				" ImportFolderIDFile2 int NOT NULL, " +
				" Hash varchar(50) NOT NULL, " +
				" DateTimeUpdated datetime NOT NULL, " +
				" CONSTRAINT [PK_DuplicateFile] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" DuplicateFileID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			return cmds;
		}

		public static List<string> CreateTableString_GroupFilter()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE GroupFilter( " +
				" GroupFilterID int IDENTITY(1,1) NOT NULL, " +
				" GroupFilterName nvarchar(max) NOT NULL, " +
				" ApplyToSeries int NOT NULL, " +
				" BaseCondition int NOT NULL, " +
				" SortingCriteria nvarchar(max), " +
				" CONSTRAINT [PK_GroupFilter] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" GroupFilterID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			return cmds;
		}

		public static List<string> CreateTableString_GroupFilterCondition()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE GroupFilterCondition( " +
				" GroupFilterConditionID int IDENTITY(1,1) NOT NULL, " +
				" GroupFilterID int NOT NULL, " +
				" ConditionType int NOT NULL, " +
				" ConditionOperator int NOT NULL, " +
				" ConditionParameter nvarchar(max) NOT NULL, " +
				" CONSTRAINT [PK_GroupFilterCondition] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" GroupFilterConditionID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Vote()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Vote ( " +
				" AniDB_VoteID int IDENTITY(1,1) NOT NULL, " +
				" EntityID int NOT NULL, " +
				" VoteValue int NOT NULL, " +
				" VoteType int NOT NULL, " +
				" CONSTRAINT [PK_AniDB_Vote] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" AniDB_VoteID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			return cmds;
		}

		public static List<string> CreateTableString_TvDB_ImageFanart()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE TvDB_ImageFanart( " +
				" TvDB_ImageFanartID int IDENTITY(1,1) NOT NULL, " +
				" Id int NOT NULL, " +
				" SeriesID int NOT NULL, " +
				" BannerPath nvarchar(MAX),  " +
				" BannerType nvarchar(MAX),  " +
				" BannerType2 nvarchar(MAX),  " +
				" Colors nvarchar(MAX),  " +
				" Language nvarchar(MAX),  " +
				" ThumbnailPath nvarchar(MAX),  " +
				" VignettePath nvarchar(MAX),  " +
				" Enabled int NOT NULL, " +
				" Chosen int NOT NULL, " +
				" CONSTRAINT PK_TvDB_ImageFanart PRIMARY KEY CLUSTERED  " +
				" ( " +
				" TvDB_ImageFanartID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_TvDB_ImageFanart_Id ON TvDB_ImageFanart(Id)");

			return cmds;
		}

		public static List<string> CreateTableString_TvDB_ImageWideBanner()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE TvDB_ImageWideBanner( " +
				" TvDB_ImageWideBannerID int IDENTITY(1,1) NOT NULL, " +
				" Id int NOT NULL, " +
				" SeriesID int NOT NULL, " +
				" BannerPath nvarchar(MAX),  " +
				" BannerType nvarchar(MAX),  " +
				" BannerType2 nvarchar(MAX),  " +
				" Language nvarchar(MAX),  " +
				" Enabled int NOT NULL, " +
				" SeasonNumber int, " +
				" CONSTRAINT PK_TvDB_ImageWideBanner PRIMARY KEY CLUSTERED  " +
				" ( " +
				" TvDB_ImageWideBannerID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_TvDB_ImageWideBanner_Id ON TvDB_ImageWideBanner(Id)");

			return cmds;
		}

		public static List<string> CreateTableString_TvDB_ImagePoster()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE TvDB_ImagePoster( " +
				" TvDB_ImagePosterID int IDENTITY(1,1) NOT NULL, " +
				" Id int NOT NULL, " +
				" SeriesID int NOT NULL, " +
				" BannerPath nvarchar(MAX),  " +
				" BannerType nvarchar(MAX),  " +
				" BannerType2 nvarchar(MAX),  " +
				" Language nvarchar(MAX),  " +
				" Enabled int NOT NULL, " +
				" SeasonNumber int, " +
				" CONSTRAINT PK_TvDB_ImagePoster PRIMARY KEY CLUSTERED  " +
				" ( " +
				" TvDB_ImagePosterID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_TvDB_ImagePoster_Id ON TvDB_ImagePoster(Id)");

			return cmds;
		}

		public static List<string> CreateTableString_TvDB_Episode()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE TvDB_Episode( " +
				" TvDB_EpisodeID int IDENTITY(1,1) NOT NULL, " +
				" Id int NOT NULL, " +
				" SeriesID int NOT NULL, " +
				" SeasonID int NOT NULL, " +
				" SeasonNumber int NOT NULL, " +
				" EpisodeNumber int NOT NULL, " +
				" EpisodeName nvarchar(MAX), " +
				" Overview nvarchar(MAX), " +
				" Filename nvarchar(MAX), " +
				" EpImgFlag int NOT NULL, " +
				" FirstAired nvarchar(MAX), " +
				" AbsoluteNumber int, " +
				" AirsAfterSeason int, " +
				" AirsBeforeEpisode int, " +
				" AirsBeforeSeason int, " +
				" CONSTRAINT PK_TvDB_Episode PRIMARY KEY CLUSTERED  " +
				" ( " +
				" TvDB_EpisodeID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_TvDB_Episode_Id ON TvDB_Episode(Id)");

			return cmds;
		}

		public static List<string> CreateTableString_TvDB_Series()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE TvDB_Series( " +
				" TvDB_SeriesID int IDENTITY(1,1) NOT NULL, " +
				" SeriesID int NOT NULL, " +
				" Overview nvarchar(MAX), " +
				" SeriesName nvarchar(MAX), " +
				" Status varchar(100), " +
				" Banner varchar(100), " +
				" Fanart varchar(100), " +
				" Poster varchar(100), " +
				" Lastupdated varchar(100), " +
				" CONSTRAINT PK_TvDB_Series PRIMARY KEY CLUSTERED  " +
				" ( " +
				" TvDB_SeriesID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_TvDB_Series_Id ON TvDB_Series(SeriesID)");

			return cmds;
		}

		public static List<string> CreateTableString_AniDB_Anime_DefaultImage()
		{
			List<string> cmds = new List<string>();
			cmds.Add("CREATE TABLE AniDB_Anime_DefaultImage ( " +
				" AniDB_Anime_DefaultImageID int IDENTITY(1,1) NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" ImageParentID int NOT NULL, " +
				" ImageParentType int NOT NULL, " +
				" ImageType int NOT NULL, " +
				" CONSTRAINT [PK_AniDB_Anime_DefaultImage] PRIMARY KEY CLUSTERED  " +
				" ( " +
				" [AniDB_Anime_DefaultImageID] ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_AniDB_Anime_DefaultImage_ImageType ON AniDB_Anime_DefaultImage(AnimeID, ImageType)");

			return cmds;
		}

		public static List<string> CreateTableString_MovieDB_Movie()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE MovieDB_Movie( " +
				" MovieDB_MovieID int IDENTITY(1,1) NOT NULL, " +
				" MovieId int NOT NULL, " +
				" MovieName nvarchar(MAX), " +
				" OriginalName nvarchar(MAX), " +
				" Overview nvarchar(MAX), " +
				" CONSTRAINT PK_MovieDB_Movie PRIMARY KEY CLUSTERED  " +
				" ( " +
				" MovieDB_MovieID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			cmds.Add("CREATE UNIQUE INDEX UIX_MovieDB_Movie_Id ON MovieDB_Movie(MovieId)");

			return cmds;
		}

		public static List<string> CreateTableString_MovieDB_Poster()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE MovieDB_Poster( " +
				" MovieDB_PosterID int IDENTITY(1,1) NOT NULL, " +
				" ImageID varchar(100), " +
				" MovieId int NOT NULL, " +
				" ImageType varchar(100), " +
				" ImageSize varchar(100),  " +
				" URL nvarchar(MAX),  " +
				" ImageWidth int NOT NULL,  " +
				" ImageHeight int NOT NULL,  " +
				" Enabled int NOT NULL, " +
				" CONSTRAINT PK_MovieDB_Poster PRIMARY KEY CLUSTERED  " +
				" ( " +
				" MovieDB_PosterID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			return cmds;
		}

		public static List<string> CreateTableString_MovieDB_Fanart()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE MovieDB_Fanart( " +
				" MovieDB_FanartID int IDENTITY(1,1) NOT NULL, " +
				" ImageID varchar(100), " +
				" MovieId int NOT NULL, " +
				" ImageType varchar(100), " +
				" ImageSize varchar(100),  " +
				" URL nvarchar(MAX),  " +
				" ImageWidth int NOT NULL,  " +
				" ImageHeight int NOT NULL,  " +
				" Enabled int NOT NULL, " +
				" CONSTRAINT PK_MovieDB_Fanart PRIMARY KEY CLUSTERED  " +
				" ( " +
				" MovieDB_FanartID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			return cmds;
		}

		public static List<string> CreateTableString_JMMUser()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE JMMUser( " +
				" JMMUserID int IDENTITY(1,1) NOT NULL, " +
				" Username nvarchar(100), " +
				" Password nvarchar(100), " +
				" IsAdmin int NOT NULL, " +
				" IsAniDBUser int NOT NULL, " +
				" IsTraktUser int NOT NULL, " +
				" HideCategories nvarchar(MAX), " +
				" CONSTRAINT PK_JMMUser PRIMARY KEY CLUSTERED  " +
				" ( " +
				" JMMUserID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			return cmds;
		}

		public static List<string> CreateTableString_Trakt_Episode()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE Trakt_Episode( " +
				" Trakt_EpisodeID int IDENTITY(1,1) NOT NULL, " +
				" Trakt_ShowID int NOT NULL, " +
				" Season int NOT NULL, " +
				" EpisodeNumber int NOT NULL, " +
				" Title nvarchar(MAX), " +
				" URL nvarchar(500), " +
				" Overview nvarchar(MAX), " +
				" EpisodeImage nvarchar(500), " +
				" CONSTRAINT PK_Trakt_Episode PRIMARY KEY CLUSTERED  " +
				" ( " +
				" Trakt_EpisodeID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			return cmds;
		}

		public static List<string> CreateTableString_Trakt_ImagePoster()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE Trakt_ImagePoster( " +
				" Trakt_ImagePosterID int IDENTITY(1,1) NOT NULL, " +
				" Trakt_ShowID int NOT NULL, " +
				" Season int NOT NULL, " +
				" ImageURL nvarchar(500), " +
				" Enabled int NOT NULL, " +
				" CONSTRAINT PK_Trakt_ImagePoster PRIMARY KEY CLUSTERED  " +
				" ( " +
				" Trakt_ImagePosterID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			return cmds;
		}

		public static List<string> CreateTableString_Trakt_ImageFanart()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE Trakt_ImageFanart( " +
				" Trakt_ImageFanartID int IDENTITY(1,1) NOT NULL, " +
				" Trakt_ShowID int NOT NULL, " +
				" Season int NOT NULL, " +
				" ImageURL nvarchar(500), " +
				" Enabled int NOT NULL, " +
				" CONSTRAINT PK_Trakt_ImageFanart PRIMARY KEY CLUSTERED  " +
				" ( " +
				" Trakt_ImageFanartID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			return cmds;
		}

		public static List<string> CreateTableString_Trakt_Show()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE Trakt_Show( " +
				" Trakt_ShowID int IDENTITY(1,1) NOT NULL, " +
				" TraktID nvarchar(500), " +
				" Title nvarchar(MAX), " +
				" Year nvarchar(500), " +
				" URL nvarchar(500), " +
				" Overview nvarchar(MAX), " +
				" TvDB_ID int NULL, " +
				" CONSTRAINT PK_Trakt_Show PRIMARY KEY CLUSTERED  " +
				" ( " +
				" Trakt_ShowID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			return cmds;
		}

		public static List<string> CreateTableString_Trakt_Season()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE Trakt_Season( " +
				" Trakt_SeasonID int IDENTITY(1,1) NOT NULL, " +
				" Trakt_ShowID int NOT NULL, " +
				" Season int NOT NULL, " +
				" URL nvarchar(500), " +
				" CONSTRAINT PK_Trakt_Season PRIMARY KEY CLUSTERED  " +
				" ( " +
				" Trakt_SeasonID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			return cmds;
		}

		public static List<string> CreateTableString_CrossRef_AniDB_Trakt()
		{
			List<string> cmds = new List<string>();

			cmds.Add("CREATE TABLE CrossRef_AniDB_Trakt( " +
				" CrossRef_AniDB_TraktID int IDENTITY(1,1) NOT NULL, " +
				" AnimeID int NOT NULL, " +
				" TraktID nvarchar(500), " +
				" TraktSeasonNumber int NOT NULL, " +
				" CrossRefSource int NOT NULL, " +
				" CONSTRAINT [PK_CrossRef_AniDB_Trakt] PRIMARY KEY CLUSTERED " +
				" ( " +
				" CrossRef_AniDB_TraktID ASC " +
				" )WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY] " +
				" ) ON [PRIMARY] ");

			return cmds;
		}

		#endregion

		public static string GetDatabasePath(string serverName)
		{
			string dbPath = "";

			// normally installed versions of sql server
			dbPath = GetDatabasePath(serverName, @"SOFTWARE\Microsoft\Microsoft SQL Server");
			if (dbPath.Length > 0) return dbPath;

			// sql server 32bit version installed on 64bit OS
			dbPath = GetDatabasePath(serverName, @"SOFTWARE\Wow6432Node\Microsoft\Microsoft SQL Server");
			return dbPath;
		}

		public static string GetDatabasePath(string serverName, string registryPoint)
		{
			string instName = GetInstanceNameFromServerName(serverName).Trim().ToUpper();



			//
			using (RegistryKey sqlServerKey = Registry.LocalMachine.OpenSubKey(registryPoint))
			{
				foreach (string subKeyName in sqlServerKey.GetSubKeyNames())
				{
					if (subKeyName.StartsWith("MSSQL"))
					{
						using (RegistryKey instanceKey = sqlServerKey.OpenSubKey(subKeyName))
						{
							object val = instanceKey.GetValue("");
							if (val != null)
							{
								string instanceName = val.ToString().Trim().ToUpper();

								if (instanceName == instName)//say
								{
									string path = instanceKey.OpenSubKey(@"Setup").GetValue("SQLDataRoot").ToString();
									path = Path.Combine(path, "Data");
									return path;
								}
							}
						}
					}
				}
			}

			return "";
		}

		public static string GetInstanceNameFromServerName(string servername)
		{
			if (!servername.Contains('\\')) return "MSSQLSERVER"; //default instance

			int pos = servername.IndexOf('\\');
			string instancename = servername.Substring(pos + 1, servername.Length - pos - 1);

			return instancename;

		}

	}

	public class SQLServerDatabase
	{
		public string MdfFileName { get; set; }
		public string MdfFilePath { get; set; }
		public string MdfFileSize { get; set; }
		public string MdfMaxFileSize { get; set; }
		public string MdfFileGrowth { get; set; }
		public string LdfFileName { get; set; }
		public string LdfFilePath { get; set; }
		public string LdfFileSize { get; set; }
		public string LdfMaxFileSize { get; set; }
		public string LdfFileGrowth { get; set; }
		public string DatabaseName { get; set; }
	}
}
