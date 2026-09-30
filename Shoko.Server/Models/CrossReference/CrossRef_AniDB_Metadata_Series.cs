using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;

namespace Shoko.Server.Models.CrossReference;

/// <summary>
/// A whole AniDB anime linked to a provider's series.
/// </summary>
public class CrossRef_AniDB_Metadata_Series : CrossRef_AniDB_Metadata, IMetadataSeriesCrossReference
{
    #region Database Columns

    public int CrossRef_AniDB_Metadata_SeriesID { get; set; }

    /// <summary>
    /// The kind of entry the link names: a series, or a movie for a film
    /// claiming the whole anime.
    /// </summary>
    public MetadataEntityType ProviderType { get; set; } = MetadataEntityType.Series;

    #endregion

    #region Cross-Reference Implementation

    /// <inheritdoc />
    public override MetadataEntityType EntityType => MetadataEntityType.Series;

    /// <inheritdoc />
    public override MetadataEntityType ProviderEntityType => ProviderType;

    #endregion

    #region Storage

    /// <inheritdoc />
    internal override int RowID => CrossRef_AniDB_Metadata_SeriesID;

    /// <inheritdoc />
    internal override (MetadataSource Source, int AnidbAnimeID, int AnidbEpisodeID) Slot => (Source, AnidbAnimeID, 0);

    #endregion
}
