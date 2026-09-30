using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Tmdb;

/// <summary>
/// A TMDB episode.
/// </summary>
public interface ITmdbEpisode : IEpisode, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    ///   The ID of the TMDB show this belongs to, the same ID
    ///   <see cref="IEpisode.SeriesID"/> holds as text.
    /// </summary>
    int TmdbShowID { get; }

    MetadataGuid IEpisode.SeriesID { get => new(MetadataSource.TMDB, MetadataEntityType.Series, TmdbShowID.ToString()); }

    /// <summary>
    ///   The TMDB episode ID, the same ID <see cref="IMetadata.ID"/> holds as
    ///   text.
    /// </summary>
    int TmdbID { get; }

    /// <summary>
    ///   TMDB's own ID for the ordering this episode is read in: the show ID
    ///   for the show's default ordering, or the episode group collection ID
    ///   for a view of it from an episode group.
    /// </summary>
    string TmdbOrderingID { get; }

    /// <summary>
    ///   The ID TMDB lists for this episode among its external IDs, if known and available.
    /// </summary>
    int? TvdbEpisodeID { get; }

    /// <summary>
    /// Get the TMDB show info for the episode, if available.
    /// </summary>
    new ITmdbShow? Series { get; }

    /// <summary>
    ///   The TMDB ordering this episode is read in: the show's default
    ///   ordering, or the episode group this view of it comes from, if the
    ///   show is available.
    /// </summary>
    ITmdbShowOrderingInformation? SeriesOrdering { get; }

    /// <summary>
    /// Get the TMDB season info for the episode, if available.
    /// </summary>
    ITmdbSeason? Season { get; }

    /// <summary>
    ///   The episode's place in the TMDB ordering it is read in, see
    ///   <see cref="SeriesOrdering"/>.
    /// </summary>
    ITmdbEpisodeOrderingInformation Ordering { get; }

    /// <summary>
    ///   The episode's places in TMDB's own orderings of its show: its place
    ///   in the default ordering first, then one for each episode group it is
    ///   in. The places in orderings others made are in
    ///   <see cref="IEpisode.Orderings"/>.
    /// </summary>
    IReadOnlyList<ITmdbEpisodeOrderingInformation> TmdbOrderings { get; }
}
