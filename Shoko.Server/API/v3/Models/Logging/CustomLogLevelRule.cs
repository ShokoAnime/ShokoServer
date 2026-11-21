using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;

using AbstractLogLevelRule = Shoko.Abstractions.Logging.Models.LogLevelRule;

namespace Shoko.Server.API.v3.Models.Logging;

/// <summary>
/// A custom log level rule of the logging settings.
/// </summary>
public class CustomLogLevelRule
{
    /// <summary>
    /// The pattern of the logger names the rule applies to, such as
    /// <c>ModelContextProtocol.*</c>. A rule for the same pattern, compared
    /// ignoring case, is replaced.
    /// </summary>
    [Required]
    [MinLength(1)]
    public string LoggerNamePattern { get; set; } = string.Empty;

    /// <summary>
    /// The lowest level written for the matching loggers, below the levels the
    /// log file, the console and the live log are set to. Must be above
    /// <see cref="MaxLevel"/> when both are set.
    /// </summary>
    public LogLevel? MinLevel { get; set; }

    /// <summary>
    /// The highest level dropped for the matching loggers. With neither level,
    /// the rule turns off a default rule for the same pattern.
    /// </summary>
    public LogLevel? MaxLevel { get; set; }

    /// <summary>
    /// Whether a message the rule drops is kept from every later rule.
    /// </summary>
    public bool Final { get; set; } = true;

    public CustomLogLevelRule() { }

    public CustomLogLevelRule(AbstractLogLevelRule rule)
    {
        LoggerNamePattern = rule.LoggerNamePattern;
        MinLevel = rule.MinLevel;
        MaxLevel = rule.MaxLevel;
        Final = rule.Final;
    }

    /// <summary>
    /// The rule for the log service.
    /// </summary>
    public AbstractLogLevelRule ToRule()
        => new() { LoggerNamePattern = LoggerNamePattern, MinLevel = MinLevel, MaxLevel = MaxLevel, Final = Final };
}
