using Shoko.Abstractions.Enums;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// Represents the count of different types of episodes.
/// </summary>
public class EpisodeCounts
{
    /// <summary>
    /// The number of normal episodes.
    /// </summary>
    public int Episodes { get; set; }

    /// <summary>
    /// The number of special episodes.
    /// </summary>
    public int Specials { get; set; }

    /// <summary>
    /// The number of credits episodes.
    /// </summary>
    public int Credits { get; set; }

    /// <summary>
    /// The number of trailer episodes.
    /// </summary>
    public int Trailers { get; set; }

    /// <summary>
    /// The number of parody episodes.
    /// </summary>
    public int Parodies { get; set; }

    /// <summary>
    /// The number of other episodes.
    /// </summary>
    public int Others { get; set; }

    /// <summary>
    /// Returns the number of episodes for the given <paramref name="type"/>
    /// </summary>
    public int this[EpisodeType type] => type switch
    {
        EpisodeType.Episode => Episodes,
        EpisodeType.Special => Specials,
        EpisodeType.Credits => Credits,
        EpisodeType.Trailer => Trailers,
        EpisodeType.Parody => Parodies,
        _ => Others
    };
}
