
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
    public NetworkAvailability NetworkAvailability { get; init; }

    /// <summary>
    /// When the last network change was detected.
    /// </summary>
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime LastChangedAt { get; init; }

    /// <summary>
    /// Is the AniDB UDP API currently reachable?
    /// </summary>
    public bool IsAniDBUdpReachable { get; init; }

    /// <summary>
    /// Are we currently banned from using the AniDB HTTP API?
    /// </summary>
    public bool IsAniDBHttpBanned { get; init; }

    /// <summary>
    /// Are we currently banned from using the AniDB UDP API?
    /// </summary>
    public bool IsAniDBUdpBanned { get; init; }
}
