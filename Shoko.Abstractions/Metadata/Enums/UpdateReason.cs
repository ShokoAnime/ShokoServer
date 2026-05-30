
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
/// Reason for an metadata update event to be dispatched.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum UpdateReason : byte
{
    /// <summary>
    /// Nothing occurred.
    /// </summary>
    None = 0,

    /// <summary>
    /// The metadata was added.
    /// </summary>
    Added = 1,

    /// <summary>
    /// The metadata was updated.
    /// </summary>
    Updated = 2,

    /// <summary>
    /// The metadata was removed.
    /// </summary>
    Removed = 4,

    /// <summary>
    /// An image was downloaded for the first time.
    /// </summary>
    ImageAdded = 8,

    /// <summary>
    /// An image was downloaded after it has already been downloaded.
    /// </summary>
    ImageUpdated = 16,

    /// <summary>
    /// An image was removed.
    /// </summary>
    ImageRemoved = 32,
}
