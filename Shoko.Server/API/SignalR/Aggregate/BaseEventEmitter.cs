using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

    /// <summary>
    /// Sends a message to the users on the feed who may see what it is about.
    /// </summary>
    /// <param name="audience">Who may see what the message is about.</param>
    /// <param name="subject">The message's subject; clients receive it as <c>&lt;name&gt;:&lt;subject&gt;</c>.</param>
    /// <param name="args">The message's arguments, serialized for the client.</param>
    /// <returns>A task completing once the message was handed to the connections.</returns>
    protected Task SendToAudienceAsync(EventAudience audience, string subject, params object[] args)
        => SendWhereAsync(audience.IsVisibleTo, subject, args);

    /// <summary>
    /// Sends a message about several entities, each user getting the parts it
    /// may see, and nothing when it may see none. Users seeing the same parts
    /// share one message.
    /// </summary>
    /// <typeparam name="TPart">The type of a part.</typeparam>
    /// <param name="subject">The message's subject; clients receive it as <c>&lt;name&gt;:&lt;subject&gt;</c>.</param>
    /// <param name="parts">The parts of the message.</param>
    /// <param name="audiences">Who may see each part, in the same order.</param>
    /// <param name="createMessage">Builds the message from the parts a user may see.</param>
    /// <returns>A task completing once the messages were handed to the connections.</returns>
    protected Task SendVisiblePartsAsync<TPart>(string subject, IReadOnlyList<TPart> parts, IReadOnlyList<EventAudience> audiences, Func<IReadOnlyList<TPart>, object> createMessage)
    {
        object[]? whole = null;
        var subsets = new Dictionary<string, object[]>();
        return SendPerUserAsync(subject, user =>
        {
            if (EventAudience.IsUnrestricted(user))
                return whole ??= [createMessage(parts)];

            var visible = Enumerable.Range(0, parts.Count).Where(index => audiences[index].IsVisibleTo(user)).ToList();
            if (visible.Count == parts.Count)
                return whole ??= [createMessage(parts)];
            if (visible.Count is 0)
                return null;

            var key = string.Join(',', visible);
            if (!subsets.TryGetValue(key, out var args))
                subsets[key] = args = [createMessage([.. visible.Select(index => parts[index])])];
            return args;
        });
    }
}
