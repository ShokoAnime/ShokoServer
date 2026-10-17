using System;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Shoko.Server.Services.MetadataStorage;

/// <summary>
///   Raises a typed metadata store's events, so one failing handler does not
///   keep the event from the rest or break the write that raised it.
/// </summary>
internal static class MetadataStoreEvents
{
    /// <summary>
    ///   Hands an event to each of its handlers, logging any that throws.
    /// </summary>
    /// <typeparam name="TEventArgs">The kind of event.</typeparam>
    /// <param name="handlers">The event's handlers, or <c>null</c> when it has none.</param>
    /// <param name="sender">The store raising it.</param>
    /// <param name="eventArgs">What happened.</param>
    /// <param name="logger">Where a handler's failure is logged.</param>
    /// <param name="eventName">The event's name, for the log.</param>
    internal static void Raise<TEventArgs>(
        EventHandler<TEventArgs>? handlers,
        object sender,
        TEventArgs eventArgs,
        ILogger logger,
        string eventName
    )
        where TEventArgs : EventArgs
    {
        if (handlers is null)
            return;

        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<TEventArgs>>())
        {
            try
            {
                handler(sender, eventArgs);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "A handler of {EventName} failed.", eventName);
            }
        }
    }
}
