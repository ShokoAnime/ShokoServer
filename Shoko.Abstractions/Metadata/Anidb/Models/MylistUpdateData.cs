using System;
using Shoko.Abstractions.Metadata.Anidb.Enums;

namespace Shoko.Abstractions.Metadata.Anidb.Models;

/// <summary>
///   Data transfer object (DTO) for updating existing entries in the AniDB
///   MyList, with support for partial updates. Properties that are not set
///   are left unchanged on the entry.
/// </summary>
public sealed class MylistUpdateData
{
    /// <summary>
    ///   The storage state of the entry. If not set, the storage state is left
    ///   unchanged.
    /// </summary>
    public MylistState? State { get; set; }

    /// <summary>
    ///   The file state of the entry. If not set, the file state is left
    ///   unchanged.
    /// </summary>
    public MylistFileState? FileState { get; set; }

    /// <summary>
    ///   Whether the entry is marked as watched. If not set, a
    ///   <see cref="ViewedAt"/> on its own implies watched; with neither set the
    ///   watched state is left unchanged.
    /// </summary>
    public bool? IsViewed { get; set; }

    /// <summary>
    ///   The date and time the entry was watched. Setting this on its own marks
    ///   the entry as watched. If not set, the watched date is left unchanged.
    /// </summary>
    public DateTime? ViewedAt { get; set; }

    /// <summary>
    ///   The storage location of the entry. If not set, the storage location
    ///   is left unchanged.
    /// </summary>
    public string? Storage { get; set; }

    /// <summary>
    ///   The source of the entry. If not set, the source is left unchanged.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    ///   Any other information about the entry. If not set, the other
    ///   information is left unchanged.
    /// </summary>
    public string? Other { get; set; }

    /// <summary>
    ///   Whether no fields are set, making an update with this data a no-op.
    /// </summary>
    public bool IsEmpty =>
        State is null &&
        FileState is null &&
        IsViewed is null &&
        ViewedAt is null &&
        Storage is null &&
        Source is null &&
        Other is null;

    /// <summary>
    ///   Converts an <see cref="MylistAddData"/> to an update with the same
    ///   values, for when an existing entry is updated instead of created.
    /// </summary>
    public static implicit operator MylistUpdateData(MylistAddData data) => new()
    {
        State = data.State,
        FileState = data.FileState,
        IsViewed = data.IsViewed,
        ViewedAt = data.ViewedAt,
        Storage = data.Storage,
        Source = data.Source,
        Other = data.Other,
    };
}
