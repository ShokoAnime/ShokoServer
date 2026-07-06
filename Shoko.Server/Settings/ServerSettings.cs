using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Plugin;

namespace Shoko.Server.Settings;

/// <summary>
/// Core Settings for the server.
/// </summary>
[Display(Name = "Core Settings")]
[StorageLocation(RelativePath = "settings-server.json")]
[Section(DisplaySectionType.Tab, AppendFloatingSectionsAtEnd = true, DefaultSectionName = "Misc.")]
public class ServerSettings : IServerSettings, INewtonsoftJsonConfiguration, IHiddenConfiguration, IConfigurationWithMigrations, IConfigurationWithCustomValidation<ServerSettings>
{
    public static string ApplyMigrations(string config, IApplicationPaths applicationPaths)
        => FixNonEmittedDefaults(SettingsMigrations.MigrateSettings(config, applicationPaths));

    public static readonly JsonSerializerSettings SerializationSettings = new()
    {
        Formatting = Formatting.Indented,
        DefaultValueHandling = DefaultValueHandling.Include,
        MissingMemberHandling = MissingMemberHandling.Ignore,
        Converters = [new StringEnumConverter()]
    };

    /// <summary>
    /// Fix the behavior of missing members in pre-4.0
    /// </summary>
    /// <param name="settings"></param>
    private static string FixNonEmittedDefaults(string settings)
    {
        if (settings.Contains("\"FirstRun\":")) return settings;
        var deserializerSettings = new JsonSerializerSettings
        {
            Converters = [new StringEnumConverter()],
            Error = (sender, args) => { args.ErrorContext.Handled = true; },
            ObjectCreationHandling = ObjectCreationHandling.Replace,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            DefaultValueHandling = DefaultValueHandling.Populate
        };
        var result = JsonConvert.DeserializeObject<ServerSettings>(settings, deserializerSettings);
        return JsonConvert.SerializeObject(result, SerializationSettings);
    }

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Validate(ServerSettings config, IConfigurationService configurationService, IPluginManager pluginManager)
    {
        var errors = new Dictionary<string, IReadOnlyList<string>>();
        if (!config.FirstRun && config.AniDb.Username is not { Length: > 0 })
            errors.Add("AniDb.Username", ["AniDb.Username cannot be empty or null if FirstRun is set to false."]);
        if (!config.FirstRun && config.AniDb.Password is not { Length: > 0 })
            errors.Add("AniDb.Password", ["AniDb.Password cannot be empty or null if FirstRun is set to false."]);
        return errors;
    }

    /// <summary>
    /// Settings version. Will be incremented by the system. DO NOT TOUCH.
    /// </summary>
    // Increment this when a new migration is added
    [UsedImplicitly]
    [Visibility(DisplayVisibility.Hidden)]
    public int SettingsVersion { get; set; } = SettingsMigrations.Version;

    /// <inheritdoc />
    [EnvironmentVariable("SHOKO_IMAGES_PATH", AllowOverride = true)]
    [RequiresRestart]
    public string? ImagesPath { get; set; }

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

    /// <summary>
    /// Configure the image settings for Shoko.
    /// </summary>
    public ImageSettings Image { get; set; } = new();

    /// <summary>
    /// The maximum number of seconds to cache a repository during startup.
    /// </summary>
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(Advanced = true)]
    [Display(Name = "Caching Database Timeout (seconds)")]
    [EnvironmentVariable("DB_CACHING_TIMEOUT")]
    [Range(1, 600, ErrorMessage = "Caching Database Timeout must be between 1 and 600")]
    public int CachingDatabaseTimeout { get; set; } = 180;

    /// <summary>
    /// Minimum number of .NET thread pool worker/I-O completion threads to
    /// keep warm. Raising this avoids the runtime's gradual "hill-climbing"
    /// thread injection delay under bursts of concurrent, blocking, or
    /// synchronous work. Set to 0 to leave the runtime's own default
    /// untouched. Positive values are used directly. Negative values are
    /// treated as a multiplier against the CPU count, offset by one, e.g.
    /// -1 means CPU count x 2, -2 means CPU count x 3, up to -9 meaning
    /// CPU count x 10.
    /// </summary>
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    [Display(Name = "Thread Pool Minimum Threads")]
    [RequiresRestart]
    [EnvironmentVariable("THREADPOOL_MIN_THREADS")]
    [Range(-9, int.MaxValue)]
    public int ThreadPoolMinThreads { get; set; }

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
    /// queue database.
    /// </summary>
    public DatabaseSettings Database { get; set; } = new();

    /// <inheritdoc />
    public QueueProcessorSettings Queue { get; set; } = new();

    /// <inheritdoc />
    public ConnectivitySettings Connectivity { get; set; } = new();

    /// <inheritdoc />
    public LanguageSettings Language { get; set; } = new();

    /// <inheritdoc />
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    public PlexSettings Plex { get; set; } = new();

    /// <inheritdoc />
    public PluginSettings Plugins { get; set; } = new();

    /// <summary>Release-level comparison preferences used by the release management system.</summary>
    public ReleaseComparisonPreferences ReleaseComparisonPreferences { get; set; } = new();

    /// <inheritdoc />
    [Display(Name = "Logging")]
    public LoggingSettings Logging { get; set; } = new();

    /// <inheritdoc />
    [Badge("Advanced", Theme = DisplayColorTheme.Primary)]
    [Visibility(Advanced = true)]
    public LinuxSettings Linux { get; set; } = new();

    /// <inheritdoc />
    public WebSettings Web { get; set; } = new();

    /// <inheritdoc />
    [SectionName("Web UI")]
    [Display(Name = "Settings")]
    [EnvironmentVariable("SHOKO_WEBUI_SETTINGS", AllowOverride = true)]
    [CodeEditor(CodeEditorLanguage.Json, AutoFormatOnLoad = true)]
    public string WebUI_Settings { get; set; } = "";

    /// <summary>
    /// Dump the settings to the log file on startup.
    /// </summary>
    /// <remarks>
    /// This is useful for debugging issues with the server configuration.
    /// </remarks>
    [EnvironmentVariable("SHOKO_DUMP_SETTINGS_ON_START")]
    [Display(Name = "Dump Settings On Start")]
    [DefaultValue(true)]
    public bool DumpSettingsOnStart { get; set; } = true;

    /// <summary>
    /// Disable Sentry error reporting in the server. This will not affect the
    /// web UI error reporting.
    /// </summary>
    [RequiresRestart]
    [EnvironmentVariable("SENTRY_OPT_OUT")]
    [Display(Name = "Sentry Opt-Out")]
    public bool SentryOptOut { get; set; } = false;
}
