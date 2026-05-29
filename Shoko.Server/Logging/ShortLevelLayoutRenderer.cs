using System.Text;
using NLog;
using NLog.Config;
using NLog.LayoutRenderers;
using Shoko.Abstractions.Extensions;

using ELogLevel = Microsoft.Extensions.Logging.LogLevel;
using NLogLevel = NLog.LogLevel;

#nullable enable
namespace Shoko.Server.Logging;

/// <summary>
/// Renders the log level using the same abbreviations as
/// <see cref="T:Shoko.Abstractions.Extensions.LoggingExtensions"/> (<c>ToShortString()</c>): VRB, DBG, INF, ….
/// Used by the Simple and Full console layouts (<c>${shortlevel}</c>).
/// </summary>
[LayoutRenderer("shortlevel")]
[ThreadAgnostic]
public sealed class ShortLevelLayoutRenderer : LayoutRenderer
{
    protected override void Append(StringBuilder builder, LogEventInfo logEvent)
        => builder.Append(ToExtensionsLogLevel(logEvent.Level).ToShortString());

    private static ELogLevel ToExtensionsLogLevel(NLogLevel level)
        => level.Ordinal switch
        {
            0 => ELogLevel.Trace,
            1 => ELogLevel.Debug,
            2 => ELogLevel.Information,
            3 => ELogLevel.Warning,
            4 => ELogLevel.Error,
            5 => ELogLevel.Critical,
            6 or _ => ELogLevel.None,
        };
}
