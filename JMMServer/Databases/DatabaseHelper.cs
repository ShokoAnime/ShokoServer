using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SQLite;
using NLog;
using NHibernate;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using JMMServer.Repositories;
using JMMServer.Entities;
using System.Threading;
using System.Collections;

namespace JMMServer.Databases
{
	public class DatabaseHelper
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public static ISessionFactory CreateSessionFactory()
		{

			if (ServerSettings.DatabaseType.Trim().Equals(Constants.DatabaseType.SqlServer, StringComparison.InvariantCultureIgnoreCase))
			{
				string connectionstring = string.Format(@"data source={0};initial catalog={1};persist security info=True;user id={2};password={3}",
					ServerSettings.DatabaseServer, ServerSettings.DatabaseName, ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword);

				//logger.Info("Conn string = {0}", connectionstring);

				return Fluently.Configure()
				.Database(MsSqlConfiguration.MsSql2008.ConnectionString(connectionstring))
				.Mappings(m =>
					m.FluentMappings.AddFromAssemblyOf<JMMService>())
				.BuildSessionFactory();
			}
			else if (ServerSettings.DatabaseType.Trim().Equals(Constants.DatabaseType.Sqlite, StringComparison.InvariantCultureIgnoreCase))
			{
				return Fluently.Configure()
				.Database(SQLiteConfiguration.Standard
					.UsingFile(SQLite.GetDatabaseFilePath()))
				.Mappings(m =>
					m.FluentMappings.AddFromAssemblyOf<JMMService>())
				.BuildSessionFactory();
			}
			else if (ServerSettings.DatabaseType.Trim().Equals(Constants.DatabaseType.MySQL, StringComparison.InvariantCultureIgnoreCase))
			{
				return Fluently.Configure()
				.Database(MySQLConfiguration.Standard.ConnectionString(x => x.Database(ServerSettings.MySQL_SchemaName)
					.Server(ServerSettings.MySQL_Hostname)
					.Username(ServerSettings.MySQL_Username)
					.Password(ServerSettings.MySQL_Password)))
				.Mappings(m =>
					m.FluentMappings.AddFromAssemblyOf<JMMService>())
				.BuildSessionFactory();
			}
			else
				return null;
		}

		public static bool InitDB()
		{
			try
			{
				if (ServerSettings.DatabaseType.Trim().ToUpper() == "SQLSERVER")
				{
					if (!SQLServer.DatabaseAlreadyExists())
					{
						logger.Error("Database: {0} does not exist", ServerSettings.DatabaseName);
						SQLServer.CreateDatabase();
						Thread.Sleep(3000);
					}

					JMMService.CloseSessionFactory();
					ServerState.Instance.CurrentSetupStatus = "Initializing Session Factory...";
					ISessionFactory temp = JMMService.SessionFactory;

					ServerState.Instance.CurrentSetupStatus = "Database - Creating Initial Schema...";
					SQLServer.CreateInitialSchema();

					ServerState.Instance.CurrentSetupStatus = "Database - Applying Schema Patches...";
					SQLServer.UpdateSchema();

					PopulateInitialData();

					return true;
				}
				else if (ServerSettings.DatabaseType.Trim().ToUpper() == "SQLITE")
				{
					ServerState.Instance.CurrentSetupStatus = "Database - Creating Database...";
					SQLite.CreateDatabase();

					JMMService.CloseSessionFactory();
					ServerState.Instance.CurrentSetupStatus = "Initializing Session Factory...";
					ISessionFactory temp = JMMService.SessionFactory;

					ServerState.Instance.CurrentSetupStatus = "Database - Creating Initial Schema...";
					SQLite.CreateInitialSchema();

					ServerState.Instance.CurrentSetupStatus = "Database - Applying Schema Patches...";
					SQLite.UpdateSchema();

					PopulateInitialData();

					return true;
				}
				else if (ServerSettings.DatabaseType.Trim().ToUpper() == "MYSQL")
				{
					logger.Trace("Database - Creating Database...");
					ServerState.Instance.CurrentSetupStatus = "Database - Creating Database...";
					MySQL.CreateDatabase();

					logger.Trace("Initializing Session Factory...");
					JMMService.CloseSessionFactory();
					ServerState.Instance.CurrentSetupStatus = "Initializing Session Factory...";
					ISessionFactory temp = JMMService.SessionFactory;

					logger.Trace("Database - Creating Initial Schema...");
					ServerState.Instance.CurrentSetupStatus = "Database - Creating Initial Schema...";
					MySQL.CreateInitialSchema();

					logger.Trace("Database - Applying Schema Patches...");
					ServerState.Instance.CurrentSetupStatus = "Database - Applying Schema Patches...";
					MySQL.UpdateSchema();
					//MySQL.UpdateSchema_Fix();

					PopulateInitialData();

					return true;
				}

				return false;
				
			}
			catch (Exception ex)
			{
				logger.ErrorException("Could not init database: " + ex.ToString(), ex);
				return false;
			}
		}

