using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Web.SignalR;

namespace Shoko.Server.API.SignalR.Aggregate;

/// <summary>
/// The feeds on the aggregate hub, the core's and the plugins', by name. Each
/// is attached to the hub here. When two share a name, the one registered
/// first is kept and the other is left out with a warning.
/// </summary>
public class EventEmitterRegistry
{
    /// <summary>
    /// The feeds, by name.
    /// </summary>
    public FrozenDictionary<string, IEventEmitter> Feeds { get; }

    /// <summary>
    /// Builds the registry from every <see cref="IEventEmitter"/> in DI.
    /// </summary>
    /// <param name="emitters">The feeds, in registration order.</param>
    /// <param name="hub">The aggregate hub's context, attached to each feed kept.</param>
    /// <param name="logger">Logs the feeds left out.</param>
    public EventEmitterRegistry(IEnumerable<IEventEmitter> emitters, IHubContext<AggregateHub> hub, ILogger<EventEmitterRegistry> logger)
    {
        var context = new AggregateHubContext(hub);
        // Matched ignoring case, as clients join by the name they were given.
        var feeds = new Dictionary<string, IEventEmitter>(StringComparer.OrdinalIgnoreCase);
        foreach (var emitter in emitters)
        {
            var name = emitter.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                logger.LogWarning("Left out the feed {Type} on the aggregate hub, since it has no name.", emitter.GetType().FullName);
                continue;
            }

            if (feeds.TryGetValue(name, out var existing))
            {
                if (!ReferenceEquals(existing, emitter))
                    logger.LogWarning(
                        "Left out the feed {Type} on the aggregate hub, since {ExistingType} registered the name {Name} first.",
                        emitter.GetType().FullName,
                        existing.GetType().FullName,
                        name
                    );
                continue;
            }

            emitter.Attach(context);
            feeds.Add(name, emitter);
        }

        Feeds = feeds.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
