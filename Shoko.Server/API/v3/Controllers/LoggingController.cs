using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Logging.Models;
using Shoko.Abstractions.Logging.Services;
using Shoko.Abstractions.Web.Attributes;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.v3.Models.Logging;
using Shoko.Server.Services;
using Shoko.Server.Settings;

using LogReadResult = Shoko.Server.API.v3.Models.Logging.LogReadResult;

#nullable enable
namespace Shoko.Server.API.v3.Controllers;

/// <summary>
/// Controller responsible for listing, reading, and downloading log files.
/// </summary>
[ApiController]
[Route("/api/v{version:apiVersion}/[controller]")]
[ApiV3]
[Authorize(Roles = "admin,init")]
[DatabaseBlockedExempt]
[InitFriendly]
public class LoggingController(ISettingsProvider settingsProvider, ILogService logService) : BaseController(settingsProvider)
{

    /// <summary>
    /// Read log entries across files filtered by a date range.
    /// </summary>
    /// <remarks>
    /// **Log filter DSL** (`logger`, `caller`, `message`, `exception` query parameters):
    ///
    /// - If the value has no **':'** in the string, it is shorthand for <c>c:&lt;entire value&gt;</c> (substring, case-sensitive).
    /// - Otherwise the first character is the **mode**: <c>c</c> contains, <c>=</c> equals, <c>^</c> starts with, <c>$</c> ends with, <c>~</c> fuzzy (always case-insensitive), <c>*</c> regex.
    /// - After the mode and before the first <c>:</c>: optional <c>!</c> (negate) and optional <c>#</c> (case-insensitive for <c>c</c>, <c>=</c>, <c>^</c>, <c>$</c>), each at most once, in either order. <c>#</c> is ignored for <c>~</c> and <c>*</c>.
    /// - **Payload** is everything after the first <c>:</c> only (e.g. <c>c:^:foo</c> contains the substring <c>^:foo</c>).
    /// - **Regex** (<c>*:</c>): raw pattern or <c>/pattern/flags</c>; default case-sensitive; flag <c>i</c> = ignore case.
    /// - **Exception** query param: entry exception text is compared as <c>null</c> coalesced to <c>""</c>. <c>=:</c> matches lines with no exception text; <c>=!:</c> matches lines with non-empty exception text.
    /// - A filter is **inactive** when the query parameter is omitted; empty or whitespace-only values are still valid DSL.
    ///
    /// Plugin authors: same strings as <see cref="LogBaseOptions"/> filter properties.
    /// </remarks>
    /// <param name="from">Optional start date (inclusive).</param>
    /// <param name="to">Optional end date (inclusive).</param>
    /// <param name="offset">Line offset to start from.</param>
    /// <param name="limit">Maximum number of entries to return.</param>
    /// <param name="descending">Set to <c>true</c> for newest-first ordering. Defaults to <c>true</c>.</param>
    /// <param name="level">Comma-separated <see cref="LogLevel"/> names to include (e.g. <c>Error,Warning</c>).</param>
    /// <param name="logger">Optional DSL filter on logger name; inactive when omitted. See remarks.</param>
    /// <param name="caller">Optional DSL filter on caller; inactive when omitted. See remarks.</param>
    /// <param name="message">Optional DSL filter on message; inactive when omitted. See remarks.</param>
    /// <param name="exception">Optional DSL filter on exception text (<c>entry.Exception ?? ""</c>); inactive when omitted. See remarks.</param>
    /// <param name="processId">Exact process id.</param>
    /// <param name="threadId">Exact thread id.</param>
    /// <returns>The paged log read result.</returns>
    [HttpGet("Range/Read")]
    public ActionResult<LogReadResult> GetLogRange(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery, Range(0, int.MaxValue)] uint offset = 0,
        [FromQuery, Range(0, 1000)] uint limit = 100,
        [FromQuery] bool descending = true,
        [FromQuery] string? level = null,
        [FromQuery] string? logger = null,
        [FromQuery] string? caller = null,
        [FromQuery] string? message = null,
        [FromQuery] string? exception = null,
        [FromQuery] int? processId = null,
        [FromQuery] int? threadId = null
    )
    {
        if (from.HasValue && to.HasValue && from > to)
        {
            ModelState.AddModelError(nameof(to), "The 'to' query parameter must be after 'from'.");
            ModelState.AddModelError(nameof(from), "The 'from' query parameter must be before 'to'.");
            return ValidationProblem(ModelState);
        }

        if (!TryBuildLogReadOptions(from, to, offset, limit, descending, level, logger, caller, message, exception, processId, threadId, out var readOptions, out var dslError))
            return ValidationProblem(dslError);

        return new LogReadResult(logService.ReadRange(readOptions));
    }
    /// <summary>
    /// Download log entries across files filtered by a date range.
    /// </summary>
    /// <remarks>
    /// **Log filter DSL** (`logger`, `caller`, `message`, `exception` query parameters):
    ///
    /// - If the value has no **':'** in the string, it is shorthand for <c>c:&lt;entire value&gt;</c> (substring, case-sensitive).
    /// - Otherwise the first character is the **mode**: <c>c</c> contains, <c>=</c> equals, <c>^</c> starts with, <c>$</c> ends with, <c>~</c> fuzzy (always case-insensitive), <c>*</c> regex.
    /// - After the mode and before the first <c>:</c>: optional <c>!</c> (negate) and optional <c>#</c> (case-insensitive for <c>c</c>, <c>=</c>, <c>^</c>, <c>$</c>), each at most once, in either order. <c>#</c> is ignored for <c>~</c> and <c>*</c>.
    /// - **Payload** is everything after the first <c>:</c> only (e.g. <c>c:^:foo</c> contains the substring <c>^:foo</c>).
    /// - **Regex** (<c>*:</c>): raw pattern or <c>/pattern/flags</c>; default case-sensitive; flag <c>i</c> = ignore case.
    /// - **Exception** query param: entry exception text is compared as <c>null</c> coalesced to <c>""</c>. <c>=:</c> matches lines with no exception text; <c>=!:</c> matches lines with non-empty exception text.
    /// - A filter is **inactive** when the query parameter is omitted; empty or whitespace-only values are still valid DSL.
    ///
    /// Plugin authors: same strings as <see cref="LogBaseOptions"/> filter properties.
    /// </remarks>
    /// <param name="from">Optional start date (inclusive).</param>
    /// <param name="to">Optional end date (inclusive).</param>
    /// <param name="format">
    /// Format to return the log entries as. Can be <c>simple</c>, <c>full</c>,
    /// <c>json</c> or <c>legacy</c>.
    /// </param>
    /// <param name="level">Comma-separated <see cref="LogLevel"/> names to include.</param>
    /// <param name="logger">Optional DSL filter on logger; inactive when omitted. See remarks.</param>
    /// <param name="caller">Optional DSL filter on caller; inactive when omitted. See remarks.</param>
    /// <param name="message">Optional DSL filter on message; inactive when omitted. See remarks.</param>
    /// <param name="exception">Optional DSL filter on exception text; inactive when omitted. See remarks.</param>
    /// <param name="processId">Exact process id.</param>
    /// <param name="threadId">Exact thread id.</param>
    /// <returns>The generated download response.</returns>
    [HttpGet("Range/Download")]
    public ActionResult DownloadLogRange(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? format = null,
        [FromQuery] string? level = null,
        [FromQuery] string? logger = null,
        [FromQuery] string? caller = null,
        [FromQuery] string? message = null,
        [FromQuery] string? exception = null,
        [FromQuery] int? processId = null,
        [FromQuery] int? threadId = null
    )
    {
        if (from.HasValue && to.HasValue && from > to)
        {
            ModelState.AddModelError(nameof(to), "The 'to' query parameter must be after 'from'.");
            ModelState.AddModelError(nameof(from), "The 'from' query parameter must be before 'to'.");
            return ValidationProblem(ModelState);
        }

        if (!TryParseLogSerializeFormat(format, out var formatEnum, out var fmtError))
            return ValidationProblem(fmtError, nameof(format));

        if (!TryBuildLogDownloadOptions(from, to, formatEnum, level, logger, caller, message, exception, processId, threadId, out var downloadOptions, out var dslError))
            return ValidationProblem(dslError);

        var download = logService.DownloadRange(downloadOptions);
        return File(download.Stream, download.ContentType, download.FileName);
    }

    /// <summary>
    /// List all available log files.
    /// </summary>
    /// <returns>The available log files.</returns>
    [HttpGet("File")]
    public ActionResult<List<LogFile>> GetLogFiles()
        => logService.GetAllLogFiles().Select(file => new LogFile(file)).ToList();

    /// <summary>
    /// Get the current log file.
    /// </summary>
    /// <returns>The current log file.</returns>
    [HttpGet("File/Current")]
    public ActionResult<LogFile> GetCurrentLogFile()
        => new LogFile(logService.GetCurrentLogFile());

    /// <summary>
    /// Read the current log file using line-based paging.
    /// </summary>
    /// <remarks>
    /// **Log filter DSL** (`logger`, `caller`, `message`, `exception` query parameters):
    ///
    /// - If the value has no **':'** in the string, it is shorthand for <c>c:&lt;entire value&gt;</c> (substring, case-sensitive).
    /// - Otherwise the first character is the **mode**: <c>c</c> contains, <c>=</c> equals, <c>^</c> starts with, <c>$</c> ends with, <c>~</c> fuzzy (always case-insensitive), <c>*</c> regex.
    /// - After the mode and before the first <c>:</c>: optional <c>!</c> (negate) and optional <c>#</c> (case-insensitive for <c>c</c>, <c>=</c>, <c>^</c>, <c>$</c>), each at most once, in either order. <c>#</c> is ignored for <c>~</c> and <c>*</c>.
    /// - **Payload** is everything after the first <c>:</c> only (e.g. <c>c:^:foo</c> contains the substring <c>^:foo</c>).
    /// - **Regex** (<c>*:</c>): raw pattern or <c>/pattern/flags</c>; default case-sensitive; flag <c>i</c> = ignore case.
    /// - **Exception** query param: entry exception text is compared as <c>null</c> coalesced to <c>""</c>. <c>=:</c> matches lines with no exception text; <c>=!:</c> matches lines with non-empty exception text.
    /// - A filter is **inactive** when the query parameter is omitted; empty or whitespace-only values are still valid DSL.
    ///
    /// Plugin authors: same strings as <see cref="LogBaseOptions"/> filter properties.
    /// </remarks>
    /// <param name="from">Optional start date (inclusive, UTC).</param>
    /// <param name="to">Optional end date (inclusive, UTC).</param>
    /// <param name="offset">Line offset to start from.</param>
    /// <param name="limit">Maximum number of entries to return.</param>
    /// <param name="descending">If <c>true</c>, return newest entries first.</param>
    /// <param name="level">Comma-separated <see cref="LogLevel"/> names to include.</param>
    /// <param name="logger">Optional DSL filter on logger; inactive when omitted. See remarks.</param>
    /// <param name="caller">Optional DSL filter on caller; inactive when omitted. See remarks.</param>
    /// <param name="message">Optional DSL filter on message; inactive when omitted. See remarks.</param>
    /// <param name="exception">Optional DSL filter on exception text; inactive when omitted. See remarks.</param>
    /// <param name="processId">Exact process id.</param>
    /// <param name="threadId">Exact thread id.</param>
    /// <returns>The paged log read result.</returns>
    [HttpGet("File/Current/Read")]
    public ActionResult<LogReadResult> GetCurrentLogs(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery, Range(0, int.MaxValue)] uint offset = 0,
        [FromQuery, Range(0, 1000)] uint limit = 100,
        [FromQuery] bool descending = false,
        [FromQuery] string? level = null,
        [FromQuery] string? logger = null,
        [FromQuery] string? caller = null,
        [FromQuery] string? message = null,
        [FromQuery] string? exception = null,
        [FromQuery] int? processId = null,
        [FromQuery] int? threadId = null
    )
    {
        if (from.HasValue && to.HasValue && from > to)
        {
            ModelState.AddModelError(nameof(to), "The 'to' query parameter must be after 'from'.");
            ModelState.AddModelError(nameof(from), "The 'from' query parameter must be before 'to'.");
            return ValidationProblem(ModelState);
        }

        var fileInfo = logService.GetCurrentLogFile();
        if (fileInfo.Format is not LogFileFormat.JsonL)
            return ValidationProblem("Only JSONL logs support offset/limit reads.");

        if (!TryBuildLogReadOptions(from, to, offset, limit, descending, level, logger, caller, message, exception, processId, threadId, out var readOptions, out var dslError))
            return ValidationProblem(dslError);

        return new LogReadResult(logService.ReadLogFile(fileInfo, readOptions));
    }

    /// <summary>
    /// Download the current log file.
    /// </summary>
    /// <remarks>
    /// **Log filter DSL** (`logger`, `caller`, `message`, `exception` query parameters):
    ///
    /// - If the value has no **':'** in the string, it is shorthand for <c>c:&lt;entire value&gt;</c> (substring, case-sensitive).
    /// - Otherwise the first character is the **mode**: <c>c</c> contains, <c>=</c> equals, <c>^</c> starts with, <c>$</c> ends with, <c>~</c> fuzzy (always case-insensitive), <c>*</c> regex.
    /// - After the mode and before the first <c>:</c>: optional <c>!</c> (negate) and optional <c>#</c> (case-insensitive for <c>c</c>, <c>=</c>, <c>^</c>, <c>$</c>), each at most once, in either order. <c>#</c> is ignored for <c>~</c> and <c>*</c>.
    /// - **Payload** is everything after the first <c>:</c> only (e.g. <c>c:^:foo</c> contains the substring <c>^:foo</c>).
    /// - **Regex** (<c>*:</c>): raw pattern or <c>/pattern/flags</c>; default case-sensitive; flag <c>i</c> = ignore case.
    /// - **Exception** query param: entry exception text is compared as <c>null</c> coalesced to <c>""</c>. <c>=:</c> matches lines with no exception text; <c>=!:</c> matches lines with non-empty exception text.
    /// - A filter is **inactive** when the query parameter is omitted; empty or whitespace-only values are still valid DSL.
    ///
    /// Plugin authors: same strings as <see cref="LogBaseOptions"/> filter properties.
    /// </remarks>
    /// <param name="from">Optional start date (inclusive, UTC).</param>
    /// <param name="to">Optional end date (inclusive, UTC).</param>
    /// <param name="format">
    /// Format to return the log entries as. Can be <c>simple</c>, <c>full</c>,
    /// <c>json</c> or <c>legacy</c>.
    /// </param>
    /// <param name="level">Comma-separated <see cref="LogLevel"/> names to include.</param>
    /// <param name="logger">Optional DSL filter on logger; inactive when omitted. See remarks.</param>
    /// <param name="caller">Optional DSL filter on caller; inactive when omitted. See remarks.</param>
    /// <param name="message">Optional DSL filter on message; inactive when omitted. See remarks.</param>
    /// <param name="exception">Optional DSL filter on exception text; inactive when omitted. See remarks.</param>
    /// <param name="processId">Exact process id.</param>
    /// <param name="threadId">Exact thread id.</param>
    /// <returns>The current log file download response.</returns>
    [HttpGet("File/Current/Download")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    public ActionResult DownloadCurrentLogFile(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? format = null,
        [FromQuery] string? level = null,
        [FromQuery] string? logger = null,
        [FromQuery] string? caller = null,
        [FromQuery] string? message = null,
        [FromQuery] string? exception = null,
        [FromQuery] int? processId = null,
        [FromQuery] int? threadId = null
    )
    {
        if (from.HasValue && to.HasValue && from > to)
        {
            ModelState.AddModelError(nameof(to), "The 'to' query parameter must be after 'from'.");
            ModelState.AddModelError(nameof(from), "The 'from' query parameter must be before 'to'.");
            return ValidationProblem(ModelState);
        }

        if (!TryParseLogSerializeFormat(format, out var formatEnum, out var fmtError))
            return ValidationProblem(fmtError, nameof(format));

        if (!TryBuildLogDownloadOptions(from, to, formatEnum, level, logger, caller, message, exception, processId, threadId, out var downloadOptions, out var dslError))
            return ValidationProblem(dslError);

        var fileInfo = logService.GetCurrentLogFile();
        var download = logService.DownloadLogFile(fileInfo, downloadOptions);
        return File(download.Stream, download.ContentType, download.FileName);
    }

    /// <summary>
    /// Get a specific log file by identifier.
    /// </summary>
    /// <param name="fileID">Log file identifier.</param>
    /// <returns>The log file metadata.</returns>
    [HttpGet("File/{fileID}")]
    public ActionResult<LogFile> GetLogFileById([FromRoute] Guid fileID)
    {
        if (logService.GetLogFileByID(fileID) is not { } fileInfo)
            return NotFound("Log file not found.");

        return new LogFile(fileInfo);
    }

    /// <summary>
    /// Read a specific log file using line-based paging.
    /// </summary>
    /// <remarks>
    /// **Log filter DSL** (`logger`, `caller`, `message`, `exception` query parameters):
    ///
    /// - If the value has no **':'** in the string, it is shorthand for <c>c:&lt;entire value&gt;</c> (substring, case-sensitive).
    /// - Otherwise the first character is the **mode**: <c>c</c> contains, <c>=</c> equals, <c>^</c> starts with, <c>$</c> ends with, <c>~</c> fuzzy (always case-insensitive), <c>*</c> regex.
    /// - After the mode and before the first <c>:</c>: optional <c>!</c> (negate) and optional <c>#</c> (case-insensitive for <c>c</c>, <c>=</c>, <c>^</c>, <c>$</c>), each at most once, in either order. <c>#</c> is ignored for <c>~</c> and <c>*</c>.
    /// - **Payload** is everything after the first <c>:</c> only (e.g. <c>c:^:foo</c> contains the substring <c>^:foo</c>).
    /// - **Regex** (<c>*:</c>): raw pattern or <c>/pattern/flags</c>; default case-sensitive; flag <c>i</c> = ignore case.
    /// - **Exception** query param: entry exception text is compared as <c>null</c> coalesced to <c>""</c>. <c>=:</c> matches lines with no exception text; <c>=!:</c> matches lines with non-empty exception text.
    /// - A filter is **inactive** when the query parameter is omitted; empty or whitespace-only values are still valid DSL.
    ///
    /// Plugin authors: same strings as <see cref="LogBaseOptions"/> filter properties.
    /// </remarks>
    /// <param name="fileID">Log file identifier.</param>
    /// <param name="from">Optional start date (inclusive, UTC).</param>
    /// <param name="to">Optional end date (inclusive, UTC).</param>
    /// <param name="offset">Line offset to start from.</param>
    /// <param name="limit">Maximum number of entries to return.</param>
    /// <param name="descending">If <c>true</c>, return newest entries first.</param>
    /// <param name="level">Comma-separated <see cref="LogLevel"/> names to include.</param>
    /// <param name="logger">Optional DSL filter on logger; inactive when omitted. See remarks.</param>
    /// <param name="caller">Optional DSL filter on caller; inactive when omitted. See remarks.</param>
    /// <param name="message">Optional DSL filter on message; inactive when omitted. See remarks.</param>
    /// <param name="exception">Optional DSL filter on exception text; inactive when omitted. See remarks.</param>
    /// <param name="processId">Exact process id.</param>
    /// <param name="threadId">Exact thread id.</param>
    /// <returns>The paged log read result.</returns>
    [HttpGet("File/{fileID}/Read")]
    public ActionResult<LogReadResult> GetLogFileById(
        [FromRoute] Guid fileID,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery, Range(0, int.MaxValue)] uint offset = 0,
        [FromQuery, Range(0, 1000)] uint limit = 100,
        [FromQuery] bool descending = false,
        [FromQuery] string? level = null,
        [FromQuery] string? logger = null,
        [FromQuery] string? caller = null,
        [FromQuery] string? message = null,
        [FromQuery] string? exception = null,
        [FromQuery] int? processId = null,
        [FromQuery] int? threadId = null
    )
    {
        if (from.HasValue && to.HasValue && from > to)
        {
            ModelState.AddModelError(nameof(to), "The 'to' query parameter must be after 'from'.");
            ModelState.AddModelError(nameof(from), "The 'from' query parameter must be before 'to'.");
            return ValidationProblem(ModelState);
        }

        if (logService.GetLogFileByID(fileID) is not { } fileInfo)
            return NotFound("Log file not found.");

        if (fileInfo.Format is not LogFileFormat.JsonL)
            return ValidationProblem("Only JSONL logs support offset/limit reads.");

        if (!TryBuildLogReadOptions(from, to, offset, limit, descending, level, logger, caller, message, exception, processId, threadId, out var readOptions, out var dslError))
            return ValidationProblem(dslError);

        return new LogReadResult(logService.ReadLogFile(fileInfo, readOptions));
    }

    /// <summary>
    /// Download a specific log file.
    /// </summary>
    /// <remarks>
    /// **Log filter DSL** (`logger`, `caller`, `message`, `exception` query parameters):
    ///
    /// - If the value has no **':'** in the string, it is shorthand for <c>c:&lt;entire value&gt;</c> (substring, case-sensitive).
    /// - Otherwise the first character is the **mode**: <c>c</c> contains, <c>=</c> equals, <c>^</c> starts with, <c>$</c> ends with, <c>~</c> fuzzy (always case-insensitive), <c>*</c> regex.
    /// - After the mode and before the first <c>:</c>: optional <c>!</c> (negate) and optional <c>#</c> (case-insensitive for <c>c</c>, <c>=</c>, <c>^</c>, <c>$</c>), each at most once, in either order. <c>#</c> is ignored for <c>~</c> and <c>*</c>.
    /// - **Payload** is everything after the first <c>:</c> only (e.g. <c>c:^:foo</c> contains the substring <c>^:foo</c>).
    /// - **Regex** (<c>*:</c>): raw pattern or <c>/pattern/flags</c>; default case-sensitive; flag <c>i</c> = ignore case.
    /// - **Exception** query param: entry exception text is compared as <c>null</c> coalesced to <c>""</c>. <c>=:</c> matches lines with no exception text; <c>=!:</c> matches lines with non-empty exception text.
    /// - A filter is **inactive** when the query parameter is omitted; empty or whitespace-only values are still valid DSL.
    ///
    /// Plugin authors: same strings as <see cref="LogBaseOptions"/> filter properties.
    /// </remarks>
    /// <param name="fileID">Log file identifier.</param>
    /// <param name="from">Optional start date (inclusive, UTC).</param>
    /// <param name="to">Optional end date (inclusive, UTC).</param>
    /// <param name="format">
    /// Format to return the log entries as. Can be <c>simple</c>, <c>full</c>,
    /// <c>json</c> or <c>legacy</c>.
    /// </param>
    /// <param name="level">Comma-separated <see cref="LogLevel"/> names to include.</param>
    /// <param name="logger">Optional DSL filter on logger; inactive when omitted. See remarks.</param>
    /// <param name="caller">Optional DSL filter on caller; inactive when omitted. See remarks.</param>
    /// <param name="message">Optional DSL filter on message; inactive when omitted. See remarks.</param>
    /// <param name="exception">Optional DSL filter on exception text; inactive when omitted. See remarks.</param>
    /// <param name="processId">Exact process id.</param>
    /// <param name="threadId">Exact thread id.</param>
    /// <returns>The file response.</returns>
    [HttpGet("File/{fileID}/Download")]
    [ProducesResponseType(typeof(FileStreamResult), 200)]
    public ActionResult DownloadLogFile(
        [FromRoute] Guid fileID,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? format = null,
        [FromQuery] string? level = null,
        [FromQuery] string? logger = null,
        [FromQuery] string? caller = null,
        [FromQuery] string? message = null,
        [FromQuery] string? exception = null,
        [FromQuery] int? processId = null,
        [FromQuery] int? threadId = null
    )
    {
        if (from.HasValue && to.HasValue && from > to)
        {
            ModelState.AddModelError(nameof(to), "The 'to' query parameter must be after 'from'.");
            ModelState.AddModelError(nameof(from), "The 'from' query parameter must be before 'to'.");
            return ValidationProblem(ModelState);
        }

        if (logService.GetLogFileByID(fileID) is not { } fileInfo)
            return NotFound("Log file not found.");

        try
        {
            if (!TryParseLogSerializeFormat(format, out var formatEnum, out var fmtError))
                return ValidationProblem(fmtError, nameof(format));

            if (!TryBuildLogDownloadOptions(from, to, formatEnum, level, logger, caller, message, exception, processId, threadId, out var downloadOptions, out var dslError))
                return ValidationProblem(dslError);

            var download = logService.DownloadLogFile(fileInfo, downloadOptions);
            return File(download.Stream, download.ContentType, download.FileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound("Log file not found.");
        }
        catch (InvalidOperationException ex)
        {
            return ValidationProblem(ex.Message);
        }
    }

    /// <summary>
    /// Delete a specific log file.
    /// </summary>
    /// <param name="fileID">Log file identifier.</param>
    /// <returns>An empty success response when the file is deleted.</returns>
    [HttpDelete("File/{fileID}")]
    public ActionResult DeleteLogFile([FromRoute] Guid fileID)
    {
        if (logService.GetLogFileByID(fileID) is not { } fileInfo)
            return NotFound("Log file not found.");

        try
        {
            logService.DeleteLogFile(fileInfo);
            return Ok();
        }
        catch (InvalidOperationException)
        {
            return ValidationProblem("Unable to delete the current log file.", nameof(fileID));
        }
    }

    private static bool TryBuildLogReadOptions(
        DateTime? from,
        DateTime? to,
        uint offset,
        uint limit,
        bool descending,
        string? level,
        string? logger,
        string? caller,
        string? message,
        string? exception,
        int? processId,
        int? threadId,
        out LogReadOptions options,
        [NotNullWhen(false)] out Dictionary<string, IReadOnlyList<string>>? errors
    )
    {
        options = new LogReadOptions
        {
            From = from?.ToUniversalTime(),
            To = to?.ToUniversalTime(),
            Offset = offset,
            Limit = limit,
            Descending = descending,
            Levels = ParseLevelsList(level),
            Logger = logger,
            Caller = caller,
            Message = message,
            Exception = exception,
            ProcessId = processId,
            ThreadId = threadId,
        };
        if (!LogService.TryCompileLogEntryFilter(options, out _, out errors))
            return false;

        return true;
    }

    private static bool TryParseLogSerializeFormat(string? format, out LogSerializeFormat value, [NotNullWhen(false)] out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(format))
        {
            value = LogSerializeFormat.Simple;
            return true;
        }

        if (!Enum.TryParse(format.Trim(), ignoreCase: true, out value) || !Enum.IsDefined(typeof(LogSerializeFormat), value))
        {
            value = LogSerializeFormat.Simple;
            error = "Invalid format. Supported values: simple, full, json, legacy.";
            return false;
        }

        return true;
    }

    private static bool TryBuildLogDownloadOptions(
        DateTime? from,
        DateTime? to,
        LogSerializeFormat format,
        string? level,
        string? logger,
        string? caller,
        string? message,
        string? exception,
        int? processId,
        int? threadId,
        out LogDownloadOptions options,
        [NotNullWhen(false)] out Dictionary<string, IReadOnlyList<string>>? errors
    )
    {
        options = new LogDownloadOptions
        {
            From = from?.ToUniversalTime(),
            To = to?.ToUniversalTime(),
            Format = format,
            Levels = ParseLevelsList(level),
            Logger = logger,
            Caller = caller,
            Message = message,
            Exception = exception,
            ProcessId = processId,
            ThreadId = threadId,
        };
        if (!LogService.TryCompileLogEntryFilter(options, out _, out errors))
            return false;

        return true;
    }

    private static IReadOnlyList<LogLevel>? ParseLevelsList(string? level)
    {
        if (string.IsNullOrWhiteSpace(level))
            return null;

        var parsed = new List<LogLevel>();
        foreach (var part in level.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse(part, ignoreCase: true, out LogLevel l))
                parsed.Add(l);
        }

        return parsed.Count > 0 ? parsed : null;
    }
}