		public static ArrayList GetData(string sql)
		{
			try
			{
				if (ServerSettings.DatabaseType.Trim().Equals(Constants.DatabaseType.SqlServer, StringComparison.InvariantCultureIgnoreCase))
					return SQLServer.GetData(sql);
				else if (ServerSettings.DatabaseType.Trim().Equals(Constants.DatabaseType.Sqlite, StringComparison.InvariantCultureIgnoreCase))
					return SQLite.GetData(sql);
				else if (ServerSettings.DatabaseType.Trim().Equals(Constants.DatabaseType.MySQL, StringComparison.InvariantCultureIgnoreCase))
					return MySQL.GetData(sql);

				return new ArrayList();

			}
			catch (Exception ex)
			{
				logger.ErrorException("Could not init database: " + ex.ToString(), ex);
				return new ArrayList();
			}
		}

		public static void PopulateInitialData()
		{
			ServerState.Instance.CurrentSetupStatus = "Database - Populating Data (Users)...";
			CreateInitialUsers();

			ServerState.Instance.CurrentSetupStatus = "Database - Populating Data (Group Filters)...";
			CreateInitialGroupFilters();

			ServerState.Instance.CurrentSetupStatus = "Database - Populating Data (Rename Script)...";
			CreateInitialRenameScript();
		}

		private static void CreateInitialGroupFilters()
		{
			// group filters
			GroupFilterRepository repFilters = new GroupFilterRepository();
			GroupFilterConditionRepository repGFC = new GroupFilterConditionRepository();

			if (repFilters.GetAll().Count() > 0) return;

			// Favorites
			GroupFilter gf = new GroupFilter();
			gf.GroupFilterName = "Favorites";
			gf.ApplyToSeries = 0;
			gf.BaseCondition = 1;

			repFilters.Save(gf);

			GroupFilterCondition gfc = new GroupFilterCondition();
			gfc.ConditionType = (int)GroupFilterConditionType.Favourite;
			gfc.ConditionOperator = (int)GroupFilterOperator.Include;
			gfc.ConditionParameter = "";
			gfc.GroupFilterID = gf.GroupFilterID;
			repGFC.Save(gfc);



			// Missing Episodes
			gf = new GroupFilter();
			gf.GroupFilterName = "Missing Episodes";
			gf.ApplyToSeries = 0;
			gf.BaseCondition = 1;

			repFilters.Save(gf);

			gfc = new GroupFilterCondition();
			gfc.ConditionType = (int)GroupFilterConditionType.MissingEpisodesCollecting;
			gfc.ConditionOperator = (int)GroupFilterOperator.Include;
			gfc.ConditionParameter = "";
			gfc.GroupFilterID = gf.GroupFilterID;
			repGFC.Save(gfc);

			// Newly Added Series
			gf = new GroupFilter();
			gf.GroupFilterName = "Newly Added Series";
			gf.ApplyToSeries = 0;
			gf.BaseCondition = 1;

			repFilters.Save(gf);

			gfc = new GroupFilterCondition();
			gfc.ConditionType = (int)GroupFilterConditionType.SeriesCreatedDate;
			gfc.ConditionOperator = (int)GroupFilterOperator.LastXDays;
			gfc.ConditionParameter = "10";
			gfc.GroupFilterID = gf.GroupFilterID;
			repGFC.Save(gfc);

			// Newly Airing Series
			gf = new GroupFilter();
			gf.GroupFilterName = "Newly Airing Series";
			gf.ApplyToSeries = 0;
			gf.BaseCondition = 1;

			repFilters.Save(gf);

			gfc = new GroupFilterCondition();
			gfc.ConditionType = (int)GroupFilterConditionType.AirDate;
			gfc.ConditionOperator = (int)GroupFilterOperator.LastXDays;
			gfc.ConditionParameter = "30";
			gfc.GroupFilterID = gf.GroupFilterID;
			repGFC.Save(gfc);

			// Votes Needed
			gf = new GroupFilter();
			gf.GroupFilterName = "Votes Needed";
			gf.ApplyToSeries = 0;
			gf.BaseCondition = 1;

			repFilters.Save(gf);

			gfc = new GroupFilterCondition();
			gfc.ConditionType = (int)GroupFilterConditionType.CompletedSeries;
			gfc.ConditionOperator = (int)GroupFilterOperator.Include;
			gfc.ConditionParameter = "";
			gfc.GroupFilterID = gf.GroupFilterID;
			repGFC.Save(gfc);

			gfc = new GroupFilterCondition();
			gfc.ConditionType = (int)GroupFilterConditionType.HasUnwatchedEpisodes;
			gfc.ConditionOperator = (int)GroupFilterOperator.Exclude;
			gfc.ConditionParameter = "";
			gfc.GroupFilterID = gf.GroupFilterID;
			repGFC.Save(gfc);

			gfc = new GroupFilterCondition();
			gfc.ConditionType = (int)GroupFilterConditionType.UserVotedAny;
			gfc.ConditionOperator = (int)GroupFilterOperator.Exclude;
			gfc.ConditionParameter = "";
			gfc.GroupFilterID = gf.GroupFilterID;
			repGFC.Save(gfc);

			// Recently Watched
			gf = new GroupFilter();
			gf.GroupFilterName = "Recently Watched";
			gf.ApplyToSeries = 0;
			gf.BaseCondition = 1;

			repFilters.Save(gf);

			gfc = new GroupFilterCondition();
			gfc.ConditionType = (int)GroupFilterConditionType.EpisodeWatchedDate;
			gfc.ConditionOperator = (int)GroupFilterOperator.LastXDays;
			gfc.ConditionParameter = "10";
			gfc.GroupFilterID = gf.GroupFilterID;
			repGFC.Save(gfc);

			// TvDB/MovieDB Link Missing
			gf = new GroupFilter();
			gf.GroupFilterName = "TvDB/MovieDB Link Missing";
			gf.ApplyToSeries = 0;
			gf.BaseCondition = 1;

			repFilters.Save(gf);

			gfc = new GroupFilterCondition();
			gfc.ConditionType = (int)GroupFilterConditionType.AssignedTvDBOrMovieDBInfo;
			gfc.ConditionOperator = (int)GroupFilterOperator.Exclude;
			gfc.ConditionParameter = "";
			gfc.GroupFilterID = gf.GroupFilterID;
			repGFC.Save(gfc);
		}

