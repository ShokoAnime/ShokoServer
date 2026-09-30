using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Abstractions.User;
using Shoko.Abstractions.User.Services;

namespace Shoko.Server.API.SignalR;

/// <summary>
///   Makes the connection's API token the actor of each hub method call, and
///   of connecting and disconnecting, on every hub.
/// </summary>
/// <param name="actorContext">Where the actor is set.</param>
/// <param name="userService">Where the connection's token is read.</param>
public class ActorHubFilter(IActorContext actorContext, IUserService userService) : IHubFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        using (actorContext.BeginScope(GetToken(invocationContext.Context)))
            return await next(invocationContext);
    }

    /// <inheritdoc />
    public async Task OnConnectedAsync(HubLifetimeContext context, Func<HubLifetimeContext, Task> next)
    {
        using (actorContext.BeginScope(GetToken(context.Context)))
            await next(context);
    }

    /// <inheritdoc />
    public async Task OnDisconnectedAsync(HubLifetimeContext context, Exception? exception, Func<HubLifetimeContext, Exception?, Task> next)
    {
        using (actorContext.BeginScope(GetToken(context.Context)))
            await next(context, exception);
    }

    /// <summary>
    ///   Reads the API token the connection was authenticated with.
    /// </summary>
    /// <param name="context">The connection.</param>
    /// <returns>The token, or <see langword="null"/> when there is none.</returns>
    private ApiToken? GetToken(HubCallerContext context)
        => context.User?.Identity?.IsAuthenticated is true && context.GetHttpContext() is { } httpContext
            ? userService.GetApiTokenFromHttpContext(httpContext)
            : null;
}
