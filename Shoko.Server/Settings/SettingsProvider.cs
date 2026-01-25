using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Force.DeepCloner;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Server.FileHelper;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using Shoko.Server.Utilities.MediaInfoLib;
using Constants = Shoko.Server.Server.Constants;
using Formatting = Newtonsoft.Json.Formatting;
using Legacy = Shoko.Server.Settings.Migration.ServerSettings_Legacy;

namespace Shoko.Server.Settings;

public class SettingsProvider : ISettingsProvider
{
    private readonly ILogger<SettingsProvider> _logger;
    private const string SettingsFilename = "settings-server.json";
    private static readonly object SettingsLock = new();
    private static IServerSettings Instance { get; set; }

    public SettingsProvider(ILogger<SettingsProvider> logger)
    {
        _logger = logger;
    }

    public IServerSettings GetSettings(bool copy = false)
    {
        if (Instance == null) LoadSettings();
        if (copy) return Instance.DeepClone();
        return Instance;
    }

    public void SaveSettings(IServerSettings settings)
    {
        Instance = settings;
        SaveSettings();
    }

    public void LoadSettings()
    {
        var appPath = Utils.ApplicationPath;
        if (!Directory.Exists(appPath))
            Directory.CreateDirectory(appPath);

        var path = Path.Combine(appPath, SettingsFilename);
        if (!File.Exists(path))
        {
            Instance = File.Exists(Path.Combine(appPath, "settings.json"))
                ? LoadLegacySettings()
                : new();
            SaveSettings();
            return;
        }

        LoadSettingsFromFile(path);
        SaveSettings();
    }

    private static ServerSettings LoadLegacySettings()
    {
        var legacy = Legacy.LoadSettingsFromFile();
#pragma warning disable CS0618 // Type or member is obsolete
        var settings = new ServerSettings
        {
            ImagesPath = legacy.ImagesPath,
            Web = new()
            {
                Port = (ushort)legacy.JMMServerPort,
            },
            WebUI_Settings = legacy.WebUI_Settings,
            FirstRun = legacy.FirstRun,
            LogRotator =
                new LogRotatorSettings
                {
                    Enabled = legacy.RotateLogs,
                    Zip = legacy.RotateLogs_Zip,
                    Delete = legacy.RotateLogs_Delete,
                    Delete_Days = legacy.RotateLogs_Delete_Days
                },
            AniDb = new AniDbSettings
            {
                Username = legacy.AniDB_Username,
                Password = legacy.AniDB_Password,
                HTTPServerUrl = $"http://${legacy.AniDB_ServerAddress}:{ushort.Parse(legacy.AniDB_ServerPort)}",
                ClientPort = ushort.Parse(legacy.AniDB_ClientPort),
                AVDumpKey = legacy.AniDB_AVDumpKey,
                AVDumpClientPort = ushort.Parse(legacy.AniDB_AVDumpClientPort),
                DownloadRelatedAnime = legacy.AniDB_DownloadRelatedAnime,
                DownloadReviews = legacy.AniDB_DownloadReviews,
                DownloadReleaseGroups = legacy.AniDB_DownloadReleaseGroups,
                MyList_AddFiles = legacy.AniDB_MyList_AddFiles,
                MyList_StorageState = legacy.AniDB_MyList_StorageState,
                MyList_DeleteType = legacy.AniDB_MyList_DeleteType,
                MyList_ReadUnwatched = legacy.AniDB_MyList_ReadUnwatched,
                MyList_ReadWatched = legacy.AniDB_MyList_ReadWatched,
                MyList_SetWatched = legacy.AniDB_MyList_SetWatched,
                MyList_SetUnwatched = legacy.AniDB_MyList_SetUnwatched,
                MyList_UpdateFrequency = legacy.AniDB_MyList_UpdateFrequency,
                Calendar_UpdateFrequency = legacy.AniDB_Calendar_UpdateFrequency,
                Anime_UpdateFrequency = legacy.AniDB_Anime_UpdateFrequency,
                File_UpdateFrequency = legacy.AniDB_File_UpdateFrequency,
                DownloadCharacters = legacy.AniDB_DownloadCharacters,
                DownloadCreators = legacy.AniDB_DownloadCreators,
                MaxRelationDepth = legacy.AniDB_MaxRelationDepth
            },
            TMDB =
                new TMDBSettings
                {
                    AutoDownloadBackdrops = legacy.MovieDB_AutoFanart,
                    MaxAutoBackdrops = legacy.MovieDB_AutoFanartAmount,
                    AutoDownloadPosters = legacy.MovieDB_AutoPosters,
                    MaxAutoPosters = legacy.MovieDB_AutoPostersAmount
                },
            Import =
                new ImportSettings
                {
                    VideoExtensions = legacy.VideoExtensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                    RunOnStart = legacy.RunImportOnStart,
                    ScanDropFoldersOnStart = legacy.ScanDropFoldersOnStart,
                    Hasher = new()
                    {
                        CRC = legacy.Hash_CRC32,
                        MD5 = legacy.Hash_MD5,
                        SHA1 = legacy.Hash_SHA1,
                    },
                    UseExistingFileWatchedStatus = legacy.Import_UseExistingFileWatchedStatus
                },
            Plex =
                new PlexSettings
                {
                    Libraries = legacy.Plex_Libraries.ToList(),
                    Server = legacy.Plex_Server
                },
            AutoGroupSeries = legacy.AutoGroupSeries,
            AutoGroupSeriesRelationExclusions = legacy.AutoGroupSeriesRelationExclusions.Replace("alternate", "alternative", StringComparison.InvariantCultureIgnoreCase).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            AutoGroupSeriesUseScoreAlgorithm = legacy.AutoGroupSeriesUseScoreAlgorithm,
            FileQualityFilterEnabled = legacy.FileQualityFilterEnabled,
            FileQualityPreferences = legacy.FileQualityFilterPreferences,
            Language = new()
            {
                UseSynonyms = legacy.LanguageUseSynonyms,
                SeriesTitleLanguageOrder = legacy.LanguagePreference.Split(',').ToList(),
                SeriesTitleSourceOrder = [legacy.SeriesNameSource],
                EpisodeTitleLanguageOrder = legacy.EpisodeLanguagePreference.Split(',').ToList(),
                EpisodeTitleSourceOrder = [legacy.EpisodeTitleSource],
                DescriptionSourceOrder = [legacy.SeriesDescriptionSource],
            },
            TraktTv = new TraktSettings
            {
                Enabled = legacy.Trakt_IsEnabled,
                AuthToken = legacy.Trakt_AuthToken,
                RefreshToken = legacy.Trakt_RefreshToken,
                TokenExpirationDate = legacy.Trakt_TokenExpirationDate,
                SyncFrequency = legacy.Trakt_SyncFrequency
            },
            Linux = new LinuxSettings
            {
                UID = legacy.Linux_UID,
                GID = legacy.Linux_GID,
                Permission = legacy.Linux_Permission
            },
            TraceLog = legacy.TraceLog,
            Database = new DatabaseSettings
            {
                MySqliteDirectory = legacy.MySqliteDirectory,
                DatabaseBackupDirectory = legacy.DatabaseBackupDirectory,
                Type = legacy.DatabaseType
            }
        };
#pragma warning restore CS0618 // Type or member is obsolete

        switch (legacy.DatabaseType)
        {
            case Constants.DatabaseType.MySQL:
                settings.Database.Username = legacy.MySQL_Username;
                settings.Database.Password = legacy.MySQL_Password;
                settings.Database.Schema = legacy.MySQL_SchemaName;
                settings.Database.Host = legacy.MySQL_Hostname;
                break;
            case Constants.DatabaseType.SqlServer:
                settings.Database.Username = legacy.DatabaseUsername;
                settings.Database.Password = legacy.DatabasePassword;
                settings.Database.Schema = legacy.DatabaseName;
                settings.Database.Host = legacy.DatabaseServer;
                break;
        }

        return settings;
    }

