
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shoko.Abstractions.Connectivity.Enums;
using Shoko.Abstractions.Connectivity.Events;

namespace Shoko.Abstractions.Connectivity.Services;

/// <summary>
/// A service used to check or monitor the current network availability.
/// </summary>
public interface IConnectivityService
{
    /// <summary>
    /// Dispatched when the network availability has changed.
    /// </summary>
    event EventHandler<NetworkAvailabilityChangedEventArgs> NetworkAvailabilityChanged;

    /// <summary>
    /// Current network availability.
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

    /// <summary>
    /// The current list of connectivity monitors used for WAN checks.
    /// </summary>
    public IReadOnlyList<IConnectivityMonitor> GetMonitorDefinitions();

    /// <summary>
    /// Add a new connectivity monitor definition.
    /// </summary>
    /// <param name="name">A unique, human-readable name for this monitor.</param>
    /// <param name="type">The type of HTTP request to perform.</param>
    /// <param name="address">The URL to check for connectivity.</param>
    /// <returns>The created monitor.</returns>
    public IConnectivityMonitor AddMonitorDefinition(string name, ConnectivityCheckType type, string address);

    /// <summary>
    /// Remove a connectivity monitor definition by name.
    /// </summary>
    /// <param name="name">The name of the monitor definition to remove.</param>
    /// <returns><c>true</c> if a definition with the given name was found and removed; otherwise <c>false</c>.</returns>
    public bool RemoveMonitorDefinition(string name);
}
