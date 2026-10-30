using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Orderings;

/// <summary>
///   How an ordering export carries the images of the orderings and their
///   groups.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MetadataOrderingImageExportMode
{
    /// <summary>
    ///   No images.
    /// </summary>
    None = 0,

    /// <summary>
    ///   Each image by its remote source and URL only. An image with no
    ///   remote source, such as one a user uploaded, is left out.
    /// </summary>
    UrlOnly = 1,

    /// <summary>
    ///   Each image by its remote source and URL, and the file itself for an
    ///   image with no remote source.
    /// </summary>
    EmbedMissingRemote = 2,

    /// <summary>
    ///   The file of every image, with its remote source and URL when known.
    /// </summary>
    EmbedAll = 3,
}
