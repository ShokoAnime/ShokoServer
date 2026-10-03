using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   What a source said of a series that needs no column of its own, kept
///   as one JSON object in <see cref="Metadata_Series.ExtraData"/>.
/// </summary>
/// <remarks>
///   A property missing from the stored JSON reads as its default, and one
///   the record does not know is skipped, so a new extra needs no schema
///   step. Add any list member to the equality below.
/// </remarks>
public sealed record Metadata_SeriesExtra : IMetadataDefaultImages<Metadata_SeriesExtra>
{
    #region Fields

    /// <summary>
    ///   A record with no value set.
    /// </summary>
    private static readonly Metadata_SeriesExtra _empty = new();

    #endregion

    #region Properties

    /// <summary>
    ///   The countries the series was made in, as its source gave them.
    /// </summary>
    public IReadOnlyList<string> ProductionCountries { get; init; } = [];

    /// <summary>
    ///   The resource ID of the primary image the series' source pins as its
    ///   default, or <c>null</c> for none.
    /// </summary>
    public string? PrimaryResourceID { get; init; }

    /// <summary>
    ///   The resource ID of the backdrop the series' source pins as its
    ///   default, or <c>null</c> for none.
    /// </summary>
    public string? BackdropResourceID { get; init; }

    /// <summary>
    ///   The resource ID of the logo the series' source pins as its
    ///   default, or <c>null</c> for none.
    /// </summary>
    public string? LogoResourceID { get; init; }

    /// <summary>
    ///   The resource ID of the banner the series' source pins as its
    ///   default, or <c>null</c> for none.
    /// </summary>
    public string? BannerResourceID { get; init; }

    /// <summary>
    ///   The resource ID of the disc image the series' source pins as its
    ///   default, or <c>null</c> for none.
    /// </summary>
    public string? DiscResourceID { get; init; }

    /// <summary>
    ///   Whether no value is set.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty => Equals(_empty);

    #endregion

    #region Methods

    /// <summary>
    ///   The record, or <c>null</c> when no value is set, which is what the
    ///   series stores then.
    /// </summary>
    /// <returns>The record, or <c>null</c>.</returns>
    public Metadata_SeriesExtra? NullIfEmpty()
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
    public Metadata_SeriesExtra WithDefaultResourceIDs(IReadOnlyDictionary<ImageEntityType, string> resourceIDs)
        => this with
        {
            PrimaryResourceID = MetadataDefaultImages.Of(resourceIDs, ImageEntityType.Primary),
            BackdropResourceID = MetadataDefaultImages.Of(resourceIDs, ImageEntityType.Backdrop),
            LogoResourceID = MetadataDefaultImages.Of(resourceIDs, ImageEntityType.Logo),
            BannerResourceID = MetadataDefaultImages.Of(resourceIDs, ImageEntityType.Banner),
            DiscResourceID = MetadataDefaultImages.Of(resourceIDs, ImageEntityType.Disc),
        };

    /// <summary>
    ///   Whether another record holds the same values, comparing lists by
    ///   their items.
    /// </summary>
    /// <param name="other">The other record.</param>
    /// <returns><c>true</c> when every value is the same.</returns>
    public bool Equals(Metadata_SeriesExtra? other)
        => other is not null &&
            (ProductionCountries ?? []).SequenceEqual(other.ProductionCountries ?? [], StringComparer.Ordinal) &&
            PrimaryResourceID == other.PrimaryResourceID &&
            BackdropResourceID == other.BackdropResourceID &&
            LogoResourceID == other.LogoResourceID &&
            BannerResourceID == other.BannerResourceID &&
            DiscResourceID == other.DiscResourceID;

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(
            (ProductionCountries ?? []).Aggregate(0, (hash, country) => HashCode.Combine(hash, StringComparer.Ordinal.GetHashCode(country))),
            PrimaryResourceID, BackdropResourceID, LogoResourceID, BannerResourceID, DiscResourceID
        );

    #endregion
}
