using System;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.User;
using Shoko.Abstractions.User.Services;
using Shoko.Abstractions.Video;

namespace Shoko.Abstractions.Metadata.Anidb.Models;

/// <summary>
///   One step of a MyList sync. A sync run returns these describing what it
///   did; a plan-only run returns the same shape describing what it would do,
///   having done none of it.
///
///   A step carries the things it acts on rather than their IDs, so a caller
///   can show a plan without looking every entity up again. Which of them are
///   set depends on what the step acts on.
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
        MylistSyncActionKind.NoOperation => MylistSyncDirection.None,
        MylistSyncActionKind.ImportWatchedState => MylistSyncDirection.Import,
        MylistSyncActionKind.ExportWatchedState => MylistSyncDirection.Export,
        MylistSyncActionKind.ExportEntryAddition => MylistSyncDirection.Export,
        MylistSyncActionKind.ExportEntryRemoval => MylistSyncDirection.Export,
        // deliberately not a catch-all: a new kind has to be classified here
        _ => throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Unclassified MyList sync action kind"),
    };

    /// <summary>
    ///   Whether the step concerns a file.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Video))]
    public bool HasVideo => Video is not null;

    /// <summary>
    ///   Whether the step concerns an episode. True even for an episode the
    ///   collection does not have, since a generic entry can cover one.
    /// </summary>
    [MemberNotNullWhen(true, nameof(AnidbEpisode))]
    public bool HasEpisode => AnidbEpisode is not null;

    /// <summary>
    ///   Whether the episode the step concerns is one the collection has. Only
    ///   then is there anywhere to write an imported watched state. The local
    ///   episode is never carried without the AniDB one it belongs to, so this
    ///   gives both.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ShokoEpisode), nameof(AnidbEpisode))]
    public bool HasLocalEpisode => ShokoEpisode is not null;

    /// <summary>
    ///   Whether AniDB already holds an entry for what the step concerns. False
    ///   on a step that creates one.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Entry))]
    public bool HasEntry => Entry is not null;

    /// <summary>
    ///   Whether <see cref="Entry"/> really is the entry for <see cref="Video"/>
    ///   or <see cref="AnidbEpisode"/>.
    ///
    ///   A step built by a sync is consistent by construction, but a plan can be
    ///   assembled by a client or a plugin, and pairing a list id with the wrong
    ///   file or episode would edit somebody else's entry. Where the entry does
    ///   not carry enough to tell — no hash, no episode id — this reads
    ///   <c>true</c>, since the pairing cannot be disproved.
    /// </summary>
    public bool IsEntryConsistent
    {
        get
        {
            if (Entry is not { } entry)
                return true;

            if (Video is { } video && entry is { ED2K: not null, Size: > 0 })
                return string.Equals(entry.ED2K, video.ED2K, StringComparison.OrdinalIgnoreCase) && entry.Size == video.Size;

            if (AnidbEpisode is { } episode && entry.EpisodeID is not 0)
                return entry.EpisodeID == episode.ID;

            return true;
        }
    }

    /// <summary>
    ///   A short line describing the step, suitable for showing to a user.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    ///   The local video the step concerns, when it concerns a file.
    /// </summary>
    public IVideo? Video { get; init; }

    /// <summary>
    ///   The local episode the step concerns, when it concerns an episode
    ///   rather than one of its files and the episode is in the collection.
    ///   Never set without <see cref="AnidbEpisode"/>.
    /// </summary>
    public IShokoEpisode? ShokoEpisode { get; init; }

    /// <summary>
    ///   The AniDB episode the step concerns. A generic entry is addressed by
    ///   anime, episode type and episode number, all of which are on here, and
    ///   unlike <see cref="ShokoEpisode"/> this is still known for an entry
    ///   covering an episode the collection does not have.
    /// </summary>
    public IAnidbEpisode? AnidbEpisode { get; init; }

    /// <summary>
    ///   The local watched state for <see cref="Video"/>, when there is one.
    ///   A file has none until it is watched, so this can be null even when
    ///   <see cref="Video"/> is set. The local side of the comparison that
    ///   produced the step, as <see cref="Entry"/> is the AniDB side.
    /// </summary>
    public IVideoUserData? VideoUserData { get; init; }

    /// <summary>
    ///   The local watched state for <see cref="ShokoEpisode"/>, when one has
    ///   been recorded.
    ///
    ///   <see cref="IUserDataService.GetEpisodeUserData"/> never returns null,
    ///   but it gets there by writing a record when none exists. This is a
    ///   snapshot rather than an accessor, and a plan-only run must not write,
    ///   so an episode nobody has touched yet has none here.
    /// </summary>
    public IEpisodeUserData? EpisodeUserData { get; init; }

    /// <summary>
    ///   The MyList entry as it stands right now, or <c>null</c> when the step
    ///   is what creates it. Everything needed to address the entry on AniDB —
    ///   its list ID, file ID, hash and size — is on here.
    /// </summary>
    public MylistEntry? Entry { get; init; }

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
