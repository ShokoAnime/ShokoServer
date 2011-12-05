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
						return false;
					}

					SQLServer.CreateInitialSchema();
					SQLServer.UpdateSchema();
					PopulateInitialData();

					return true;
				}
				else
				{
					SQLite.CreateDatabase();
					SQLite.CreateInitialSchema();
					SQLite.UpdateSchema();
					PopulateInitialData();

					return true;
				}

				
			}
			catch (Exception ex)
			{
				logger.ErrorException("Could not init database: " + ex.ToString(), ex);
				return false;
			}
		}

		public static void PopulateInitialData()
		{
			CreateInitialUsers();
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
	}
}
