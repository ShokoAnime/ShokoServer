using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Shoko.Server.API.SignalR.Aggregate;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// A hub context for the aggregate hub's feeds that records every message
/// sent through it, with the connections it was sent to.
/// </summary>
public sealed class RecordingHub
{
    #region Properties

    /// <summary>
    /// The aggregate hub's typed context, for a core feed.
    /// </summary>
    public IHubContext<AggregateHub> Typed { get; }

    /// <summary>
    /// The untyped context, for a feed attached directly.
    /// </summary>
    public IHubContext Untyped { get; }

    /// <summary>
    /// Every message sent, in order. A group send names the group as
    /// <c>group:&lt;name&gt;</c> in place of the connections.
    /// </summary>
    public List<SentMessage> Sent { get; } = [];

    #endregion

    #region Constructors

    public RecordingHub()
    {
        var clients = new Mock<IHubClients>();
        clients.Setup(c => c.Clients(It.IsAny<IReadOnlyList<string>>()))
            .Returns((IReadOnlyList<string> connectionIDs) => Proxy([.. connectionIDs]));
        clients.Setup(c => c.Client(It.IsAny<string>()))
            .Returns((string connectionID) => SingleProxy(connectionID));
        clients.Setup(c => c.Group(It.IsAny<string>()))
            .Returns((string group) => Proxy(["group:" + group]));

        var groups = new Mock<IGroupManager>();
        groups.Setup(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        groups.Setup(g => g.RemoveFromGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var typed = new Mock<IHubContext<AggregateHub>>();
        typed.Setup(h => h.Clients).Returns(clients.Object);
        typed.Setup(h => h.Groups).Returns(groups.Object);
        Typed = typed.Object;
        Untyped = new AggregateHubContext(Typed);
    }

    #endregion

    #region Queries

    /// <summary>
    /// The connections a message was sent to, across every send of it.
    /// </summary>
    /// <param name="method">The message's name, <c>&lt;feed&gt;:&lt;subject&gt;</c>.</param>
    /// <returns>The connection IDs, sorted.</returns>
    public IReadOnlyList<string> ReceivedBy(string method)
        => [.. Sent.Where(message => message.Method == method).SelectMany(message => message.ConnectionIDs).Order()];

    #endregion

    #region Helpers

    private IClientProxy Proxy(IReadOnlyList<string> connectionIDs)
    {
        var proxy = new Mock<IClientProxy>();
        proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback((string method, object?[] args, CancellationToken _) => Sent.Add(new(connectionIDs, method, args)))
            .Returns(Task.CompletedTask);
        return proxy.Object;
    }

    private ISingleClientProxy SingleProxy(string connectionID)
    {
        var proxy = new Mock<ISingleClientProxy>();
        proxy.Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback((string method, object?[] args, CancellationToken _) => Sent.Add(new([connectionID], method, args)))
            .Returns(Task.CompletedTask);
        return proxy.Object;
    }

    #endregion

    #region Nested Types

    /// <summary>
    /// One message sent through the hub.
    /// </summary>
    /// <param name="ConnectionIDs">The connections it was sent to.</param>
    /// <param name="Method">The message's name.</param>
    /// <param name="Args">The message's arguments.</param>
    public sealed record SentMessage(IReadOnlyList<string> ConnectionIDs, string Method, object?[] Args);

    #endregion
}
