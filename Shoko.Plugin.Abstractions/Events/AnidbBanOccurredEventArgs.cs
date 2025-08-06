using System;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Dispatched when an AniDB ban has occurred.
/// </summary>
public class AnidbBanOccurredEventArgs : EventArgs
{
    /// <summary>
    /// The type of ban.
    /// </summary>
    public required AnidbBanType Type { get; init; }

    /// <summary>
    /// When the ban occurred. It should be basically "now" for new events.
    /// </summary>
    /// <remarks>
    /// Will be set to the UNIX epoch for the initial event before we receive a
    /// ban.
    /// </remarks>
    public required DateTime OccurredAt { get; init; }

    /// <summary>
    /// When Shoko will resume the communications. This time is just a guess. We
    /// get no data or hint of any kind for this value to prevent additional
    /// bans.
    /// </summary>
    /// <remarks>
    /// Will be set to the UNIX epoch for the initial event before we receive a
    /// ban.
    /// </remarks>
    public required DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Indicates if the ban is still active.
    /// </summary>
    public bool IsBanned => ExpiresAt > DateTime.UtcNow;
}
