using System;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Metadata.CrossReferences;

/// <summary>
/// A link Shoko made between one of its own entries and an entry at a metadata
/// provider: Shoko's claim that the two are the same work, unlike a provider's
/// <see cref="IRelatedMetadata"/> or <see cref="ISuggestedMetadata"/>.
/// </summary>
public interface IMetadataCrossReference
{
    /// <summary>
    /// AniDB anime id of the linked entry.
    /// </summary>
    int AnidbAnimeID { get; }

    /// <summary>
    ///   The provider entry this links to, or <c>null</c> when the Shoko entry
    ///   is deliberately on no entry of <see cref="Source"/>, so matching
    ///   leaves it alone.
    /// </summary>
    MetadataGuid? ProviderID { get; }

    /// <summary>
    ///   The level the link is made at, which is the kind of entry
    ///   <see cref="ProviderID"/> names when it names one, but for a film
    ///   claiming a whole anime: that is kept at the series level and names
    ///   the film.
    /// </summary>
    MetadataEntityType EntityType { get; }

    /// <summary>
    ///   The source the link points at, which is the source of
    ///   <see cref="ProviderID"/> when it names an entry.
    /// </summary>
    MetadataSource Source { get; }

    /// <summary>
    /// How the link was arrived at, and how much to trust it.
    /// </summary>
    MatchRating MatchRating { get; }

    /// <summary>
    /// Which link this is, when more than one points at the same Shoko entry.
    /// Best first, starting at <c>0</c>, and <c>0</c> where the relation cannot
    /// repeat.
    /// </summary>
    int Ordering { get; }

    /// <summary>
    /// The linked series, if it is in the collection.
    /// </summary>
    IShokoSeries? ShokoSeries { get; }

    /// <summary>
    /// The provider entry, if it is stored.
    /// </summary>
    IMetadata? Provider { get; }

    /// <summary>
    /// The provider that wrote the link, where one did. Only where it came
    /// from: a link belongs to its source, not to a provider.
    /// </summary>
    Guid? WrittenBy { get; }
}

/// <summary>
/// A cross-reference with its provider entry typed.
/// </summary>
/// <typeparam name="TProvider">The provider entry's type.</typeparam>
public interface IMetadataCrossReference<out TProvider> : IMetadataCrossReference where TProvider : IMetadata
{
    /// <summary>
    /// The provider entry, if it is stored.
    /// </summary>
    new TProvider? Provider { get; }
}
