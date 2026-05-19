using Shoko.Abstractions.Metadata.Image;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
/// Image entity types.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum ImageEntityType : byte
{
    /// <summary>
    ///   No image type. Only visible as a placeholder when the
    ///   <see cref="IImage"/> is not linked to an entity.
    /// </summary>
    None = 0,

    /// <summary>
    ///   Primary image for the linked entity.
    /// </summary>
    Primary = 1,

    /// <summary>
    ///   Backdrop image for the linked entity.
    /// </summary>
    Backdrop = 2,

    /// <summary>
    ///   Banner image for the linked entity.
    /// </summary>
    Banner = 3,

    /// <summary>
    ///   Logo image for the linked entity.
    /// </summary>
    Logo = 4,

    /// <summary>
    ///   Disc image for the linked entity.
    /// </summary>
    Disc = 5,
}
