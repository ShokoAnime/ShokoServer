using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Orderings;

/// <summary>
///   Where an ordering import restores each image from.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MetadataOrderingImageImportMode
{
    /// <summary>
    ///   The file in the payload, else the remote URL.
    /// </summary>
    PayloadFirst = 0,

    /// <summary>
    ///   The remote URL, else the file in the payload.
    /// </summary>
    UrlFirst = 1,

    /// <summary>
    ///   Only the file in the payload.
    /// </summary>
    PayloadOnly = 2,

    /// <summary>
    ///   Only the remote URL.
    /// </summary>
    UrlOnly = 3,

    /// <summary>
    ///   No images.
    /// </summary>
    None = 4,
}
