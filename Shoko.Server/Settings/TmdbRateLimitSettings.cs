using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Server.Settings;

/// <summary>
/// Settings for rate limiting the TMDB provider.
/// </summary>
public class TmdbRateLimitSettings
{
    /// <summary>
    /// Maximum number of requests allowed within the rate limit window.
    /// TMDB enforces a maximum of 40 requests per second; this defaults to 10 to avoid overwhelming
    /// end-user hardware with API requests and the data processing that follows each one.
    ///</summary>
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(Size = DisplayElementSize.Small, Advanced = true)]
    [Display(Name = "Max Requests Per Window")]
    [Range(1, 40)]
    [JsonProperty("TMDB_API_Limits")]
    public int MaxRequestsPerWindow { get; set; } = 10;

    /// <summary>
    /// Duration of the sliding rate limit window in milliseconds.
    /// </summary>
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(Size = DisplayElementSize.Small, Advanced = true)]
    [Display(Name = "Window Duration (ms)")]
    [Range(100, 10000)]
    public int WindowDurationMs { get; set; } = 1000;
}
