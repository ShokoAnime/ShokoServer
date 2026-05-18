
namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
/// Image entity types.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum ImageEntityType : byte
{
    /// <summary>
    /// No image type.
    /// </summary>
    None = 0,

    /// <summary>
    /// Primary image.
    /// </summary>
    Primary = 1,

    /// <summary>
    /// Backdrop image.
    /// </summary>
    Backdrop = 2,

    /// <summary>
    /// Banner image.
    /// </summary>
    Banner = 3,

    /// <summary>
    /// Logo image.
    /// </summary>
    Logo = 4,

    /// <summary>
    /// Disc image.
    /// </summary>
    Disc = 5,
}
