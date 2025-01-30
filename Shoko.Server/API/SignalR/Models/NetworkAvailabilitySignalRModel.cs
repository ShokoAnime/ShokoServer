using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;

namespace Shoko.Server.API.SignalR.Models;

public class NetworkAvailabilitySignalRModel
{
    /// <summary>
    /// The current network availability.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public NetworkAvailability NetworkAvailability { get; }

    /// <summary>
    /// When the last network change was detected.
    /// </summary>
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime LastChangedAt { get; }

    public NetworkAvailabilitySignalRModel(NetworkAvailability networkAvailability, DateTime lastCheckedAt)
    {
        NetworkAvailability = networkAvailability;
        LastChangedAt = lastCheckedAt.ToUniversalTime();
    }

    public NetworkAvailabilitySignalRModel(NetworkAvailabilityChangedEventArgs eventArgs)
    {
        NetworkAvailability = eventArgs.NetworkAvailability;
        LastChangedAt = eventArgs.LastCheckedAt.ToUniversalTime();
    }
}
