using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Plugin.Tmdb;

/// <summary>
/// Settings for rate limiting the requests sent to TMDB.
/// </summary>
public class TmdbRateLimitConfiguration
{
    /// <summary>
    /// Maximum number of requests allowed within the rate limit window. TMDB
    /// allows up to 40 requests per second; this defaults to 10 to spare
    /// end-user hardware the data processing that follows each request.
    /// </summary>
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(Size = DisplayElementSize.Small, Advanced = true)]
    [Display(Name = "Max Requests Per Window")]
    [Range(1, 40)]
    [EnvironmentVariable("TMDB_RATE_LIMIT_MAX_REQUESTS_PER_WINDOW")]
    public int MaxRequestsPerWindow { get; set; } = 10;

    /// <summary>
    /// Duration of the sliding rate limit window in milliseconds.
    /// </summary>
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(Size = DisplayElementSize.Small, Advanced = true)]
    [Display(Name = "Window Duration (ms)")]
    [Range(100, 10000)]
    [EnvironmentVariable("TMDB_RATE_LIMIT_WINDOW_DURATION_MS")]
    public int WindowDurationMs { get; set; } = 1000;
}
