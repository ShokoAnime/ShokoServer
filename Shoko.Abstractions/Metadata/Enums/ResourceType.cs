
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
///   Categorizes the kind of external resource/link an entity is
///   linked to.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum ResourceType : byte
{
    /// <summary>
    ///   Official website.
    /// </summary>
    Website = 0,

    /// <summary>
    ///   Streaming service page.
    /// </summary>
    Streaming = 1,

    /// <summary>
    ///   General third-party info page (e.g. Wikipedia).
    /// </summary>
    Metadata = 2,

    /// <summary>
    ///   Link to the same entity in another database Shoko
    ///   cross-references against (e.g. MyAnimeList, TheTVDB, IMDb,
    ///   AllCinema, AnimeNewsNetwork, VNDB).
    /// </summary>
    CrossReference = 3,
}
