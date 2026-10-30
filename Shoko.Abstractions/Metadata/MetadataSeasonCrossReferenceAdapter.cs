using Shoko.Abstractions.Metadata.CrossReferences;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   A season-level link read through its typed contract.
/// </summary>
/// <typeparam name="TProvider">The provider entry's type.</typeparam>
/// <param name="link">The link.</param>
internal sealed class MetadataSeasonCrossReferenceAdapter<TProvider>(IMetadataSeasonCrossReference link)
    : MetadataCrossReferenceAdapter<IMetadataSeasonCrossReference, TProvider>(link), IMetadataSeasonCrossReference<TProvider>
    where TProvider : ISeason
{
    #region IMetadataSeasonCrossReference Implementation

    /// <summary>
    ///   The provider series the linked season belongs to.
    /// </summary>
    public MetadataGuid ProviderParentID => Link.ProviderParentID;

    /// <summary>
    ///   The season's number within the show it belongs to.
    /// </summary>
    public int SeasonNumber => Link.SeasonNumber;

    #endregion
}
