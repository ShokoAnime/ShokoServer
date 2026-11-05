using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Targets;
using OpenTelemetry;

using ELogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Shoko.Server.Telemetry;

/// <summary>
/// Hands the server's logs to the OpenTelemetry logger. Every log goes through NLog, so this is the one place that sees
/// them all, including those of the bootstrap logger factory and of direct <see cref="LogManager"/> use. Writes on the
/// logging thread, so a log carries the trace of the span it was written in.
/// </summary>
/// <remarks>
/// Starts out holding the most recent <see cref="MaxBufferedEvents"/> logs, since the settings saying what to do with
/// them are read after the first logs are written. <see cref="Connect"/> then replays them or lets them go.
/// </remarks>
[Target("Otlp")]
internal sealed class OtlpLogTarget : Target
{
    /// <summary>
    /// How many logs to hold until the settings are read.
    /// </summary>
    internal const int MaxBufferedEvents = 512;

    /// <summary>
    /// The target's name in the logging configuration.
    /// </summary>
    internal const string TargetName = "otlp";

    /// <summary>
    /// The loggers whose logs are never exported, since the export writing to them would cause more exports.
    /// </summary>
    private static readonly string[] _excludedLoggerPrefixes = ["Shoko.Server.Telemetry", "System.Net.Http.HttpClient.Otlp", "OpenTelemetry"];

    [ThreadStatic]
    private static bool _isEmitting;

    private Queue<BufferedLog>? _buffer = new();

    private ILoggerFactory? _loggerFactory;

    public OtlpLogTarget()
    {
        Name = TargetName;
    }

    /// <summary>
    /// Whether this target still takes logs, which is until it is told there is no export to give them to.
    /// </summary>
    public bool IsActive
    {
        get
        {
            lock (SyncRoot)
                return _buffer is not null || _loggerFactory is not null;
        }
    }

    /// <summary>
    /// Replays the held logs to a logger factory and forwards every log from now on, or, without one, lets them go and
    /// stops taking logs.
    /// </summary>
    /// <param name="loggerFactory">The factory of the OpenTelemetry logger, or <c>null</c> when logs are not exported.</param>
    public void Connect(ILoggerFactory? loggerFactory)
    {
        lock (SyncRoot)
        {
            var buffered = _buffer;
            _buffer = null;
            _loggerFactory = loggerFactory;
            if (loggerFactory is null || buffered is null)
                return;

            foreach (var log in buffered)
                Emit(loggerFactory, log, log.Timestamp);
        }
    }

    protected override void Write(LogEventInfo logEvent)
    {
        if (_isEmitting || Sdk.SuppressInstrumentation || IsExcluded(logEvent.LoggerName))
            return;

        var log = new BufferedLog(
            logEvent.TimeStamp.ToUniversalTime(),
            (ELogLevel)logEvent.Level.Ordinal,
            logEvent.LoggerName ?? string.Empty,
            logEvent.FormattedMessage,
            logEvent.Exception
        );
        if (_loggerFactory is { } loggerFactory)
        {
            Emit(loggerFactory, log, null);
        }
        else if (_buffer is { } buffer)
        {
            if (buffer.Count >= MaxBufferedEvents)
                buffer.Dequeue();
            buffer.Enqueue(log);
        }
    }

    private static bool IsExcluded(string? loggerName)
    {
        if (loggerName is null)
            return false;

        foreach (var prefix in _excludedLoggerPrefixes)
        {
            if (loggerName.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static void Emit(ILoggerFactory loggerFactory, BufferedLog log, DateTime? timestamp)
    {
        _isEmitting = true;
        OtlpLogTimestampProcessor.NextTimestamp = timestamp;
        try
        {
            loggerFactory.CreateLogger(log.LoggerName).Log(log.Level, default, TelemetrySecretScrubber.Scrub(log.Message), log.Exception, static (message, _) => message);
        }
        finally
        {
            OtlpLogTimestampProcessor.NextTimestamp = null;
            _isEmitting = false;
        }
    }

    private sealed record BufferedLog(DateTime Timestamp, ELogLevel Level, string LoggerName, string Message, Exception? Exception);
}
