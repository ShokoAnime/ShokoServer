using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Shoko.Server.API.v3.Models.Common;

/// <summary>
/// Available data sources to chose from.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum DataSource
{
    /// <summary>
    /// Shoko.
    /// </summary>
    Shoko = 0,

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
    TMDB = 3,
}