		private static void CreateInitialUsers()
		{
			JMMUserRepository repUsers = new JMMUserRepository();

			if (repUsers.GetAll().Count() > 0) return;

			JMMUser defaultUser = new JMMUser();
			defaultUser.CanEditServerSettings = 1;
			defaultUser.HideCategories = "";
			defaultUser.IsAdmin = 1;
			defaultUser.IsAniDBUser = 1;
			defaultUser.IsTraktUser = 1;
			defaultUser.Password = "";
			defaultUser.Username = "Default";
			repUsers.Save(defaultUser);

			JMMUser familyUser = new JMMUser();
			familyUser.CanEditServerSettings = 1;
			familyUser.HideCategories = "Ecchi,Nudity,Sex,Sexual Abuse,Horror,Erotic Game,Incest,18 Restricted";
			familyUser.IsAdmin = 1;
			familyUser.IsAniDBUser = 1;
			familyUser.IsTraktUser = 1;
			familyUser.Password = "";
			familyUser.Username = "Family Friendly";
			repUsers.Save(familyUser);
		}

		private static void CreateInitialRenameScript()
		{
			RenameScriptRepository repScripts = new RenameScriptRepository();

			if (repScripts.GetAll().Count() > 0) return;

			RenameScript initialScript = new RenameScript();

			initialScript.ScriptName = "Default";
			initialScript.IsEnabledOnImport = 0;
			initialScript.Script = "// Sample Output: [Coalgirls]_Highschool_of_the_Dead_-_01_(1920x1080_Blu-ray_H264)_[90CC6DC1].mkv" + Environment.NewLine +
				"// Sub group name" + Environment.NewLine +
				"DO ADD '[%grp] '" + Environment.NewLine +
				"// Anime Name, use english name if it exists, otherwise use the Romaji name" + Environment.NewLine +
				"IF I(eng) DO ADD '%eng '" + Environment.NewLine +
				"IF I(ann);I(!eng) DO ADD '%ann '" + Environment.NewLine +
				"// Episode Number, don't use episode number for movies" + Environment.NewLine +
				"IF T(!Movie) DO ADD '- %enr'" + Environment.NewLine +
				"// If the file version is v2 or higher add it here" + Environment.NewLine +
				"IF F(!1) DO ADD 'v%ver'" + Environment.NewLine +
				"// Video Resolution" + Environment.NewLine +
				"DO ADD ' (%res'" + Environment.NewLine +
				"// Video Source (only if blu-ray or DVD)" + Environment.NewLine +
				"IF R(DVD),R(Blu-ray) DO ADD ' %src'" + Environment.NewLine +
				"// Video Codec" + Environment.NewLine +
				"DO ADD ' %vid'" + Environment.NewLine +
				"// Video Bit Depth (only if 10bit)" + Environment.NewLine +
				"IF Z(10) DO ADD ' %bitbit'" + Environment.NewLine +
				"DO ADD ') '" + Environment.NewLine +
				"DO ADD '[%CRC]'" + Environment.NewLine +
				"" + Environment.NewLine +
				"// Replacement rules (cleanup)" + Environment.NewLine +
				"DO REPLACE ' ' '_' // replace spaces with underscores" + Environment.NewLine +
				"DO REPLACE 'H264/AVC' 'H264'" + Environment.NewLine +
				"DO REPLACE '0x0' ''" + Environment.NewLine +
				"DO REPLACE '__' '_'" + Environment.NewLine +
				"DO REPLACE '__' '_'" + Environment.NewLine +
				"" + Environment.NewLine +
				"// Replace all illegal file name characters" + Environment.NewLine +
				"DO REPLACE '<' '('" + Environment.NewLine +
				"DO REPLACE '>' ')'" + Environment.NewLine +
				"DO REPLACE ':' '-'" + Environment.NewLine +
				"DO REPLACE '" + ((Char)34).ToString() + "' '`'" + Environment.NewLine +
				"DO REPLACE '/' '_'" + Environment.NewLine +
				"DO REPLACE '/' '_'" + Environment.NewLine +
				"DO REPLACE '\\' '_'" + Environment.NewLine +
				"DO REPLACE '|' '_'" + Environment.NewLine +
				"DO REPLACE '?' '_'" + Environment.NewLine +
				"DO REPLACE '*' '_'" + Environment.NewLine;

			repScripts.Save(initialScript);


		}

