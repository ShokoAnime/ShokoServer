using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   A film link read through its typed contract.
/// </summary>
/// <typeparam name="TProvider">The provider entry's type.</typeparam>
/// <param name="link">The link.</param>
internal sealed class MetadataMovieCrossReferenceAdapter<TProvider>(IMetadataMovieCrossReference link)
    : MetadataCrossReferenceAdapter<IMetadataMovieCrossReference, TProvider>(link), IMetadataMovieCrossReference<TProvider>
    where TProvider : IMovie
{
    #region IMetadataMovieCrossReference Implementation

    /// <summary>
    ///   AniDB episode id of the linked entry.
    /// </summary>
    public int AnidbEpisodeID => Link.AnidbEpisodeID;

    /// <summary>
    ///   The linked episode, if it is in the collection.
    /// </summary>
    public IShokoEpisode? ShokoEpisode => Link.ShokoEpisode;

    #endregion
}
