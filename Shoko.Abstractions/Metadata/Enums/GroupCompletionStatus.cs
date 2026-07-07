
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
///   A release group's completion status for a single anime, as
///   reported by AniDB's group status data.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum GroupCompletionStatus : byte
{
    /// <summary>
    ///   The completion status is not known.
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///   The group is still actively releasing episodes.
    /// </summary>
    Ongoing = 1,

    /// <summary>
    ///   The group has stalled and is not actively releasing
    ///   episodes, but has not formally dropped the anime.
    /// </summary>
    Stalled = 2,

    /// <summary>
    ///   The group has released all regular episodes for the
    ///   anime.
    /// </summary>
    Complete = 3,

    /// <summary>
    ///   The group has dropped the anime before completing it.
    /// </summary>
    Dropped = 4,

    /// <summary>
    ///   The group has released all episodes, including any
    ///   specials.
    /// </summary>
    Finished = 5,

    /// <summary>
    ///   The group has only released specials for the anime, and
    ///   not any regular episodes.
    /// </summary>
    SpecialsOnly = 6,
}
