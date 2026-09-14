using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Shoko.Abstractions.User.Services;

/// <summary>
/// Tracks failed authentication attempts per client and per user, and blocks further attempts with an
/// escalating lockout once the allowed number of attempts within the attempt window is exceeded.
/// </summary>
/// <remarks>
/// <para>
/// The service is a singleton shared by the core and every plugin, so a client or user locked out
/// by one endpoint is locked out for all of them. This includes
/// <see cref="IUserService.AuthenticateUser"/>, which already throttles by username on its own.
/// </para>
/// <para>
/// The policy values are configured by the server admin, and change at runtime whenever the server
/// settings are saved. Read them when they are needed instead of caching them.
/// </para>
/// <para>
/// Failures can only be registered for existing users here, while
/// <see cref="IUserService.AuthenticateUser"/> registers them for any username. Check a username with
/// <see cref="ThrottleAuthentication"/> before looking up the user, so a locked out user gets the same
/// answer as an unknown one and the response doesn't reveal which usernames exist.
/// </para>
/// </remarks>
public interface IAuthenticationThrottleService
{
    /// <summary>
    /// The number of failed attempts within <see cref="AttemptWindow"/> that are allowed before the
    /// client or user is locked out. Between 1 and 100, and 10 by default.
    /// </summary>
    int MaxFailedAttempts { get; }

    /// <summary>
    /// The sliding window in which failed attempts are counted. Between 1 minute and 1 day, and 15
    /// minutes by default.
    /// </summary>
    TimeSpan AttemptWindow { get; }

    /// <summary>
    /// The lockout duration applied on the first excess attempt, doubling for every attempt after that.
    /// Between 1 minute and 1 day, never above <see cref="MaxLockout"/>, and 15 minutes by default.
    /// </summary>
    TimeSpan InitialLockout { get; }

    /// <summary>
    /// The maximum lockout duration, regardless of how many failed attempts have been made. Lowering it
    /// also shortens existing lockouts. Between 15 minutes and 7 days, and 1 day by default.
    /// </summary>
    TimeSpan MaxLockout { get; }

    /// <summary>
    /// Checks whether an authentication attempt for the username from the client making the HTTP
    /// request should be blocked, because either the client or the username is locked out. When it
    /// should, the <c>Retry-After</c> response header is set to the longer of the two remaining
    /// lockouts, and the throttled attempt is logged.
    /// </summary>
    /// <remarks>
    /// Call this before looking up or authenticating the user. The username need not belong to an
    /// existing user, so unknown and existing usernames get the same answer. This only reads lockouts,
    /// so record the outcome of the attempt with <see cref="RegisterFailure(HttpContext)"/> or
    /// <see cref="Reset(HttpContext)"/> afterwards. <see cref="IUserService.AuthenticateUser"/> already
    /// records the outcome for the username itself.
    /// </remarks>
    /// <param name="context">The HTTP context of the request.</param>
    /// <param name="username">The username the authentication attempt is for.</param>
    /// <returns>A <c>429 Too Many Requests</c> result if the attempt should be blocked; otherwise <see langword="null"/>.</returns>
    StatusCodeResult? ThrottleAuthentication(HttpContext context, string username);

    /// <summary>
    /// Gets the remaining lockout for the client making the HTTP request, if any.
    /// </summary>
    /// <param name="context">The HTTP context of the request.</param>
    /// <returns>The remaining lockout duration; otherwise <see langword="null"/> if not locked out.</returns>
    TimeSpan? GetRemainingLockout(HttpContext context);

    /// <summary>
    /// Gets the remaining lockout for the client behind the SignalR hub connection, if any.
    /// </summary>
    /// <param name="context">The hub caller context of the connection.</param>
    /// <returns>The remaining lockout duration; otherwise <see langword="null"/> if not locked out.</returns>
    TimeSpan? GetRemainingLockout(HubCallerContext context);

    /// <summary>
    /// Gets the remaining lockout for the user, if any.
    /// </summary>
    /// <param name="user">The user to check.</param>
    /// <returns>The remaining lockout duration; otherwise <see langword="null"/> if not locked out.</returns>
    TimeSpan? GetRemainingLockout(IUser user);

    /// <summary>
    /// Registers a failed authentication attempt for the client making the HTTP request, starting or
    /// extending the lockout if needed.
    /// </summary>
    /// <param name="context">The HTTP context of the request.</param>
    void RegisterFailure(HttpContext context);

    /// <summary>
    /// Registers a failed authentication attempt for the client behind the SignalR hub connection, starting
    /// or extending the lockout if needed.
    /// </summary>
    /// <param name="context">The hub caller context of the connection.</param>
    void RegisterFailure(HubCallerContext context);

    /// <summary>
    /// Registers a failed authentication attempt for the user, starting or extending the lockout if needed.
    /// </summary>
    /// <param name="user">The user to register the failure for.</param>
    void RegisterFailure(IUser user);

    /// <summary>
    /// Clears all tracked failures for the client making the HTTP request, as is appropriate after
    /// a successful authentication.
    /// </summary>
    /// <param name="context">The HTTP context of the request.</param>
    void Reset(HttpContext context);

    /// <summary>
    /// Clears all tracked failures for the client behind the SignalR hub connection, as is
    /// appropriate after a successful authentication.
    /// </summary>
    /// <param name="context">The hub caller context of the connection.</param>
    void Reset(HubCallerContext context);

    /// <summary>
    /// Clears all tracked failures for the user, as is appropriate after a successful authentication.
    /// </summary>
    /// <param name="user">The user to reset.</param>
    void Reset(IUser user);
}
