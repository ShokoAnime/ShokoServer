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

namespace Shoko.Server.Settings;

/// <summary>
/// Configuration for per-logger max level override rules.
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
        // no setter
        set { }
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
    /// Optional max level for this logger rule.
    /// </summary>
    [Display(Name = "Max Level")]
    [DefaultValue(LogLevel.Information)]
    public LogLevel? MaxLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Whether this rule should stop processing later rules.
    /// </summary>
    [DefaultValue(true)]
    public bool Final { get; set; } = true;

    [ConfigurationAction(ConfigurationActionType.Validate)]
    public static ConfigurationActionResult Validate(LogLevelRuleConfiguration config, IConfigurationService configurationService, IPluginManager pluginManager)
    {
        var errors = new Dictionary<string, IReadOnlyList<string>>();
        if (string.IsNullOrWhiteSpace(config.LoggerNamePattern))
            errors.Add(nameof(LoggerNamePattern), [$"{nameof(LoggerNamePattern)} cannot be empty."]);
        return new() { ValidationErrors = errors };
    }

    public override int GetHashCode()
        => HashCode.Combine(LoggerNamePattern, MaxLevel, Final);

    public override bool Equals(object? obj)
        => obj is LogLevelRuleConfiguration other && Equals(other);

    public bool Equals(LogLevelRuleConfiguration? other)
        => other is not null && GetHashCode() == other.GetHashCode();
}
