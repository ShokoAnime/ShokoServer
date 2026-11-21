using Microsoft.Extensions.Logging;

namespace Shoko.Abstractions.Logging.Models;

/// <summary>
///   A log level rule for the loggers whose names match a pattern.
/// </summary>
/// <remarks>
///   <para>
///     <see cref="MaxLevel"/> drops the matching loggers' messages up to and
///     including that level. <see cref="MinLevel"/> writes their messages from
///     that level up to the log file, the console and the live log, even when
///     the levels those are set to are higher; it never sends them to an
///     OpenTelemetry collector the default level would not. A rule with
///     neither changes nothing, which is how a custom rule turns off a
///     default rule for the same pattern.
///   </para>
///   <para>
///     Rules are applied in order: the core's defaults, then each active
///     plugin's in load order, then the custom rules of the logging settings.
///     A later rule for the
///     same pattern, compared ignoring case, replaces the earlier one.
///   </para>
/// </remarks>
public sealed class LogLevelRule
{
    /// <summary>
    ///   The pattern of the logger names the rule applies to, as NLog matches
    ///   them: a name, or one with a <c>*</c> at its start, its end or both,
    ///   such as <c>ModelContextProtocol.*</c>.
    /// </summary>
    public required string LoggerNamePattern { get; init; }

    /// <summary>
    ///   The lowest level written for the matching loggers, below the levels
    ///   the log file, the console and the live log are set to, or
    ///   <c>null</c> to leave them be. Must be above <see cref="MaxLevel"/>
    ///   when both are set.
    /// </summary>
    public LogLevel? MinLevel { get; init; }

    /// <summary>
    ///   The highest level dropped for the matching loggers, or <c>null</c> to
    ///   drop nothing.
    /// </summary>
    public LogLevel? MaxLevel { get; init; }

    /// <summary>
    ///   Whether a message the rule drops is kept from every later rule,
    ///   including those of broader patterns. Defaults to <c>true</c>.
    /// </summary>
    public bool Final { get; init; } = true;
}
