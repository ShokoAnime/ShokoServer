using System;
using System.Collections.Generic;
using Shoko.Abstractions.Exceptions;
using Shoko.Abstractions.Logging.Models;

namespace Shoko.Abstractions.Logging.Services;

/// <summary>
///   Service responsible for listing, reading, downloading, and maintaining
///   server log files.
/// </summary>
public interface ILogService
{
    #region Maintenance

    /// <summary>
    ///   Starts scheduled log maintenance.
    /// </summary>
    void StartMaintenance();

    /// <summary>
    ///   Runs log maintenance immediately using current rotation settings.
    /// </summary>
    void RunRotationMaintenance();

    #endregion

    #region Log File Operations

    /// <summary>
    ///   Gets the ID of the currently running process. May be useful to filter
    ///   log entries.
    /// </summary>
    int ProcessID { get; }

    /// <summary>
    ///   Lists all available log files, in most recent order, with the current
    ///   log file first.
    /// </summary>
    /// <returns>
    ///   The available log files.
    /// </returns>
    IReadOnlyList<LogFileInfo> GetAllLogFiles();

    /// <summary>
    ///   Gets the current log file info.
    /// </summary>
    /// <returns>
    ///   The current log file info.
    /// </returns>
    LogFileInfo GetCurrentLogFile();

    /// <summary>
    ///   Gets the log file info by it's identifier.
    /// </summary>
    /// <param name="fileID">
    ///   The log file identifier.
    /// </param>
    /// <returns>
    ///   The log file info, or <c>null</c> if not found.
    /// </returns>
    LogFileInfo? GetLogFileByID(Guid fileID);

    /// <summary>
    ///   Reads entries from the specified log file using line-based paging in
    ///   ascending or descending order.
    /// </summary>
    /// <param name="fileInfo">The log file to read.</param>
    /// <param name="options">
    ///   Optional. Paging, sort, date bounds, and <see cref="LogBaseOptions"/> filters.
    ///   When <c>null</c>, uses default offset/limit and ascending order.
    /// </param>
    /// <exception cref="GenericValidationException">
    ///   Thrown when the options are invalid.
    /// </exception>
    /// <returns>
    ///   The paged read result, with the first entry being the least recent in
    ///   ascending order, or the most recent in descending order.
    /// </returns>
    LogReadResult ReadLogFile(LogFileInfo fileInfo, LogReadOptions? options = null);

    /// <summary>
    ///   Opens a stream for downloading a specific log file.
    /// </summary>
    /// <param name="fileInfo">The log file to download.</param>
    /// <param name="options">
    ///   Optional. Format, date bounds, and <see cref="LogBaseOptions"/> filters.
    ///   When <c>null</c>, uses <see cref="LogSerializeFormat.Simple"/> without extra constraints.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <see cref="LogDownloadOptions.Format"/> is invalid.
    /// </exception>
    /// <exception cref="GenericValidationException">
    ///   Thrown when the options are invalid.
    /// </exception>
    /// <returns>
    ///   The download metadata and stream.
    /// </returns>
    LogDownloadResult DownloadLogFile(LogFileInfo fileInfo, LogDownloadOptions? options = null);

    /// <summary>
    ///   Deletes the specified log file.
    /// </summary>
    /// <param name="fileInfo">
    ///   The log file to delete.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when attempting to delete the current log file.
    /// </exception>
    /// <returns>
    ///   <c>true</c> if the file was deleted; otherwise, <c>false</c>.
    /// </returns>
    void DeleteLogFile(LogFileInfo fileInfo);

    #endregion

    #region Range Operations

    /// <summary>
    ///   Reads entries across all readable log files in descending order.
    ///   With optional date-range filtering and line-based paging.
    /// </summary>
    /// <param name="options">
    ///   Optional. When <c>null</c>, uses descending order, default limit, and no date or DSL filters.
    /// </param>
    /// <exception cref="GenericValidationException">
    ///   Thrown when the options are invalid.
    /// </exception>
    /// <returns>
    ///   The paged read result, with the first entry being the most recent.
    /// </returns>
    LogReadResult ReadRange(LogReadOptions? options = null);

    /// <summary>
    ///   Opens a stream for downloading log entries across files in a date range.
    /// </summary>
    /// <param name="options">
    ///   Optional. When <c>null</c>, uses <see cref="LogSerializeFormat.Simple"/> and default range behavior.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   Thrown when <see cref="LogDownloadOptions.Format"/> is invalid.
    /// </exception>
    /// <exception cref="GenericValidationException">
    ///   Thrown when the options are invalid.
    /// </exception>
    /// <returns>
    ///   The download metadata and stream.
    /// </returns>
    LogDownloadResult DownloadRange(LogDownloadOptions? options = null);

    #endregion
}
