using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Shoko.Abstractions.User;

namespace Shoko.Abstractions.Web.SignalR;

/// <summary>
/// A feed on the server's aggregate hub at <c>/signalr/aggregate</c>. Register
/// one in DI as an <see cref="IEventEmitter"/> and clients can list, join and
/// leave it there like the server's own feeds. Derive from
/// <see cref="EventEmitter"/> rather than implementing this yourself.
/// </summary>
public interface IEventEmitter
{
    /// <summary>
    /// The feed's name, which clients join it by. It is also the SignalR group
    /// of the feed's connections, and every message the feed sends is named
    /// <c>&lt;name&gt;:&lt;subject&gt;</c>.
    /// </summary>
    /// <remarks>
    /// Name a plugin's feeds after the plugin, in lower case (<c>myplugin</c>,
    /// <c>myplugin.audit</c>). When two feeds share a name, the server keeps the
    /// one registered first and logs a warning.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Hands the feed the aggregate hub it sends through. The server calls it
    /// once, when it registers the feed, before any client can join.
    /// </summary>
    /// <param name="hub">The aggregate hub's context, for sending to the feed's connections and managing its group.</param>
    /// <exception cref="ArgumentNullException"><paramref name="hub"/> is <see langword="null"/>.</exception>
    void Attach(IHubContext hub);

    /// <summary>
    /// Checks whether a connection has joined the feed.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID.</param>
    /// <returns><see langword="true"/> if the connection has joined the feed; otherwise <see langword="false"/>.</returns>
    bool IsListening(string connectionId);

    /// <summary>
    /// Adds a connection to the feed and sends it the feed's initial messages.
    /// The hub calls it when a client joins the feed.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID.</param>
    /// <param name="user">The user the connection signed in as.</param>
    /// <param name="lastConnectedAt">When the client was last connected, if it said so when joining, so the feed can send only what changed since.</param>
    /// <returns><see langword="true"/> if the connection joined; <see langword="false"/> if it had already joined or the user may not join the feed.</returns>
    /// <exception cref="InvalidOperationException">The feed was not attached to a hub yet.</exception>
    Task<bool> ConnectAsync(string connectionId, IUser user, DateTime? lastConnectedAt = null);

    /// <summary>
    /// Removes a connection from the feed. The hub calls it when a client
    /// leaves the feed or disconnects.
    /// </summary>
    /// <param name="connectionId">The SignalR connection ID.</param>
    /// <returns><see langword="true"/> if the connection had joined the feed and left it; otherwise <see langword="false"/>.</returns>
    Task<bool> DisconnectAsync(string connectionId);

    /// <summary>
    /// Sends a message to every connection that joined the feed.
    /// </summary>
    /// <param name="subject">The message's subject; clients receive it as <c>&lt;name&gt;:&lt;subject&gt;</c>.</param>
    /// <param name="args">The message's arguments, serialized for the client.</param>
    /// <returns>A task completing once the message was handed to the connections. Before the feed is attached, nobody can have joined it, and nothing is sent.</returns>
    Task SendAsync(string subject, params object[] args);

    /// <summary>
    /// Sends a message to the connections of one user that joined the feed.
    /// </summary>
    /// <param name="user">The user whose connections receive the message.</param>
    /// <param name="subject">The message's subject; clients receive it as <c>&lt;name&gt;:&lt;subject&gt;</c>.</param>
    /// <param name="args">The message's arguments, serialized for the client.</param>
    /// <returns>A task completing once the message was handed to the user's connections, of which there may be none.</returns>
    Task SendToUserAsync(IUser user, string subject, params object[] args);
}
