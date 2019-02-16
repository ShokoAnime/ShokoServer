using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Shoko.Models.Server;
using NHibernate.Util;
using NLog;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Databases
{
    public abstract class BaseDatabase<T>
    {
        // ReSharper disable once StaticMemberInGenericType
        protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public string GetDatabaseBackupName(int version)
        {
            string backupath = ServerSettings.DatabaseBackupDirectory;
            try
            {
                Directory.CreateDirectory(backupath);
            }
            catch
            {
                //ignored
            }
            string fname = ServerSettings.DatabaseName + "_" + version.ToString("D3") + "_" +
                           DateTime.Now.Year.ToString("D4") + DateTime.Now.Month.ToString("D2") +
                           DateTime.Now.Day.ToString("D2") + DateTime.Now.Hour.ToString("D2") +
                           DateTime.Now.Minute.ToString("D2");
            return Path.Combine(backupath, fname);
        }


        protected abstract Tuple<bool, string> ExecuteCommand(T connection, string command);
        protected abstract void Execute(T connection, string command);
        protected abstract long ExecuteScalar(T connection, string command);
        protected abstract ArrayList ExecuteReader(T connection, string command);
        public abstract string GetConnectionString();

        public virtual bool TestConnection()
        {
            // For SQLite, we assume conection succeeds
            return true;
        }

        protected abstract void ConnectionWrapper(string connectionstring, Action<T> action);

        protected Dictionary<string, Dictionary<string, Versions>> AllVersions { get; set; }
        protected List<DatabaseCommand> Fixes = new List<DatabaseCommand>();


        public void Init()
        {
            try
            {
                AllVersions = RepoFactory.Versions.GetAllByType(Constants.DatabaseTypeKey);
            }
            catch //First Time
            {
                AllVersions = new Dictionary<string, Dictionary<string, Versions>>();
            }
            Fixes = new List<DatabaseCommand>();
        }

        protected void AddVersion(string version, string revision, string command)
        {
            Versions v = new Versions
            {
                VersionType = Constants.DatabaseTypeKey,
                VersionValue = version,
                VersionRevision = revision,
                VersionCommand = command,
                VersionProgram = ServerState.Instance.ApplicationVersion
            };
            RepoFactory.Versions.Save(v);
            Dictionary<string, Versions> dv = new Dictionary<string, Versions>();
            if (AllVersions.ContainsKey(v.VersionValue))
                dv = AllVersions[v.VersionValue];
            else
                AllVersions.Add(v.VersionValue, dv);
            dv.Add(v.VersionRevision, v);
        }

        protected void ExecuteWithException(T connection, DatabaseCommand cmd)
        {
            Tuple<bool, string> t = ExecuteCommand(connection, cmd);
            if (!t.Item1)
                throw new DatabaseCommandException(t.Item2, cmd);
        }

        protected void ExecuteWithException(T connection, IEnumerable<DatabaseCommand> cmds)
        {
            cmds.ForEach(a => ExecuteWithException(connection, a));
        }

        public int GetDatabaseVersion()
        {
            if (AllVersions.Count == 0)
                return 0;
            return AllVersions.Keys.Select(int.Parse).ToList().Max();
        }

        public ArrayList GetData(string sql)
        {
            ArrayList ret = null;
            ConnectionWrapper(GetConnectionString(), (myConn) => { ret = ExecuteReader(myConn, sql); });
            return ret;
        }

        internal void PreFillVersions(IEnumerable<DatabaseCommand> commands)
        {
            if (AllVersions.Count != 1 || AllVersions.Values.ElementAt(0).Count != 1) return;

            Versions v = AllVersions.Values.ElementAt(0).Values.ElementAt(0);
            string value = v.VersionValue;
            AllVersions.Clear();
            RepoFactory.Versions.Delete(v);
            foreach (DatabaseCommand dc in commands)
            {
                if (dc.Version <= int.Parse(value))
                    AddVersion(dc.Version.ToString(), dc.Revision.ToString(), dc.CommandName);
            }
        }

        public void ExecuteDatabaseFixes()
        {
            foreach (DatabaseCommand cmd in Fixes)
            {
                try
                {
                    string message = cmd.CommandName;
                    if (message.Length > 42)
                        message = message.Substring(0, 42) + "...";
                    message = ServerState.Instance.CurrentSetupStatus =
                        Commons.Properties.Resources.Database_ApplySchema + cmd.Version + "." + cmd.Revision +
                        " - " + message;
                    Logger.Info($"Starting Server: {message}");
                    ServerState.Instance.CurrentSetupStatus = message;

                    cmd.DatabaseFix();
                    AddVersion(cmd.Version.ToString(), cmd.Revision.ToString(), cmd.CommandName);
                }
                catch (Exception e)
                {
                    throw new DatabaseCommandException(e.ToString(), cmd);
                }
            }
        }

        public void AddFix(DatabaseCommand cmd)
        {
            Fixes.Add(cmd);
        }


        public Tuple<bool, string> ExecuteCommand(T connection, DatabaseCommand cmd)
        {
            if (cmd.Version != 0 && cmd.Revision != 0 && AllVersions.ContainsKey(cmd.Version.ToString()) &&
                AllVersions[cmd.Version.ToString()].ContainsKey(cmd.Revision.ToString()))
                return new Tuple<bool, string>(true, null);

            Tuple<bool, string> ret;

            string message = cmd.CommandName;
            if (message.Length > 42)
                message = message.Substring(0, 42) + "...";
            message = ServerState.Instance.CurrentSetupStatus =
                Commons.Properties.Resources.Database_ApplySchema + cmd.Version + "." + cmd.Revision +
                " - " + message;
            ServerState.Instance.CurrentSetupStatus = message;

            switch (cmd.Type)
            {
                case DatabaseCommandType.CodedCommand:
                    ret = cmd.UpdateCommand(connection);
                    break;
                case DatabaseCommandType.PostDatabaseFix:
                    try
                    {
                        AddFix(cmd);
                        ret = new Tuple<bool, string>(true, null);
                    }
                    catch (Exception e)
                    {
                        ret = new Tuple<bool, string>(false, e.ToString());
                    }
                    break;
                default:
                    ret = ExecuteCommand(connection, cmd.Command);
                    break;
            }
            if (cmd.Version != 0 && ret.Item1 && cmd.Type != DatabaseCommandType.PostDatabaseFix)
                AddVersion(cmd.Version.ToString(), cmd.Revision.ToString(), cmd.CommandName);

            return ret;
        }

        public void PopulateInitialData()
        {
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            string message = Commons.Properties.Resources.Database_Users;

            Logger.Info($"Starting Server: {message}");
            ServerState.Instance.CurrentSetupStatus = message;
            CreateInitialUsers();

            message = Commons.Properties.Resources.Database_Filters;
            Logger.Info($"Starting Server: {message}");
            ServerState.Instance.CurrentSetupStatus = message;
            CreateInitialGroupFilters();

            message = Commons.Properties.Resources.Database_LockFilters;
            Logger.Info($"Starting Server: {message}");
            ServerState.Instance.CurrentSetupStatus = message;
            CreateOrVerifyLockedFilters();

            message = Commons.Properties.Resources.Database_RenameScripts;
            Logger.Info($"Starting Server: {message}");
            ServerState.Instance.CurrentSetupStatus = message;
            CreateInitialRenameScript();

            message = Commons.Properties.Resources.Database_CustomTags;
            Logger.Info($"Starting Server: {message}");
            ServerState.Instance.CurrentSetupStatus = message;
            CreateInitialCustomTags();
        }

        public void CreateOrVerifyLockedFilters()
        {
            RepoFactory.GroupFilter.CreateOrVerifyLockedFilters();
        }

        private void CreateInitialGroupFilters()
        {
            // group filters
            // Do to DatabaseFixes, some filters may be made, namely directory filters
            // All, Continue Watching, Years, Seasons, Tags... 6 seems to be enough to tell for now
            // We can't just check the existence of anything specific, as the user can delete most of these
            if (RepoFactory.GroupFilter.GetTopLevel().Count() > 6) return;

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            // Favorites
            SVR_GroupFilter gf = new SVR_GroupFilter
            {
                GroupFilterName = Commons.Properties.Resources.Filter_Favorites,
                ApplyToSeries = 0,
                BaseCondition = 1,
                Locked = 0,
                FilterType = (int)GroupFilterType.UserDefined
            };
            GroupFilterCondition gfc = new GroupFilterCondition
            {
                ConditionType = (int)GroupFilterConditionType.Favourite,
                ConditionOperator = (int)GroupFilterOperator.Include,
                ConditionParameter = string.Empty
            };
            gf.Conditions.Add(gfc);
            gf.CalculateGroupsAndSeries();
            RepoFactory.GroupFilter.Save(gf);

            // Missing Episodes
            gf = new SVR_GroupFilter
            {
                GroupFilterName = Commons.Properties.Resources.Filter_MissingEpisodes,
                ApplyToSeries = 0,
                BaseCondition = 1,
                Locked = 0,
                FilterType = (int)GroupFilterType.UserDefined
            };
            gfc = new GroupFilterCondition
            {
                ConditionType = (int)GroupFilterConditionType.MissingEpisodesCollecting,
                ConditionOperator = (int)GroupFilterOperator.Include,
                ConditionParameter = string.Empty
            };
            gf.Conditions.Add(gfc);
            gf.CalculateGroupsAndSeries();
            RepoFactory.GroupFilter.Save(gf);


            // Newly Added Series
            gf = new SVR_GroupFilter
            {
                GroupFilterName = Commons.Properties.Resources.Filter_Added,
                ApplyToSeries = 0,
                BaseCondition = 1,
                Locked = 0,
                FilterType = (int)GroupFilterType.UserDefined
            };
            gfc = new GroupFilterCondition
            {
                ConditionType = (int)GroupFilterConditionType.SeriesCreatedDate,
                ConditionOperator = (int)GroupFilterOperator.LastXDays,
                ConditionParameter = "10"
            };
            gf.Conditions.Add(gfc);
            gf.CalculateGroupsAndSeries();
            RepoFactory.GroupFilter.Save(gf);

            // Newly Airing Series
            gf = new SVR_GroupFilter
            {
                GroupFilterName = Commons.Properties.Resources.Filter_Airing,
                ApplyToSeries = 0,
                BaseCondition = 1,
                Locked = 0,
                FilterType = (int)GroupFilterType.UserDefined
            };
            gfc = new GroupFilterCondition
            {
                ConditionType = (int)GroupFilterConditionType.AirDate,
                ConditionOperator = (int)GroupFilterOperator.LastXDays,
                ConditionParameter = "30"
            };
            gf.Conditions.Add(gfc);
            gf.CalculateGroupsAndSeries();
            RepoFactory.GroupFilter.Save(gf);

            // Votes Needed
            gf = new SVR_GroupFilter
            {
                GroupFilterName = Commons.Properties.Resources.Filter_Votes,
                ApplyToSeries = 1,
                BaseCondition = 1,
                Locked = 0,
                FilterType = (int)GroupFilterType.UserDefined
            };
            gfc = new GroupFilterCondition
            {
                ConditionType = (int)GroupFilterConditionType.CompletedSeries,
                ConditionOperator = (int)GroupFilterOperator.Include,
                ConditionParameter = string.Empty
            };
            gf.Conditions.Add(gfc);
            gfc = new GroupFilterCondition
            {
                ConditionType = (int)GroupFilterConditionType.HasUnwatchedEpisodes,
                ConditionOperator = (int)GroupFilterOperator.Exclude,
                ConditionParameter = string.Empty
            };
            gf.Conditions.Add(gfc);
            gfc = new GroupFilterCondition
            {
                ConditionType = (int)GroupFilterConditionType.UserVotedAny,
                ConditionOperator = (int)GroupFilterOperator.Exclude,
                ConditionParameter = string.Empty
            };
            gf.Conditions.Add(gfc);
            gf.CalculateGroupsAndSeries();
            RepoFactory.GroupFilter.Save(gf);

            // Recently Watched
            gf = new SVR_GroupFilter
            {
                GroupFilterName = Commons.Properties.Resources.Filter_RecentlyWatched,
                ApplyToSeries = 0,
                BaseCondition = 1,
                Locked = 0,
                FilterType = (int)GroupFilterType.UserDefined
            };
            gfc = new GroupFilterCondition
            {
                ConditionType = (int)GroupFilterConditionType.EpisodeWatchedDate,
                ConditionOperator = (int)GroupFilterOperator.LastXDays,
                ConditionParameter = "10"
            };
            gf.Conditions.Add(gfc);
            gf.CalculateGroupsAndSeries();
            RepoFactory.GroupFilter.Save(gf);

            // TvDB/MovieDB Link Missing
            gf = new SVR_GroupFilter
            {
                GroupFilterName = Commons.Properties.Resources.Filter_LinkMissing,
                ApplyToSeries = 1, // This makes far more sense as applied to series
                BaseCondition = 1,
                Locked = 0,
                FilterType = (int)GroupFilterType.UserDefined
            };
            gfc = new GroupFilterCondition
            {
                ConditionType = (int)GroupFilterConditionType.AssignedTvDBOrMovieDBInfo,
                ConditionOperator = (int)GroupFilterOperator.Exclude,
                ConditionParameter = string.Empty
            };
            gf.Conditions.Add(gfc);
            gf.CalculateGroupsAndSeries();
            RepoFactory.GroupFilter.Save(gf);
        }

        private void CreateInitialUsers()
        {
            if (RepoFactory.JMMUser.GetAll().Any()) return;

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            SVR_JMMUser defaultUser = new SVR_JMMUser
            {
                CanEditServerSettings = 1,
                HideCategories = string.Empty,
                IsAdmin = 1,
                IsAniDBUser = 1,
                IsTraktUser = 1,
                Password = ServerSettings.DefaultUserPassword,
                Username = ServerSettings.DefaultUserUsername
            };
            RepoFactory.JMMUser.Save(defaultUser, true);

            SVR_JMMUser familyUser = new SVR_JMMUser
            {
                CanEditServerSettings = 1,
                HideCategories = "ecchi,nudity,sex,sexual abuse,horror,erotic game,incest,18 restricted",
                IsAdmin = 1,
                IsAniDBUser = 1,
                IsTraktUser = 1,
                Password = string.Empty,
                Username = "Family Friendly"
            };
            RepoFactory.JMMUser.Save(familyUser, true);
        }

        private void CreateInitialRenameScript()
        {
            if (RepoFactory.RenameScript.GetAll().Any()) return;

            RenameScript initialScript = new RenameScript();

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            initialScript.ScriptName = Commons.Properties.Resources.Rename_Default;
            initialScript.IsEnabledOnImport = 0;
            initialScript.RenamerType = "Legacy";
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
                string.Empty + Environment.NewLine +
                "// Replacement rules (cleanup)" + Environment.NewLine +
                "DO REPLACE ' ' '_' // replace spaces with underscores" + Environment.NewLine +
                "DO REPLACE 'H264/AVC' 'H264'" + Environment.NewLine +
                "DO REPLACE '0x0' ''" + Environment.NewLine +
                "DO REPLACE '__' '_'" + Environment.NewLine +
                "DO REPLACE '__' '_'" + Environment.NewLine +
                string.Empty + Environment.NewLine +
                "// Replace all illegal file name characters" + Environment.NewLine +
                "DO REPLACE '<' '('" + Environment.NewLine +
                "DO REPLACE '>' ')'" + Environment.NewLine +
                "DO REPLACE ':' '-'" + Environment.NewLine +
                "DO REPLACE '" + ((Char) 34).ToString() + "' '`'" + Environment.NewLine +
                "DO REPLACE '/' '_'" + Environment.NewLine +
                "DO REPLACE '/' '_'" + Environment.NewLine +
                "DO REPLACE '\\' '_'" + Environment.NewLine +
                "DO REPLACE '|' '_'" + Environment.NewLine +
                "DO REPLACE '?' '_'" + Environment.NewLine +
                "DO REPLACE '*' '_'" + Environment.NewLine;

            RepoFactory.RenameScript.Save(initialScript);
        }

        public void CreateInitialCustomTags()
        {
            try
            {
                // group filters

                if (RepoFactory.CustomTag.GetAll().Any()) return;

                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                // Dropped
                CustomTag tag = new CustomTag
                {
                    TagName = Commons.Properties.Resources.CustomTag_Dropped,
                    TagDescription = Commons.Properties.Resources.CustomTag_DroppedInfo
                };
                RepoFactory.CustomTag.Save(tag);

                // Pinned
                tag = new CustomTag
                {
                    TagName = Commons.Properties.Resources.CustomTag_Pinned,
                    TagDescription = Commons.Properties.Resources.CustomTag_PinnedInfo
                };
                RepoFactory.CustomTag.Save(tag);

                // Ongoing
                tag = new CustomTag
                {
                    TagName = Commons.Properties.Resources.CustomTag_Ongoing,
                    TagDescription = Commons.Properties.Resources.CustomTag_OngoingInfo
                };
                RepoFactory.CustomTag.Save(tag);

                // Waiting for Series Completion
                tag = new CustomTag
                {
                    TagName = Commons.Properties.Resources.CustomTag_SeriesComplete,
                    TagDescription = Commons.Properties.Resources.CustomTag_SeriesCompleteInfo
                };
                RepoFactory.CustomTag.Save(tag);

                // Waiting for Bluray Completion
                tag = new CustomTag
                {
                    TagName = Commons.Properties.Resources.CustomTag_BlurayComplete,
                    TagDescription = Commons.Properties.Resources.CustomTag_BlurayCompleteInfo
                };
                RepoFactory.CustomTag.Save(tag);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Could not Create Initial Custom Tags: " + ex);
            }
        }

        /*
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
             gfc.ConditionParameter = string.Empty;
             gfc.GroupFilterID = gf.GroupFilterID;
             repGFC.Save(gfc);

             gfc = new GroupFilterCondition();
             gfc.ConditionType = (int)GroupFilterConditionType.HasUnwatchedEpisodes;
             gfc.ConditionOperator = (int)GroupFilterOperator.Include;
             gfc.ConditionParameter = string.Empty;
             gfc.GroupFilterID = gf.GroupFilterID;
             repGFC.Save(gfc);
         }
     }
     */
    }
}