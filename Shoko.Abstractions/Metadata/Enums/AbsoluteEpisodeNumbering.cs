using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
///   What an ordering's absolute episode numbers
///   (<see cref="IEpisodeOrderingInformation.AbsoluteEpisodeNumber"/>) are.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum AbsoluteEpisodeNumbering : byte
{
    /// <summary>
    ///   The ordering has none.
    /// </summary>
    None = 0,

    /// <summary>
    ///   The regular episodes are numbered on across the ordering's groups,
    ///   each group starting where the one before it ended. Specials have none.
    /// </summary>
    Continuous = 1,

    /// <summary>
    ///   Each episode's own absolute number, as its source gives it
    ///   (<see cref="Storage.MetadataEpisodeData.AbsoluteEpisodeNumber"/>).
    ///   What a series' default ordering has.
    /// </summary>
    Source = 2,
}
