using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
/// Where a work is in its release.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum ReleaseStatus : byte
{
    /// <summary>
    /// The source does not say.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Every part has been released.
    /// </summary>
    Finished = 1,

    /// <summary>
    /// Parts are still being released.
    /// </summary>
    Releasing = 2,

    /// <summary>
    /// Announced, with nothing released yet.
    /// </summary>
    NotYetReleased = 3,

    /// <summary>
    /// Stopped before it was finished, for good.
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// Paused, with more expected.
    /// </summary>
    Hiatus = 5,
}
