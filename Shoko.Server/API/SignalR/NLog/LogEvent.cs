using System;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

#nullable enable
namespace Shoko.Server.API.SignalR.NLog;

public class LogEvent : EventArgs
{
    /// <summary>
    ///   Entry timestamp in UTC.
    /// </summary>
    public DateTime TimeStamp { get; init; }

    /// <summary>
    ///   Log level.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public LogLevel Level { get; init; }

    /// <summary>
    ///   Thread ID associated with the entry.
    /// </summary>
    public int ThreadId { get; init; }

    /// <summary>
    ///   Process ID associated with the entry.
    /// </summary>
    public int ProcessId { get; init; }

    /// <summary>
    ///   Logger category or name.
    /// </summary>
    public string Logger { get; init; }

    /// <summary>
    ///   Caller information from the logging pipeline.
    /// </summary>
    public string Caller { get; init; }

    /// <summary>
    ///   Rendered log message.
    /// </summary>
    public string Message { get; init; }

    /// <summary>
    ///   Optional rendered exception information.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Exception { get; init; }

    internal LogEvent(global::NLog.LogEventInfo eventInfo, string renderedMessage, int threadId, int processId)
    {
        Level = eventInfo.Level.Ordinal switch
        {
            0 => LogLevel.Trace,
            1 => LogLevel.Debug,
            2 => LogLevel.Information,
            3 => LogLevel.Warning,
            4 => LogLevel.Error,
            5 => LogLevel.Critical,
            6 or _ => LogLevel.None,
        };
        TimeStamp = eventInfo.TimeStamp.ToUniversalTime();
        Logger = eventInfo.LoggerName;
        Caller = $"{eventInfo.CallerClassName}.{eventInfo.CallerMemberName}";
        ProcessId = processId;
        ThreadId = threadId;
        Message = renderedMessage;
        Exception = eventInfo.Exception?.ToString();
    }
}
