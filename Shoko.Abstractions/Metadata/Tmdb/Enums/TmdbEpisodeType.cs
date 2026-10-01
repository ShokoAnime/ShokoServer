using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Tmdb.Enums;

/// <summary>
///   TMDB's own classification of an episode within its season.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum TmdbEpisodeType : byte
{
    /// <summary>
    ///   A regular episode.
    /// </summary>
    Standard = 0,

    /// <summary>
    ///   The last episode before a mid-season break.
    /// </summary>
    MidSeason = 1,

    /// <summary>
    ///   The season or series finale.
    /// </summary>
    Finale = 2,
}
