using System.Collections.Generic;
using Shoko.Models;

namespace Shoko.Server.Settings;

public interface IServerSettings
{
    /// <summary>
    /// Path where the images are stored. If set to <c>null</c> then it will use
    /// the default location.
    /// </summary>
    /// <remarks>
    /// The default location is the "Images" folder in the same directory as the executable.
    /// </remarks>
    string ImagesPath { get; set; }

    /// <summary>
    /// The culture to use when formatting strings.
    /// </summary>
    string Culture { get; set; }

    /// <summary>
    /// Indicates this the first time the server has been started.
    /// </summary>
    bool FirstRun { get; set; }

    /// <summary>
    /// Auto group series based on the detected relation.
    /// </summary>
    bool AutoGroupSeries { get; set; }

    /// <summary>
    /// The list of relation types to exclude from auto grouping.
    /// </summary>
    List<string> AutoGroupSeriesRelationExclusions { get; set; }

    /// <summary>
    /// Use the score algorithm for auto grouping.
    /// </summary>
    bool AutoGroupSeriesUseScoreAlgorithm { get; set; }

    /// <summary>
    /// Load image metadata from the file system and send to the clients.
    /// </summary>
    bool LoadImageMetadata { get; set; }

    /// <summary>
    /// The timeout in seconds for the caching database.
    /// </summary>
    int CachingDatabaseTimeout { get; set; }

    /// <summary>
    /// The database settings.
    /// </summary>
    DatabaseSettings Database { get; set; }

    /// <summary>
    /// The Quartz.NET settings.
    /// </summary>
    QuartzSettings Quartz { get; set; }

    /// <summary>
    /// The connectivity settings.
    /// </summary>
    ConnectivitySettings Connectivity { get; set; }

    /// <summary>
    /// The language settings.
    /// </summary>
    LanguageSettings Language { get; set; }

    /// <summary>
    /// The AniDB settings.
    /// </summary>
    AniDbSettings AniDb { get; set; }

    /// <summary>
    /// The TMDB settings.
    /// </summary>
    TMDBSettings TMDB { get; set; }

    /// <summary>
    /// The import settings.
    /// </summary>
    ImportSettings Import { get; set; }

    /// <summary>
    /// The Plex settings.
    /// </summary>
    PlexSettings Plex { get; set; }

    /// <summary>
    /// The TraktTV settings.
    /// </summary>
    TraktSettings TraktTv { get; set; }

    /// <summary>
    /// The plugin settings.
    /// </summary>
    PluginSettings Plugins { get; set; }

    /// <summary>
    /// Filter out video files based on quality.
    /// </summary>
    bool FileQualityFilterEnabled { get; set; }

    /// <summary>
    /// The file quality preferences.
    /// </summary>
    FileQualityPreferences FileQualityPreferences { get; set; }

    /// <summary>
    /// The log rotator settings.
    /// </summary>
    LogRotatorSettings LogRotator { get; set; }

    /// <summary>
    /// Linux runtime settings. Windows users can ignore this.
    /// </summary>
    LinuxSettings Linux { get; set; }

    /// <summary>
    /// Configure settings related to the HTTP(S) hosting.
    /// </summary>
    WebSettings Web { get; set; }

    /// <summary>
    /// The web UI settings, as a stringified JSON object.
    /// </summary>
    string WebUI_Settings { get; set; }

    /// <summary>
    /// Indicates if trace logging enabled.
    /// </summary>
    bool TraceLog { get; set; }

    /// <summary>
    /// Opt out of sending error reports to Sentry.
    /// </summary>
    bool SentryOptOut { get; set; }
}
