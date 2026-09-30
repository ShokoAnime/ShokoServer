namespace Shoko.Abstractions.Metadata.Matching;

/// <summary>
///   How to line two sets of episodes up.
/// </summary>
public enum EpisodeMatchStrategy : byte
{
    /// <summary>
    ///   Let the matcher decide from the shape of what it is given.
    /// </summary>
    Auto = 0,

    /// <summary>
    ///   Line them up on air dates and titles, within seasons. Suits a source
    ///   that groups episodes into seasons and dates them.
    /// </summary>
    DateAndTitleWithinSeasons = 1,

    /// <summary>
    ///   Line them up on air date, falling back to episode number only to
    ///   settle what the dates leave open. Suits a source with one run of
    ///   episodes and no seasons.
    /// </summary>
    /// <remarks>
    ///   The number is never the first axis here. Two episodes sharing an air
    ///   date are separated by number; a number alone does not place one.
    /// </remarks>
    DateThenNumber = 2,
}
