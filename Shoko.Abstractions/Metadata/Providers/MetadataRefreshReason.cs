namespace Shoko.Abstractions.Metadata.Providers;

/// <summary>
///   Why the core asked a provider to refresh an entry.
/// </summary>
public enum MetadataRefreshReason
{
    /// <summary>
    ///   Routine upkeep, such as AniDB telling the core about the anime the
    ///   entry is linked to, or a library-wide refresh.
    /// </summary>
    Scheduled = 0,

    /// <summary>
    ///   The entry was just linked to an anime.
    /// </summary>
    Linked = 1,

    /// <summary>
    ///   Somebody asked for the entry to be refreshed, such as through an
    ///   action or the API.
    /// </summary>
    Requested = 2,
}
