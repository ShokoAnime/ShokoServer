using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
///   The gender a source gives a person or a character.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum PersonGender
{
    /// <summary>
    ///   The source does not say.
    /// </summary>
    Unknown = 0,

    /// <summary>
    ///   Female.
    /// </summary>
    Female = 1,

    /// <summary>
    ///   Male.
    /// </summary>
    Male = 2,

    /// <summary>
    ///   Non-binary, or any gender other than female or male.
    /// </summary>
    NonBinary = 3,
}
