using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.User.Events;
using Shoko.Abstractions.User.Services;

namespace Shoko.Server.API.SignalR;

/// <summary>
///   Closes every hub connection, on every hub, whose API token is
///   invalidated or expires, so the token stops receiving events at once
///   instead of until the client reconnects.
/// </summary>
public sealed class ApiTokenHubFilter : IHubFilter, IDisposable
{
    /// <summary>
    ///   The claim the API key is kept in on an authenticated connection.
    /// </summary>
    private const string ApiKeyClaim = "apikey";

    /// <summary>
    ///   The longest a timer may wait; a later expiry is waited for in steps.
    /// </summary>
    private static readonly TimeSpan LongestWait = TimeSpan.FromDays(7);

    private readonly IUserService _userService;

    private readonly ILogger<ApiTokenHubFilter> _logger;

    private readonly TimeProvider _timeProvider;

    /// <summary>
    ///   The open connections of each token, by the token's key, then by
    ///   connection ID.
    /// </summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, TrackedConnection>> _connections = new(StringComparer.Ordinal);

    /// <summary>
    ///   Starts listening for invalidated tokens.
    /// </summary>
    /// <param name="userService">Raises the invalidated tokens, and looks a connection's token up again.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="timeProvider">The clock expiries are timed with.</param>
    public ApiTokenHubFilter(IUserService userService, ILogger<ApiTokenHubFilter> logger, TimeProvider? timeProvider = null)
    {
        _userService = userService;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _userService.ApiTokenInvalidated += OnApiTokenInvalidated;
    }

    /// <inheritdoc />
    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        await next(context);

        var connection = context.Context;
        if (GetKey(connection) is not { } key)
            return;

        var tracked = new TrackedConnection(connection);
        _connections.GetOrAdd(key, _ => new(StringComparer.Ordinal))[connection.ConnectionId] = tracked;

        if (connection.GetHttpContext() is not { } httpContext)
            return;

        // The token may have been invalidated while the connection was being
        // set up, after it was authenticated and before it was recorded here.
        if (_userService.GetApiTokenFromHttpContext(httpContext) is not { } token)
        {
            Close(key);
            return;
        }

        if (token.ExpiresAt is { } expiresAt)
            ScheduleExpiry(key, tracked, expiresAt);
    }

    /// <inheritdoc />
    public async Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        try
        {
            await next(context, exception);
        }
        finally
        {
            var connection = context.Context;
            if (GetKey(connection) is { } key && _connections.TryGetValue(key, out var connections))
            {
                if (connections.TryRemove(connection.ConnectionId, out var tracked))
                    tracked.Dispose();
                if (connections.IsEmpty)
                    _connections.TryRemove(new(key, connections));
            }
        }
    }

    /// <summary>
    ///   Closes the connections of an invalidated token.
    /// </summary>
    private void OnApiTokenInvalidated(object? sender, ApiTokenChangedEventArgs eventArgs)
        => Close(eventArgs.ApiToken.Token);

    /// <summary>
    ///   Closes a connection once its token expires.
    /// </summary>
    /// <param name="key">The token's key.</param>
    /// <param name="tracked">The connection.</param>
    /// <param name="expiresAt">When the token expires, in local time, as the tokens are stored.</param>
    private void ScheduleExpiry(string key, TrackedConnection tracked, DateTime expiresAt)
    {
        var wait = expiresAt - _timeProvider.GetLocalNow().DateTime;
        if (wait <= TimeSpan.Zero)
        {
            CloseExpired(key, tracked);
            return;
        }

        var step = wait > LongestWait ? LongestWait : wait;
        tracked.Replace(_timeProvider.CreateTimer(
            _ =>
            {
                if (wait > LongestWait)
                    ScheduleExpiry(key, tracked, expiresAt);
                else
                    CloseExpired(key, tracked);
            },
            null,
            step,
            Timeout.InfiniteTimeSpan
        ));
    }

    /// <summary>
    ///   Closes and forgets a connection whose token expired.
    /// </summary>
    /// <param name="key">The token's key.</param>
    /// <param name="tracked">The connection.</param>
    private void CloseExpired(string key, TrackedConnection tracked)
    {
        if (!_connections.TryGetValue(key, out var connections) || !connections.TryRemove(tracked.Connection.ConnectionId, out _))
            return;

        if (connections.IsEmpty)
            _connections.TryRemove(new(key, connections));

        Abort(tracked);
        _logger.LogDebug("Closed hub connection {ConnectionID} of an expired API token.", tracked.Connection.ConnectionId);
    }

    /// <summary>
    ///   Closes and forgets every connection of a token.
    /// </summary>
    /// <param name="key">The token's key.</param>
    private void Close(string key)
    {
        if (!_connections.TryRemove(key, out var connections))
            return;

        foreach (var tracked in connections.Values)
            Abort(tracked);

        if (connections.Count > 0)
            _logger.LogDebug("Closed {Count} hub connections of an invalidated API token.", connections.Count);
    }

    /// <summary>
    ///   Stops a connection's timer and closes the connection.
    /// </summary>
    /// <param name="tracked">The connection.</param>
    private void Abort(TrackedConnection tracked)
    {
        tracked.Dispose();
        try
        {
            tracked.Connection.Abort();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to close hub connection {ConnectionID}.", tracked.Connection.ConnectionId);
        }
    }

    /// <summary>
    ///   Reads the API key a connection was authenticated with.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <returns>The key, or <c>null</c> when the connection has none.</returns>
    private static string? GetKey(HubCallerContext connection)
        => connection.User?.FindFirst(ApiKeyClaim)?.Value is { Length: > 0 } key ? key : null;

    /// <inheritdoc />
    public void Dispose()
    {
        _userService.ApiTokenInvalidated -= OnApiTokenInvalidated;
        foreach (var connections in _connections.Values)
            foreach (var tracked in connections.Values)
                tracked.Dispose();
        _connections.Clear();
    }

    /// <summary>
    ///   An open connection, with the timer that closes it when its token
    ///   expires, if it does.
    /// </summary>
    /// <param name="connection">The connection.</param>
    private sealed class TrackedConnection(HubCallerContext connection) : IDisposable
    {
        private ITimer? _expiry;

        /// <summary>
        ///   The connection.
        /// </summary>
        public HubCallerContext Connection { get; } = connection;

        /// <summary>
        ///   Swaps in a new expiry timer, stopping the old one.
        /// </summary>
        /// <param name="timer">The new timer.</param>
        public void Replace(ITimer timer)
            => Interlocked.Exchange(ref _expiry, timer)?.Dispose();

        /// <inheritdoc />
        public void Dispose()
            => Interlocked.Exchange(ref _expiry, null)?.Dispose();
    }
}
