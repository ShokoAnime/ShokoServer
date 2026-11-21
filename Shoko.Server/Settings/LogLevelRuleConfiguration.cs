using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Attributes;
using Shoko.Abstractions.Config.Enums;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.UI.Attributes;
using Shoko.Abstractions.UI.Enums;

namespace Shoko.Server.Settings;

/// <summary>
/// A custom log level rule from the logging settings. It replaces a core or plugin default for
/// the same pattern; one with neither level turns that default off.
/// </summary>
public class LogLevelRuleConfiguration : IEquatable<LogLevelRuleConfiguration>
{
    /// <inheritdoc/>
    [Visibility(DisplayVisibility.Hidden), Key]
    [DefaultValue("New Log Level Rule")]
    public string Key
    {
        get => !string.IsNullOrWhiteSpace(LoggerNamePattern)
            ? LoggerNamePattern
            : "New Log Level Rule";
    }

    /// <summary>
    /// Logger name pattern targeted by this rule.
    /// </summary>
    [Display(Name = "Pattern")]
    [Visibility(Size = DisplayElementSize.Full)]
    [DefaultValue("")]
    [Required]
    [MinLength(1)]
    public string LoggerNamePattern { get; set; } = string.Empty;

    /// <summary>
    /// Optional lowest level written for the matching loggers, below the
    /// levels the log file, the console and the live log are set to. Must be
    /// above <see cref="MaxLevel"/> when both are set.
    /// </summary>
    [Display(Name = "Min Level")]
    [DefaultValue(null)]
    public LogLevel? MinLevel { get; set; }

    /// <summary>
    /// Optional highest level dropped for the matching loggers.
    /// </summary>
    [Display(Name = "Max Level")]
    [DefaultValue(LogLevel.Information)]
    public LogLevel? MaxLevel { get; set; } = LogLevel.Information;

    [ConfigurationAction(ConfigurationActionType.Validate)]
    public static ConfigurationActionResult Validate(LogLevelRuleConfiguration config, IConfigurationService configurationService, IPluginManager pluginManager)
    {
        var errors = new Dictionary<string, IReadOnlyList<string>>();
        if (string.IsNullOrWhiteSpace(config.LoggerNamePattern))
            errors.Add(nameof(LoggerNamePattern), [$"{nameof(LoggerNamePattern)} cannot be empty."]);
        if (config is { MinLevel: { } min, MaxLevel: { } max } && min <= max)
            errors.Add(nameof(MinLevel), [$"{nameof(MinLevel)} must be above {nameof(MaxLevel)}."]);
        return new() { ValidationErrors = errors };
    }

    public override int GetHashCode()
        => HashCode.Combine(LoggerNamePattern, MinLevel, MaxLevel);

    public override bool Equals(object? obj)
        => obj is LogLevelRuleConfiguration other && Equals(other);

    public bool Equals(LogLevelRuleConfiguration? other)
        => other is not null && GetHashCode() == other.GetHashCode();
}
