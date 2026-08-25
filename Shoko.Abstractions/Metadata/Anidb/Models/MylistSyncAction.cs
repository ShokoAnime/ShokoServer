using System;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Anidb.Models;

/// <summary>
///   One step of a MyList sync. A sync run returns these describing what it
///   did; a preview run returns the same shape describing what it would do,
///   having done none of it.
///
///   Which identity fields are set depends on what the step acts on, and the
///   set of them mirrors the ways an entry can be addressed: by list ID, by
///   file ID, by hash and size, or by anime and episode.
/// </summary>
public record MylistSyncAction
{
    /// <summary>
    ///   What this step does.
    /// </summary>
    public required MylistSyncActionKind Kind { get; init; }

    /// <summary>
    ///   Which side this step writes to, derived from <see cref="Kind"/>.
    ///   Prefer this over inspecting the kind when all that matters is whether
    ///   the local library or the MyList changes.
    /// </summary>
    public MylistSyncDirection Direction => Kind switch
    {
        MylistSyncActionKind.ImportWatchedState => MylistSyncDirection.Import,
        MylistSyncActionKind.ExportWatchedState => MylistSyncDirection.Export,
        MylistSyncActionKind.ExportEntryAddition => MylistSyncDirection.Export,
        MylistSyncActionKind.ExportEntryRemoval => MylistSyncDirection.Export,
        // deliberately not a catch-all: a new kind has to be classified here
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unclassified Mylist sync action kind"),
    };

    /// <summary>
    ///   A short line describing the step, suitable for showing in a preview.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    ///   The local video the step concerns, if it concerns one.
    /// </summary>
    public int? VideoID { get; init; }

    /// <summary>
    ///   The local episode the step concerns, if it concerns one.
    /// </summary>
    public int? EpisodeID { get; init; }

    /// <summary>
    ///   The list ID (lid) of the entry, when AniDB already holds one.
    /// </summary>
    public ulong? MylistID { get; init; }

    /// <summary>
    ///   The file ID (fid) of the entry, when it stands for a real file.
    /// </summary>
    public int? FileID { get; init; }

    /// <summary>
    ///   The ED2K hash identifying the entry, when it is addressed that way.
    /// </summary>
    public string? ED2K { get; init; }

    /// <summary>
    ///   The file size paired with <see cref="ED2K"/>.
    /// </summary>
    public long? FileSize { get; init; }

    /// <summary>
    ///   The anime ID, when the entry is a generic one addressed by episode.
    /// </summary>
    public int? AnidbAnimeID { get; init; }

    /// <summary>
    ///   The episode type, paired with <see cref="AnidbAnimeID"/>.
    /// </summary>
    public EpisodeType? EpisodeType { get; init; }

    /// <summary>
    ///   The episode number, paired with <see cref="AnidbAnimeID"/>.
    /// </summary>
    public int? EpisodeNumber { get; init; }

    /// <summary>
    ///   The watched date being written, in whichever direction the step goes.
    ///   <c>null</c> on a step that carries a watched state means "not
    ///   watched", so read it together with <see cref="Kind"/>.
    /// </summary>
    public DateTime? WatchedAt { get; init; }

    /// <summary>
    ///   The storage state being written, if the step sets one.
    /// </summary>
    public MylistState? State { get; init; }

    /// <summary>
    ///   The delete type a removal applies, if the step is a removal.
    /// </summary>
    public MylistDeleteType? DeleteType { get; init; }
}
