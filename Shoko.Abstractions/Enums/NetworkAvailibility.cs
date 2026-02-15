
namespace Shoko.Abstractions.Enums;

/// <summary>
/// Network availability.
/// </summary>
public enum NetworkAvailability : byte
{
    /// <summary>
    /// We were unable to find any network interfaces.
    /// </summary>
    NoInterfaces = 0,

    /// <summary>
    /// We were unable to find any local gateways to use.
    /// </summary>
    NoGateways = 1,

    /// <summary>
    /// We were able to find a local gateway.
    /// </summary>
    LocalOnly = 2,

    /// <summary>
    /// We were able to connect to some internet endpoints in WAN.
    /// </summary>
    PartialInternet = 3,

    /// <summary>
    /// We were able to connect to all internet endpoints in WAN.
    /// </summary>
    Internet = 4,
}
