using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Server.Settings;

/// <summary>
/// Settings for throttling failed authentication attempts per client and per user.
/// </summary>
public class AuthenticationThrottleSettings
{
    /// <summary>
    /// The number of failed attempts within the attempt window that are allowed before the client or
    /// user is locked out. Must be between 1 and 100. Defaults to 10.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Small)]
    [Display(Name = "Max Failed Attempts")]
    [EnvironmentVariable("SHOKO_WEB_AUTHENTICATION_THROTTLE_MAX_FAILED_ATTEMPTS")]
    [DefaultValue(10)]
    [Range(1, 100)]
    public int MaxFailedAttempts { get; set; } = 10;

    /// <summary>
    /// The sliding window in minutes in which failed attempts are counted. Must be between 1 and 1440
    /// minutes. Defaults to 15 minutes.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Small)]
    [Display(Name = "Attempt Window (minutes)")]
    [EnvironmentVariable("SHOKO_WEB_AUTHENTICATION_THROTTLE_ATTEMPT_WINDOW_MINUTES")]
    [DefaultValue(15)]
    [Range(1, 1440)]
    public int AttemptWindowMinutes { get; set; } = 15;

    /// <summary>
    /// The lockout duration in minutes applied on the first excess attempt, doubling for every attempt
    /// after that. Must be between 1 and 1440 minutes, and not exceed <see cref="MaxLockoutMinutes"/>.
    /// Defaults to 15 minutes.
    /// </summary>
    [Visibility(Size = DisplayElementSize.Small)]
    [Display(Name = "Initial Lockout (minutes)")]
    [EnvironmentVariable("SHOKO_WEB_AUTHENTICATION_THROTTLE_INITIAL_LOCKOUT_MINUTES")]
    [DefaultValue(15)]
    [Range(1, 1440)]
    public int InitialLockoutMinutes { get; set; } = 15;

    /// <summary>
    /// The maximum lockout duration in minutes, regardless of how many failed attempts have been made.
    /// Lowering it also shortens existing lockouts. Must be between 15 and 10080 minutes (7 days). Defaults
    /// to 1440 minutes (1 day).
    /// </summary>
    [Visibility(Size = DisplayElementSize.Small)]
    [Display(Name = "Max Lockout (minutes)")]
    [EnvironmentVariable("SHOKO_WEB_AUTHENTICATION_THROTTLE_MAX_LOCKOUT_MINUTES")]
    [DefaultValue(1440)]
    [Range(15, 10080)]
    public int MaxLockoutMinutes { get; set; } = 1440;
}
