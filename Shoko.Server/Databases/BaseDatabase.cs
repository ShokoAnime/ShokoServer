using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NHibernate;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using Constants = Shoko.Server.Server.Constants;

namespace Shoko.Server.Databases;

public abstract class BaseDatabase<T> : IDatabase
{
    // ReSharper disable once StaticMemberInGenericType
    protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private static string _databaseBackupDirectoryPath;
    private static string DatabaseBackupDirectoryPath
    {
        get
        {
            if (_databaseBackupDirectoryPath != null)
                return _databaseBackupDirectoryPath;

            var dirPath =  Utils.SettingsProvider.GetSettings().Database.DatabaseBackupDirectory;
            if (string.IsNullOrWhiteSpace(dirPath))
                return _databaseBackupDirectoryPath = Utils.ApplicationPath;

            return _databaseBackupDirectoryPath = Path.Combine(Utils.ApplicationPath, dirPath);
        }
    }

    public abstract int RequiredVersion { get; }

    public string GetDatabaseBackupName(int version)
    {
        var settings = Utils.SettingsProvider.GetSettings();
        try
        {
            Directory.CreateDirectory(DatabaseBackupDirectoryPath);
        }
        catch
        {
            //ignored
        }

        var fname = settings.Database.Schema + "_" + version.ToString("D3") + "_" +
                    DateTime.Now.Year.ToString("D4") + DateTime.Now.Month.ToString("D2") +
                    DateTime.Now.Day.ToString("D2") + DateTime.Now.Hour.ToString("D2") +
                    DateTime.Now.Minute.ToString("D2");
        return Path.Combine(DatabaseBackupDirectoryPath, fname);
    }


    protected abstract Tuple<bool, string> ExecuteCommand(T connection, string command);
    protected abstract void Execute(T connection, string command);
    protected abstract long ExecuteScalar(T connection, string command);
    protected abstract ArrayList ExecuteReader(T connection, string command);

    public abstract string GetConnectionString();

    public virtual bool TestConnection()
    {
        // For SQLite, we assume connection succeeds
        return true;
    }

    public abstract bool HasVersionsTable();
    public abstract string GetTestConnectionString();

    protected abstract void ConnectionWrapper(string connectionstring, Action<T> action);

    protected Dictionary<(string Version, string Revision), Versions> AllVersions { get; set; }
    protected List<DatabaseCommand> Fixes = new();


    public void Init()
    {
        try
        {
            AllVersions = HasVersionsTable() ? RepoFactory.Versions.GetAllByType(Constants.DatabaseTypeKey) : new Dictionary<(string, string), Versions>();
        }
        catch (Exception e) //First Time
        {
            Logger.Error(e, "There was an error setting up the database: {Message}", e);
            AllVersions = new Dictionary<(string, string), Versions>();
        }

        Fixes = new List<DatabaseCommand>();
    }

    protected void AddVersion(string version, string revision, string command)
    {
        var v = new Versions
        {
            VersionType = Constants.DatabaseTypeKey,
            VersionValue = version,
            VersionRevision = revision,
            VersionCommand = command,
            VersionProgram = ServerState.Instance.ApplicationVersion
        };
        RepoFactory.Versions.Save(v);

        AllVersions.Add((v.VersionValue, v.VersionRevision), v);
    }

    protected void ExecuteWithException(T connection, DatabaseCommand cmd)
    {
        var t = ExecuteCommand(connection, cmd);
        if (!t.Item1)
        {
            throw new DatabaseCommandException(t.Item2, cmd);
        }
    }

    protected void ExecuteWithException(T connection, IEnumerable<DatabaseCommand> cmds)
    {
        cmds.ForEach(a => ExecuteWithException(connection, a));
    }

    public int GetDatabaseVersion()
    {
        if (AllVersions.Count == 0) return 0;
        return AllVersions.Keys.Select(a => int.Parse(a.Version)).Max();
    }

    public abstract ISessionFactory CreateSessionFactory();
    public abstract bool DatabaseAlreadyExists();
    public abstract void CreateDatabase();
    public abstract void CreateAndUpdateSchema();
    public abstract void BackupDatabase(string fullfilename);

    public ArrayList GetData(string sql)
    {
        ArrayList ret = null;
        ConnectionWrapper(GetConnectionString(), myConn => { ret = ExecuteReader(myConn, sql); });
        return ret;
    }

    public abstract string Name { get; }

    internal void PreFillVersions(IEnumerable<DatabaseCommand> commands)
    {
        // Get first version. If the first patch has been fully run, return
        if (AllVersions.Count <= 1 || AllVersions.Count(a => a.Key.Version == "1") != 1) return;

        var v = AllVersions.FirstOrDefault().Value;
        var value = v.VersionValue;
        AllVersions.Clear();
        RepoFactory.Versions.Delete(v);
        var version = int.Parse(value);

        foreach (var dc in commands.Where(a => a.Version <= version))
        {
            AddVersion(dc.Version.ToString(), dc.Revision.ToString(), dc.CommandName);
        }
    }

