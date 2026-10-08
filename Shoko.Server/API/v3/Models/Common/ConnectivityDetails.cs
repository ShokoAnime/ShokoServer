using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Connectivity.Enums;

namespace Shoko.Server.API.v3.Models.Common;

public class ConnectivityDetails
{
    /// <summary>
    /// Current network availability.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public NetworkAvailability NetworkAvailability { get; init; }

    /// <summary>
    /// When the last network change was detected.
    /// </summary>
    [Required, JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime LastChangedAt { get; init; }

    /// <summary>
    /// Is the AniDB UDP API currently reachable?
    /// </summary>
    [Required]
    public bool IsAniDBUdpReachable { get; init; }

    /// <summary>
    /// Are we currently banned from using the AniDB HTTP API?
    /// </summary>
    [Required]
    [Obsolete("Use the suspension service (ISuspensionService, /api/v3/Suspension, the suspension feed).")]
    public bool IsAniDBHttpBanned { get; init; }

    /// <summary>
    /// Are we currently banned from using the AniDB UDP API?
    /// </summary>
    [Required]
    [Obsolete("Use the suspension service (ISuspensionService, /api/v3/Suspension, the suspension feed).")]
    public bool IsAniDBUdpBanned { get; init; }
}
