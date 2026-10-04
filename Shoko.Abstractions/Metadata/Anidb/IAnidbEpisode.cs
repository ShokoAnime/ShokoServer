namespace Shoko.Abstractions.Metadata.Anidb;

/// <summary>
/// An AniDB episode.
/// </summary>
public interface IAnidbEpisode : IEpisode<IAnidbAnime, IAnidbEpisode>
{
    /// <summary>
    ///   The ID of the AniDB anime this belongs to, the same ID
    ///   <see cref="IEpisode.SeriesID"/> holds as text.
    /// </summary>
    int AnidbAnimeID { get; }

    MetadataGuid IEpisode.SeriesID { get => new(MetadataSource.AniDB, MetadataEntityType.Series, AnidbAnimeID.ToString()); }

    /// <summary>
    ///   The AniDB episode ID, the same ID <see cref="IMetadata.ID"/> holds as
    ///   text.
    /// </summary>
    int AnidbID { get; }
}
