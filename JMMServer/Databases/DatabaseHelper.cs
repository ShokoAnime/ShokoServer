using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AniDBAPI;
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Repositories;
using NHibernate;
using NLog;

namespace JMMServer.Databases
{
    public class DatabaseHelper
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public static ISessionFactory CreateSessionFactory()
        {
            if (ServerSettings.DatabaseType.Trim()
                .Equals(Constants.DatabaseType.SqlServer, StringComparison.InvariantCultureIgnoreCase))
            {
                var connectionstring =
                    string.Format(
                        @"data source={0};initial catalog={1};persist security info=True;user id={2};password={3}",
                        ServerSettings.DatabaseServer, ServerSettings.DatabaseName, ServerSettings.DatabaseUsername,
                        ServerSettings.DatabasePassword);

                //logger.Info("Conn string = {0}", connectionstring);

                return Fluently.Configure()
                    .Database(MsSqlConfiguration.MsSql2008.ConnectionString(connectionstring))
                    .Mappings(m =>
                        m.FluentMappings.AddFromAssemblyOf<JMMService>())
                    .BuildSessionFactory();
            }
            if (ServerSettings.DatabaseType.Trim()
                .Equals(Constants.DatabaseType.Sqlite, StringComparison.InvariantCultureIgnoreCase))
            {
                return Fluently.Configure()
                    .Database(SQLiteConfiguration.Standard
                        .UsingFile(SQLite.GetDatabaseFilePath()))
                    .Mappings(m =>
                        m.FluentMappings.AddFromAssemblyOf<JMMService>())
                    .BuildSessionFactory();
            }
            if (ServerSettings.DatabaseType.Trim()
                .Equals(Constants.DatabaseType.MySQL, StringComparison.InvariantCultureIgnoreCase))
            {
                return Fluently.Configure()
                    .Database(
                        MySQLConfiguration.Standard.ConnectionString(
                            x => x.Database(ServerSettings.MySQL_SchemaName + ";CharSet=utf8mb4")
                                .Server(ServerSettings.MySQL_Hostname)
                                .Username(ServerSettings.MySQL_Username)
                                .Password(ServerSettings.MySQL_Password)))
                    .Mappings(m =>
                        m.FluentMappings.AddFromAssemblyOf<JMMService>())
                    .BuildSessionFactory();
            }
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
                    ServerState.Instance.CurrentSetupStatus = Resources.Database_Initializing;
                    var temp = JMMService.SessionFactory;

                    ServerState.Instance.CurrentSetupStatus = Resources.Database_CreateSchema;
                    SQLServer.CreateInitialSchema();

                    ServerState.Instance.CurrentSetupStatus = Resources.Database_ApplySchema;
                    SQLServer.UpdateSchema();

                    PopulateInitialData();
                    CreateInitialCustomTags();

                    return true;
                }
                if (ServerSettings.DatabaseType.Trim().ToUpper() == "SQLITE")
                {
                    ServerState.Instance.CurrentSetupStatus = Resources.Database_CreateDatabase;
                    SQLite.CreateDatabase();

                    JMMService.CloseSessionFactory();
                    ServerState.Instance.CurrentSetupStatus = Resources.Database_Initializing;
                    var temp = JMMService.SessionFactory;

                    ServerState.Instance.CurrentSetupStatus = Resources.Database_CreateSchema;
                    SQLite.CreateInitialSchema();

                    ServerState.Instance.CurrentSetupStatus = Resources.Database_ApplySchema;
                    SQLite.UpdateSchema();

                    PopulateInitialData();
                    CreateInitialCustomTags();

                    return true;
                }
                if (ServerSettings.DatabaseType.Trim().ToUpper() == "MYSQL")
                {
                    logger.Trace("Database - Creating Database...");
                    ServerState.Instance.CurrentSetupStatus = Resources.Database_CreateDatabase;
                    MySQL.CreateDatabase();

                    logger.Trace("Initializing Session Factory...");
                    JMMService.CloseSessionFactory();
                    ServerState.Instance.CurrentSetupStatus = Resources.Database_Initializing;
                    var temp = JMMService.SessionFactory;

                    logger.Trace("Database - Creating Initial Schema...");
                    ServerState.Instance.CurrentSetupStatus = Resources.Database_CreateSchema;
                    MySQL.CreateInitialSchema();

                    logger.Trace("Database - Applying Schema Patches...");
                    ServerState.Instance.CurrentSetupStatus = Resources.Database_ApplySchema;
                    MySQL.UpdateSchema();
                    //MySQL.UpdateSchema_Fix();

                    PopulateInitialData();
                    CreateInitialCustomTags();

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                logger.ErrorException("Could not init database: " + ex, ex);
                return false;
            }
        }

        public static ArrayList GetData(string sql)
        {
            try
            {
                if (ServerSettings.DatabaseType.Trim()
                    .Equals(Constants.DatabaseType.SqlServer, StringComparison.InvariantCultureIgnoreCase))
                    return SQLServer.GetData(sql);
                if (ServerSettings.DatabaseType.Trim()
                    .Equals(Constants.DatabaseType.Sqlite, StringComparison.InvariantCultureIgnoreCase))
                    return SQLite.GetData(sql);
                if (ServerSettings.DatabaseType.Trim()
                    .Equals(Constants.DatabaseType.MySQL, StringComparison.InvariantCultureIgnoreCase))
                    return MySQL.GetData(sql);

                return new ArrayList();
            }
            catch (Exception ex)
            {
                logger.ErrorException("Could not init database: " + ex, ex);
                return new ArrayList();
            }
        }

        public static void PopulateInitialData()
        {
            ServerState.Instance.CurrentSetupStatus = "Database - Populating Data (Users)...";
            CreateInitialUsers();

            ServerState.Instance.CurrentSetupStatus = "Database - Populating Data (Group Filters)...";
            CreateInitialGroupFilters();
            CreateContinueWatchingGroupFilter();

            ServerState.Instance.CurrentSetupStatus = "Database - Populating Data (Rename Script)...";
            CreateInitialRenameScript();
        }

        private static void CreateInitialGroupFilters()
        {
            // group filters
            var repFilters = new GroupFilterRepository();
            var repGFC = new GroupFilterConditionRepository();

            if (repFilters.GetAll().Count() > 0) return;

            // Favorites
            var gf = new GroupFilter();
            gf.GroupFilterName = "Favorites";
            gf.ApplyToSeries = 0;
            gf.BaseCondition = 1;
            gf.Locked = 0;
            gf.FilterType = (int)GroupFilterType.UserDefined;

            repFilters.Save(gf);

            var gfc = new GroupFilterCondition();
            gfc.ConditionType = (int)GroupFilterConditionType.Favourite;
            gfc.ConditionOperator = (int)GroupFilterOperator.Include;
            gfc.ConditionParameter = "";
            gfc.GroupFilterID = gf.GroupFilterID;
            repGFC.Save(gfc);


            // Missing Episodes
            gf = new GroupFilter();
            gf.GroupFilterName = Resources.Filter_MissingEpisodes;
            gf.ApplyToSeries = 0;
            gf.BaseCondition = 1;
            gf.Locked = 0;
            gf.FilterType = (int)GroupFilterType.UserDefined;

            repFilters.Save(gf);

            gfc = new GroupFilterCondition();
            gfc.ConditionType = (int)GroupFilterConditionType.MissingEpisodesCollecting;
            gfc.ConditionOperator = (int)GroupFilterOperator.Include;
            gfc.ConditionParameter = "";
            gfc.GroupFilterID = gf.GroupFilterID;
            repGFC.Save(gfc);

            // Newly Added Series
            gf = new GroupFilter();
            gf.GroupFilterName = Resources.Filter_Added;
            gf.ApplyToSeries = 0;
            gf.BaseCondition = 1;
            gf.Locked = 0;
            gf.FilterType = (int)GroupFilterType.UserDefined;

            repFilters.Save(gf);

            gfc = new GroupFilterCondition();
            gfc.ConditionType = (int)GroupFilterConditionType.SeriesCreatedDate;
            gfc.ConditionOperator = (int)GroupFilterOperator.LastXDays;
            gfc.ConditionParameter = "10";
            gfc.GroupFilterID = gf.GroupFilterID;
            repGFC.Save(gfc);

            // Newly Airing Series
            gf = new GroupFilter();
            gf.GroupFilterName = Resources.Filter_Airing;
            gf.ApplyToSeries = 0;
            gf.BaseCondition = 1;
            gf.Locked = 0;
            gf.FilterType = (int)GroupFilterType.UserDefined;

            repFilters.Save(gf);

            gfc = new GroupFilterCondition();
            gfc.ConditionType = (int)GroupFilterConditionType.AirDate;
            gfc.ConditionOperator = (int)GroupFilterOperator.LastXDays;
            gfc.ConditionParameter = "30";
            gfc.GroupFilterID = gf.GroupFilterID;
            repGFC.Save(gfc);

            // Votes Needed
            gf = new GroupFilter();
            gf.GroupFilterName = Resources.Filter_Votes;
            gf.ApplyToSeries = 0;
            gf.BaseCondition = 1;
            gf.Locked = 0;
            gf.FilterType = (int)GroupFilterType.UserDefined;

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
            gf.GroupFilterName = Resources.Filter_RecentlyWatched;
            gf.ApplyToSeries = 0;
            gf.BaseCondition = 1;
            gf.Locked = 0;
            gf.FilterType = (int)GroupFilterType.UserDefined;

            repFilters.Save(gf);

            gfc = new GroupFilterCondition();
            gfc.ConditionType = (int)GroupFilterConditionType.EpisodeWatchedDate;
            gfc.ConditionOperator = (int)GroupFilterOperator.LastXDays;
            gfc.ConditionParameter = "10";
            gfc.GroupFilterID = gf.GroupFilterID;
            repGFC.Save(gfc);

            // TvDB/MovieDB Link Missing
            gf = new GroupFilter();
            gf.GroupFilterName = Resources.Filter_LinkMissing;
            gf.ApplyToSeries = 0;
            gf.BaseCondition = 1;
            gf.Locked = 0;
            gf.FilterType = (int)GroupFilterType.UserDefined;

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
            var repUsers = new JMMUserRepository();

            if (repUsers.GetAll().Count() > 0) return;

            var defaultUser = new JMMUser();
            defaultUser.CanEditServerSettings = 1;
            defaultUser.HideCategories = "";
            defaultUser.IsAdmin = 1;
            defaultUser.IsAniDBUser = 1;
            defaultUser.IsTraktUser = 1;
            defaultUser.Password = "";
            defaultUser.Username = "Default";
            repUsers.Save(defaultUser);

            var familyUser = new JMMUser();
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
            var repScripts = new RenameScriptRepository();

            if (repScripts.GetAll().Count() > 0) return;

            var initialScript = new RenameScript();

            initialScript.ScriptName = "Default";
            initialScript.IsEnabledOnImport = 0;
            initialScript.Script =
                "// Sample Output: [Coalgirls]_Highschool_of_the_Dead_-_01_(1920x1080_Blu-ray_H264)_[90CC6DC1].mkv" +
                Environment.NewLine +
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
                "DO REPLACE '" + (char)34 + "' '`'" + Environment.NewLine +
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
            var repAnime = new AniDB_AnimeRepository();

            // delete all TvDB link duplicates
            var repCrossRefTvDB = new CrossRef_AniDB_TvDBRepository();

            var xrefsTvDBProcessed = new List<CrossRef_AniDB_TvDB>();
            var xrefsTvDBToBeDeleted = new List<CrossRef_AniDB_TvDB>();

            var xrefsTvDB = repCrossRefTvDB.GetAll();
            foreach (var xrefTvDB in xrefsTvDB)
            {
                var deleteXref = false;
                foreach (var xref in xrefsTvDBProcessed)
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


            foreach (var xref in xrefsTvDBToBeDeleted)
            {
                var msg = "";
                var anime = repAnime.GetByAnimeID(xref.AnimeID);
                if (anime != null) msg = anime.MainTitle;

                logger.Warn("Deleting TvDB Link because of a duplicate: {0} ({1}) - {2}/{3}", xref.AnimeID, msg,
                    xref.TvDBID, xref.TvDBSeasonNumber);
                repCrossRefTvDB.Delete(xref.CrossRef_AniDB_TvDBID);
            }
        }

        public static void FixDuplicateTraktLinks()
        {
            var repAnime = new AniDB_AnimeRepository();

            // delete all Trakt link duplicates
            var repCrossRefTrakt = new CrossRef_AniDB_TraktRepository();

            var xrefsTraktProcessed = new List<CrossRef_AniDB_Trakt>();
            var xrefsTraktToBeDeleted = new List<CrossRef_AniDB_Trakt>();

            var xrefsTrakt = repCrossRefTrakt.GetAll();
            foreach (var xrefTrakt in xrefsTrakt)
            {
                var deleteXref = false;
                foreach (var xref in xrefsTraktProcessed)
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


            foreach (var xref in xrefsTraktToBeDeleted)
            {
                var msg = "";
                var anime = repAnime.GetByAnimeID(xref.AnimeID);
                if (anime != null) msg = anime.MainTitle;

                logger.Warn("Deleting Trakt Link because of a duplicate: {0} ({1}) - {2}/{3}", xref.AnimeID, msg,
                    xref.TraktID, xref.TraktSeasonNumber);
                repCrossRefTrakt.Delete(xref.CrossRef_AniDB_TraktID);
            }
        }

        public static void MigrateTvDBLinks_V1_to_V2()
        {
            try
            {
                var repAnime = new AniDB_AnimeRepository();
                var repEps = new TvDB_EpisodeRepository();

                var repCrossRefTvDB = new CrossRef_AniDB_TvDBRepository();
                var repCrossRefTvDBNew = new CrossRef_AniDB_TvDBV2Repository();

                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var xrefsTvDB = repCrossRefTvDB.GetAll();
                    foreach (var xrefTvDB in xrefsTvDB)
                    {
                        var xrefNew = new CrossRef_AniDB_TvDBV2();
                        xrefNew.AnimeID = xrefTvDB.AnimeID;
                        xrefNew.CrossRefSource = xrefTvDB.CrossRefSource;
                        xrefNew.TvDBID = xrefTvDB.TvDBID;
                        xrefNew.TvDBSeasonNumber = xrefTvDB.TvDBSeasonNumber;

                        var ser = xrefTvDB.GetTvDBSeries(session);
                        if (ser != null)
                            xrefNew.TvDBTitle = ser.SeriesName;

                        // determine start ep type
                        if (xrefTvDB.TvDBSeasonNumber == 0)
                            xrefNew.AniDBStartEpisodeType = (int)enEpisodeType.Special;
                        else
                            xrefNew.AniDBStartEpisodeType = (int)enEpisodeType.Episode;

                        xrefNew.AniDBStartEpisodeNumber = 1;
                        xrefNew.TvDBStartEpisodeNumber = 1;

                        repCrossRefTvDBNew.Save(xrefNew);
                    }

                    // create cross ref's for specials
                    foreach (var xrefTvDB in xrefsTvDB)
                    {
                        var anime = repAnime.GetByAnimeID(xrefTvDB.AnimeID);
                        if (anime == null) continue;

                        // this anime has specials
                        if (anime.EpisodeCountSpecial <= 0) continue;

                        // this tvdb series has a season 0 (specials)
                        var seasons = repEps.GetSeasonNumbersForSeries(xrefTvDB.TvDBID);
                        if (!seasons.Contains(0)) continue;

                        //make sure we are not doubling up
                        var temp = repCrossRefTvDBNew.GetByTvDBID(xrefTvDB.TvDBID, 0, 1, xrefTvDB.AnimeID,
                            (int)enEpisodeType.Special, 1);
                        if (temp != null) continue;

                        var xrefNew = new CrossRef_AniDB_TvDBV2();
                        xrefNew.AnimeID = xrefTvDB.AnimeID;
                        xrefNew.CrossRefSource = xrefTvDB.CrossRefSource;
                        xrefNew.TvDBID = xrefTvDB.TvDBID;
                        xrefNew.TvDBSeasonNumber = 0;
                        xrefNew.TvDBStartEpisodeNumber = 1;
                        xrefNew.AniDBStartEpisodeType = (int)enEpisodeType.Special;
                        xrefNew.AniDBStartEpisodeNumber = 1;

                        var ser = xrefTvDB.GetTvDBSeries(session);
                        if (ser != null)
                            xrefNew.TvDBTitle = ser.SeriesName;

                        repCrossRefTvDBNew.Save(xrefNew);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("Could not MigrateTvDBLinks_V1_to_V2: " + ex, ex);
            }
        }

        public static void MigrateTraktLinks_V1_to_V2()
        {
            try
            {
                var repAnime = new AniDB_AnimeRepository();
                var repEps = new Trakt_EpisodeRepository();
                var repShows = new Trakt_ShowRepository();

                var repCrossRefTrakt = new CrossRef_AniDB_TraktRepository();
                var repCrossRefTraktNew = new CrossRef_AniDB_TraktV2Repository();

                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var xrefsTrakt = repCrossRefTrakt.GetAll();
                    foreach (var xrefTrakt in xrefsTrakt)
                    {
                        var xrefNew = new CrossRef_AniDB_TraktV2();
                        xrefNew.AnimeID = xrefTrakt.AnimeID;
                        xrefNew.CrossRefSource = xrefTrakt.CrossRefSource;
                        xrefNew.TraktID = xrefTrakt.TraktID;
                        xrefNew.TraktSeasonNumber = xrefTrakt.TraktSeasonNumber;

                        var show = xrefTrakt.GetByTraktShow(session);
                        if (show != null)
                            xrefNew.TraktTitle = show.Title;

                        // determine start ep type
                        if (xrefTrakt.TraktSeasonNumber == 0)
                            xrefNew.AniDBStartEpisodeType = (int)enEpisodeType.Special;
                        else
                            xrefNew.AniDBStartEpisodeType = (int)enEpisodeType.Episode;

                        xrefNew.AniDBStartEpisodeNumber = 1;
                        xrefNew.TraktStartEpisodeNumber = 1;

                        repCrossRefTraktNew.Save(xrefNew);
                    }

                    // create cross ref's for specials
                    foreach (var xrefTrakt in xrefsTrakt)
                    {
                        var anime = repAnime.GetByAnimeID(xrefTrakt.AnimeID);
                        if (anime == null) continue;

                        var show = xrefTrakt.GetByTraktShow(session);
                        if (show == null) continue;

                        // this anime has specials
                        if (anime.EpisodeCountSpecial <= 0) continue;

                        // this Trakt series has a season 0 (specials)
                        var seasons = repEps.GetSeasonNumbersForSeries(show.Trakt_ShowID);
                        if (!seasons.Contains(0)) continue;

                        //make sure we are not doubling up
                        var temp = repCrossRefTraktNew.GetByTraktID(xrefTrakt.TraktID, 0, 1, xrefTrakt.AnimeID,
                            (int)enEpisodeType.Special, 1);
                        if (temp != null) continue;

                        var xrefNew = new CrossRef_AniDB_TraktV2();
                        xrefNew.AnimeID = xrefTrakt.AnimeID;
                        xrefNew.CrossRefSource = xrefTrakt.CrossRefSource;
                        xrefNew.TraktID = xrefTrakt.TraktID;
                        xrefNew.TraktSeasonNumber = 0;
                        xrefNew.TraktStartEpisodeNumber = 1;
                        xrefNew.AniDBStartEpisodeType = (int)enEpisodeType.Special;
                        xrefNew.AniDBStartEpisodeNumber = 1;
                        xrefNew.TraktTitle = show.Title;

                        repCrossRefTraktNew.Save(xrefNew);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("Could not MigrateTraktLinks_V1_to_V2: " + ex, ex);
            }
        }

        private static void CreateContinueWatchingGroupFilter()
        {
            // group filters
            var repFilters = new GroupFilterRepository();
            var repGFC = new GroupFilterConditionRepository();

            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // check if it already exists
                var lockedGFs = repFilters.GetLockedGroupFilters(session);

                if (lockedGFs != null)
                {
                    // if it already exists we can leave
                    foreach (var gfTemp in lockedGFs)
                    {
                        if (gfTemp.FilterType == (int)GroupFilterType.ContinueWatching)
                            return;
                    }

                    // the default value when the column was added to the database was '1'
                    // this is only needed for users of a migrated database
                    foreach (var gfTemp in lockedGFs)
                    {
                        if (
                            gfTemp.GroupFilterName.Equals(Constants.GroupFilterName.ContinueWatching,
                                StringComparison.InvariantCultureIgnoreCase) &&
                            gfTemp.FilterType != (int)GroupFilterType.ContinueWatching)
                        {
                            FixContinueWatchingGroupFilter_20160406();
                            return;
                        }
                    }
                }

                var gf = new GroupFilter();
                gf.GroupFilterName = Constants.GroupFilterName.ContinueWatching;
                gf.Locked = 1;
                gf.SortingCriteria = "4;2"; // by last watched episode desc
                gf.ApplyToSeries = 0;
                gf.BaseCondition = 1; // all
                gf.FilterType = (int)GroupFilterType.ContinueWatching;

                repFilters.Save(gf);

                var gfc = new GroupFilterCondition();
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

        public static void FixContinueWatchingGroupFilter_20160406()
        {
            // group filters
            var repFilters = new GroupFilterRepository();
            var repGFC = new GroupFilterConditionRepository();

            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // check if it already exists
                var lockedGFs = repFilters.GetLockedGroupFilters(session);

                if (lockedGFs != null)
                {
                    // if it already exists we can leave
                    foreach (var gf in lockedGFs)
                    {
                        if (gf.GroupFilterName.Equals(Constants.GroupFilterName.ContinueWatching,
                            StringComparison.InvariantCultureIgnoreCase))
                        {
                            gf.FilterType = (int)GroupFilterType.ContinueWatching;
                            repFilters.Save(gf);
                        }
                    }
                }
            }
        }

        public static void RemoveOldMovieDBImageRecords()
        {
            try
            {
                var repFanart = new MovieDB_FanartRepository();
                foreach (var fanart in repFanart.GetAll())
                {
                    repFanart.Delete(fanart.MovieDB_FanartID);
                }

                var repPoster = new MovieDB_PosterRepository();
                foreach (var poster in repPoster.GetAll())
                {
                    repPoster.Delete(poster.MovieDB_PosterID);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("Could not RemoveOldMovieDBImageRecords: " + ex, ex);
            }
        }

        public static void PopulateTagWeight()
        {
            try
            {
                var repTags = new AniDB_Anime_TagRepository();
                foreach (var atag in repTags.GetAll())
                {
                    atag.Weight = 0;
                    repTags.Save(atag);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("Could not PopulateTagWeight: " + ex, ex);
            }
        }

        public static void CreateInitialCustomTags()
        {
            try
            {
                // group filters
                var repTags = new CustomTagRepository();

                if (repTags.GetAll().Count() > 0) return;

                // Dropped
                var tag = new CustomTag();
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
                logger.ErrorException("Could not Create Initial Custom Tags: " + ex, ex);
            }
        }

        public static void FixHashes()
        {
            try
            {
                var repVids = new VideoLocalRepository();

                foreach (var vid in repVids.GetAll())
                {
                    var fixedHash = false;
                    if (vid.CRC32.Equals("00000000"))
                    {
                        vid.CRC32 = null;
                        fixedHash = true;
                    }
                    if (vid.MD5.Equals("00000000000000000000000000000000"))
                    {
                        vid.MD5 = null;
                        fixedHash = true;
                    }
                    if (vid.SHA1.Equals("0000000000000000000000000000000000000000"))
                    {
                        vid.SHA1 = null;
                        fixedHash = true;
                    }
                    if (fixedHash)
                    {
                        repVids.Save(vid);
                        logger.Info("Fixed hashes on file: {0}", vid.FullServerPath);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }
    }
}