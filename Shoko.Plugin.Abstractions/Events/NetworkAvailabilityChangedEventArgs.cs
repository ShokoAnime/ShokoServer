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

        public NetworkAvailabilityChangedEventArgs(NetworkAvailability networkAvailability)
        {
            NetworkAvailability = networkAvailability;
        }
    }
}
