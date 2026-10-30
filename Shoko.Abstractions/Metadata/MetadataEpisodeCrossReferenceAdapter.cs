using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   An episode-level link read through its typed contract.
/// </summary>
/// <typeparam name="TProvider">The provider entry's type.</typeparam>
/// <param name="link">The link.</param>
internal sealed class MetadataEpisodeCrossReferenceAdapter<TProvider>(IMetadataEpisodeCrossReference link)
    : MetadataCrossReferenceAdapter<IMetadataEpisodeCrossReference, TProvider>(link), IMetadataEpisodeCrossReference<TProvider>
    where TProvider : IEpisode
{
    #region IMetadataEpisodeCrossReference Implementation

    /// <summary>
    ///   AniDB episode id of the linked entry.
    /// </summary>
    public int AnidbEpisodeID => Link.AnidbEpisodeID;

    /// <summary>
    ///   The provider series the linked episode belongs to, if known.
    /// </summary>
    public MetadataGuid? ProviderParentID => Link.ProviderParentID;

    /// <summary>
    ///   The provider season the linked episode sits in, if known.
    /// </summary>
    public MetadataGuid? SeasonID => Link.SeasonID;

    /// <summary>
    ///   The number of the season the linked episode sits in, if known.
    /// </summary>
    public int? SeasonNumber => Link.SeasonNumber;

    /// <summary>
    ///   The linked episode's number within its season, if known.
    /// </summary>
    public int? EpisodeNumber => Link.EpisodeNumber;

    /// <summary>
    ///   The linked episode, if it is in the collection.
    /// </summary>
    public IShokoEpisode? ShokoEpisode => Link.ShokoEpisode;

    #endregion
}
