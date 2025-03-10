using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Shoko.Server.API.v3.Models.AniDB;

/// <summary>
/// Series type. What kind of series the anime or
/// show this is.
/// </summary>
[JsonConverter(typeof(StringEnumConverter))]
public enum AnimeType
{
    /// <summary>
    /// The series type is unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A catch-all type for future extensions when a provider can't use a current episode type, but knows what the future type should be.
    /// </summary>
    Other = 1,

    /// <summary>
    /// Standard TV series.
    /// </summary>
    TV = 2,

    /// <summary>
    /// TV special.
    /// </summary>
    TVSpecial = 3,

    /// <summary>
    /// Web series.
    /// </summary>
    Web = 4,

    /// <summary>
    /// All movies, regardless of source (e.g. web or theater)
    /// </summary>
    Movie = 5,

    /// <summary>
    /// Original Video Animations, AKA standalone releases that don't air on TV or the web.
    /// </summary>
    OVA = 6,

    /// <summary>
    /// Music Video
    /// </summary>
    MusicVideo = 7,
}
