using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Shoko.Abstractions.Metadata.Anilist.Enums;

/// <summary>
/// The current releasing status of the media.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum AnilistMediaStatus : byte
{
    /// <summary>
    /// Unknown or not yet reported.
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///   Has completed and is no longer being released.
    /// </summary>
    [DataMember(Name = "FINISHED")]
    [JsonStringEnumMemberName("FINISHED")]
    Finished = 1,

    /// <summary>
    ///   Currently releasing.
    /// </summary>
    [DataMember(Name = "RELEASING")]
    [JsonStringEnumMemberName("RELEASING")]
    Releasing = 2,

    /// <summary>
    ///   To be released at a later date.
    /// </summary>
    [DataMember(Name = "NOT_YET_RELEASED")]
    [JsonStringEnumMemberName("NOT_YET_RELEASED")]
    NotYetReleased = 3,

    /// <summary>
    ///   Ended before the work could be finished.
    /// </summary>
    [DataMember(Name = "CANCELLED")]
    [JsonStringEnumMemberName("CANCELLED")]
    Cancelled = 4,

    /// <summary>
    ///   Version 2 only. Is currently paused from releasing and will resume at a later date.
    /// </summary>
    [DataMember(Name = "HIATUS")]
    [JsonStringEnumMemberName("HIATUS")]
    Hiatus = 5,
}
