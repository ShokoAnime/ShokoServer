using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.API.v3.Models.Common;

namespace Shoko.Server.API.v3.Models.Airing;

/// <summary>
/// A yearly season of the season view, with how many cached AniDB anime are
/// in it.
/// </summary>
public class AiringSeason
{
    /// <summary>
    /// The year.
    /// </summary>
    [Required]
    public int Year { get; init; }

    /// <summary>
    /// The season.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public YearlySeason AnimeSeason { get; init; }

    /// <summary>
    /// How many cached anime have a regular episode airing in the season,
    /// under the route's filters.
    /// </summary>
    [Required]
    public int Count { get; init; }

    /// <summary>
    /// Whether the season is the one under way today.
    /// </summary>
    [Required]
    public bool IsCurrent { get; init; }

    /// <summary>
    /// The poster of the season's best anime by weighted rating among those
    /// starting in it that have one. Only set with
    /// <see cref="IncludeDetails.Images"/>; <c>null</c> when none has one.
    /// </summary>
    public Image? Poster { get; init; }

    /// <summary>
    /// The backdrop of the anime <see cref="Poster"/> comes from. Only set
    /// with <see cref="IncludeDetails.Images"/>; <c>null</c> when it has none.
    /// </summary>
    public Image? Backdrop { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AiringSeason"/> class.
    /// </summary>
    public AiringSeason() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AiringSeason"/> class
    /// from a season of the cached anime.
    /// </summary>
    /// <param name="season">The season.</param>
    public AiringSeason(AnidbAnimeSeasonCount season)
    {
        Year = season.Year;
        AnimeSeason = season.Season;
        Count = season.Count;
        IsCurrent = season.IsCurrent;
        Poster = season.Poster is { } poster ? new Image(poster) : null;
        Backdrop = season.Backdrop is { } backdrop ? new Image(backdrop) : null;
    }

    /// <summary>
    /// The extra details a season listing can be sent with.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum IncludeDetails
    {
        /// <summary>
        /// A representative poster and backdrop, in <see cref="AiringSeason.Poster"/>
        /// and <see cref="AiringSeason.Backdrop"/>.
        /// </summary>
        Images,
    }
}