    public static T Deserialize<T>(string json) where T : class
    {
        return Deserialize(typeof(T), json) as T;
    }

    public static object Deserialize(Type t, string json)
    {
        var serializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new NullToDefaultValueResolver(),
            Converters = new List<JsonConverter> { new StringEnumConverter() },
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MissingMemberHandling = MissingMemberHandling.Ignore
        };
        var result = JsonConvert.DeserializeObject(json, t, serializerSettings);
        if (result == null)
        {
            return null;
        }

        var context = new ValidationContext(result, null, null);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(result, context, results))
        {
            throw new ValidationException(string.Join("\n", results.Select(a => a.ErrorMessage)));
        }

        return result;
    }

    public void LoadSettingsFromFile(string path)
    {
        var settings = File.ReadAllText(path);
        settings = SettingsMigrations.MigrateSettings(settings);
        settings = FixNonEmittedDefaults(path, settings);
        try
        {
            Instance = Deserialize<ServerSettings>(settings);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred while loading the settings from file");
        }
    }

    /// <summary>
    /// Fix the behavior of missing members in pre-4.0
    /// </summary>
    /// <param name="path"></param>
    /// <param name="settings"></param>
    private static string FixNonEmittedDefaults(string path, string settings)
    {
        if (settings.Contains("\"FirstRun\":")) return settings;

        var serializerSettings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter> { new StringEnumConverter() },
            Error = (sender, args) => { args.ErrorContext.Handled = true; },
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Populate
        };
        var result = JsonConvert.DeserializeObject<ServerSettings>(settings, serializerSettings);
        var inCode = Serialize(result, true);
        File.WriteAllText(path, inCode);
        return inCode;
    }

    public void SaveSettings()
    {
        if (Instance == null)
        {
            _logger.LogWarning("Tried to save settings, but the settings were null");
            return;
        }

        // Always update the trace logging settings when the settings change.
        Utils.SetTraceLogging(Instance.TraceLog);

        var path = Path.Combine(Utils.ApplicationPath, SettingsFilename);

        var context = new ValidationContext(Instance, null, null);
        var results = new List<ValidationResult>();

        if (!Validator.TryValidateObject(Instance, context, results))
        {
            results.ForEach(s => _logger.LogError("{ex}", s.ErrorMessage));
            throw new ValidationException();
        }

        lock (SettingsLock)
        {
            var onDisk = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            var inCode = Serialize(Instance, true);
            if (!onDisk.Equals(inCode, StringComparison.Ordinal))
            {
                File.WriteAllText(path, inCode);
                ShokoEventHandler.Instance.OnSettingsSaved();
            }
        }
    }

    public static string Serialize(object obj, bool indent = false)
    {
        var serializerSettings = new JsonSerializerSettings
        {
            Formatting = indent ? Formatting.Indented : Formatting.None,
            DefaultValueHandling = DefaultValueHandling.Include,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            Converters = new List<JsonConverter> { new StringEnumConverter() }
        };
        return JsonConvert.SerializeObject(obj, serializerSettings);
    }

    private void DumpSettings(object obj, string path = "")
    {
        if (obj == null)
        {
            _logger.LogInformation("{Path}: null", path);
            return;
        }

        foreach (var prop in obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var type = prop.PropertyType;
            if (type.FullName.StartsWith("Shoko.Server") ||
                type.FullName.StartsWith("Shoko.Models") ||
                type.FullName.StartsWith("Shoko.Plugin"))
            {
                DumpSettings(prop.GetValue(obj), path + $".{prop.Name}");
                continue;
            }

            var value = prop.GetValue(obj);

            if (!IsPrimitive(type))
            {
                value = Serialize(value);
            }

            if (prop.Name.ToLower().EndsWith("password") || prop.Name.ToLower().EndsWith("avdumpkey"))
            {
                value = "***HIDDEN***";
            }

            _logger.LogInformation("{Path}.{PropName}: {Value}", path, prop.Name, value);
        }
    }

    private static bool IsPrimitive(Type type)
    {
        if (type.IsPrimitive)
        {
            return true;
        }

        if (type.IsValueType)
        {
            return true;
        }

        return false;
    }

    public void DebugSettingsToLog()
    {
        #region System Info

        _logger.LogInformation("-------------------- SYSTEM INFO -----------------------");

        try
        {
            var assembly = Assembly.GetEntryAssembly();
            var serverVersion = Utils.GetApplicationVersion(assembly);
            var extraVersionDict = Utils.GetApplicationExtraVersion(assembly);
            if (!extraVersionDict.TryGetValue("tag", out var tag))
                tag = null;

            if (!extraVersionDict.TryGetValue("commit", out var commit))
                commit = null;

            var releaseChannel = ReleaseChannel.Debug;
            if (extraVersionDict.TryGetValue("channel", out var rawChannel))
                if (Enum.TryParse<ReleaseChannel>(rawChannel, true, out var channel))
                    releaseChannel = channel;

            DateTime? releaseDate = null;
            if (extraVersionDict.TryGetValue("date", out var dateText) && DateTime.TryParse(dateText, out var releaseDate1))
                releaseDate = releaseDate1;

            _logger.LogInformation("Shoko Server Version: v{ApplicationVersion}, Channel: {Channel}, Tag: {Tag}, Commit: {Commit}", serverVersion,
                releaseChannel, tag, commit);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error in log (server version lookup): {Ex}", ex);
        }

        /*
        try
        {
            if (DatabaseFactory.Instance != null)
                logger.Info($"Database Version: {DatabaseFactory.Instance.GetDatabaseVersion()}");
        }
        catch (Exception ex)
        {
            // whops, can't create file
            logger.Warn("Error in log (database version lookup: {0}", ex.Message);
        }
        */
        _logger.LogInformation("Operating System: {OSInfo}", RuntimeInformation.OSDescription);

        try
        {
            var mediaInfoVersion = "**** MediaInfo Not found *****";

            var tempVersion = MediaInfo.GetVersion();
            if (tempVersion != null) mediaInfoVersion = $"MediaInfo: {tempVersion}";
            _logger.LogInformation("{msg}", mediaInfoVersion);

            var hasherInfoVersion = "**** Hasher - DLL NOT found *****";

            tempVersion = Hasher.GetVersion();
            if (tempVersion != null) hasherInfoVersion = $"RHash: {tempVersion}";
            _logger.LogInformation("{msg}", hasherInfoVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError("Error in log (hasher / info): {Message}", ex.Message);
        }

        _logger.LogInformation("-------------------------------------------------------");

        #endregion

        _logger.LogInformation("----------------- SERVER SETTINGS ----------------------");

        DumpSettings(Instance, "Settings");

        _logger.LogInformation("-------------------------------------------------------");
    }
}
