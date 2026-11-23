using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Logging.Models;
using Shoko.Server.API.v3.Models.Plugin;

namespace Shoko.Server.API.v3.Models.Logging;

/// <summary>
/// A log level rule in effect, with where it comes from.
/// </summary>
public class LogLevelRule
{
    /// <summary>
    /// The pattern of the logger names the rule applies to.
    /// </summary>
    [Required]
    public string LoggerNamePattern { get; }

    /// <summary>
    /// The lowest level written for the matching loggers, below the levels the
    /// log file, the console and the live log are set to.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public LogLevel? MinLevel { get; }

    /// <summary>
    /// The highest level dropped for the matching loggers.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public LogLevel? MaxLevel { get; }

    /// <summary>
    /// Where the rule comes from: <c>Core</c>, <c>Plugin</c> or <c>Custom</c>.
    /// </summary>
    [Required]
    public LogLevelRuleSource Source { get; }

    /// <summary>
    /// The plugin that registered the rule.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public PluginInfo? Plugin { get; }

    /// <summary>
    /// The rule it replaced, with the one that rule replaced in turn, when it
    /// replaced one for the same pattern. Removing a custom rule brings this
    /// one back.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public LogLevelRule? Replaces { get; }

    public LogLevelRule(LogLevelRuleInfo info)
    {
        LoggerNamePattern = info.Rule.LoggerNamePattern;
        MinLevel = info.Rule.MinLevel;
        MaxLevel = info.Rule.MaxLevel;
        Source = info.Source;
        Plugin = info.Plugin is { } plugin ? new(plugin) : null;
        Replaces = info.Replaces is { } replaced ? new(replaced) : null;
    }
}
