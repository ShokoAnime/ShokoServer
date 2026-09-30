using System;
using System.Net;

namespace Shoko.Abstractions.User.Events;

/// <summary>
///   Dispatched when a failed authentication attempt is registered with the
///   authentication throttle. It never carries the password or code that was
///   tried.
/// </summary>
public class AuthenticationFailedEventArgs : EventArgs
{
    /// <summary>
    ///   The remote address of the client the failure was charged to, or
    ///   <see langword="null"/> when it was charged to a user rather than a
    ///   client, or the address could not be read.
    /// </summary>
    public required IPAddress? RemoteAddress { get; init; }

    /// <summary>
    ///   The username the attempt was for, when known: the user's name for a
    ///   failure charged to a user, or the name given to
    ///   <see cref="Services.IAuthenticationThrottleService.ThrottleAuthentication"/>
    ///   earlier in the same request. The name need not belong to a user.
    /// </summary>
    public required string? Username { get; init; }

    /// <summary>
    ///   The user the failure was charged to, when it was charged to a user;
    ///   otherwise <see langword="null"/>.
    /// </summary>
    public required IUser? User { get; init; }

    /// <summary>
    ///   The path of the request or hub connection that failed, so the core's
    ///   sign-in can be told from a plugin's endpoint, or
    ///   <see langword="null"/> when there was no request to read it from.
    ///   Never the query string, but an endpoint that takes a token or code in
    ///   its route puts it here, so mask such segments before storing it.
    /// </summary>
    public required string? Path { get; init; }

    /// <summary>
    ///   Whether this failure started a lockout, rather than adding to one that
    ///   was already running or staying under the attempt limit.
    /// </summary>
    public required bool StartedLockout { get; init; }

    /// <summary>
    ///   When the lockout of the client or user ends, in UTC, if this failure
    ///   left one running (started or extended); otherwise
    ///   <see langword="null"/>.
    /// </summary>
    public required DateTime? LockedOutUntil { get; init; }

    /// <summary>
    ///   When the event occurred.
    /// </summary>
    public required DateTime OccurredAt { get; init; }
}
