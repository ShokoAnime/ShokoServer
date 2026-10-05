namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Whether an anime of a season view has a next airing.
/// </summary>
public enum SeasonAnimeAiringStatus
{
    /// <summary>
    ///   Neither a next airing nor an end is known.
    /// </summary>
    Unknown,

    /// <summary>
    ///   The anime has a next airing.
    /// </summary>
    Upcoming,

    /// <summary>
    ///   The anime has no next airing and the last day its end date can mean
    ///   has passed.
    /// </summary>
    Finished,
}
