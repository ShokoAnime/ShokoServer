using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
///   Whether a stored text is a title or an overview.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum TextKind : byte
{
    /// <summary>
    ///   A title, or another name the entry goes by.
    /// </summary>
    Title = 0,

    /// <summary>
    ///   An overview of the entry.
    /// </summary>
    Overview = 1,
}
