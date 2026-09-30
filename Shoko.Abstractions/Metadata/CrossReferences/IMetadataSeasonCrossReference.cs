namespace Shoko.Abstractions.Metadata.CrossReferences;

/// <summary>
/// A cross-reference keyed on a season of a provider's show: the episodes Shoko
/// linked into that season say the anime covers it.
/// </summary>
/// <remarks>
/// Built from the season each episode link records
/// (<see cref="IMetadataEpisodeCrossReference.SeasonID"/> with its number and
/// series) rather than stored, so it exists only while episodes point into
/// the season. A number or series the link lacks is read from the series
/// store; an episode link whose season, number or series stays unknown adds
/// no season.
/// </remarks>
public interface IMetadataSeasonCrossReference : IMetadataCrossReference
{
    /// <summary>
    ///   The provider series the linked season belongs to.
    /// </summary>
    MetadataGuid ProviderParentID { get; }

    /// <summary>
    /// The season's number within the show it belongs to.
    /// </summary>
    int SeasonNumber { get; }
}

/// <summary>
/// A season cross-reference with its provider entry typed.
/// </summary>
/// <typeparam name="TProvider">The provider entry's type.</typeparam>
public interface IMetadataSeasonCrossReference<out TProvider> : IMetadataSeasonCrossReference, IMetadataCrossReference<TProvider>
    where TProvider : IMetadata;
