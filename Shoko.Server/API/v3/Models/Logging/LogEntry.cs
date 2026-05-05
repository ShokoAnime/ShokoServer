using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Newtonsoft.Json;

using AbstractLogEntry = Shoko.Abstractions.Logging.Models.LogEntry;

#nullable enable
namespace Shoko.Server.API.v3.Models.Logging;

/// <summary>
/// API model containing a single log entry.
/// </summary>
public class LogEntry
{
    /// <summary>
    /// Entry timestamp in UTC.
    /// </summary>
    [Required]
    public DateTime TimeStamp { get; }

    /// <summary>
    /// Log level.
    /// </summary>
    [Required]
    public string Level { get; }

    /// <summary>
    /// Logger category or name.
    /// </summary>
    [Required]
    public string Logger { get; }

    /// <summary>
    /// Caller information.
    /// </summary>
    [Required]
    public string Caller { get; }

    /// <summary>
    /// Thread ID for the entry.
    /// </summary>
    [Required]
    public int ThreadId { get; }

    /// <summary>
    /// Process ID for the entry.
    /// </summary>
    [Required]
    public int ProcessId { get; }

    /// <summary>
    /// Rendered log message.
    /// </summary>
    [Required]
    public string Message { get; }

    /// <summary>
    /// Optional rendered exception information.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Exception { get; }

    public LogEntry(AbstractLogEntry entry)
    {
        TimeStamp = entry.TimeStamp;
        Level = entry.Level.ToString();
        Logger = entry.Logger;
        Caller = entry.Caller;
        ThreadId = entry.ThreadId;
        ProcessId = entry.ProcessId;
        Message = entry.Message;
        Exception = entry.Exception;
    }
}
