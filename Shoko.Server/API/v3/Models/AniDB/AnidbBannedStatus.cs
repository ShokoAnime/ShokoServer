using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Events;

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
    public AnidbBanType Type { get; set; }

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
    /// Creates a new <see cref="AnidbBannedStatus"/> based on the specified
    /// <see cref="AnidbBanOccurredEventArgs"/>.
    /// </summary>
    /// <param name="eventArgs">The <see cref="AnidbBanOccurredEventArgs"/>.</param>
    public AnidbBannedStatus(AnidbBanOccurredEventArgs eventArgs)
    {
        Type = eventArgs.Type;
        IsBanned = eventArgs.IsBanned;
        BanDuration = eventArgs.IsBanned ? eventArgs.ExpiresAt - eventArgs.OccurredAt : null;
        LastUpdatedAt = eventArgs.OccurredAt;
    }
}
