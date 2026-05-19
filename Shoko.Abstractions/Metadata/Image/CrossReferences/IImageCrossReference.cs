using System;
using System.Diagnostics.CodeAnalysis;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Image.CrossReferences;

/// <summary>
/// Represents a link between an image and an entity. This interface provides a unified
/// way to associate images with various entity types across different data sources,
/// supporting different image types (primary, backdrop, banner, etc.) and their metadata.
/// </summary>
public interface IImageCrossReference : IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    ///   The local cross-reference identifier for administrative purposes.
    /// </summary>
    int ID { get; }

    /// <summary>
    ///   Gets the universally/globally unique identifier (UUID/GUID) for the
    ///   image.
    /// </summary>
    Guid ImageID { get; }

    /// <summary>
    ///   Gets the ID of the primary image for the associated entity.
    /// </summary>
    Guid PrimaryImageID { get; set; }

    /// <summary>
    /// Gets the image type for this cross-reference. The image type indicates the role
    /// this image plays for the associated entity (e.g., poster, backdrop, banner).
    /// </summary>
    ImageEntityType ImageType { get; }

    /// <summary>
    ///   Gets the image source.
    /// </summary>
    DataSource ImageSource { get; }

    /// <summary>
    ///   Gets the entity ID. This is the stringified identifier of the linked entity.
    /// </summary>
    string EntityID { get; }

    /// <summary>
    /// Gets the metadata entity type.
    /// </summary>
    DataEntityType EntityType { get; }

    /// <summary>
    /// Gets the metadata entity source. This indicates where the entity originates from
    /// (e.g., AniDB, TMDB, AniList, Shoko).
    /// </summary>
    DataSource EntitySource { get; }

    /// <summary>
    /// Gets the season number if the linked entity is a season. If the linked entity
    /// is an episode, this returns the episode's season number. This is used for
    /// sorting entity cross-references for images across multiple seasons.
    /// </summary>
    int? EntitySeasonNumber { get; }

    /// <summary>
    /// Gets the episode number if the linked entity is an episode. This is used for
    /// sorting entity cross-references for images within a specific episode.
    /// </summary>
    int? EntityEpisodeNumber { get; }

    /// <summary>
    /// Gets the date when the entity was released. This can be used to sort entity
    /// cross-references for images, particularly useful for multi-season series
    /// where images should be ordered by release date.
    /// </summary>
    DateOnly? EntityReleasedAt { get; }

    /// <summary>
    /// Gets the ordering number for where the image should appear in the list of
    /// images for the entity and image type. Lower values appear first.
    /// </summary>
    int Ordering { get; }

    /// <summary>
    /// Gets a value indicating whether the image is enabled. Disabled images should
    /// not be displayed but may still be available for administrative purposes.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Gets a value indicating whether auto-download of the image is enabled.
    /// </summary>
    bool IsDesired { get; }

    /// <summary>
    /// Gets a value indicating whether this image is the preferred image for the
    /// entity. Only one image per entity+type combination should be preferred.
    /// The preferred image is typically shown first in UI listings.
    /// </summary>
    bool IsPreferred { get; }

    /// <summary>
    ///   Indicates that the image cross-reference has a rating and number of
    ///   votes.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Rating), nameof(RatingVotes))]
    bool HasRating => Rating.HasValue && RatingVotes.HasValue;

    /// <summary>
    /// Overall user rating for the image, normalized on a scale of 1-10, if
    /// available. This is typically sourced from TMDB or other providers that
    /// provide community ratings.
    /// </summary>
    double? Rating { get; }

    /// <summary>
    /// Number of votes for the image rating, if available. This indicates
    /// how many users have rated this image.
    /// </summary>
    int? RatingVotes { get; }

    /// <summary>
    ///   The source of the cross-reference.
    /// </summary>
    DataSource Source { get; }

    /// <summary>
    ///   Gets the image associated with this cross-reference, if available.
    /// </summary>
    IImage? GetImage();

    /// <summary>
    ///   Gets the primary image for the associated entity, if available.
    /// </summary>
    /// <returns></returns>
    IImage? GetPrimaryImage();

    /// <summary>
    ///   Gets the metadata entity associated with this cross-reference, if
    ///   available.
    /// </summary>
    IWithImages? GetEntity();
}
