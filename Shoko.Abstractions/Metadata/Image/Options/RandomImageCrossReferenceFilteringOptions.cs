using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;

namespace Shoko.Abstractions.Metadata.Image.Options;

/// <summary>
///   Options for filtering a random image cross-reference from
///   <see cref="IImageManager.GetRandomImageCrossReference"/>.
///   The <c>ImageSource</c> and <c>ImageType</c> are required parameters of
///   the method itself; only the optional fields are in this class.
/// </summary>
public sealed class RandomImageCrossReferenceFilteringOptions
{
    /// <summary>
    ///   Optional. If set, will restrict to cross-references from the given
    ///   source (e.g. AniDB, TMDB, AniList, User, etc.).
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
}
