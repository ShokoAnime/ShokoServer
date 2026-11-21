using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Logging.Models;

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
    /// Whether a message the rule drops is kept from every later rule.
    /// </summary>
    [Required]
    public bool Final { get; }

    /// <summary>
    /// Where the rule comes from: <c>Core</c>, <c>Plugin</c> or <c>Custom</c>.
    /// </summary>
    [Required]
    public LogLevelRuleSource Source { get; }

    /// <summary>
    /// The ID of the plugin that registered the rule.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Guid? PluginID { get; }

    /// <summary>
    /// The name of the plugin that registered the rule.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? PluginName { get; }

    /// <summary>
    /// Where the rule it replaced came from, when it replaced one for the same
    /// pattern.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public LogLevelRuleSource? Replaces { get; }

    public LogLevelRule(LogLevelRuleInfo info)
    {
        LoggerNamePattern = info.Rule.LoggerNamePattern;
        MinLevel = info.Rule.MinLevel;
        MaxLevel = info.Rule.MaxLevel;
        Final = info.Rule.Final;
        Source = info.Source;
        PluginID = info.PluginID;
        PluginName = info.PluginName;
        Replaces = info.Replaces;
    }
}
