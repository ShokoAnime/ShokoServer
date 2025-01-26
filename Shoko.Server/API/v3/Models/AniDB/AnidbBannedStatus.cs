using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Server.Providers.AniDB;

#nullable enable
namespace Shoko.Server.API.v3.Models.AniDB;

/// <summary>
/// Represents the status of an AniDB ban.
/// </summary>
public class AnidbBannedStatus
{
    /// <summary>
    /// The type of update.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public UpdateType Type { get; set; }

    /// <summary>
    /// Whether the AniDB account is banned.
    /// </summary>
    public bool IsBanned { get; set; }

    /// <summary>
    /// The duration of the ban.
    /// </summary>
    public TimeSpan? BanDuration { get; set; }

    /// <summary>
    /// The date and time the status was last updated.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AnidbBannedStatus"/> class.
    /// </summary>
    /// <param name="statusUpdate">The <see cref="AniDBStateUpdate"/> to initialize from.</param>
    public AnidbBannedStatus(AniDBStateUpdate statusUpdate)
    {
        Type = statusUpdate.UpdateType;
        IsBanned = statusUpdate.Value;
        LastUpdatedAt = statusUpdate.UpdateTime;
        BanDuration = IsBanned ? TimeSpan.FromSeconds(statusUpdate.PauseTimeSecs) : null;
    }
}

