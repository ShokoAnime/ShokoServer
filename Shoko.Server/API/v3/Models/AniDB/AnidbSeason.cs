using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.API.v3.Models.Common;

namespace Shoko.Server.API.v3.Models.AniDB;

/// <summary>
/// A season with cached AniDB anime in it.
/// </summary>
public class AnidbSeason
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

    public AnidbSeason() { }

    public AnidbSeason(AnidbAnimeSeasonCount season)
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
        /// A representative poster and backdrop, in <see cref="AnidbSeason.Poster"/>
        /// and <see cref="AnidbSeason.Backdrop"/>.
        /// </summary>
        Images,
    }
}
