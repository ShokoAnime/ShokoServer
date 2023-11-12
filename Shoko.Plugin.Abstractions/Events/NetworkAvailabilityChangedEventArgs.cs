using System;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions
{
    public class NetworkAvailabilityChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The new network availibility.
        /// </summary>
        public NetworkAvailability NetworkAvailability { get; }

        /// <summary>
        /// When the last network change was detected.
        /// </summary>
        public DateTime LastCheckedAt { get; set; }

        public NetworkAvailabilityChangedEventArgs(NetworkAvailability networkAvailability, DateTime lastChange)
        {
            NetworkAvailability = networkAvailability;
            LastCheckedAt = lastChange;
        }
    }
}
