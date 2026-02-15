using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;

namespace Shoko.Server.Settings;

public class LogRotatorSettings
{
    /// <summary>
    /// Indicates that the log rotation should be used.
    /// </summary>
    [Display(Name = "Use Log Rotation")]
    [DefaultValue(true)]
    [RequiresRestart]
    [EnvironmentVariable("LOG_ROTATOR_ENABLED")]
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Indicates that we should compress the log files.
    /// </summary>
    [DefaultValue(true)]
    [RequiresRestart]
    [EnvironmentVariable("LOG_ROTATOR_ZIP")]
    public bool Zip { get; set; } = true;

    /// <summary>
    /// Indicates that we should delete older log files.
    /// </summary>
    [DefaultValue(true)]
    [RequiresRestart]
    [EnvironmentVariable("LOG_ROTATOR_DELETE")]
    public bool Delete { get; set; } = true;

    /// <summary>
    /// Number of days to keep log files before deleting. This should be set to
    /// a value to enable deletion.
    /// </summary>
    [Display(Name = "Keep period (days)")]
    [RequiresRestart]
    [EnvironmentVariable("LOG_ROTATOR_DELETE_DAYS")]
    [DefaultValue(null)]
    [RegularExpression(@"^\d*$", ErrorMessage = "Must be a number")]
    public string Delete_Days { get; set; } = "";
}
