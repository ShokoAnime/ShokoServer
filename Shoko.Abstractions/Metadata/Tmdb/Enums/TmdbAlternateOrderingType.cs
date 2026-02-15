
namespace Shoko.Abstractions.Metadata.Tmdb.Enums;

/// <summary>
/// TMDB alternate ordering type.
/// </summary>
public enum TmdbAlternateOrderingType
{
    /// <summary>
    /// Main ordering for the show.
    /// </summary>
    Default = -1,

    /// <summary>
    /// Unknown ordering type.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Original air date.
    /// </summary>
    OriginalAirDate = 1,

    /// <summary>
    /// Absolute ordering.
    /// </summary>
    Absolute = 2,

    /// <summary>
    /// DVD ordering.
    /// </summary>
    DVD = 3,

    /// <summary>
    /// Digital ordering.
    /// </summary>
    Digital = 4,

    /// <summary>
    /// Web ordering. Aliased to Digital.
    /// </summary>
    Web = Digital,

    /// <summary>
    /// Story arc ordering.
    /// </summary>
    StoryArc = 5,

    /// <summary>
    /// Production ordering.
    /// </summary>
    Production = 6,

    /// <summary>
    /// TV ordering.
    /// </summary>
    TV = 7,
}
