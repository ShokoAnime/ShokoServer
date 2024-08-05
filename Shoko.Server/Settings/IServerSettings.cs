﻿using System.Collections.Generic;
using Shoko.Models;
using Shoko.Models.Enums;

namespace Shoko.Server.Settings;

public interface IServerSettings
{
    static string ApplicationPath { get; set; }
    ushort ServerPort { get; set; }
    string Culture { get; set; }
    string WebUI_Settings { get; set; }
    bool FirstRun { get; set; }
    LanguageSettings Language { get; set; }
    int LegacyRenamerMaxEpisodeLength { get; set; }
    LogRotatorSettings LogRotator { get; set; }
    DatabaseSettings Database { get; set; }
    QuartzSettings Quartz { get; set; }
    AniDbSettings AniDb { get; set; }
    WebCacheSettings WebCache { get; set; }
    TvDBSettings TvDB { get; set; }
    TMDBSettings TMDB { get; set; }
    ImportSettings Import { get; set; }
    PlexSettings Plex { get; set; }
    PluginSettings Plugins { get; set; }
    TraktSettings TraktTv { get; set; }
    LinuxSettings Linux { get; set; }
    FileQualityPreferences FileQualityPreferences { get; set; }
    ConnectivitySettings Connectivity { get; set; }
    bool AutoGroupSeries { get; set; }
    List<string> AutoGroupSeriesRelationExclusions { get; set; }
    bool AutoGroupSeriesUseScoreAlgorithm { get; set; }
    bool FileQualityFilterEnabled { get; set; }
    /// <summary>
    /// Path where the images are stored. If set to <c>null</c> then it will use
    /// the default location.
    /// </summary>
    string ImagesPath { get; set; }
    /// <summary>
    /// Load image metadata from the file system and send to the clients.
    /// </summary>
    bool LoadImageMetadata { get; set; }
    string UpdateChannel { get; set; }
    bool TraceLog { get; set; }
    int CachingDatabaseTimeout { get; set; }
    bool SentryOptOut { get; set; }
}
