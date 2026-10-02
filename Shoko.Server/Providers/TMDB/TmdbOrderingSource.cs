using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Repositories.Direct.TMDB.Optional;
using Shoko.Server.Services;

namespace Shoko.Server.Providers.TMDB;

/// <summary>
///   Serves TMDB's episode groups, kept in TMDB's own tables, as orderings of
///   its shows, and TMDB's typed views of a show's default ordering.
/// </summary>
/// <param name="orderingRepository">TMDB's episode groups.</param>
/// <param name="episodeRepository">The episodes' places in them.</param>
public class TmdbOrderingSource(
    TMDB_AlternateOrderingRepository orderingRepository,
    TMDB_AlternateOrdering_EpisodeRepository episodeRepository
) : ICoreOrderingSource
{
    #region ICoreOrderingSource Implementation

    /// <inheritdoc />
    public MetadataSource Source => MetadataSource.TMDB;

    /// <inheritdoc />
    public IReadOnlyList<IOrdering> GetOrderings(ISeries series)
        => ShowID(series.ID) is { } showID
            ? [.. orderingRepository.GetByTmdbShowID(showID).OrderBy(ordering => ordering.CreatedAt).ThenBy(ordering => ordering.TMDB_AlternateOrderingID)]
            : [];

    /// <inheritdoc />
    public IOrdering? GetOrdering(MetadataGuid orderingID)
        => orderingID.Source == MetadataSource.TMDB && orderingID.EntityType == MetadataEntityType.Ordering
            ? orderingRepository.GetByTmdbEpisodeGroupCollectionID(orderingID.ID)
            : null;

    /// <inheritdoc />
    public IReadOnlyList<IEpisodeOrderingInformation> GetEpisodeOrderings(IEpisode episode)
        => episode.ID.Source == MetadataSource.TMDB && episode.ID.EntityType == MetadataEntityType.Episode && episode.ID.TryGetNumericID<int>(out var episodeID)
            ? [.. episodeRepository.GetByTmdbEpisodeID(episodeID)]
            : [];

    /// <inheritdoc />
    public IOrdering? GetDefaultOrdering(ISeries series, MetadataOrderingService service)
        => series is TMDB_Show show ? new TMDB_Show_DefaultOrdering(show, service) : null;

    /// <inheritdoc />
    public IEpisodeOrderingInformation? GetDefaultEpisodeOrdering(IEpisode episode, ISeries? series, MetadataOrderingService service)
        => episode is TMDB_Episode tmdbEpisode ? new TMDB_Episode_DefaultOrdering(tmdbEpisode, series as TMDB_Show, service) : null;

    #endregion

    #region Helpers

    /// <summary>
    ///   The TMDB show ID a series ID holds.
    /// </summary>
    /// <param name="seriesID">The series.</param>
    /// <returns>The show ID, or <c>null</c> when the ID is not a TMDB show's.</returns>
    private static int? ShowID(MetadataGuid seriesID)
        => seriesID.Source == MetadataSource.TMDB && seriesID.EntityType == MetadataEntityType.Series && seriesID.TryGetNumericID<int>(out var showID)
            ? showID
            : null;

    #endregion
}
