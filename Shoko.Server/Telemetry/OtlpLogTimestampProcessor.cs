using System;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Shoko.Server.Telemetry;

/// <summary>
/// Gives a replayed log the time it was written at, rather than the time it was replayed. Processors run on the thread
/// writing the log, so the target says which time to use through <see cref="NextTimestamp"/>.
/// </summary>
internal sealed class OtlpLogTimestampProcessor : BaseProcessor<LogRecord>
{
    /// <summary>
    /// The time to give the log this thread is writing, or <c>null</c> to leave it.
    /// </summary>
    [ThreadStatic]
    internal static DateTime? NextTimestamp;

    public override void OnEnd(LogRecord data)
    {
        if (NextTimestamp is { } timestamp)
            data.Timestamp = timestamp;
    }
}
