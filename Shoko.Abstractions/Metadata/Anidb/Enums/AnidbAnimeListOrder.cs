using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata.Anidb.Models;

namespace Shoko.Abstractions.Metadata.Anidb.Enums;

/// <summary>
///   How <see cref="AnidbAnimeListOptions"/> orders the cached anime.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum AnidbAnimeListOrder
{
    /// <summary>
    ///   By the preferred title, then by AniDB ID.
    /// </summary>
    Title = 0,

    /// <summary>
    ///   By the air date, earliest first and unknown last, then by the
    ///   preferred title.
    /// </summary>
    AirDate = 1,

    /// <summary>
    ///   By the AniDB rating, highest first, then by the preferred title.
    /// </summary>
    Rating = 2,
}
