using System;
using Shoko.Abstractions.Services;

namespace Shoko.Abstractions.Enums;

/// <summary>
/// Determines how to refresh an AniDB anime in the <see cref="IAnidbService"/>.
/// </summary>
[Flags]
public enum AnidbRefreshMethod : int
{
    /// <summary>
    /// Complex default rules based on settings.
    /// </summary>
    Auto = -1,

    /// <summary>
    /// Do nothing.
    /// </summary>
    None = 0,

    /// <summary>
    /// Default refresh method.
    /// </summary>
    Default = Cache | Remote | DeferToRemoteIfUnsuccessful,

    /// <summary>
    /// Use the remote AniDB HTTP API.
    /// </summary>
    Remote = 1,

    /// <summary>
    /// Use the local AniDB HTTP cache.
    /// </summary>
    Cache = 2,

    /// <summary>
    /// Prefer the local AniDB HTTP cache over the remote AniDB HTTP API.
    /// </summary>
    PreferCacheOverRemote = 4,

    /// <summary>
    /// Defer to a later remote update if the current update fails.
    /// </summary>
    DeferToRemoteIfUnsuccessful = 8,

    /// <summary>
    /// Ignore the time check and forces a refresh even if the anime was
    /// recently updated.
    /// </summary>
    IgnoreTimeCheck = 16,

    /// <summary>
    /// Ignore any active HTTP bans and forcefully asks the server for the data.
    /// </summary>
    IgnoreHttpBans = 32,

    /// <summary>
    /// Download related anime until the maximum depth is reached.
    /// </summary>
    DownloadRelations = 64,

    /// <summary>
    /// Create a Shoko series entry if one does not exist.
    /// </summary>
    CreateShokoSeries = 128,

    /// <summary>
    /// Skip updating related TMDB entities after update.
    /// </summary>
    SkipTmdbUpdate = 256,
}
