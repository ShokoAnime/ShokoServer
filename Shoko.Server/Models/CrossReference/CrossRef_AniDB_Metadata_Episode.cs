using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.CrossReference;

/// <summary>
/// One AniDB episode linked to a provider's episode.
/// </summary>
public class CrossRef_AniDB_Metadata_Episode : CrossRef_AniDB_Metadata, IMetadataEpisodeCrossReference
{
    #region Database Columns

    public int CrossRef_AniDB_Metadata_EpisodeID { get; set; }

    /// <summary>
    /// The AniDB episode being linked.
    /// </summary>
    public int AnidbEpisodeID { get; set; }

    /// <summary>
    /// The provider's entry above this one, such as the show the episode
    /// belongs to. Empty when there is none.
    /// </summary>
    public string ProviderParentID { get; set; } = string.Empty;

    /// <summary>
    /// The provider's own ID for the season the episode sits in, or
    /// <c>null</c> when it is not known.
    /// </summary>
    public string? ProviderSeasonID { get; set; }

    /// <summary>
    /// The number of the season the episode sits in, when known.
    /// </summary>
    public int? SeasonNumber { get; set; }

    /// <summary>
    /// The episode's number within its season, when known.
    /// </summary>
    public int? EpisodeNumber { get; set; }

    #endregion

    #region Cross-Reference Implementation

    /// <inheritdoc />
    public override MetadataEntityType EntityType => MetadataEntityType.Episode;

    /// <inheritdoc />
    public IShokoEpisode? ShokoEpisode => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(AnidbEpisodeID);

    /// <inheritdoc />
    MetadataGuid? IMetadataEpisodeCrossReference.ProviderParentID => ToProviderID(Source, MetadataEntityType.Series, ProviderParentID);

    /// <inheritdoc />
    MetadataGuid? IMetadataEpisodeCrossReference.SeasonID
        => ToProviderID(Source, MetadataEntityType.Season, ProviderSeasonID) ?? StoredEpisode?.SeasonID;

    /// <inheritdoc />
    int? IMetadataEpisodeCrossReference.SeasonNumber => SeasonNumber ?? StoredEpisode?.SeasonNumber;

    /// <inheritdoc />
    int? IMetadataEpisodeCrossReference.EpisodeNumber => EpisodeNumber ?? StoredEpisode?.EpisodeNumber;

    /// <summary>
    /// The stored episode a link names, which fills in the numbers a link
    /// written before they were recorded does not have.
    /// </summary>
    private IEpisode? StoredEpisode
        => !Source.IsCore && !string.IsNullOrEmpty(ProviderID)
            ? RepoFactory.Metadata_Episode?.GetByProviderID(Source, ProviderID)
            : null;

    #endregion

    #region Storage

    /// <inheritdoc />
    internal override int RowID => CrossRef_AniDB_Metadata_EpisodeID;

    /// <inheritdoc />
    internal override (MetadataSource Source, int AnidbAnimeID, int AnidbEpisodeID) Slot => (Source, AnidbAnimeID, AnidbEpisodeID);

    #endregion
}
