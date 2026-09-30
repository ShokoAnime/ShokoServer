using System;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;

namespace Shoko.Abstractions.Metadata;

/// <summary>
///   A link read through its typed contract: every member is the link's own,
///   and the provider entry is the link's when it is a
///   <typeparamref name="TProvider"/>.
/// </summary>
/// <typeparam name="TCrossReference">The level of the link.</typeparam>
/// <typeparam name="TProvider">The provider entry's type.</typeparam>
/// <param name="link">The link.</param>
internal abstract class MetadataCrossReferenceAdapter<TCrossReference, TProvider>(TCrossReference link) : IMetadataCrossReference<TProvider>
    where TCrossReference : IMetadataCrossReference
    where TProvider : IMetadata
{
    #region Link

    /// <summary>
    ///   The link behind the adapter.
    /// </summary>
    protected TCrossReference Link { get; } = link;

    #endregion

    #region IMetadataCrossReference Implementation

    /// <summary>
    ///   AniDB anime id of the linked entry.
    /// </summary>
    public int AnidbAnimeID => Link.AnidbAnimeID;

    /// <summary>
    ///   The provider entry the link points at, or <c>null</c> when it
    ///   points at nothing.
    /// </summary>
    public MetadataGuid? ProviderID => Link.ProviderID;

    /// <summary>
    ///   The level the link is made at.
    /// </summary>
    public MetadataEntityType EntityType => Link.EntityType;

    /// <summary>
    ///   The source the link points at.
    /// </summary>
    public MetadataSource Source => Link.Source;

    /// <summary>
    ///   How the link was arrived at, and how much to trust it.
    /// </summary>
    public MatchRating MatchRating => Link.MatchRating;

    /// <summary>
    ///   Which link this is among the links of the same Shoko entry.
    /// </summary>
    public int Ordering => Link.Ordering;

    /// <summary>
    ///   The linked series, if it is in the collection.
    /// </summary>
    public IShokoSeries? ShokoSeries => Link.ShokoSeries;

    /// <summary>
    ///   The provider entry, if it is stored and is a
    ///   <typeparamref name="TProvider"/>.
    /// </summary>
    public TProvider? Provider => Link.Provider is TProvider provider ? provider : default;

    /// <summary>
    ///   The provider that wrote the link, where one did.
    /// </summary>
    public Guid? WrittenBy => Link.WrittenBy;

    IMetadata? IMetadataCrossReference.Provider => Link.Provider;

    #endregion
}

/// <summary>
///   A series-level link read through its typed contract.
/// </summary>
/// <typeparam name="TProvider">The provider entry's type.</typeparam>
/// <param name="link">The link.</param>
internal sealed class MetadataSeriesCrossReferenceAdapter<TProvider>(IMetadataSeriesCrossReference link)
    : MetadataCrossReferenceAdapter<IMetadataSeriesCrossReference, TProvider>(link), IMetadataSeriesCrossReference<TProvider>
    where TProvider : ISeries;

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
