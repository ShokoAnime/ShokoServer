using System;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Events;

public class NetworkAvailabilityChangedEventArgs : EventArgs
{
    /// <summary>
    /// The new network availability.
    /// </summary>
    public NetworkAvailability NetworkAvailability { get; }

    /// <summary>
    /// When the last network change was detected.
    /// </summary>
    public DateTime LastCheckedAt { get; }

    public NetworkAvailabilityChangedEventArgs(NetworkAvailability networkAvailability, DateTime lastCheckedAt)
    {
        NetworkAvailability = networkAvailability;
        LastCheckedAt = lastCheckedAt;
    }
}
