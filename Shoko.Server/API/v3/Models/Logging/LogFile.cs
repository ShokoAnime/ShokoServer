using System;
using System.ComponentModel.DataAnnotations;
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
    [Required]
    public Guid ID { get; }

    /// <summary>
    /// Date of the log file.
    /// </summary>
    [Required]
    public DateOnly Date { get; }

    /// <summary>
    /// Daily number of the log file. Will be 0 for the latest log file of the
    /// day, then ascending order starting at 1, from oldest to newest.
    /// </summary>
    [Required]
    public uint DailyNumber { get; }

    /// <summary>
    /// File name.
    /// </summary>
    [Required]
    public string Name { get; }

    /// <summary>
    /// File size in bytes at the time of retrieval.
    /// </summary>
    [Required]
    public long Size { get; }

    /// <summary>
    /// Indicates whether the file is the current log file.
    /// </summary>
    [Required]
    public bool IsCurrent { get; }

    /// <summary>
    /// Indicates whether the file is compressed.
    /// </summary>
    [Required]
    public bool IsCompressed { get; }

    /// <summary>
    /// Detected file format.
    /// </summary>
    [Required]
    public string Format { get; }

    /// <summary>
    /// Last modified timestamp in UTC.
    /// </summary>
    [Required]
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
