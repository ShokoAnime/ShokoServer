
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
///   Types of characters.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum CharacterType : byte
{
    /// <summary>
    /// The character's type is not known yet.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A single person / singular persona.
    /// </summary>
    Character = 1,

    // ??? = 2,

    /// <summary>
    /// A company or organization.
    /// </summary>
    Organization = 3,
}
