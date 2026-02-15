using System;
using Shoko.Abstractions.Enums;

namespace Shoko.Abstractions.Events;

/// <summary>
/// Dispatched when the network availability changes.
/// </summary>
public class NetworkAvailabilityChangedEventArgs : EventArgs
{
    /// <summary>
    /// The new network availability.
    /// </summary>
    public NetworkAvailability NetworkAvailability { get; private set; }

    /// <summary>
    /// When the last network change was detected.
    /// </summary>
    public DateTime LastCheckedAt { get; private set; }

    /// <summary>
    /// Creates a new <see cref="NetworkAvailabilityChangedEventArgs"/>.
    /// </summary>
    /// <param name="networkAvailability">The new network availability.</param>
    /// <param name="lastCheckedAt">When the last network change was detected.</param>
    public NetworkAvailabilityChangedEventArgs(NetworkAvailability networkAvailability, DateTime lastCheckedAt)
    {
        NetworkAvailability = networkAvailability;
        LastCheckedAt = lastCheckedAt;
    }
}
