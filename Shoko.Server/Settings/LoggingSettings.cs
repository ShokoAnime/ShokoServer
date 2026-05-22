using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Logging.Models;

namespace Shoko.Server.Settings;

public class LoggingSettings
{
    /// <summary>
    /// Indicates that the log rotation should be used.
    /// </summary>
    [Display(Name = "Use Log Rotation")]
    [DefaultValue(true)]
    [RequiresRestart]
    [EnvironmentVariable("LOGGING_ROTATION_ENABLED")]
    public bool RotationEnabled { get; set; } = true;

    /// <summary>
    /// Indicates that we should compress the log files.
    /// </summary>
    [DefaultValue(true)]
    [EnvironmentVariable("LOGGING_ROTATION_COMPRESS")]
    public bool RotationCompress { get; set; } = true;

    /// <summary>
    /// Indicates that we should delete older log files.
    /// </summary>
    [DefaultValue(true)]
    [EnvironmentVariable("LOGGING_ROTATION_DELETE_ENABLED")]
    public bool RotationDeleteEnabled { get; set; } = true;

    /// <summary>
    /// Number of days to keep log files before deleting.
    /// </summary>
    [Display(Name = "Keep period (days)")]
    [EnvironmentVariable("LOGGING_ROTATION_DELETE_DAYS")]
    [DefaultValue(null)]
    [Range(0, int.MaxValue)]
    public int? RotationDeleteDays { get; set; }

    /// <summary>
    /// Enable trace logging in the log file and web UI live console.
    /// </summary>
    [Display(Name = "Enable Trace Logging")]
    [EnvironmentVariable("SHOKO_TRACE_LOG")]
    public bool TraceLog { get; set; }

    /// <summary>
    /// Console layout format for runtime logs.
    /// </summary>
    [DefaultValue(LogSerializeFormat.Console)]
    [EnvironmentVariable("LOGGING_CONSOLE_FORMAT")]
    public LogSerializeFormat ConsoleFormat { get; set; } = LogSerializeFormat.Console;

    /// <summary>
    /// Optional user-defined log level override rules keyed by logger pattern.
    /// </summary>
    [List(ListType = DisplayListType.ComplexInline)]
    [EnvironmentVariable("LOGGING_LOG_LEVEL_RULES", AllowOverride = true)]
    public List<LogLevelRuleConfiguration> LogLevelRules { get; set; } = [];
}
