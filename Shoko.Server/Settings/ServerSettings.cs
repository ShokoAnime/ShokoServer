using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using Shoko.Models;
using Shoko.Plugin.Abstractions.Config;

namespace Shoko.Server.Settings;

/// <summary>
/// Shoko Core Settings.
/// </summary>
[Display(Name = "Shoko Core")]
public class ServerSettings : IServerSettings, INewtonsoftJsonConfiguration, IHiddenConfiguration
{
    /// <summary>
    /// Settings version. Will be incremented by the system. DO NOT TOUCH.
    /// </summary>
    // Increment this when a new migration is added
    [UsedImplicitly]
    public int SettingsVersion { get; set; } = SettingsMigrations.Version;

    /// <inheritdoc />
    public string ImagesPath { get; set; }

    /// <inheritdoc />
    public string Culture { get; set; } = "en";

    /// <inheritdoc />
    public bool FirstRun { get; set; } = true;

    /// <inheritdoc />
    public bool AutoGroupSeries { get; set; }

    /// <inheritdoc />
    public List<string> AutoGroupSeriesRelationExclusions { get; set; } = ["same setting", "character", "other"];

    /// <inheritdoc />
    public bool AutoGroupSeriesUseScoreAlgorithm { get; set; }

    /// <inheritdoc />
    public bool LoadImageMetadata { get; set; } = false;

    /// <inheritdoc />
    public int CachingDatabaseTimeout { get; set; } = 180;

    /// <inheritdoc />
    public DatabaseSettings Database { get; set; } = new();

    /// <inheritdoc />
    public QuartzSettings Quartz { get; set; } = new();

    /// <inheritdoc />
    public ConnectivitySettings Connectivity { get; set; } = new();

    /// <inheritdoc />
    public LanguageSettings Language { get; set; } = new();

    /// <inheritdoc />
    public AniDbSettings AniDb { get; set; } = new();

    /// <inheritdoc />
    public TMDBSettings TMDB { get; set; } = new();

    /// <inheritdoc />
    public ImportSettings Import { get; set; } = new();

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
    public LogRotatorSettings LogRotator { get; set; } = new();

    /// <inheritdoc />
    public LinuxSettings Linux { get; set; } = new();

    /// <inheritdoc />
    public WebSettings Web { get; set; } = new();

    /// <inheritdoc />
    public string WebUI_Settings { get; set; } = "";

    /// <inheritdoc />
    public bool TraceLog { get; set; }

    /// <inheritdoc />
    public bool SentryOptOut { get; set; } = false;
}