    public void ExecuteDatabaseFixes()
    {
        foreach (var cmd in Fixes)
        {
            try
            {
                var message = cmd.CommandName;
                if (message.Length > 42)
                {
                    message = message.Substring(0, 42) + "...";
                }

                message = ServerState.Instance.ServerStartingStatus =
                    Resources.Database_ApplySchema + cmd.Version + "." + cmd.Revision +
                    " - " + message;
                Logger.Info($"Starting Server: {message}");
                ServerState.Instance.ServerStartingStatus = message;

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
        if (cmd.Version != 0 && cmd.Revision != 0 && AllVersions.ContainsKey((cmd.Version.ToString(), cmd.Revision.ToString())))
        {
            return new Tuple<bool, string>(true, null);
        }

        Tuple<bool, string> ret;

        var message = cmd.CommandName;
        if (message.Length > 42)
        {
            message = message.Substring(0, 42) + "...";
        }

        message = ServerState.Instance.ServerStartingStatus =
            Resources.Database_ApplySchema + cmd.Version + "." + cmd.Revision +
            " - " + message;
        ServerState.Instance.ServerStartingStatus = message;

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
        {
            AddVersion(cmd.Version.ToString(), cmd.Revision.ToString(), cmd.CommandName);
        }

        return ret;
    }

    public void PopulateInitialData()
    {
        var message = Resources.Database_Users;

        Logger.Info($"Starting Server: {message}");
        ServerState.Instance.ServerStartingStatus = message;
        CreateInitialUsers();

        message = Resources.Database_Filters;
        Logger.Info($"Starting Server: {message}");
        ServerState.Instance.ServerStartingStatus = message;
        CreateInitialGroupFilters();

        message = Resources.Database_LockFilters;
        Logger.Info($"Starting Server: {message}");
        ServerState.Instance.ServerStartingStatus = message;
        CreateOrVerifyLockedFilters();

        message = Resources.Database_RenameScripts;
        Logger.Info($"Starting Server: {message}");
        ServerState.Instance.ServerStartingStatus = message;
        CreateInitialRenameScript();

        message = Resources.Database_CustomTags;
        Logger.Info($"Starting Server: {message}");
        ServerState.Instance.ServerStartingStatus = message;
        CreateInitialCustomTags();
    }

    public void CreateOrVerifyLockedFilters()
    {
        RepoFactory.GroupFilter.CreateOrVerifyLockedFilters();
        RepoFactory.FilterPreset.CreateOrVerifyLockedFilters();
    }

    private void CreateInitialGroupFilters()
    {
        RepoFactory.GroupFilter.CreateInitialGroupFilters();
        RepoFactory.FilterPreset.CreateInitialFilters();
    }

    private void CreateInitialUsers()
    {
        if (RepoFactory.JMMUser.GetAll().Any())
        {
            return;
        }

        var settings = Utils.SettingsProvider.GetSettings();

        var defaultPassword = settings.Database.DefaultUserPassword == ""
            ? ""
            : Digest.Hash(settings.Database.DefaultUserPassword);
        var defaultUser = new SVR_JMMUser
        {
            CanEditServerSettings = 1,
            HideCategories = string.Empty,
            IsAdmin = 1,
            IsAniDBUser = 1,
            IsTraktUser = 1,
            Password = defaultPassword,
            Username = settings.Database.DefaultUserUsername
        };
        RepoFactory.JMMUser.Save(defaultUser, true);

        var familyUser = new SVR_JMMUser
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
        if (RepoFactory.RenameScript.GetAll().Any())
        {
            return;
        }

        var initialScript = new RenameScript();

        initialScript.ScriptName = Resources.Rename_Default;
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
            "DO REPLACE '" + (char)34 + "' '`'" + Environment.NewLine +
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

            if (RepoFactory.CustomTag.GetAll().Any())
            {
                return;
            }

            // Dropped
            var tag = new CustomTag
            {
                TagName = Resources.CustomTag_Dropped, TagDescription = Resources.CustomTag_DroppedInfo
            };
            RepoFactory.CustomTag.Save(tag);

            // Pinned
            tag = new CustomTag
            {
                TagName = Resources.CustomTag_Pinned, TagDescription = Resources.CustomTag_PinnedInfo
            };
            RepoFactory.CustomTag.Save(tag);

            // Ongoing
            tag = new CustomTag
            {
                TagName = Resources.CustomTag_Ongoing, TagDescription = Resources.CustomTag_OngoingInfo
            };
            RepoFactory.CustomTag.Save(tag);

            // Waiting for Series Completion
            tag = new CustomTag
            {
                TagName = Resources.CustomTag_SeriesComplete,
                TagDescription = Resources.CustomTag_SeriesCompleteInfo
            };
            RepoFactory.CustomTag.Save(tag);

            // Waiting for Bluray Completion
            tag = new CustomTag
            {
                TagName = Resources.CustomTag_BlurayComplete,
                TagDescription = Resources.CustomTag_BlurayCompleteInfo
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
