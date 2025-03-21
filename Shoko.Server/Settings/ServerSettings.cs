using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using Shoko.Models;
using Shoko.Plugin.Abstractions.Config;
using Shoko.Plugin.Abstractions.Config.Attributes;
using Shoko.Plugin.Abstractions.Config.Enums;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Server.Settings;

/// <summary>
/// Core Settings for the server.
/// </summary>
[Display(Name = "Core Settings")]
[Section(DisplaySectionType.Tab, AppendFloatingSectionsAtEnd = true, DefaultSectionName = "Misc.")]
public class ServerSettings : IServerSettings, INewtonsoftJsonConfiguration, IHiddenConfiguration
{
    /// <summary>
    /// Settings version. Will be incremented by the system. DO NOT TOUCH.
    /// </summary>
    // Increment this when a new migration is added
    [UsedImplicitly]
    [Visibility(DisplayVisibility.Hidden)]
    public int SettingsVersion { get; set; } = SettingsMigrations.Version;

    /// <inheritdoc />
    public string ImagesPath { get; set; }

    /// <inheritdoc />
    [Visibility(DisplayVisibility.Hidden)]
    public string Culture { get; set; } = "en";

    /// <inheritdoc />
    [EnvironmentVariable("SHOKO_FIRST_RUN", AllowOverride = true)]
    [Visibility(DisplayVisibility.Hidden)]
    public bool FirstRun { get; set; } = true;

    /// <inheritdoc />
    public bool AutoGroupSeries { get; set; }

    /// <inheritdoc />
    public List<string> AutoGroupSeriesRelationExclusions { get; set; } = ["same setting", "character", "other"];

    /// <inheritdoc />
    public bool AutoGroupSeriesUseScoreAlgorithm { get; set; }

    /// <inheritdoc />
    public bool LoadImageMetadata { get; set; } = false;

    /// <summary>
    /// The maximum number of seconds to cache a repository during startup.
    /// </summary>
    [Display(Name = "Caching Database Timeout (seconds)")]
    [EnvironmentVariable("DB_CACHING_TIMEOUT")]
    [Range(1, 600, ErrorMessage = "Caching Database Timeout must be between 1 and 600")]
    public int CachingDatabaseTimeout { get; set; } = 180;

    /// <inheritdoc />
    public ImportSettings Import { get; set; } = new();

    /// <summary>
    /// Configure the information Shoko retrieves from AniDB for the series in
    /// your collection, and set your preferences for MyList options and the
    /// general updating of AniDB data.
    /// </summary>
    [Display(Name = "AniDB")]
    public AniDbSettings AniDb { get; set; } = new();

    /// <summary>
    /// Configure the information Shoko retrieves from TMDB for the series in
    /// your collection.
    /// </summary>
    public TMDBSettings TMDB { get; set; } = new();

    /// <summary>
    /// Configure the main database settings. These settings will not affect the
    /// Quartz database.
    /// </summary>
    public DatabaseSettings Database { get; set; } = new();

    /// <inheritdoc />
    public QuartzSettings Quartz { get; set; } = new();

    /// <inheritdoc />
    public ConnectivitySettings Connectivity { get; set; } = new();

    /// <inheritdoc />
    public LanguageSettings Language { get; set; } = new();

    /// <inheritdoc />
    public PlexSettings Plex { get; set; } = new();

    /// <inheritdoc />
    public TraktSettings TraktTv { get; set; } = new();

    /// <inheritdoc />
    public PluginSettings Plugins { get; set; } = new();

    /// <inheritdoc />
    public bool FileQualityFilterEnabled { get; set; }

    /// <inheritdoc />
    public FileQualityPreferences FileQualityPreferences { get; set; } = new();

    /// <inheritdoc />
    [Display(Name = "Log Rotation")]
    public LogRotatorSettings LogRotator { get; set; } = new();

    /// <inheritdoc />
    public LinuxSettings Linux { get; set; } = new();

    /// <inheritdoc />
    public WebSettings Web { get; set; } = new();

    /// <inheritdoc />
    [SectionName("Web UI")]
    [Display(Name = "Settings")]
    [EnvironmentVariable("SHOKO_WEBUI_SETTINGS", AllowOverride = true)]
    [CodeEditor(CodeLanguage.Json, AutoFormatOnLoad = true)]
    public string WebUI_Settings { get; set; } = "";

    /// <summary>
    /// Enable trace logging in the log file and web UI live console.
    /// </summary>
    [Display(Name = "Enable Trace Logging")]
    [EnvironmentVariable("SHOKO_TRACE_LOG")]
    public bool TraceLog { get; set; }

    /// <summary>
    /// Disable Sentry error reporting in the server. This will not affect the
    /// web UI error reporting.
    /// </summary>
    [RequiresRestart]
    [EnvironmentVariable("SENTRY_OPT_OUT")]
    [Display(Name = "Sentry Opt-Out")]
    public bool SentryOptOut { get; set; } = false;
}
