using System;
using Shoko.Abstractions.Logging.Models;

#nullable enable
namespace Shoko.Server.API.v3.Models.Logging;

/// <summary>
/// API model containing log file metadata.
/// </summary>
public class LogFile
{
    /// <summary>
    /// Log file identifier.
    /// </summary>
    public Guid ID { get; }

    /// <summary>
    /// Date of the log file.
    /// </summary>
    public DateOnly Date { get; }

    /// <summary>
    /// Daily number of the log file. Will be 0 for the latest log file of the
    /// day, then ascending order starting at 1, from oldest to newest.
    /// </summary>
    public uint DailyNumber { get; }

    /// <summary>
    /// File name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// File size in bytes at the time of retrieval.
    /// </summary>
    public long Size { get; }

    /// <summary>
    /// Indicates whether the file is the current log file.
    /// </summary>
    public bool IsCurrent { get; }

    /// <summary>
    /// Indicates whether the file is compressed.
    /// </summary>
    public bool IsCompressed { get; }

    /// <summary>
    /// Detected file format.
    /// </summary>
    public string Format { get; }

    /// <summary>
    /// Last modified timestamp in UTC.
    /// </summary>
    public DateTime LastModifiedAt { get; }

    public LogFile(LogFileInfo file)
    {
        ID = file.ID;
        Date = file.Date;
        DailyNumber = file.DailyNumber;
        Name = file.FileName;
        Size = file.Size;
        IsCurrent = file.IsCurrent;
        IsCompressed = file.IsCompressed;
        Format = file.Format.ToString().ToLowerInvariant();
        LastModifiedAt = file.LastModifiedAt.ToUniversalTime();
    }
}
