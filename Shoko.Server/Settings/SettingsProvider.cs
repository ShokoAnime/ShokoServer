using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Force.DeepCloner;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Commons.Utils;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.FileHelper;
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
        var settings = new ServerSettings
        {
            ImagesPath = legacy.ImagesPath,
            ServerPort = (ushort)legacy.JMMServerPort,
            PluginAutoWatchThreshold = double.Parse(legacy.PluginAutoWatchThreshold, CultureInfo.InvariantCulture),
            Culture = legacy.Culture,
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
                DownloadSimilarAnime = legacy.AniDB_DownloadSimilarAnime,
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
                MyListStats_UpdateFrequency = legacy.AniDB_MyListStats_UpdateFrequency,
                File_UpdateFrequency = legacy.AniDB_File_UpdateFrequency,
                DownloadCharacters = legacy.AniDB_DownloadCharacters,
                DownloadCreators = legacy.AniDB_DownloadCreators,
                MaxRelationDepth = legacy.AniDB_MaxRelationDepth
            },
            WebCache = new WebCacheSettings
            {
                Address = legacy.WebCache_Address,
                XRefFileEpisode_Get = legacy.WebCache_XRefFileEpisode_Get,
                XRefFileEpisode_Send = legacy.WebCache_XRefFileEpisode_Send,
                TvDB_Get = legacy.WebCache_TvDB_Get,
                TvDB_Send = legacy.WebCache_TvDB_Send,
                Trakt_Get = legacy.WebCache_Trakt_Get,
                Trakt_Send = legacy.WebCache_Trakt_Send
            },
            TvDB =
                new TvDBSettings
                {
                    AutoLink = legacy.TvDB_AutoLink,
                    AutoFanart = legacy.TvDB_AutoFanart,
                    AutoFanartAmount = legacy.TvDB_AutoFanartAmount,
                    AutoWideBanners = legacy.TvDB_AutoWideBanners,
                    AutoWideBannersAmount = legacy.TvDB_AutoWideBannersAmount,
                    AutoPosters = legacy.TvDB_AutoPosters,
                    AutoPostersAmount = legacy.TvDB_AutoPostersAmount,
                    UpdateFrequency = legacy.TvDB_UpdateFrequency,
                    Language = legacy.TvDB_Language
                },
            MovieDb =
                new MovieDbSettings
                {
                    AutoFanart = legacy.MovieDB_AutoFanart,
                    AutoFanartAmount = legacy.MovieDB_AutoFanartAmount,
                    AutoPosters = legacy.MovieDB_AutoPosters,
                    AutoPostersAmount = legacy.MovieDB_AutoPostersAmount
                },
            Import =
                new ImportSettings
                {
                    VideoExtensions = legacy.VideoExtensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
                    DefaultSeriesLanguage = legacy.DefaultSeriesLanguage,
                    DefaultEpisodeLanguage = legacy.DefaultEpisodeLanguage,
                    RunOnStart = legacy.RunImportOnStart,
                    ScanDropFoldersOnStart = legacy.ScanDropFoldersOnStart,
                    Hash_CRC32 = legacy.Hash_CRC32,
                    Hash_MD5 = legacy.Hash_MD5,
                    Hash_SHA1 = legacy.Hash_SHA1,
                    UseExistingFileWatchedStatus = legacy.Import_UseExistingFileWatchedStatus
                },
            Plex =
                new PlexSettings
                {
                    ThumbnailAspects = legacy.PlexThumbnailAspects,
                    Libraries = legacy.Plex_Libraries.ToList(),
                    Token = legacy.Plex_Token,
                    Server = legacy.Plex_Server
                },
            AutoGroupSeries = legacy.AutoGroupSeries,
            AutoGroupSeriesRelationExclusions = legacy.AutoGroupSeriesRelationExclusions.Replace("alternate", "alternative", StringComparison.InvariantCultureIgnoreCase).Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            AutoGroupSeriesUseScoreAlgorithm = legacy.AutoGroupSeriesUseScoreAlgorithm,
            FileQualityFilterEnabled = legacy.FileQualityFilterEnabled,
            FileQualityPreferences = legacy.FileQualityFilterPreferences,
            LanguagePreference = legacy.LanguagePreference.Split(',').ToList(),
            EpisodeLanguagePreference = legacy.EpisodeLanguagePreference.Split(',').ToList(),
            LanguageUseSynonyms = legacy.LanguageUseSynonyms,
            CloudWatcherTime = legacy.CloudWatcherTime,
            EpisodeTitleSource = legacy.EpisodeTitleSource,
            SeriesDescriptionSource = legacy.SeriesDescriptionSource,
            SeriesNameSource = legacy.SeriesNameSource,
            TraktTv = new TraktSettings
            {
                Enabled = legacy.Trakt_IsEnabled,
                PIN = legacy.Trakt_PIN,
                AuthToken = legacy.Trakt_AuthToken,
                RefreshToken = legacy.Trakt_RefreshToken,
                TokenExpirationDate = legacy.Trakt_TokenExpirationDate,
                UpdateFrequency = legacy.Trakt_UpdateFrequency,
                SyncFrequency = legacy.Trakt_SyncFrequency
            },
            UpdateChannel = legacy.UpdateChannel,
            Linux = new LinuxSettings
            {
                UID = legacy.Linux_UID, GID = legacy.Linux_GID, Permission = legacy.Linux_Permission
            },
            TraceLog = legacy.TraceLog,
            Database = new DatabaseSettings
            {
                MySqliteDirectory = legacy.MySqliteDirectory,
                DatabaseBackupDirectory = legacy.DatabaseBackupDirectory,
                Type = legacy.DatabaseType
            }
        };

        switch (legacy.DatabaseType)
        {
            case Constants.DatabaseType.MySQL:
                settings.Database.Username = legacy.MySQL_Username;
                settings.Database.Password = legacy.MySQL_Password;
                settings.Database.Schema = legacy.MySQL_SchemaName;
                settings.Database.Host = legacy.MySQL_Hostname;
                break;
            case Constants.DatabaseType.PostgreSQL:
                settings.Database.Username = legacy.DatabaseUsername;
                settings.Database.Password = legacy.DatabasePassword;
                settings.Database.Schema = legacy.DatabaseName;
                settings.Database.Host = legacy.DatabaseServer;
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
            results.ForEach(s => _logger.LogError(s.ErrorMessage));
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

    private static IEnumerable<object> ToEnum(Array a)
    {
        for (var i = 0; i < a.Length; i++) { yield return a.GetValue(i); }
    }

    public void DebugSettingsToLog()
    {
        #region System Info

        _logger.LogInformation("-------------------- SYSTEM INFO -----------------------");

        var a = Assembly.GetEntryAssembly();
        try
        {
            var serverVersion = new ComponentVersion { Version = Utils.GetApplicationVersion() };
            var extraVersionDict = Utils.GetApplicationExtraVersion();
            if (extraVersionDict.TryGetValue("tag", out var tag))
                serverVersion.Tag = tag;
            if (extraVersionDict.TryGetValue("commit", out var commit))
                serverVersion.Commit = commit;
            if (extraVersionDict.TryGetValue("channel", out var rawChannel))
                if (Enum.TryParse<ReleaseChannel>(rawChannel, true, out var channel))
                    serverVersion.ReleaseChannel = channel;
                else
                    serverVersion.ReleaseChannel = ReleaseChannel.Debug;
            if (extraVersionDict.TryGetValue("date", out var dateText) && DateTime.TryParse(dateText, out var releaseDate))
                serverVersion.ReleaseDate = releaseDate;

            _logger.LogInformation("Shoko Server Version: v{ApplicationVersion}, Channel: {Channel}, Tag: {Tag}, Commit: {Commit}", serverVersion.Version,
                serverVersion.ReleaseChannel, serverVersion.Tag, serverVersion.Commit);
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
            // oopps, can't create file
            logger.Warn("Error in log (database version lookup: {0}", ex.Message);
        }
        */
        _logger.LogInformation("Operating System: {OSInfo}", Utils.GetOSInfo());

        try
        {
            var mediaInfoVersion = "**** MediaInfo Not found *****";

            var tempVersion = MediaInfo.GetVersion();
            if (tempVersion != null) mediaInfoVersion = $"MediaInfo: {tempVersion}";
            _logger.LogInformation(mediaInfoVersion);

            var hasherInfoVersion = "**** Hasher - DLL NOT found *****";

            tempVersion = Hasher.GetVersion();
            if (tempVersion != null) hasherInfoVersion = $"RHash: {tempVersion}";
            _logger.LogInformation(hasherInfoVersion);
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
