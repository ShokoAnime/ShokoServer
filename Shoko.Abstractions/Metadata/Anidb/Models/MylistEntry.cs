using System;
using Shoko.Abstractions.Metadata.Anidb.Enums;

namespace Shoko.Abstractions.Metadata.Anidb.Models;

/// <summary>
///   An AniDB MyList entry.
/// </summary>
public sealed record MylistEntry
{
    /// <summary>
    ///   The AniDB username the entry belongs to.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    ///   The AniDB user ID (uid) the entry belongs to.
    /// </summary>
    public ulong? UserID { get; init; }

    /// <summary>
    ///   The MyList ID (lid) of the entry.
    /// </summary>
    public ulong MylistID { get; init; }

    /// <summary>
    ///   The AniDB anime ID (aid) of the entry.
    /// </summary>
    public int AnimeID { get; init; }

    /// <summary>
    ///   The AniDB episode ID (eid) of the entry.
    /// </summary>
    public int EpisodeID { get; init; }

    /// <summary>
    ///   The AniDB file ID (fid) of the entry.
    /// </summary>
    public int FileID { get; init; }

    /// <summary>
    ///   The ED2K hash of the file. Not part of the AniDB MyList response;
    ///   it is enriched locally from the file's release info when known.
    /// </summary>
    public string? ED2K { get; init; }

    /// <summary>
    ///   The size of the file. Not part of the AniDB MyList response; it is
    ///   enriched locally from the file's release info when known.
    /// </summary>
    public long? Size { get; init; }

    /// <summary>
    ///   The date the entry was last updated on AniDB.
    /// </summary>
    public DateOnly UpdatedAt { get; init; }

    /// <summary>
    ///   Indicates that the video file, or the episode entry for generic
    ///   files, has been watched to completion.
    /// </summary>
    public bool IsViewed { get; init; }

    /// <summary>
    ///   The date and time the video file, or the episode entry for generic
    ///   files, was last watched to completion.
    /// </summary>
    public DateTime? ViewedAt { get; init; }

    /// <summary>
    ///   The storage state of the entry.
    /// </summary>
    public MylistState State { get; init; }

    /// <summary>
    ///   Whether the entry is a generic entry — one that stands for an episode
    ///   rather than for a file the user actually has — or <c>null</c> when
    ///   that could not be determined.
    /// </summary>
    /// <remarks>
    ///   AniDB's MyList export carries no field for this, and the file state is
    ///   only a convention many generic entries do not follow, so it cannot be
    ///   inferred from the entry alone. It is <c>true</c> for an entry obtained
    ///   through a generic operation, resolved for the rest when the server has
    ///   a supplementary index of generic file IDs available, and <c>null</c>
    ///   when it does not — which is a genuine "unknown", not a "no".
    /// </remarks>
    public bool? IsGeneric { get; init; }

    /// <summary>
    ///   The file state of the entry — the Type in AniDB's UI. It is the
    ///   user's to set and does not indicate whether the entry is generic.
    /// </summary>
    public MylistFileState FileState { get; init; }

    /// <summary>
    ///   The storage location of the video file, be it physical or virtual.
    ///   Set by the user, for the user.
    /// </summary>
    public string? Storage { get; init; }

    /// <summary>
    ///   The source media of the video file, be it physical or digital. Set
    ///   by the user, for the user.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    ///   Other information about the video file. Set by the user, for the
    ///   user.
    /// </summary>
    public string? Other { get; init; }
}
