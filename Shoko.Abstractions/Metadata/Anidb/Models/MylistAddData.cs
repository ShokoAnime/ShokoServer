using System;
using Shoko.Abstractions.Metadata.Anidb.Enums;

namespace Shoko.Abstractions.Metadata.Anidb.Models;

/// <summary>
///   Data transfer object (DTO) for adding new entries to the AniDB MyList,
///   containing the initial data for the entry.
/// </summary>
public sealed class MylistAddData
{
    /// <summary>
    ///   The storage state of the entry. If not set, the configured default
    ///   storage state will be used.
    /// </summary>
    public MylistState? State { get; set; }

    /// <summary>
    ///   The file state of the entry — the Type in AniDB's UI. If not set,
    ///   AniDB creates the entry with a normal file state, for generic entries
    ///   just as for real files.
    /// </summary>
    public MylistFileState? FileState { get; set; }

    /// <summary>
    ///   Whether the entry is marked as watched. If not set, the local
    ///   watched state of the file or episode will be used.
    /// </summary>
    public bool? IsViewed { get; set; }

    /// <summary>
    ///   The date and time the entry was watched. Setting this on its own marks
    ///   the entry as watched. If not set, the local watched date of the file or
    ///   episode will be used.
    /// </summary>
    public DateTime? ViewedAt { get; set; }

    /// <summary>
    ///   The storage location of the entry.
    /// </summary>
    public string? Storage { get; set; }

    /// <summary>
    ///   The source of the entry.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    ///   Any other information about the entry.
    /// </summary>
    public string? Other { get; set; }
}
