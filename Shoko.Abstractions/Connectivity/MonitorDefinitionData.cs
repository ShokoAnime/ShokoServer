using Shoko.Abstractions.Connectivity.Enums;

namespace Shoko.Abstractions.Connectivity;

/// <summary>
///   Data transfer object for creating a new connectivity monitor
///   definition.
/// </summary>
public sealed class MonitorDefinitionData
{
    /// <summary>
    ///   A unique, human-readable name for this monitor.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    ///   The type of HTTP request to perform.
    /// </summary>
    public required ConnectivityCheckType Type { get; set; }

    /// <summary>
    ///   The URL to check for connectivity.
    /// </summary>
    public required string Address { get; set; }
}
