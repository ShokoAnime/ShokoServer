using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;

namespace Shoko.Server.Settings;

/// <summary>
/// Settings for rate limiting the AniList provider.
/// </summary>
public class AnilistRateLimitSettings
{
    /// <summary>
    /// Maximum number of requests allowed within the rate limit window.
    /// Together with <see cref="WindowDurationMs"/> this defaults to one
    /// request every four seconds, the same pace as AniDB, which is far below
    /// what AniList allows. Raise it if you want faster updates; the limiter
    /// never exceeds the limit AniList advertises in its response headers.
    /// </summary>
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(Size = DisplayElementSize.Small, Advanced = true)]
    [Display(Name = "Max Requests Per Window")]
    [Range(1, 90)]
    [EnvironmentVariable("ANILIST_RATE_LIMIT_MAX_REQUESTS_PER_WINDOW")]
    public int MaxRequestsPerWindow { get; set; } = 1;

    /// <summary>
    /// Duration of the sliding rate limit window in milliseconds. Defaults to
    /// four seconds.
    /// </summary>
    [Badge("Debug", Theme = DisplayColorTheme.Warning)]
    [Visibility(Size = DisplayElementSize.Small, Advanced = true)]
    [Display(Name = "Window Duration (ms)")]
    [Range(1000, 120000)]
    [EnvironmentVariable("ANILIST_RATE_LIMIT_WINDOW_DURATION_MS")]
    public int WindowDurationMs { get; set; } = 4000;
}
