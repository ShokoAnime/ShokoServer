using Shoko.Abstractions.Plugin.Models;

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
    ///   The plugin that registered the rule, when <see cref="Source"/> is
    ///   <see cref="LogLevelRuleSource.Plugin"/>.
    /// </summary>
    public LocalPluginInfo? Plugin { get; init; }

    /// <summary>
    ///   The rule it replaced, with the one that rule replaced in turn, when
    ///   it replaced one for the same pattern. Removing a custom rule brings
    ///   this one back.
    /// </summary>
    public LogLevelRuleInfo? Replaces { get; init; }
}
