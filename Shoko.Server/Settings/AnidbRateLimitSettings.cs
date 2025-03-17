using System.ComponentModel.DataAnnotations;
using Shoko.Plugin.Abstractions.Config.Attributes;
using Shoko.Plugin.Abstractions.Config.Enums;

namespace Shoko.Server.Settings;

/// <summary>
/// Settings for rate limiting the Anidb provider.
/// </summary>
public class AnidbRateLimitSettings
{
    /// <summary>
    /// Base rate in seconds for request and the multipliers.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Small)]
    [Display(Name = "Base Rate (seconds)")]
    [Range(2, 60)]
    public int BaseRateInSeconds { get; set; } = 2;

    /// <summary>
    /// Slow rate multiplier applied to the <seealso cref="BaseRateInSeconds">Base Rate</seealso>.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Small)]
    [Range(2, 99)]
    public int SlowRateMultiplier { get; set; } = 3;

    /// <summary>
    /// Slow rate period multiplier applied to the <seealso cref="BaseRateInSeconds">Base Rate</seealso>.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Small)]
    [Range(2, 99)]
    public int SlowRatePeriodMultiplier { get; set; } = 5;

    /// <summary>
    /// Reset period multiplier applied to the <seealso cref="BaseRateInSeconds">Base Rate</seealso>.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Small)]
    [Range(2, 99)]
    public int ResetPeriodMultiplier { get; set; } = 60;
}
