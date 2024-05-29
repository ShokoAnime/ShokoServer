
using System;
using System.Threading.Tasks;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions.Services
{
    /// <summary>
    /// A service used to check or monitor the current network availability.
    /// </summary>
    public interface IConnectivityService
    {
        /// <summary>
        /// Dispatched when the network availibility has changed.
        /// </summary>
        event EventHandler<NetworkAvailabilityChangedEventArgs> NetworkAvailabilityChanged;

        /// <summary>
        /// Current network availibility.
        /// </summary>
        public NetworkAvailability NetworkAvailability { get; }

        /// <summary>
        /// When the last network change was detected.
        /// </summary>
        public DateTime LastChangedAt { get; }

        /// <summary>
        /// Check for network availability now.
        /// </summary>
        /// <returns>The updated network availability status.</returns>
        public Task<NetworkAvailability> CheckAvailability();
    }
}
