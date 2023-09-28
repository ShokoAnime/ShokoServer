using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Server.ImageDownload;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

namespace Shoko.Server.Settings;

public class ServerSettings : IServerSettings
{
    // Increment this when a new migration is added
    public int SettingsVersion { get; set; } = SettingsMigrations.Version;

    [Range(1, 65535, ErrorMessage = "PluginAutoWatchThreshold must be between 1 and 65535")]
    public ushort ServerPort { get; set; } = 8111;

    [Range(0, 1, ErrorMessage = "PluginAutoWatchThreshold must be between 0 and 1")]
    public double PluginAutoWatchThreshold { get; set; } = 0.89;

    public int CachingDatabaseTimeout { get; set; } = 180;

    public string Culture { get; set; } = "en";

    /// <summary>
    /// Store json settings inside string
    /// </summary>
    public string WebUI_Settings { get; set; } = "";

    /// <summary>
    /// FirstRun indicates if DB was configured or not, as it needed as backend for user authentication
    /// </summary>
    public bool FirstRun { get; set; } = true;

    public int LegacyRenamerMaxEpisodeLength { get; set; } = 33;

    public LogRotatorSettings LogRotator { get; set; } = new();

    public DatabaseSettings Database { get; set; } = new();

    public AniDbSettings AniDb { get; set; } = new();

    public WebCacheSettings WebCache { get; set; } = new();

    public TvDBSettings TvDB { get; set; } = new();

    public MovieDbSettings MovieDb { get; set; } = new();

    public ImportSettings Import { get; set; } = new();

    public PlexSettings Plex { get; set; } = new();

    public PluginSettings Plugins { get; set; } = new();

    public ConnectivitySettings Connectivity { get; set; } = new();

    public bool AutoGroupSeries { get; set; }

    public List<string> AutoGroupSeriesRelationExclusions { get; set; } = new() { "same setting", "character" };

    public bool AutoGroupSeriesUseScoreAlgorithm { get; set; }

    public bool FileQualityFilterEnabled { get; set; }

    public FileQualityPreferences FileQualityPreferences { get; set; } = new();

    private List<string> _languagePreference = new()
    {
        "x-jat",
        "en",
    };

    public List<string> LanguagePreference
    {
        get => _languagePreference;
        set
        {
            _languagePreference = value;
            Languages.PreferredNamingLanguages = null;
            Languages.PreferredNamingLanguageNames = null;
        }
    }

    private List<string> _episodeLanguagePreference = new()
    {
        "en",
    };

    public List<string> EpisodeLanguagePreference {
        get => _episodeLanguagePreference;
        set
        {
            _episodeLanguagePreference = value;
            Languages.PreferredEpisodeNamingLanguages = null;
        }
    }

    public bool LanguageUseSynonyms { get; set; } = true;

    public int CloudWatcherTime { get; set; } = 3;

    public DataSourceType EpisodeTitleSource { get; set; } = DataSourceType.AniDB;
    public DataSourceType SeriesDescriptionSource { get; set; } = DataSourceType.AniDB;
    public DataSourceType SeriesNameSource { get; set; } = DataSourceType.AniDB;

    [JsonIgnore] public string _ImagesPath;

    /// <inheritdoc />
    public string ImagesPath
    {
        get => _ImagesPath;
        set
        {
            _ImagesPath = value;
            ServerState.Instance.BaseImagePath = ImageUtils.GetBaseImagesPath();
        }
    }

    /// <inheritdoc/> />
    public bool LoadImageMetadata { get; set; } = false;

    public TraktSettings TraktTv { get; set; } = new();

    public string UpdateChannel { get; set; } = "Stable";

    public LinuxSettings Linux { get; set; } = new();

    public bool TraceLog { get; set; }

    public bool SentryOptOut { get; set; } = false;
}