		public static void FixDuplicateTvDBLinks()
		{
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();

			// delete all TvDB link duplicates
			CrossRef_AniDB_TvDBRepository repCrossRefTvDB = new CrossRef_AniDB_TvDBRepository();

			List<CrossRef_AniDB_TvDB> xrefsTvDBProcessed = new List<CrossRef_AniDB_TvDB>();
			List<CrossRef_AniDB_TvDB> xrefsTvDBToBeDeleted = new List<CrossRef_AniDB_TvDB>();

			List<CrossRef_AniDB_TvDB> xrefsTvDB = repCrossRefTvDB.GetAll();
			foreach (CrossRef_AniDB_TvDB xrefTvDB in xrefsTvDB)
			{
				bool deleteXref = false;
				foreach (CrossRef_AniDB_TvDB xref in xrefsTvDBProcessed)
				{
					if (xref.TvDBID == xrefTvDB.TvDBID && xref.TvDBSeasonNumber == xrefTvDB.TvDBSeasonNumber)
					{
						xrefsTvDBToBeDeleted.Add(xrefTvDB);
						deleteXref = true;
					}
				}
				if (!deleteXref)
					xrefsTvDBProcessed.Add(xrefTvDB);
			}


			foreach (CrossRef_AniDB_TvDB xref in xrefsTvDBToBeDeleted)
			{
				string msg = "";
				AniDB_Anime anime = repAnime.GetByAnimeID(xref.AnimeID);
				if (anime != null) msg = anime.MainTitle;

				logger.Warn("Deleting TvDB Link because of a duplicate: {0} ({1}) - {2}/{3}", xref.AnimeID, msg, xref.TvDBID, xref.TvDBSeasonNumber);
				repCrossRefTvDB.Delete(xref.CrossRef_AniDB_TvDBID);
			}
		}

		public static void FixDuplicateTraktLinks()
		{
			AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();

			// delete all Trakt link duplicates
			CrossRef_AniDB_TraktRepository repCrossRefTrakt = new CrossRef_AniDB_TraktRepository();

			List<CrossRef_AniDB_Trakt> xrefsTraktProcessed = new List<CrossRef_AniDB_Trakt>();
			List<CrossRef_AniDB_Trakt> xrefsTraktToBeDeleted = new List<CrossRef_AniDB_Trakt>();

			List<CrossRef_AniDB_Trakt> xrefsTrakt = repCrossRefTrakt.GetAll();
			foreach (CrossRef_AniDB_Trakt xrefTrakt in xrefsTrakt)
			{
				bool deleteXref = false;
				foreach (CrossRef_AniDB_Trakt xref in xrefsTraktProcessed)
				{
					if (xref.TraktID == xrefTrakt.TraktID && xref.TraktSeasonNumber == xrefTrakt.TraktSeasonNumber)
					{
						xrefsTraktToBeDeleted.Add(xrefTrakt);
						deleteXref = true;
					}
				}
				if (!deleteXref)
					xrefsTraktProcessed.Add(xrefTrakt);
			}


			foreach (CrossRef_AniDB_Trakt xref in xrefsTraktToBeDeleted)
			{
				string msg = "";
				AniDB_Anime anime = repAnime.GetByAnimeID(xref.AnimeID);
				if (anime != null) msg = anime.MainTitle;

				logger.Warn("Deleting Trakt Link because of a duplicate: {0} ({1}) - {2}/{3}", xref.AnimeID, msg, xref.TraktID, xref.TraktSeasonNumber);
				repCrossRefTrakt.Delete(xref.CrossRef_AniDB_TraktID);
			}
		}

