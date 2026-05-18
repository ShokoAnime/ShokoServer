using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;

namespace Shoko.Abstractions.Metadata.Image;

/// <summary>
///   Image metadata. This interface represents a unified image entity that can
///   be associated with multiple entities across different data sources. The
///   image can come from various providers and supports different image types.
/// </summary>
public interface IImage : IEquatable<IImage>, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    ///   The universally/globally unique identifier (UUID/GUID) for the image
    ///   derived from the <seealso cref="Source"/> and
    ///   <seealso cref="ResourceID"/>.
    /// </summary>
    Guid ID { get; }

    /// <summary>
    ///   Primary image ID in the linked image list.
    /// </summary>
    Guid PrimaryID { get; }

    /// <summary>
    ///   Extra image IDs in the linked image list, except the primary image.
    /// </summary>
    IReadOnlyList<Guid> LinkedIDs { get; }

    /// <summary>
    ///   Auto-incremented local ID used for backwards compatibility with older
    ///   APIs.
    /// </summary>
    [Obsolete("Only for backwards compatibility with older APIs. Use the universally unique identifier instead.")]
    int LocalID { get; }

    /// <summary>
    ///   Resource identifier for the image source's template URL to retrieve
    ///   the image from the source, or an MD5 hash digest for locally generated
    ///   or user uploaded images.
    /// </summary>
    string ResourceID { get; }

    /// <summary>
    ///   Provider source (AniDB, TMDB, AniList). This indicates where the image
    ///   originated from and is used for routing, display, and management
    ///   purposes.
    /// </summary>
    DataSource Source { get; }

    /// <summary>
    ///   The image type. Will always be <see cref="ImageEntityType.None"/> when
    ///   the image is directly retrieved from image manager. Will be set to any
    ///   other type when retrieved from a cross-reference or from an entity.
    /// </summary>
    ImageEntityType Type { get; }

    /// <summary>
    ///   MIME type for the image.
    /// </summary>
    /// <remarks>
    ///   Will be set to <c>application/octet-stream</c> if we're unable to
    ///   determine the intended type from the resource ID or the on-disc
    ///   resource.
    /// </remarks>
    string ContentType { get; }

    /// <summary>
    ///   Indicates the image is enabled for use on at least one entity. This
    ///   will check if the image has any cross-references which are enabled.
    ///   Disabled images should not be displayed in any UI by default, but may
    ///   still be useful for administrative purposes.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    ///   Indicates the image is desired to be retrieved from the remote if
    ///   possible. This will check if the image has any cross-references which
    ///   are both enabled and desired.
    /// </summary>
    bool IsDesired { get; }

    /// <summary>
    ///   Indicates the image is the preferred image for the linked entity.
    /// </summary>
    /// <remarks>
    ///   Only one image per entity and image type combination can be preferred.
    ///   Preferred status is stored on the cross-reference, not the image
    ///   itself.
    /// </remarks>
    bool IsPreferred { get; }

    /// <summary>
    ///   Indicates the image is locked and cannot be removed by the user. It
    ///   can still be disabled.
    /// </summary>
    bool IsLocked { get; }

    /// <summary>
    ///   Indicates that the image is readily available from the local file
    ///   system.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    ///   The number of times the image has been attempted to be downloaded from
    ///   the remote provider.
    /// </summary>
    byte DownloadAttempts { get; }

    /// <summary>
    ///   Indicates that we know the dimensions and aspect ratio of the image.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Width), nameof(Height), nameof(AspectRatio))]
    bool HasSize => Width.HasValue && Height.HasValue;

    /// <summary>
    ///   Image aspect ratio. This is calculated as the width divided by the
    ///   height if both are set, or <c>null</c> otherwise.
    /// </summary>
    double? AspectRatio { get => Width.HasValue && Height.HasValue ? (double)Width.Value / Height.Value : null; }

    /// <summary>
    ///   Image width in pixels. Will be greater than 0 if set.
    /// </summary>
    uint? Width { get; }

    /// <summary>
    ///   Image height in pixels. Will be greater than 0 if set.
    /// </summary>
    uint? Height { get; }

    /// <summary>
    ///   Indicates that the image has a rating and number of votes. This is
    ///   used to check if rating data is available before accessing the
    ///   <see cref="Rating"/> and <see cref="RatingVotes"/> properties.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Rating), nameof(RatingVotes))]
    bool HasRating => Rating.HasValue && RatingVotes.HasValue;

    /// <summary>
    ///   Overall user rating for the image, normalized on a scale of 1-10, if
    ///   available. This is typically sourced from TMDB or other providers that
    ///   provide community ratings.
    /// </summary>
    double? Rating { get; }

    /// <summary>
    ///   Number of votes for the image rating, if available. This indicates
    ///   how many users have rated this image.
    /// </summary>
    uint? RatingVotes { get; }

    /// <summary>
    ///   ISO 639-1 alpha-2 language code for the main language used for the
    ///   text in the image, if any. Or <c>null</c> if the image doesn't contain
    ///   any text.
    /// </summary>
    string? LanguageCode { get; }

    /// <summary>
    ///   ISO 3166-1 alpha-2 country code for region-specific images. This is
    ///   used in combination with the language code to identify region-specific
    ///   variants of images when the language alone is not enough. E.g. "pt-BR"
    ///   for Brazilian Portuguese, where "pt" is the language code and "BR" is
    ///   the country code.
    /// </summary>
    string? CountryCode { get; }

    /// <summary>
    ///   The language used for any text in the image, if any. This provides a
    ///   typed representation of the language code, or
    ///   <see cref="TitleLanguage.None"/> if the image doesn't contain any
    ///   language specifics.
    /// </summary>
    TitleLanguage Language { get; }

    /// <summary>
    ///   Local absolute path to where the image is stored. Will be null if the
    ///   image is currently not locally available. This path points to the
    ///   actual image file on disk.
    /// </summary>
    string LocalPath { get; }

    /// <summary>
    ///   Gets the primary image for the image. Will return itself if it is the
    ///   primary image in a linked image list.
    /// </summary>
    /// <returns></returns>
    IImage? GetPrimaryImage();

    /// <summary>
    ///   Gets all images in the linked image list for the image.
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<IImage> GetLinkedImages(bool includePrimaryImage = true);

    /// <summary>
    ///   Get a stream that reads the image contents from the local copy.
    ///   Returns <c>null</c> if the image is currently unavailable. The caller
    ///   is responsible for disposing the stream.
    /// </summary>
    /// <returns>
    ///   A <see cref="Stream"/> of the image content, or <c>null</c> if
    ///   unavailable.
    /// </returns>
    Stream? GetStream();

    /// <summary>
    /// Get all cross-references for this image.
    /// </summary>
    /// <param name="imageType">
    ///   Optional. Filter by image type (e.g. Primary, Backdrop, Banner, etc.).
    /// </param>
    /// <param name="xrefSource">
    ///   Optional. Filter by cross-reference source (e.g. AniDB, TMDB, AniList,
    ///   User, etc.).
    /// </param>
    /// <param name="entitySource">
    /// Optional filter for the entity source (AniDB, tMDB, AniList).
    /// </param>
    /// <param name="entityType">
    /// Optional filter for the entity type (Group, Series, Episode, Video).
    /// </param>
    /// <param name="isEnabled">
    ///   Optional. Filter by enabled state. Pass <c>true</c> to get only
    ///   enabled, <c>false</c> to get only disabled, or <c>null</c> to get
    ///   both. Defaults to <c>null</c>.
    /// </param>
    /// <param name="isDesired">
    ///   Optional. Filter by desired state. Pass <c>true</c> to get only
    ///   desired, <c>false</c> to get only undesired, or <c>null</c> to get
    ///   both. Defaults to <c>null</c>.
    /// </param>
    /// <returns>
    ///   A readonly list of cross-references for the entity.
    /// </returns>
    IReadOnlyList<IImageCrossReference> GetCrossReferences(
        ImageEntityType? imageType = null,
        Enums.DataSource? xrefSource = null,
        DataSource? entitySource = null,
        DataEntityType? entityType = null,
        bool? isEnabled = null,
        bool? isDesired = null
    );
}
