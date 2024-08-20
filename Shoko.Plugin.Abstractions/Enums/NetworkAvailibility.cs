
namespace Shoko.Plugin.Abstractions.Enums;

/// <summary>
/// Network availability.
/// </summary>
public enum NetworkAvailability
{
    /// <summary>
    /// We were unable to find any network interfaces.
    /// </summary>
    NoInterfaces = 0,

    /// <summary>
    /// We were unable to find any local gateways to use.
    /// </summary>
    NoGateways,

    /// <summary>
    /// We were able to find a local gateway.
    /// </summary>
    LocalOnly,

    /// <summary>
    /// We were able to connect to some internet endpoints in WAN.
    /// </summary>
    PartialInternet,

    /// <summary>
    /// We were able to connect to all internet endpoints in WAN.
    /// </summary>
    Internet,
}
