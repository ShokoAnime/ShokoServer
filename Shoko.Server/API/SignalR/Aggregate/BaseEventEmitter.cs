using Microsoft.AspNetCore.SignalR;
using Shoko.Abstractions.Web.SignalR;

namespace Shoko.Server.API.SignalR.Aggregate;

/// <summary>
/// A core feed on the aggregate hub: an <see cref="EventEmitter"/> attached to
/// the hub as it is built, so it can send before the hub registers it.
/// </summary>
public abstract class BaseEventEmitter : EventEmitter
{
    /// <summary>
    /// The aggregate hub's context.
    /// </summary>
    protected readonly IHubContext<Hub> Hub;

    /// <summary>
    /// Attaches the feed to the aggregate hub.
    /// </summary>
    /// <param name="hub">The aggregate hub's context.</param>
    protected BaseEventEmitter(IHubContext<AggregateHub> hub)
    {
        Hub = hub;
        Attach(new AggregateHubContext(hub));
    }
}
