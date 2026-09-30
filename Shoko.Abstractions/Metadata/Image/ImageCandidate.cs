using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Image;

/// <summary>
///   An image a provider offers for one of its entities, which the core may
///   link and download.
/// </summary>
/// <remarks>
///   Kept apart from <see cref="ImageData"/> and the cross-reference data: a
///   candidate is what the source says it has, and the core decides what
///   becomes of it. The image's source is always the provider's own.
/// </remarks>
public sealed record ImageCandidate
{
    /// <summary>
    ///   What completes the source's template URL, at most 128 characters. It
    ///   is also the image's identity on the source, so keep it stable.
    /// </summary>
    public required string ResourceID { get; init; }

    /// <summary>
    ///   What the image is for the entity: a poster, a backdrop, a logo and so
    ///   on. Never <see cref="ImageEntityType.None"/>.
    /// </summary>
    public required ImageEntityType ImageType { get; init; }

    /// <summary>
    ///   The width in pixels, when known.
    /// </summary>
    public int? Width { get; init; }

    /// <summary>
    ///   The height in pixels, when known.
    /// </summary>
    public int? Height { get; init; }

    /// <summary>
    ///   The ISO 639-1 code of the language of the text in the image, or
    ///   <c>null</c> when it has none.
    /// </summary>
    public string? LanguageCode { get; init; }

    /// <summary>
    ///   The ISO 3166-1 alpha-2 code of the country the image is for, when it
    ///   is region specific.
    /// </summary>
    public string? CountryCode { get; init; }

    /// <summary>
    ///   The community rating, from 1 to 10, or <c>null</c> when unrated.
    /// </summary>
    public double? Rating { get; init; }

    /// <summary>
    ///   How many votes <see cref="Rating"/> is from.
    /// </summary>
    public int? RatingVotes { get; init; }

    /// <summary>
    ///   Whether the source uses this image as the entity's default of its
    ///   type. The default is always downloaded when its type is.
    /// </summary>
    public bool IsDefault { get; init; }
}
