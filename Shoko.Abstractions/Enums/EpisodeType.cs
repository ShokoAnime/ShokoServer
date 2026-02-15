
namespace Shoko.Abstractions.Enums;

/// <summary>
/// Episode type.
/// </summary>
public enum EpisodeType : byte
{
    /// <summary>
    /// Normal episode.
    /// </summary>
    Episode = 1,

    /// <summary>
    /// Credits. Be it opening credits or ending credits.
    /// </summary>
    Credits = 2,

    /// <summary>
    /// Special episode.
    /// </summary>
    Special = 3,

    /// <summary>
    /// Trailer.
    /// </summary>
    Trailer = 4,

    /// <summary>
    /// Parody.
    /// </summary>
    Parody = 5,
    /// <summary>
    /// Other.
    /// </summary>
    Other = 6,
}
