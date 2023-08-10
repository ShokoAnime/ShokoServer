using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Server.API.SignalR.Models;

public class NetworkAvailabilitySignalRModel
{
    /// <summary>
    /// The current network availability.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public NetworkAvailability NetworkAvailability { get; }

    public NetworkAvailabilitySignalRModel(NetworkAvailability networkAvailability)
    {
        NetworkAvailability = networkAvailability;
    }

    public NetworkAvailabilitySignalRModel(NetworkAvailabilityChangedEventArgs eventArgs)
    {
        NetworkAvailability = eventArgs.NetworkAvailability;
    }
}
