using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Services.Ordering;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   An episode's place in a group of a stored ordering.
/// </summary>
/// <typeparam name="TSeries">The series' type.</typeparam>
/// <typeparam name="TEpisode">The episodes' type.</typeparam>
/// <param name="group">The group the episode is at home in.</param>
/// <param name="episode">The episode.</param>
/// <param name="episodeNumber">The episode's number in the group, from <c>1</c>, placed specials skipped.</param>
/// <param name="airing">Where a placed special airs, on its special group place.</param>
internal sealed class StoredEpisodeOrdering<TSeries, TEpisode>(StoredOrderingGroup<TSeries, TEpisode> group, TEpisode episode, int episodeNumber, OrderingAiring? airing)
    : IEpisodeOrderingInformation<TSeries, TEpisode>
    where TSeries : class, ISeries
    where TEpisode : class, IEpisode
{
    #region IEpisodeOrderingInformation<TSeries, TEpisode> Implementation

    /// <inheritdoc />
    public MetadataGuid SeriesID => group.SeriesID;

    /// <inheritdoc />
    public MetadataGuid OrderingID => group.Ordering.ID;

    /// <inheritdoc />
    public MetadataGuid? SeasonID => group.ID;

    /// <inheritdoc />
    public MetadataGuid EpisodeID => episode.ID;

    /// <inheritdoc />
    public int? SeasonNumber => group.SeasonNumber;

    /// <inheritdoc />
    public int EpisodeNumber => episodeNumber;

    /// <inheritdoc />
    public EpisodeType EpisodeType => group.IsSpecial ? EpisodeType.Special : EpisodeType.Episode;

    /// <inheritdoc />
    public bool IsDefault => false;

    /// <inheritdoc />
    public bool IsPreferred => group.Ordering.IsPreferred;

    /// <inheritdoc />
    public int? AirsBeforeSeasonNumber => airing?.AirsBeforeSeasonNumber;

    /// <inheritdoc />
    public int? AirsBeforeEpisodeNumber => airing?.AirsBeforeEpisodeNumber;

    /// <inheritdoc />
    public int? AirsAfterSeasonNumber => airing?.AirsAfterSeasonNumber;

    /// <inheritdoc />
    public MetadataGuid? AirsAfterEpisodeID => airing?.AirsAfterEpisodeID;

    /// <inheritdoc />
    public MetadataGuid? AirsBeforeEpisodeID => airing?.AirsBeforeEpisodeID;

    /// <inheritdoc />
    public TSeries Series => group.Series;

    /// <inheritdoc />
    public ISeason<TSeries, TEpisode>? Season => group;

    /// <inheritdoc />
    public TEpisode Episode => episode;

    #endregion

    #region Dates

    /// <inheritdoc />
    public DateTime CreatedAt => group.Ordering.CreatedAt;

    /// <inheritdoc />
    public DateTime LastUpdatedAt => group.Ordering.LastUpdatedAt;

    #endregion
}
