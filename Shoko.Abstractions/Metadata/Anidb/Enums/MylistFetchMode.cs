using System;

namespace Shoko.Abstractions.Metadata.Anidb.Enums;

/// <summary>
///   How to fetch entries from the AniDB MyList. The freshness of the local
///   cache only gates the HTTP transport, while UDP is never time-gated.
/// </summary>
[Flags]
public enum MylistFetchMode : sbyte
{
    /// <summary>
    ///   Use the default fetch mode configured in the server settings.
    /// </summary>
    Auto = -1,

    /// <summary>
    ///   Do nothing. No cache lookups and no remote fetches.
    /// </summary>
    None = 0,

    /// <summary>
    ///   Fetch the full MyList over HTTP, refreshing the local cache.
    /// </summary>
    Http = 1,

    /// <summary>
    ///   Fetch single entries over UDP.
    /// </summary>
    Udp = 2,

    /// <summary>
    ///   Allow serving entries from the local cache.
    /// </summary>
    Cache = 4,

    /// <summary>
    ///   Bypass the time check that gates remote fetches when the cache is
    ///   fresh. Also bypasses the sync schedule gate during a sync.
    /// </summary>
    IgnoreTimeCheck = 8,

    /// <summary>
    ///   The default fetch mode; also the default value of the setting.
    /// </summary>
    Default = Http | Cache | Udp,
}
