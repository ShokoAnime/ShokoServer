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

namespace JMMServer.Databases
{
	public class DatabaseHelper
	{
		private static Logger logger = LogManager.GetCurrentClassLogger();

		public static ISessionFactory CreateSessionFactory()
		{
			
			if (ServerSettings.DatabaseType.Trim().ToUpper() == "SQLSERVER")
			{
				string connectionstring = string.Format(@"data source={0};initial catalog={1};persist security info=True;user id={2};password={3}",
					ServerSettings.DatabaseServer, ServerSettings.DatabaseName, ServerSettings.DatabaseUsername, ServerSettings.DatabasePassword);

				return Fluently.Configure()
				.Database(MsSqlConfiguration.MsSql2008.ConnectionString(connectionstring))
				.Mappings(m =>
					m.FluentMappings.AddFromAssemblyOf<JMMService>())
				.BuildSessionFactory();
			}
			else if (ServerSettings.DatabaseType.Trim().ToUpper() == "SQLITE")
			{
				return Fluently.Configure()
				.Database(SQLiteConfiguration.Standard
					.UsingFile(SQLite.GetDatabaseFilePath()))
				.Mappings(m =>
					m.FluentMappings.AddFromAssemblyOf<JMMService>())
				.BuildSessionFactory();
			}
			else if (ServerSettings.DatabaseType.Trim().ToUpper() == "MYSQL")
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
					}

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

					ServerState.Instance.CurrentSetupStatus = "Database - Creating Initial Schema...";
					SQLite.CreateInitialSchema();

					ServerState.Instance.CurrentSetupStatus = "Database - Applying Schema Patches...";
					SQLite.UpdateSchema();

					PopulateInitialData();

					return true;
				}
				else if (ServerSettings.DatabaseType.Trim().ToUpper() == "MYSQL")
				{
					ServerState.Instance.CurrentSetupStatus = "Database - Creating Database...";
					MySQL.CreateDatabase();

					ServerState.Instance.CurrentSetupStatus = "Database - Creating Initial Schema...";
					MySQL.CreateInitialSchema();

					ServerState.Instance.CurrentSetupStatus = "Database - Applying Schema Patches...";
					MySQL.UpdateSchema();

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

		public static void PopulateInitialData()
		{
			ServerState.Instance.CurrentSetupStatus = "Database - Populating Data (Users)...";
			CreateInitialUsers();

			ServerState.Instance.CurrentSetupStatus = "Database - Populating Data (Group Filters)...";
			CreateInitialGroupFilters();
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
	}
}
