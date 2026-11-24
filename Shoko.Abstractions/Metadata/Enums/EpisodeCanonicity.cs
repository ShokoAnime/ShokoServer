using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
///   Whether an episode is part of the canon story, as its source says.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum EpisodeCanonicity : byte
{
    /// <summary>
    ///   The source says nothing.
    /// </summary>
    None = 0,

    /// <summary>
    ///   Canon.
    /// </summary>
    Canon = 1,

    /// <summary>
    ///   Partly canon, partly filler.
    /// </summary>
    MixedCanonFiller = 2,

    /// <summary>
    ///   Filler.
    /// </summary>
    Filler = 3,

    /// <summary>
    ///   A recap of earlier episodes.
    /// </summary>
    Recap = 4,
}
