using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.User.Events;
using Shoko.Abstractions.User.Services;

namespace Shoko.Server.API.SignalR;

/// <summary>
///   Closes every hub connection, on every hub, whose API token is
///   invalidated, so a revoked token stops receiving events at once instead
///   of until the client reconnects.
/// </summary>
public sealed class ApiTokenHubFilter : IHubFilter, IDisposable
{
    /// <summary>
    ///   The claim the API key is kept in on an authenticated connection.
    /// </summary>
    private const string ApiKeyClaim = "apikey";

    private readonly IUserService _userService;

    private readonly ILogger<ApiTokenHubFilter> _logger;

    /// <summary>
    ///   The open connections of each token, by the token's key, then by
    ///   connection ID.
    /// </summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, HubCallerContext>> _connections = new(StringComparer.Ordinal);

    /// <summary>
    ///   Starts listening for invalidated tokens.
    /// </summary>
    /// <param name="userService">Raises the invalidated tokens, and looks a connection's token up again.</param>
    /// <param name="logger">The logger.</param>
    public ApiTokenHubFilter(IUserService userService, ILogger<ApiTokenHubFilter> logger)
    {
        _userService = userService;
        _logger = logger;
        _userService.ApiTokenInvalidated += OnApiTokenInvalidated;
    }

    /// <inheritdoc />
    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        await next(context);

        var connection = context.Context;
        if (GetKey(connection) is not { } key)
            return;

        _connections.GetOrAdd(key, _ => new(StringComparer.Ordinal))[connection.ConnectionId] = connection;

        // The token may have been invalidated while the connection was being
        // set up, after it was authenticated and before it was recorded here.
        if (connection.GetHttpContext() is { } httpContext && _userService.GetApiTokenFromHttpContext(httpContext) is null)
            Close(key);
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
                connections.TryRemove(connection.ConnectionId, out _);
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
    ///   Closes and forgets every connection of a token.
    /// </summary>
    /// <param name="key">The token's key.</param>
    private void Close(string key)
    {
        if (!_connections.TryRemove(key, out var connections))
            return;

        foreach (var connection in connections.Values)
        {
            try
            {
                connection.Abort();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to close hub connection {ConnectionID} of an invalidated API token.", connection.ConnectionId);
            }
        }

        if (connections.Count > 0)
            _logger.LogDebug("Closed {Count} hub connections of an invalidated API token.", connections.Count);
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
        => _userService.ApiTokenInvalidated -= OnApiTokenInvalidated;
}
