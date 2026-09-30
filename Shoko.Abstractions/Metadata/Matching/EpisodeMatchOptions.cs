namespace Shoko.Abstractions.Metadata.Matching;

/// <summary>
///   How a caller wants its episodes lined up.
/// </summary>
public sealed record EpisodeMatchOptions
{
    /// <summary>
    ///   Which approach to take.
    /// </summary>
    public EpisodeMatchStrategy Strategy { get; init; } = EpisodeMatchStrategy.Auto;

    /// <summary>
    ///   Whether to line up specials as well as ordinary episodes.
    /// </summary>
    public bool IncludeSpecials { get; init; } = true;
}
