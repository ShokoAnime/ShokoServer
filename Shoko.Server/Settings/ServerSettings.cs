using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using Shoko.Models;
using Shoko.Plugin.Abstractions.Config;

namespace Shoko.Server.Settings;

[Display(Name = "Shoko Core")]
public class ServerSettings : IServerSettings, INewtonsoftJsonConfiguration
{
    // Increment this when a new migration is added
    [UsedImplicitly]
    public int SettingsVersion { get; set; } = SettingsMigrations.Version;

    /// <inheritdoc />
    public string ImagesPath { get; set; }

    public string Culture { get; set; } = "en";

    public bool FirstRun { get; set; } = true;

    public bool AutoGroupSeries { get; set; }

    public List<string> AutoGroupSeriesRelationExclusions { get; set; } = ["same setting", "character", "other"];

    public bool AutoGroupSeriesUseScoreAlgorithm { get; set; }

    public bool LoadImageMetadata { get; set; } = false;

    public int CachingDatabaseTimeout { get; set; } = 180;

    public DatabaseSettings Database { get; set; } = new();

    public QuartzSettings Quartz { get; set; } = new();

    public ConnectivitySettings Connectivity { get; set; } = new();

    public LanguageSettings Language { get; set; } = new();

    public AniDbSettings AniDb { get; set; } = new();

    public TMDBSettings TMDB { get; set; } = new();

    public ImportSettings Import { get; set; } = new();

    public PlexSettings Plex { get; set; } = new();

    public TraktSettings TraktTv { get; set; } = new();

    public PluginSettings Plugins { get; set; } = new();

    public bool FileQualityFilterEnabled { get; set; }

    public FileQualityPreferences FileQualityPreferences { get; set; } = new();

    public LogRotatorSettings LogRotator { get; set; } = new();

    public LinuxSettings Linux { get; set; } = new();

    public WebSettings Web { get; set; } = new();

    public string WebUI_Settings { get; set; } = "";

    public bool TraceLog { get; set; }

    public bool SentryOptOut { get; set; } = false;
}
