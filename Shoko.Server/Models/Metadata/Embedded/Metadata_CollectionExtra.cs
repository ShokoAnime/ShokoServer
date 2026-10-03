using System.Collections.Generic;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   What a source said of a collection that needs no column of its own, kept
///   as one JSON object in <see cref="Metadata_Collection.ExtraData"/>.
/// </summary>
/// <remarks>
///   A property missing from the stored JSON reads as its default, and one
///   the record does not know is skipped, so a new extra needs no schema
///   step. Keep the members values or strings, so two records still compare
///   by value.
/// </remarks>
public sealed record Metadata_CollectionExtra : IMetadataDefaultImages<Metadata_CollectionExtra>
{
    #region Fields

    /// <summary>
    ///   A record with no value set.
    /// </summary>
    private static readonly Metadata_CollectionExtra _empty = new();

    #endregion

    #region Properties

    /// <summary>
    ///   The resource ID of the primary image the collection's source pins as its
    ///   default, or <c>null</c> for none.
    /// </summary>
    public string? PrimaryResourceID { get; init; }

    /// <summary>
    ///   The resource ID of the backdrop the collection's source pins as its
    ///   default, or <c>null</c> for none.
    /// </summary>
    public string? BackdropResourceID { get; init; }

    /// <summary>
    ///   The resource ID of the logo the collection's source pins as its
    ///   default, or <c>null</c> for none.
    /// </summary>
    public string? LogoResourceID { get; init; }

    /// <summary>
    ///   The resource ID of the banner the collection's source pins as its
    ///   default, or <c>null</c> for none.
    /// </summary>
    public string? BannerResourceID { get; init; }

    /// <summary>
    ///   The resource ID of the disc image the collection's source pins as its
    ///   default, or <c>null</c> for none.
    /// </summary>
    public string? DiscResourceID { get; init; }

    /// <summary>
    ///   Whether no value is set.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty => this == _empty;

    #endregion

    #region Methods

    /// <summary>
    ///   The record, or <c>null</c> when no value is set, which is what the
    ///   collection stores then.
    /// </summary>
    /// <returns>The record, or <c>null</c>.</returns>
    public Metadata_CollectionExtra? NullIfEmpty()
        => IsEmpty ? null : this;

    /// <inheritdoc />
    public string? GetDefaultResourceID(ImageEntityType imageType)
        => imageType switch
        {
            ImageEntityType.Primary => PrimaryResourceID,
            ImageEntityType.Backdrop => BackdropResourceID,
            ImageEntityType.Logo => LogoResourceID,
            ImageEntityType.Banner => BannerResourceID,
            ImageEntityType.Disc => DiscResourceID,
            _ => null,
        };

    /// <inheritdoc />
    public Metadata_CollectionExtra WithDefaultResourceIDs(IReadOnlyDictionary<ImageEntityType, string> resourceIDs)
        => this with
        {
            PrimaryResourceID = MetadataDefaultImages.Of(resourceIDs, ImageEntityType.Primary),
            BackdropResourceID = MetadataDefaultImages.Of(resourceIDs, ImageEntityType.Backdrop),
            LogoResourceID = MetadataDefaultImages.Of(resourceIDs, ImageEntityType.Logo),
            BannerResourceID = MetadataDefaultImages.Of(resourceIDs, ImageEntityType.Banner),
            DiscResourceID = MetadataDefaultImages.Of(resourceIDs, ImageEntityType.Disc),
        };

    #endregion
}
