using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Orderings;

/// <summary>
///   The file an ordering export is written as.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MetadataOrderingContainer
{
    /// <summary>
    ///   A zip archive when image files are embedded, else plain JSON.
    /// </summary>
    Auto = 0,

    /// <summary>
    ///   Plain JSON, with any embedded image file inline as base64.
    /// </summary>
    Json = 1,

    /// <summary>
    ///   A zip archive holding <c>manifest.json</c> and each embedded image
    ///   file as <c>images/&lt;sha256&gt;.&lt;ext&gt;</c>.
    /// </summary>
    Zip = 2,
}
