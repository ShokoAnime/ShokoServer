using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
///   What became of one link in a write.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MetadataLinkChangeKind : byte
{
    /// <summary>
    ///   The link was made.
    /// </summary>
    Added = 0,

    /// <summary>
    ///   The link was taken away.
    /// </summary>
    Removed = 1,

    /// <summary>
    ///   The link stayed, trusted differently.
    /// </summary>
    RatingChanged = 2,

    /// <summary>
    ///   The same AniDB entry was linked to another entry of the source in
    ///   place of the one it had, at the same level.
    /// </summary>
    Replaced = 3,
}
