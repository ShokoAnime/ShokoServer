
namespace Shoko.Plugin.Abstractions.Enums
{
    public enum NetworkAvailability
    {
        /// <summary>
        /// Shoko was unable to find any network interfaces.
        /// </summary>
        NoInterfaces = 0,

        /// <summary>
        /// Shoko was unable to find any local gateways to use.
        /// </summary>
        NoGateways,

        /// <summary>
        /// Shoko was able to find a local gateway.
        /// </summary>
        LocalOnly,

        /// <summary>
        /// Shoko was able to connect to some internet endpoints in WAN.
        /// </summary>
        PartialInternet,

        /// <summary>
        /// Shoko was able to connect to all internet endpoints in WAN.
        /// </summary>
        Internet,
    }
}
