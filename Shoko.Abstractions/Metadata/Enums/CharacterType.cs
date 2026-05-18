
namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
///   Types of characters.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
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
