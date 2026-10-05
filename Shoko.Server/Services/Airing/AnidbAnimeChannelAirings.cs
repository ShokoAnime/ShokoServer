using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

#nullable enable
namespace Shoko.Server.Services.Airing;

/// <summary>
/// Where an AniDB anime's stored airings on some channels fall: the calendar
/// quarters, and whether one is still to come.
/// </summary>
internal sealed class AnidbAnimeChannelAirings
{
    /// <summary>
    /// The calendar quarters the airings fall in, as yearly seasons.
    /// </summary>
    public HashSet<(int Year, YearlySeason Season)> Seasons { get; } = [];

    /// <summary>
    /// Whether one of the airings is still to come.
    /// </summary>
    public bool HasUpcoming { get; set; }
}
