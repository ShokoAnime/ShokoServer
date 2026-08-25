namespace Shoko.Abstractions.Metadata.Anidb.Enums;

/// <summary>
///   How to record a locally watched episode when the MyList already holds one
///   or more file entries for it but no generic entry. With a generic entry
///   present the sync simply reconciles it, and with nothing present it creates
///   one; this only governs the case in between.
/// </summary>
public enum MylistWatchedEpisodeMode : byte
{
    /// <summary>
    ///   Leave the MyList alone. The watch is not exported, on the grounds
    ///   that AniDB already has an entry covering the episode.
    /// </summary>
    Ignore = 0,

    /// <summary>
    ///   Set the watched state on the oldest of those entries — the lowest
    ///   MyList ID, falling back to the lowest file ID when the entry carries
    ///   no list ID yet. Records the watch against an entry that already
    ///   exists rather than adding a second one for the same episode.
    /// </summary>
    AttachToOldest = 1,

    /// <summary>
    ///   Add a generic entry alongside the file entries, leaving the episode
    ///   covered twice.
    /// </summary>
    CreateGeneric = 2,
}
