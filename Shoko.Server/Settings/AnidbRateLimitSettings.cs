using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.Settings;

/// <summary>
/// Settings for rate limiting the Anidb provider.
/// </summary>
public class AnidbRateLimitSettings
{
    /// <summary>
    /// Base rate in seconds for request and the multipliers.
    /// </summary>
    [Range(2, 1000)]
    public int BaseRateInSeconds { get; set; } = 2;

    /// <summary>
    /// Slow rate multiplier applied to the <seealso cref="BaseRateInSeconds"/>.
    /// </summary>
    [Range(2, 1000)]
    public int SlowRateMultiplier { get; set; } = 3;

    /// <summary>
    /// Slow rate period multiplier applied to the <seealso cref="BaseRateInSeconds"/>.
    /// </summary>
    [Range(2, 1000)]
    public int SlowRatePeriodMultiplier { get; set; } = 5;

    /// <summary>
    /// Reset period multiplier applied to the <seealso cref="BaseRateInSeconds"/>.
    /// </summary>
    [Range(2, 1000)]
    public int ResetPeriodMultiplier { get; set; } = 60;
}