		public static void MigrateTvDBLinks_V1_to_V2()
		{
			try
			{
				AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
				TvDB_EpisodeRepository repEps = new TvDB_EpisodeRepository();

				CrossRef_AniDB_TvDBRepository repCrossRefTvDB = new CrossRef_AniDB_TvDBRepository();
				CrossRef_AniDB_TvDBV2Repository repCrossRefTvDBNew = new CrossRef_AniDB_TvDBV2Repository();

				using (var session = JMMService.SessionFactory.OpenSession())
				{
					List<CrossRef_AniDB_TvDB> xrefsTvDB = repCrossRefTvDB.GetAll();
					foreach (CrossRef_AniDB_TvDB xrefTvDB in xrefsTvDB)
					{
						CrossRef_AniDB_TvDBV2 xrefNew = new CrossRef_AniDB_TvDBV2();
						xrefNew.AnimeID = xrefTvDB.AnimeID;
						xrefNew.CrossRefSource = xrefTvDB.CrossRefSource;
						xrefNew.TvDBID = xrefTvDB.TvDBID;
						xrefNew.TvDBSeasonNumber = xrefTvDB.TvDBSeasonNumber;

						TvDB_Series ser = xrefTvDB.GetTvDBSeries(session);
						if (ser != null)
							xrefNew.TvDBTitle = ser.SeriesName;

						// determine start ep type
						if (xrefTvDB.TvDBSeasonNumber == 0)
							xrefNew.AniDBStartEpisodeType = (int)AniDBAPI.enEpisodeType.Special;
						else
							xrefNew.AniDBStartEpisodeType = (int)AniDBAPI.enEpisodeType.Episode;

						xrefNew.AniDBStartEpisodeNumber = 1;
						xrefNew.TvDBStartEpisodeNumber = 1;

						repCrossRefTvDBNew.Save(xrefNew);
					}

					// create cross ref's for specials
					foreach (CrossRef_AniDB_TvDB xrefTvDB in xrefsTvDB)
					{
						AniDB_Anime anime = repAnime.GetByAnimeID(xrefTvDB.AnimeID);
						if (anime == null) continue;

						// this anime has specials
						if (anime.EpisodeCountSpecial <= 0) continue;

						// this tvdb series has a season 0 (specials)
						List<int> seasons = repEps.GetSeasonNumbersForSeries(xrefTvDB.TvDBID);
						if (!seasons.Contains(0)) continue;

						//make sure we are not doubling up
						CrossRef_AniDB_TvDBV2 temp = repCrossRefTvDBNew.GetByTvDBID(xrefTvDB.TvDBID, 0, 1, xrefTvDB.AnimeID, (int)AniDBAPI.enEpisodeType.Special, 1);
						if (temp != null) continue;

						CrossRef_AniDB_TvDBV2 xrefNew = new CrossRef_AniDB_TvDBV2();
						xrefNew.AnimeID = xrefTvDB.AnimeID;
						xrefNew.CrossRefSource = xrefTvDB.CrossRefSource;
						xrefNew.TvDBID = xrefTvDB.TvDBID;
						xrefNew.TvDBSeasonNumber = 0;
						xrefNew.TvDBStartEpisodeNumber = 1;
						xrefNew.AniDBStartEpisodeType = (int)AniDBAPI.enEpisodeType.Special;
						xrefNew.AniDBStartEpisodeNumber = 1;

						TvDB_Series ser = xrefTvDB.GetTvDBSeries(session);
						if (ser != null)
							xrefNew.TvDBTitle = ser.SeriesName;

						repCrossRefTvDBNew.Save(xrefNew);
					}
				}

			}
			catch (Exception ex)
			{
				logger.ErrorException("Could not MigrateTvDBLinks_V1_to_V2: " + ex.ToString(), ex);
			}
		}
	}
}
