
using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Services;

namespace Shoko.Server.API.v3.Models.Common;

public class ConnectivityDetails
{
    /// <summary>
    /// Current network availibility.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public readonly NetworkAvailability NetworkAvailability;

    /// <summary>
    /// When the last network change was detected.
    /// </summary>
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public readonly DateTime LastChangedAt;

    /// <summary>
    /// Is the AniDB UDP API currently reachable?
    /// </summary>
    public readonly bool IsAniDBUdpReachable;

    /// <summary>
    /// Are we currently banned from using the AniDB HTTP API?
    /// </summary>
    public readonly bool IsAniDBHttpBanned;

    /// <summary>
    /// Are we currently banned from using the AniDB UDP API?
    /// </summary>
    public readonly bool IsAniDBUdpBanned;

    public ConnectivityDetails(IConnectivityService service)
    {
        NetworkAvailability = service.NetworkAvailability;
        LastChangedAt = service.LastChangedAt.ToUniversalTime();
        IsAniDBUdpReachable = service.IsAniDBUdpReachable;
        IsAniDBHttpBanned = service.IsAniDBHttpBanned;
        IsAniDBUdpBanned = service.IsAniDBUdpBanned;
    }
}
