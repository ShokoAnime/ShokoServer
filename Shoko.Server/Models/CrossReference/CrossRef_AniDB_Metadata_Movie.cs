using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.CrossReference;

/// <summary>
/// A whole AniDB anime linked to a provider's film, kept against the episode
/// standing for it.
/// </summary>
public class CrossRef_AniDB_Metadata_Movie : CrossRef_AniDB_Metadata, IMetadataMovieCrossReference
{
    #region Database Columns

    public int CrossRef_AniDB_Metadata_MovieID { get; set; }

    /// <summary>
    /// The AniDB episode standing for the film.
    /// </summary>
    public int AnidbEpisodeID { get; set; }

    #endregion

    #region Cross-Reference Implementation

    /// <inheritdoc />
    public override MetadataEntityType EntityType => MetadataEntityType.Movie;

    /// <inheritdoc />
    public IShokoEpisode? ShokoEpisode => RepoFactory.AnimeEpisode.GetByAniDBEpisodeID(AnidbEpisodeID);

    #endregion

    #region Storage

    /// <inheritdoc />
    internal override int RowID => CrossRef_AniDB_Metadata_MovieID;

    /// <inheritdoc />
    internal override (MetadataSource Source, int AnidbAnimeID, int AnidbEpisodeID) Slot => (Source, AnidbAnimeID, AnidbEpisodeID);

    #endregion
}
