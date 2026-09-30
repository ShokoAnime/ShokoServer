using System;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Services;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   An episode's place in the default ordering of its series: its own
///   season and number.
/// </summary>
/// <param name="episode">The episode.</param>
/// <param name="series">The episode's series, if it is available.</param>
/// <param name="service">The ordering service, which knows the choice, if there is one.</param>
public class DefaultEpisodeOrdering(IEpisode episode, ISeries? series, MetadataOrderingService? service) : IEpisodeOrderingInformation
{
    #region IEpisodeOrderingInformation Implementation

    /// <inheritdoc />
    public MetadataGuid SeriesID => episode.SeriesID;

    /// <inheritdoc />
    public MetadataGuid OrderingID { get; } = MetadataOrderingService.DefaultOrderingID(episode.SeriesID);

    /// <inheritdoc />
    public MetadataGuid? SeasonID => episode.SeasonID;

    /// <inheritdoc />
    public MetadataGuid EpisodeID => episode.ID;

    /// <inheritdoc />
    public int? SeasonNumber => episode.SeasonNumber ?? Season?.SeasonNumber;

    /// <inheritdoc />
    public int EpisodeNumber => episode.EpisodeNumber;

    /// <inheritdoc />
    public EpisodeType EpisodeType => episode.Type;

    /// <inheritdoc />
    public bool IsDefault => true;

    /// <inheritdoc />
    public bool IsPreferred => series is null || service is null || service.IsPreferred(series, OrderingID);

    /// <inheritdoc />
    public ISeries? Series => series;

    /// <inheritdoc />
    public ISeason? Season => SeasonID is { } seasonID ? series?.Seasons.FirstOrDefault(season => season.ID == seasonID) : null;

    /// <inheritdoc />
    public IEpisode Episode => episode;

    #endregion

    #region Dates

    /// <inheritdoc />
    public DateTime CreatedAt => episode switch
    {
        IWithCreationDate created => created.CreatedAt,
        IWithUpdateDate updated => updated.LastUpdatedAt,
        _ => series is IWithCreationDate seriesCreated ? seriesCreated.CreatedAt : DateTime.UnixEpoch,
    };

    /// <inheritdoc />
    public DateTime LastUpdatedAt => episode is IWithUpdateDate updated ? updated.LastUpdatedAt : CreatedAt;

    #endregion
}
