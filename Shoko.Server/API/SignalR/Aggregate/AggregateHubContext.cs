using Microsoft.AspNetCore.SignalR;

namespace Shoko.Server.API.SignalR.Aggregate;

/// <summary>
/// The aggregate hub's context as the untyped <see cref="IHubContext"/> the
/// feeds send through, which <see cref="IHubContext{THub}"/> does not implement.
/// </summary>
/// <param name="hub">The aggregate hub's typed context.</param>
public sealed class AggregateHubContext(IHubContext<AggregateHub> hub) : IHubContext
{
    /// <inheritdoc />
    public IHubClients Clients => hub.Clients;

    /// <inheritdoc />
    public IGroupManager Groups => hub.Groups;
}
