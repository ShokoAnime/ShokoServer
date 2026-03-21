using Shoko.Abstractions.Connectivity.Enums;

namespace Shoko.Abstractions.Connectivity;

/// <summary>
/// Represents a connectivity monitor endpoint used for WAN availability checks.
/// </summary>
public interface IConnectivityMonitor
{
    /// <summary>
    /// A unique, human-readable name for this monitor.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The type of HTTP request to perform against the <see cref="Address"/>.
    /// </summary>
    ConnectivityCheckType Type { get; }

    /// <summary>
    /// The URL to check for connectivity.
    /// </summary>
    string Address { get; }
}
