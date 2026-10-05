using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Shoko.Abstractions.Metadata.Anidb.Models;

#nullable enable
namespace Shoko.Server.API.v3.Models.Airing;

/// <summary>
/// A year of the season archive, with its seasons.
/// </summary>
public class AiringSeasonYear
{
    /// <summary>
    /// The year.
    /// </summary>
    [Required]
    public int Year { get; init; }

    /// <summary>
    /// Its listed seasons, from winter to fall.
    /// </summary>
    [Required]
    public List<AiringSeason> Seasons { get; init; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="AiringSeasonYear"/> class.
    /// </summary>
    public AiringSeasonYear() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AiringSeasonYear"/> class
    /// from a year of the cached anime.
    /// </summary>
    /// <param name="year">The year.</param>
    public AiringSeasonYear(AnidbAnimeSeasonYear year)
    {
        Year = year.Year;
        Seasons = [.. year.Seasons.Select(season => new AiringSeason(season))];
    }
}
