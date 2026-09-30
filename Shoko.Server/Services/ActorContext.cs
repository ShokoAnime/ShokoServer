using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.User;
using Shoko.Abstractions.User.Services;

namespace Shoko.Server.Services;

/// <summary>
///   Keeps who the current flow runs for in one <see cref="AsyncLocal{T}"/>,
///   shared by every instance, so the core's static event raisers can read it
///   through <see cref="CurrentActor"/>.
/// </summary>
/// <remarks>
///   A scope ends for every copy of the flow once disposed, as the link change
///   tracker's reasons do: a timer or task started inside a request goes back
///   to the actor outside the request once the request is over, instead of
///   keeping the request's actor for as long as it runs.
/// </remarks>
public sealed class ActorContext : IActorContext
{
    #region Fields

    /// <summary>
    ///   The innermost scope begun in the current flow, if any.
    /// </summary>
    private static readonly AsyncLocal<Scope?> _scope = new();

    #endregion

    #region Current

    /// <summary>
    ///   The actor of the current flow: the token of the innermost scope that
    ///   is still active, or <see langword="null"/> when there is none.
    /// </summary>
    public static ApiToken? CurrentActor
    {
        get
        {
            for (var scope = _scope.Value; scope is not null; scope = scope.Previous)
            {
                if (scope.IsActive)
                    return scope.Token;
            }

            return null;
        }
    }

    /// <inheritdoc />
    public ApiToken? Current => CurrentActor;

    #endregion

    #region Scopes

    /// <inheritdoc />
    public IDisposable BeginScope(ApiToken? token)
        => Begin(token);

    /// <summary>
    ///   Makes <paramref name="token"/> the actor of the current flow until the
    ///   returned scope is disposed.
    /// </summary>
    /// <param name="token">The token to act for, or <see langword="null"/> for the system.</param>
    /// <returns>The scope, which puts back the previous actor once disposed.</returns>
    public static IDisposable Begin(ApiToken? token)
    {
        var scope = new Scope(token, _scope.Value);
        _scope.Value = scope;
        return scope;
    }

    /// <summary>
    ///   Wraps fire-and-forget work a caller starts for itself, such as a
    ///   maintenance action run from a request that returns at once, so it runs
    ///   for the current actor for as long as it runs, not only until the
    ///   request ends.
    /// </summary>
    /// <param name="work">The work to run later.</param>
    /// <returns>The work, run in a scope of its own for the actor current now.</returns>
    public static Func<Task> Carry(Func<Task> work)
    {
        var actor = CurrentActor;
        return async () =>
        {
            using (Begin(actor))
                await work().ConfigureAwait(false);
        };
    }

    /// <summary>
    ///   Wraps fire-and-forget work a caller starts for itself, so it runs for
    ///   the current actor for as long as it runs, not only until the request
    ///   that started it ends.
    /// </summary>
    /// <param name="work">The work to run later.</param>
    /// <returns>The work, run in a scope of its own for the actor current now.</returns>
    public static Action Carry(Action work)
    {
        var actor = CurrentActor;
        return () =>
        {
            using (Begin(actor))
                work();
        };
    }

    /// <summary>
    ///   One actor set for a flow, until it is disposed.
    /// </summary>
    /// <param name="token">The token, or <see langword="null"/> for the system.</param>
    /// <param name="previous">The scope it was begun in.</param>
    private sealed class Scope(ApiToken? token, Scope? previous) : IDisposable
    {
        // Read by every copy of the flow, on whatever thread it runs.
        private volatile bool _isActive = true;

        /// <summary>
        ///   The token, or <see langword="null"/> for the system.
        /// </summary>
        public ApiToken? Token => token;

        /// <summary>
        ///   The scope it was begun in, which the flow returns to.
        /// </summary>
        public Scope? Previous => previous;

        /// <summary>
        ///   Whether it still applies, in every copy of the flow.
        /// </summary>
        public bool IsActive => _isActive;

        /// <inheritdoc />
        public void Dispose()
        {
            _isActive = false;
            if (_scope.Value == this)
                _scope.Value = previous;
        }
    }

    #endregion
}
