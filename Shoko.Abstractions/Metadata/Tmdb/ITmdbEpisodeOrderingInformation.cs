namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
///   Where a TMDB episode sits in one of TMDB's own orderings of its show.
///   The generic <see cref="IEpisodeOrderingInformation"/> with TMDB's typed
///   navigation.
/// </summary>
public interface ITmdbEpisodeOrderingInformation : IEpisodeOrderingInformation
{
    /// <summary>
    ///   The TMDB show ID, the same ID
    ///   <see cref="IEpisodeOrderingInformation.SeriesID"/> holds as text.
    /// </summary>
    int TmdbShowID { get; }

    MetadataGuid IEpisodeOrderingInformation.SeriesID { get => new(MetadataSource.TMDB, MetadataEntityType.Series, TmdbShowID.ToString()); }

    /// <summary>
    ///   The TMDB episode ID, the same ID
    ///   <see cref="IEpisodeOrderingInformation.EpisodeID"/> holds as text.
    /// </summary>
    int TmdbEpisodeID { get; }

    MetadataGuid IEpisodeOrderingInformation.EpisodeID { get => new(MetadataSource.TMDB, MetadataEntityType.Episode, TmdbEpisodeID.ToString()); }

    /// <summary>
    ///   The TMDB show, if it is available.
    /// </summary>
    new ITmdbShow? Series { get; }

    ISeries? IEpisodeOrderingInformation.Series { get => Series; }

    /// <summary>
    ///   The TMDB season the episode is in: its own season for the default
    ///   ordering, or a group of an episode group, if it is available.
    /// </summary>
    new ITmdbSeason? Season { get; }

    ISeason? IEpisodeOrderingInformation.Season { get => Season; }

    /// <summary>
    ///   The TMDB episode.
    /// </summary>
    new ITmdbEpisode Episode { get; }

    IEpisode IEpisodeOrderingInformation.Episode { get => Episode; }
}
