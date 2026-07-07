using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Abstractions.Metadata.Image.Options;

/// <summary>
///   Options for filtering image cross-references from
///   <see cref="IImageManager"/> and <see cref="IWithImages"/>.
/// </summary>
public sealed class ImageCrossReferenceFilteringOptions
{
    /// <summary>
    ///   Optional. If set, will restrict the returned list to only containing
    ///   cross-references for images from the given image source.
    /// </summary>
    public DataSource? ImageSource { get; set; }

    /// <summary>
    ///   Optional. If set, will restrict the returned list to only containing
    ///   the cross-references of the given image type.
    /// </summary>
    public ImageEntityType? ImageType { get; set; }

    /// <summary>
    ///   Optional. If set, will restrict the returned list to only containing
    ///   cross-references from the given source.
    /// </summary>
    public DataSource? XrefSource { get; set; }

    /// <summary>
    ///   Optional. Filter by entity source (e.g. Shoko, AniDB, TMDB).
    /// </summary>
    public DataSource? EntitySource { get; set; }

    /// <summary>
    ///   Optional. Filter by entity type (e.g. Series, Episode, Movie).
    /// </summary>
    public DataEntityType? EntityType { get; set; }

    /// <summary>
    ///   Optional. Filter by enabled state. Pass <c>true</c> to get only
    ///   enabled, <c>false</c> to get only disabled, or <c>null</c> to get
    ///   both. Defaults to <c>null</c>.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    ///   Optional. Filter by desired state. Pass <c>true</c> to get only
    ///   desired, <c>false</c> to get only undesired, or <c>null</c> to get
    ///   both. Defaults to <c>null</c>.
    /// </summary>
    public bool? IsDesired { get; set; }

    /// <summary>
    ///   Optional. Filter by preferred state. Pass <c>true</c> to get only
    ///   preferred, <c>false</c> to get only not preferred, or <c>null</c> to
    ///   get both. Defaults to <c>null</c>.
    /// </summary>
    public bool? IsPreferred { get; set; }

    /// <summary>
    ///   Optional. Filter by available state. Pass <c>true</c> to get only
    ///   available, <c>false</c> to get only unavailable, or <c>null</c> to get
    ///   both. Defaults to <c>null</c>.
    /// </summary>
    public bool? IsAvailable { get; set; }

    /// <summary>
    ///   Optional. Filter by primary image flag. Pass <c>true</c> to get only
    ///   cross-references where <c>PrimaryImageID == ImageID</c>, <c>false</c>
    ///   for where they differ, or <c>null</c> to get both.
    /// </summary>
    public bool? IsPrimaryImage { get; set; }

    /// <summary>
    ///   Optional. Filter by primary image availability on the cross-reference.
    ///   Pass <c>true</c> to get only cross-references where the primary image
    ///   is available, <c>false</c> for only where it is unavailable, or
    ///   <c>null</c> for no filter.
    /// </summary>
    public bool? IsPrimaryAvailable { get; set; }

    /// <summary>
    ///   Optional. Set to <c>false</c> to only retrieve cross-references for
    ///   the entity's own images. Set to <c>true</c> to also retrieve
    ///   cross-references for images from other entities linked to the entity.
    ///   Set to <c>null</c> to let the service decide based on the entity.
    ///   Defaults to <c>null</c>.
    /// </summary>
    public bool? LinkedEntityImages { get; set; }
}
