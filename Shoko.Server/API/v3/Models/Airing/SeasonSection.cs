using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

#nullable enable
namespace Shoko.Server.API.v3.Models.Airing;

/// <summary>
/// One section of the season view, with its anime.
/// </summary>
public class SeasonSection
{
    /// <summary>
    /// The section's ID, from its definition.
    /// </summary>
    [Required]
    public required string ID { get; init; }

    /// <summary>
    /// The section's heading, from its definition.
    /// </summary>
    [Required]
    public required string Title { get; init; }

    /// <summary>
    /// The anime it took, sorted by next airing: the scheduled ones soonest
    /// first, then those known only by an AniDB air date, then those with
    /// neither by premiere, each tier then by title.
    /// </summary>
    [Required]
    public required List<SeasonAnime> Anime { get; init; }
}
