using System;

namespace Shoko.Abstractions.User.Services;

/// <summary>
///   Who the current flow of work is done for: the API token of the request,
///   hub call or queued job it belongs to, or <see langword="null"/> for the
///   system. Set by the core, and carried by the events that have an
///   <c>Actor</c>, stamped when they are raised.
/// </summary>
/// <remarks>
///   The actor follows the async flow, so tasks and timers started inside a
///   scope see it, but only until the scope is disposed. Work that outlives
///   the operation, such as a loop or a timer, should begin a scope of its own.
/// </remarks>
public interface IActorContext
{
    /// <summary>
    ///   The API token the current flow runs for, or <see langword="null"/>
    ///   when it runs for the system or outside any scope.
    /// </summary>
    ApiToken? Current { get; }

    /// <summary>
    ///   Makes <paramref name="token"/> the actor of the current flow until the
    ///   returned scope is disposed.
    /// </summary>
    /// <param name="token">
    ///   The API token to act for, or <see langword="null"/> to act for the
    ///   system, as a background loop started from a request should.
    /// </param>
    /// <returns>
    ///   A scope that puts back the previous actor once disposed. Disposing it
    ///   also ends the actor for every task and timer started inside it.
    /// </returns>
    IDisposable BeginScope(ApiToken? token);
}
