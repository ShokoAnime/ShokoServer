using System;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Metadata.Anidb.Events;

namespace Shoko.Server.Providers.AniDB.Interfaces;

/// <summary>
/// Shared interface for both the UDP and HTTP AniDB connection handlers.
/// </summary>
public interface IConnectionHandler
{
    /// <summary>
    /// The connection type. It's currently a string, but it can be "UDP" or "HTTP".
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Hours from <seealso cref="BanTime"/> until the ban expires.
    /// </summary>
    double BanTimerResetLength { get; }

    /// <summary>
    /// The ban state for this protocol: whether we are banned, when the ban
    /// was registered and when it expires, all in UTC.
    /// </summary>
    AniDbBanState BanState { get; }

    /// <summary>
    /// When the ban was registered, in UTC.
    /// </summary>
    DateTime? BanTime { get; }

    /// <summary>
    /// Dispatched when the <seealso cref="State"/> is updated. Used for internal state updates.
    /// </summary>
    event EventHandler<AniDBStateUpdate>? AniDBStateUpdate;

    /// <summary>
    /// Dispatched when a ban occurs.
    /// </summary>
    event EventHandler<AnidbBanOccurredEventArgs>? BanOccurred;

    /// <summary>
    /// Dispatched when a ban expires.
    /// </summary>
    event EventHandler<AnidbBanOccurredEventArgs>? BanExpired;

    /// <summary>
    /// The current state of the connection.
    /// </summary>
    AniDBStateUpdate State { get; set; }

    /// <summary>
    /// Indicates whether we're currently banned.
    /// </summary>
    [MemberNotNullWhen(true, nameof(BanTime))]
    bool IsBanned { get; }

    /// <summary>
    /// Indicates that the connection is alive and usable.
    /// </summary>
    bool IsAlive { get; }
}
