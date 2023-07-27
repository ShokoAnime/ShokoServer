using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// Available data sources to chose from.
/// </summary>
/// <remarks>
/// Should be in sync with <see cref="global::Shoko.Models.Enums.DataSourceType"/>.
/// </remarks>
[JsonConverter(typeof(StringEnumConverter))]
public enum DataSource
{
    /// <summary>
    /// No source.
    /// </summary>
    None = 0,

    /// <summary>
    /// AniDB.
    /// </summary>
    AniDB = 1,

    /// <summary>
    /// The Tv Database (TvDB).
    /// </summary>
    TvDB = 2,

    /// <summary>
    /// The Movie Database (TMDB).
    /// </summary>
    TMDB = 4,

    /// <summary>
    /// Trakt.
    /// </summary>
    Trakt = 8,

    /// <summary>
    /// My Anime List (MAL).
    /// </summary>
    MAL = 16,

    /// <summary>
    /// AniList (AL).
    /// </summary>
    AniList = 32,

    /// <summary>
    /// Animeshon.
    /// </summary>
    Animeshon = 64,

    /// <summary>
    /// Kitsu.
    /// </summary>
    Kitsu = 128,

    /// <summary>
    /// Shoko.
    /// </summary>
    Shoko = 1024,
}
