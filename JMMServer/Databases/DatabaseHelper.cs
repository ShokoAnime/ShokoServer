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
using System.Collections.Specialized;
using System.Configuration;
using System.Globalization;

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
                .Database(MySQLConfiguration.Standard.ConnectionString(x => x.Database(ServerSettings.MySQL_SchemaName + ";CharSet=utf8mb4")
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
				DatabaseFixes.InitFixes();
				if (ServerSettings.DatabaseType.Trim().ToUpper() == "SQLSERVER")
				{
					if (!SQLServer.DatabaseAlreadyExists())
					{
						logger.Error("Database: {0} does not exist", ServerSettings.DatabaseName);
						SQLServer.CreateDatabase();
						Thread.Sleep(3000);
					}

					JMMService.CloseSessionFactory();
					ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Database_Initializing;
					ISessionFactory temp = JMMService.SessionFactory;

					ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Database_CreateSchema;
					SQLServer.CreateInitialSchema();

					ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Database_ApplySchema;
					SQLServer.UpdateSchema();

                    InitCache();

                    PopulateInitialData();

					return true;
				}
				else if (ServerSettings.DatabaseType.Trim().ToUpper() == "SQLITE")
				{
					ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Database_CreateDatabase;
					SQLite.CreateDatabase();

					JMMService.CloseSessionFactory();
					ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Database_Initializing;
                    ISessionFactory temp = JMMService.SessionFactory;

					ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Database_CreateSchema;
                    SQLite.CreateInitialSchema();

					ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Database_ApplySchema;
                    SQLite.UpdateSchema();

                    InitCache();

                    PopulateInitialData();

					return true;
				}
				else if (ServerSettings.DatabaseType.Trim().ToUpper() == "MYSQL")
				{
					logger.Trace("Database - Creating Database...");
					ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Database_CreateDatabase;
                    MySQL.CreateDatabase();

					logger.Trace("Initializing Session Factory...");
					JMMService.CloseSessionFactory();
					ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Database_Initializing;
                    ISessionFactory temp = JMMService.SessionFactory;

					logger.Trace("Database - Creating Initial Schema...");
					ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Database_CreateSchema;
                    MySQL.CreateInitialSchema();

					logger.Trace("Database - Applying Schema Patches...");
					ServerState.Instance.CurrentSetupStatus = JMMServer.Properties.Resources.Database_ApplySchema;
                    MySQL.UpdateSchema();
					//MySQL.UpdateSchema_Fix();

                    InitCache();

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

	    public static void InitCache()
	    {
            AniDB_AnimeRepository.InitCache();
            VideoInfoRepository.InitCache();
            VideoLocalRepository.InitCache();
            VideoLocal_UserRepository.InitCache();
            GroupFilterRepository.InitCache();
            AnimeEpisodeRepository.InitCache();
            AnimeEpisode_UserRepository.InitCache();
            AnimeSeriesRepository.InitCache();
            AnimeSeries_UserRepository.InitCache();
            AnimeGroupRepository.InitCache();
            AnimeGroup_UserRepository.InitCache();
	        GroupFilterRepository.CreateFakeAllFilter();
			DatabaseFixes.ExecuteDatabaseFixes();

		}
        //TO be translated
        public static string InitCacheTitle = "Database Cache - Caching  - {0}{1}...";

		public static void PopulateInitialData()
		{
			ServerState.Instance.CurrentSetupStatus = "Database - Populating Data (Users)...";
			CreateInitialUsers();

			ServerState.Instance.CurrentSetupStatus = "Database - Populating Data (Group Filters)...";
			CreateInitialGroupFilters();
			CreateContinueWatchingGroupFilter();

			ServerState.Instance.CurrentSetupStatus = "Database - Populating Data (Rename Script)...";
			CreateInitialRenameScript();

			ServerState.Instance.CurrentSetupStatus = "Database - Populating Data (Custom Tags)...";
			DatabaseHelper.CreateInitialCustomTags();

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
            gf.Locked = 0;
            gf.FilterType = (int)GroupFilterType.UserDefined;

            repFilters.Save(gf,true,null);

			GroupFilterCondition gfc = new GroupFilterCondition();
			gfc.ConditionType = (int)GroupFilterConditionType.Favourite;
			gfc.ConditionOperator = (int)GroupFilterOperator.Include;
			gfc.ConditionParameter = "";
			gfc.GroupFilterID = gf.GroupFilterID;
			repGFC.Save(gfc);



			// Missing Episodes
			gf = new GroupFilter();
			gf.GroupFilterName = JMMServer.Properties.Resources.Filter_MissingEpisodes;
			gf.ApplyToSeries = 0;
			gf.BaseCondition = 1;
            gf.Locked = 0;
            gf.FilterType = (int)GroupFilterType.UserDefined;

            repFilters.Save(gf,true,null);

			gfc = new GroupFilterCondition();
			gfc.ConditionType = (int)GroupFilterConditionType.MissingEpisodesCollecting;
			gfc.ConditionOperator = (int)GroupFilterOperator.Include;
			gfc.ConditionParameter = "";
			gfc.GroupFilterID = gf.GroupFilterID;
			repGFC.Save(gfc);

			// Newly Added Series
			gf = new GroupFilter();
			gf.GroupFilterName = JMMServer.Properties.Resources.Filter_Added;
			gf.ApplyToSeries = 0;
			gf.BaseCondition = 1;
            gf.Locked = 0;
            gf.FilterType = (int)GroupFilterType.UserDefined;

            repFilters.Save(gf,true,null);

			gfc = new GroupFilterCondition();
			gfc.ConditionType = (int)GroupFilterConditionType.SeriesCreatedDate;
			gfc.ConditionOperator = (int)GroupFilterOperator.LastXDays;
			gfc.ConditionParameter = "10";
			gfc.GroupFilterID = gf.GroupFilterID;
			repGFC.Save(gfc);

			// Newly Airing Series
			gf = new GroupFilter();
			gf.GroupFilterName = JMMServer.Properties.Resources.Filter_Airing;
			gf.ApplyToSeries = 0;
			gf.BaseCondition = 1;
            gf.Locked = 0;
            gf.FilterType = (int)GroupFilterType.UserDefined;

            repFilters.Save(gf,true,null);

			gfc = new GroupFilterCondition();
			gfc.ConditionType = (int)GroupFilterConditionType.AirDate;
			gfc.ConditionOperator = (int)GroupFilterOperator.LastXDays;
			gfc.ConditionParameter = "30";
			gfc.GroupFilterID = gf.GroupFilterID;
			repGFC.Save(gfc);

			// Votes Needed
			gf = new GroupFilter();
			gf.GroupFilterName = JMMServer.Properties.Resources.Filter_Votes;
			gf.ApplyToSeries = 0;
			gf.BaseCondition = 1;
            gf.Locked = 0;
            gf.FilterType = (int)GroupFilterType.UserDefined;

            repFilters.Save(gf,true,null);

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
			gf.GroupFilterName = JMMServer.Properties.Resources.Filter_RecentlyWatched;
			gf.ApplyToSeries = 0;
			gf.BaseCondition = 1;
            gf.Locked = 0;
            gf.FilterType = (int)GroupFilterType.UserDefined;

            repFilters.Save(gf,true,null);

			gfc = new GroupFilterCondition();
			gfc.ConditionType = (int)GroupFilterConditionType.EpisodeWatchedDate;
			gfc.ConditionOperator = (int)GroupFilterOperator.LastXDays;
			gfc.ConditionParameter = "10";
			gfc.GroupFilterID = gf.GroupFilterID;
			repGFC.Save(gfc);

			// TvDB/MovieDB Link Missing
			gf = new GroupFilter();
			gf.GroupFilterName = JMMServer.Properties.Resources.Filter_LinkMissing;
			gf.ApplyToSeries = 0;
			gf.BaseCondition = 1;
            gf.Locked = 0;
            gf.FilterType = (int)GroupFilterType.UserDefined;

            repFilters.Save(gf,true,null);

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
			repUsers.Save(defaultUser,true);

			JMMUser familyUser = new JMMUser();
			familyUser.CanEditServerSettings = 1;
			familyUser.HideCategories = "Ecchi,Nudity,Sex,Sexual Abuse,Horror,Erotic Game,Incest,18 Restricted";
			familyUser.IsAdmin = 1;
			familyUser.IsAniDBUser = 1;
			familyUser.IsTraktUser = 1;
			familyUser.Password = "";
			familyUser.Username = "Family Friendly";
			repUsers.Save(familyUser,true);
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

		private static void CreateContinueWatchingGroupFilter()
		{
			// group filters
			GroupFilterRepository repFilters = new GroupFilterRepository();
			GroupFilterConditionRepository repGFC = new GroupFilterConditionRepository();

			using (var session = JMMService.SessionFactory.OpenSession())
			{
				// check if it already exists
				List<GroupFilter> lockedGFs = repFilters.GetLockedGroupFilters(session);

				if (lockedGFs != null)
				{
                    // if it already exists we can leave
                    foreach (GroupFilter gfTemp in lockedGFs)
                    {
                        if (gfTemp.FilterType == (int)GroupFilterType.ContinueWatching)
                            return;
                    }

                    // the default value when the column was added to the database was '1'
                    // this is only needed for users of a migrated database
                    foreach (GroupFilter gfTemp in lockedGFs)
                    {
                        if (gfTemp.GroupFilterName.Equals(Constants.GroupFilterName.ContinueWatching, StringComparison.InvariantCultureIgnoreCase) &&
                            gfTemp.FilterType != (int)GroupFilterType.ContinueWatching)
                        {
	                        DatabaseFixes.FixContinueWatchingGroupFilter_20160406();
                            return;
                        }
                    }
				}

				GroupFilter gf = new GroupFilter();
				gf.GroupFilterName = Constants.GroupFilterName.ContinueWatching;
				gf.Locked = 1;
				gf.SortingCriteria = "4;2"; // by last watched episode desc
				gf.ApplyToSeries = 0;
				gf.BaseCondition = 1; // all
                gf.FilterType = (int)GroupFilterType.ContinueWatching;

                repFilters.Save(gf,true,null);

				GroupFilterCondition gfc = new GroupFilterCondition();
				gfc.ConditionType = (int)GroupFilterConditionType.HasWatchedEpisodes;
				gfc.ConditionOperator = (int)GroupFilterOperator.Include;
				gfc.ConditionParameter = "";
				gfc.GroupFilterID = gf.GroupFilterID;
				repGFC.Save(gfc);

				gfc = new GroupFilterCondition();
				gfc.ConditionType = (int)GroupFilterConditionType.HasUnwatchedEpisodes;
				gfc.ConditionOperator = (int)GroupFilterOperator.Include;
				gfc.ConditionParameter = "";
				gfc.GroupFilterID = gf.GroupFilterID;
				repGFC.Save(gfc);
			}
		}

		public static void CreateInitialCustomTags()
        {
            try
            {
                // group filters
                CustomTagRepository repTags = new CustomTagRepository();

                if (repTags.GetAll().Count() > 0) return;

                // Dropped
                CustomTag tag = new CustomTag();
                tag.TagName = "Dropped";
                tag.TagDescription = "Started watching this series, but have since dropped it";
                repTags.Save(tag);

                // Pinned
                tag = new CustomTag();
                tag.TagName = "Pinned";
                tag.TagDescription = "Pinned this series for whatever reason you like";
                repTags.Save(tag);

                // Ongoing
                tag = new CustomTag();
                tag.TagName = "Ongoing";
                tag.TagDescription = "This series does not have an end date";
                repTags.Save(tag);

                // Waiting for Series Completion
                tag = new CustomTag();
                tag.TagName = "Waiting for Series Completion";
                tag.TagDescription = "Will start watching this once this series is finished";
                repTags.Save(tag);

                // Waiting for Bluray Completion
                tag = new CustomTag();
                tag.TagName = "Waiting for Bluray Completion";
                tag.TagDescription = "Will start watching this once I have all episodes in bluray";
                repTags.Save(tag);
            }
            catch (Exception ex)
            {
                logger.ErrorException("Could not Create Initial Custom Tags: " + ex.ToString(), ex);
            }

        }
	}
}
