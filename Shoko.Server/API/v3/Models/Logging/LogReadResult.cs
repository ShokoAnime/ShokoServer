using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

using AbstractLogReadResult = Shoko.Abstractions.Logging.Models.LogReadResult;

#nullable enable
namespace Shoko.Server.API.v3.Models.Logging;

/// <summary>
/// API model containing paged log read data.
/// </summary>
public class LogReadResult
{
    /// <summary>
    /// Next offset to continue reading from, or null if there are no more
    /// entries.
    /// </summary>
    public uint? NextOffset { get; }

    /// <summary>
    /// Returned log entries.
    /// </summary>
    [Required]
    public IReadOnlyList<LogEntry> Entries { get; }

    public LogReadResult(AbstractLogReadResult result)
    {
        NextOffset = result.NextOffset;
        Entries = result.Entries.Select(entry => new LogEntry(entry)).ToList();
    }
}
