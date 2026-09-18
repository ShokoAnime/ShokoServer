namespace Shoko.Server.API.v3.Models.Airing;

/// <summary>
/// The display data a caller can ask an airing read to resolve. Everything here
/// costs at least one extra lookup, which is why none of it is returned by
/// default.
/// </summary>
public enum AiringDataToInclude
{
    /// <summary>
    /// The title of the episode.
    /// </summary>
    EpisodeTitle = 0,

    /// <summary>
    /// The title and IDs of the series the episode belongs to.
    /// </summary>
    Series = 1,

    /// <summary>
    /// The series' primary image.
    /// </summary>
    Poster = 2,

    /// <summary>
    /// The episode's backdrop image, falling back to the series' own.
    /// </summary>
    Thumbnail = 3,
}
