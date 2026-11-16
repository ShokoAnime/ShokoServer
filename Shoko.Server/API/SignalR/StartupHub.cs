using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Shoko.Abstractions.Web.Attributes;

namespace Shoko.Server.API.SignalR;

/// <summary>
///   The startup feed, open only while the server starts, waits in setup mode
///   or has failed to start: what the start says, as
///   <see cref="StartupEmitter"/> sends it. Clients call nothing on it.
/// </summary>
/// <remarks>
///   <see cref="InitFriendlyAttribute"/> lets the not-running check pass both
///   of its endpoints, negotiate and the connection. The <c>startup</c> policy
///   refuses them once the server has started, before SignalR sees the
///   request; <see cref="AllowAnonymousAttribute"/> would skip that policy.
/// </remarks>
[Authorize("startup")]
[InitFriendly]
[DatabaseBlockedExempt]
public class StartupHub : Hub
{
    private readonly StartupEmitter _startupEmitter;

    public StartupHub(StartupEmitter emitter)
    {
        _startupEmitter = emitter;
    }

    public override Task OnConnectedAsync()
    {
        var context = Context;
        return _startupEmitter.JoinAsync(context.ConnectionId, context.Abort, context.User?.Identity?.IsAuthenticated is true);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _startupEmitter.Leave(Context.ConnectionId);
        return Task.CompletedTask;
    }
}
