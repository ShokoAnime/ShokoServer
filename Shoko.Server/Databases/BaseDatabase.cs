using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using NLog;
using Shoko.Models;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Renamer;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using Constants = Shoko.Server.Server.Constants;

namespace Shoko.Server.Databases;

public abstract class BaseDatabase<T> : IDatabase
{
    // ReSharper disable once StaticMemberInGenericType
    protected static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    // ReSharper disable once StaticMemberInGenericType
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
    protected abstract List<object[]> ExecuteReader(T connection, string command);

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
                    $"Database - Applying Schema Patches...{cmd.Version}.{cmd.Revision} - {message}";
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

        message = ServerState.Instance.ServerStartingStatus = $"Database - Applying Schema Patches...{cmd.Version}.{cmd.Revision} - {message}";
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
        var message = "Database - Populating Data (Users)...";

        Logger.Info($"Starting Server: {message}");
        ServerState.Instance.ServerStartingStatus = message;
        CreateInitialUsers();

        message = "Database - Populating Data (Group Filters)...";
        Logger.Info($"Starting Server: {message}");
        ServerState.Instance.ServerStartingStatus = message;
        CreateInitialGroupFilters();

        message = "Database - Populating Data (Locked Group Filters)...";
        Logger.Info($"Starting Server: {message}");
        ServerState.Instance.ServerStartingStatus = message;
        CreateOrVerifyLockedFilters();

        message = "Database - Populating Data (Rename Script)...";
        Logger.Info($"Starting Server: {message}");
        ServerState.Instance.ServerStartingStatus = message;
        CreateInitialRenameScript();

        message = "Database - Populating Data (Custom Tags)...";
        Logger.Info($"Starting Server: {message}");
        ServerState.Instance.ServerStartingStatus = message;
        CreateInitialCustomTags();
    }

    public void CreateOrVerifyLockedFilters()
    {
        RepoFactory.FilterPreset.CreateOrVerifyLockedFilters();
    }

    private void CreateInitialGroupFilters()
    {
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
        RepoFactory.JMMUser.Save(defaultUser);

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
        RepoFactory.JMMUser.Save(familyUser);
    }

    private void CreateInitialRenameScript()
    {
        if (RepoFactory.RenamerConfig.GetAll().Any())
        {
            return;
        }

        var renamerService = Utils.ServiceContainer.GetRequiredService<RenameFileService>();
        renamerService.RenamersByKey.TryGetValue("WebAOM", out var renamer);
        
        if (renamer == null)
            return;
        
        var defaultSettings = renamer.GetType().GetInterfaces().FirstOrDefault(a => a.IsGenericType && a.GetGenericTypeDefinition() == typeof(IRenamer<>))
            ?.GetProperties(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(a => a.Name == "DefaultSettings")?.GetMethod?.Invoke(renamer, null);
        
        var config = new RenamerConfig
        {
            Name = "Default",
            Type = typeof(WebAOMRenamer),
            Settings = defaultSettings,
        };

        RepoFactory.RenamerConfig.Save(config);
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
                TagName = "Dropped", TagDescription = "Started watching this series, but have since dropped it"
            };
            RepoFactory.CustomTag.Save(tag);

            // Pinned
            tag = new CustomTag
            {
                TagName = "Pinned", TagDescription = "Pinned this series for whatever reason you like"
            };
            RepoFactory.CustomTag.Save(tag);

            // Ongoing
            tag = new CustomTag
            {
                TagName = "Ongoing", TagDescription = "This series does not have an end date"
            };
            RepoFactory.CustomTag.Save(tag);

            // Waiting for Series Completion
            tag = new CustomTag
            {
                TagName = "Waiting for Series Completion",
                TagDescription = "Will start watching this once this series is finished"
            };
            RepoFactory.CustomTag.Save(tag);

            // Waiting for Bluray Completion
            tag = new CustomTag
            {
                TagName = "Waiting for Blu-ray Completion",
                TagDescription = "Will start watching this once all episodes are available in Blu-Ray"
            };
            RepoFactory.CustomTag.Save(tag);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Could not Create Initial Custom Tags: " + ex);
        }
    }
}
