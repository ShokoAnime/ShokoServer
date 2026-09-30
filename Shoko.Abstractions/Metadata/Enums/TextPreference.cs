using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
///   How strongly a user prefers a stored text for its entry.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum TextPreference : byte
{
    /// <summary>
    ///   Not preferred. The text is chosen only by the language and source
    ///   orders.
    /// </summary>
    None = 0,

    /// <summary>
    ///   Preferred for its own language. It wins once the language order
    ///   reaches that language.
    /// </summary>
    Language = 1,

    /// <summary>
    ///   Preferred over every other text of the entry, whatever the language
    ///   and source orders say.
    /// </summary>
    Overall = 2,
}
