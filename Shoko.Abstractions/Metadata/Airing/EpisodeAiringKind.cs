using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   What kind of showing an episode airing is. Only a
///   <see cref="Normal"/> airing counts towards a schedule's cadence, the
///   slot its estimates are learned from, and the delays a write infers.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum EpisodeAiringKind : byte
{
    /// <summary>
    ///   The episode's regular showing in the schedule's run.
    /// </summary>
    Normal = 0,

    /// <summary>
    ///   An advance screening, shown ahead of the regular showing, e.g. at an
    ///   event or on a premium service.
    /// </summary>
    Advance = 1,

    /// <summary>
    ///   A rerun of an episode that already had its regular showing.
    /// </summary>
    Rerun = 2,
}
