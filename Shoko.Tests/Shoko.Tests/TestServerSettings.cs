using System.Collections.Generic;
using Shoko.Models;
using Shoko.Models.Enums;
using Shoko.Server.Settings;

namespace Shoko.Tests;

public class TestServerSettings
{
    public ushort ServerPort { get; set; } = 8111;
    public double PluginAutoWatchThreshold { get; set; } = 0.89;
    public int CachingDatabaseTimeout { get; set; } = 180;
    public string Culture { get; set; } = "en";
    public string WebUI_Settings { get; set; } = "";
    public bool FirstRun { get; set; } = true;
    public int LegacyRenamerMaxEpisodeLength { get; set; } = 33;
    public LogRotatorSettings LogRotator { get; set; } = new();
    public DatabaseSettings Database { get; set; } = new();
    public AniDbSettings AniDb { get; set; } = new();
    public WebCacheSettings WebCache { get; set; } = new();
    public TvDBSettings TvDB { get; set; } = new();
    public TMDBSettings TMDB { get; set; } = new();
    public ImportSettings Import { get; set; } = new();
    public PlexSettings Plex { get; set; } = new();
    public PluginSettings Plugins { get; set; } = new();
    public bool AutoGroupSeries { get; set; }
    public string AutoGroupSeriesRelationExclusions { get; set; } = "same setting|character";
    public bool AutoGroupSeriesUseScoreAlgorithm { get; set; }
    public bool FileQualityFilterEnabled { get; set; }
    public FileQualityPreferences FileQualityPreferences { get; set; } = new();
    public List<string> LanguagePreference { get; set; } = new() { "x-jat", "en" };
    public string EpisodeLanguagePreference { get; set; } = string.Empty;
    public bool LanguageUseSynonyms { get; set; } = true;
    public int CloudWatcherTime { get; set; } = 3;
    public DataSourceType EpisodeTitleSource { get; set; } = DataSourceType.AniDB;
    public DataSourceType SeriesDescriptionSource { get; set; } = DataSourceType.AniDB;
    public DataSourceType SeriesNameSource { get; set; } = DataSourceType.AniDB;
    public string ImagesPath { get; set; }
    public TraktSettings TraktTv { get; set; } = new();
    public string UpdateChannel { get; set; } = "Stable";
    public LinuxSettings Linux { get; set; } = new();
    public bool TraceLog { get; set; }
}
