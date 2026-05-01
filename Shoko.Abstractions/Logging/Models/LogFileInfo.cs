using System;

namespace Shoko.Abstractions.Logging.Models;

/// <summary>
///   Metadata about a log file available for reading or downloading.
/// </summary>
public class LogFileInfo
{
    /// <summary>
    ///   Stable log file identifier.
    /// </summary>
    public required Guid ID { get; init; }

    /// <summary>
    ///   Date of the log file.
    /// </summary>
    public required DateOnly Date { get; init; }

    /// <summary>
    ///   Daily number of the log file. Will be 0 for the latest log file of the
    ///   day, then ascending order starting at 1, from oldest to newest.
    /// </summary>
    public required uint DailyNumber { get; init; }

    /// <summary>
    ///   Display file name.
    /// </summary>
    public required string FileName { get; init; }

    /// <summary>
    ///   Full file-system path.
    /// </summary>
    public required string FullPath { get; init; }

    /// <summary>
    ///   File size in bytes at the time of retrieval.
    /// </summary>
    public required long Size { get; init; }

    /// <summary>
    ///   Indicates whether the file is the current log file.
    /// </summary>
    public required bool IsCurrent { get; init; }

    /// <summary>
    ///   Indicates whether the file is compressed.
    /// </summary>
    public required bool IsCompressed { get; init; }

    /// <summary>
    ///   Detected content format.
    /// </summary>
    public required LogFileFormat Format { get; init; }

    /// <summary>
    ///   Last modification timestamp in UTC.
    /// </summary>
    public required DateTime LastModifiedAt { get; init; }
}
