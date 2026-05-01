using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Shoko.Abstractions.Extensions;

namespace Shoko.Abstractions.Logging.Models;

/// <summary>
///   A structured log entry parsed from JSONL log files.
/// </summary>
public class LogEntry
{
    /// <summary>
    ///   Entry timestamp in UTC.
    /// </summary>
    public required DateTime TimeStamp { get; init; }

    /// <summary>
    ///   Log level.
    /// </summary>
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    [Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
    public required LogLevel Level { get; init; }

    /// <summary>
    ///   Thread ID associated with the entry.
    /// </summary>
    public required int ThreadId { get; init; }

    /// <summary>
    ///   Process ID associated with the entry.
    /// </summary>
    public required int ProcessId { get; init; }

    /// <summary>
    ///   Logger category or name.
    /// </summary>
    public required string Logger { get; init; }

    /// <summary>
    ///   Caller information from the logging pipeline.
    /// </summary>
    public required string Caller { get; init; }

    /// <summary>
    ///   Rendered log message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    ///   Optional rendered exception information.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Exception { get; init; }

    /// <inheritdoc/>
    public override string ToString()
        => ToString(LogSerializeFormat.Simple);

    /// <summary>
    ///   Returns a string representation of the log entry.
    /// </summary>
    /// <param name="format">Serialization layout to use.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <paramref name="format"/> is invalid.
    /// </exception>
    /// <returns>
    ///   A string representation of the log entry.
    /// </returns>
    public string ToString(LogSerializeFormat format)
        => format switch
        {
            LogSerializeFormat.Simple => $"[{TimeStamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level.ToShortString()}] {Logger.Split('.').Last()}: {Message}{(Exception is { Length: > 0 } ? $": {Exception}" : string.Empty)}",
            LogSerializeFormat.Full => $"[{TimeStamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level.ToShortString()}] [{ThreadId:000}] {Logger}: {Message}{(Exception is { Length: > 0 } ? Environment.NewLine + Exception : string.Empty)}",
            LogSerializeFormat.Json => System.Text.Json.JsonSerializer.Serialize(this),
            LogSerializeFormat.Legacy => $"[{TimeStamp:yyyy-MM-dd HH:mm:ss.fff}] {Level.ToNLogString()}|{Logger} > {Message}{(Exception is { Length: > 0 } ? $": {Exception}" : string.Empty)}",
            LogSerializeFormat.Console => $"[{TimeStamp:HH:mm:ss}| {Logger.Split('.').Last()} --- {Message}{(Exception is { Length: > 0 } ? $": {Exception}" : string.Empty)}",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
}
