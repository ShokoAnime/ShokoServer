
namespace Shoko.Abstractions.Enums;

/// <summary>
/// Data sources.
/// </summary>
public enum DataSource : byte
{
    /// <summary>
    /// No Source.
    /// </summary>
    None = 0xFD,

    /// <summary>
    /// User (Manual).
    /// </summary>
    User = 0xFE,

    /// <summary>
    /// Shoko.
    /// </summary>
    Shoko = 0xFF,

    /// <summary>
    /// AniDB.
    /// </summary>
    AniDB = 0,

    /// <summary>
    /// The Movie DataBase (TMDB).
    /// </summary>
    TMDB = 1,

    /// <summary>
    /// The Tv DataBase (TvDB).
    /// </summary>
    TvDB = 2,

    /// <summary>
    /// AniList.
    /// </summary>
    AniList = 3,

    /// <summary>
    /// Animeshon.
    /// </summary>
    Animeshon = 4,
}
