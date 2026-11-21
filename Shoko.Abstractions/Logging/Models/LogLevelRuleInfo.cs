using System;

namespace Shoko.Abstractions.Logging.Models;

/// <summary>
///   A log level rule in effect, with where it comes from.
/// </summary>
public sealed class LogLevelRuleInfo
{
    /// <summary>
    ///   The rule.
    /// </summary>
    public required LogLevelRule Rule { get; init; }

    /// <summary>
    ///   Where the rule comes from.
    /// </summary>
    public required LogLevelRuleSource Source { get; init; }

    /// <summary>
    ///   The ID of the plugin that registered the rule, when
    ///   <see cref="Source"/> is <see cref="LogLevelRuleSource.Plugin"/>.
    /// </summary>
    public Guid? PluginID { get; init; }

    /// <summary>
    ///   The name of the plugin that registered the rule, when
    ///   <see cref="Source"/> is <see cref="LogLevelRuleSource.Plugin"/>.
    /// </summary>
    public string? PluginName { get; init; }

    /// <summary>
    ///   Where the rule it replaced came from, when it replaced one for the
    ///   same pattern.
    /// </summary>
    public LogLevelRuleSource? Replaces { get; init; }
}
