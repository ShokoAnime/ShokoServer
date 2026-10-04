using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Services.Ordering;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   An episode's place in a group of a stored ordering, and the episode as
///   that group presents it, numbered by the place.
/// </summary>
/// <param name="ordering">The ordering.</param>
/// <param name="group">The group the episode is listed in, as a season of the ordering.</param>
/// <param name="episode">The episode.</param>
/// <param name="episodeNumber">The episode's number in the group, from <c>1</c>.</param>
/// <param name="airing">Where a placed special airs, on its special group place.</param>
internal class StoredEpisodeOrdering(IOrdering ordering, ISeason group, IEpisode episode, int episodeNumber, OrderingAiring? airing)
    : EpisodeInOrdering(episode)
{
    #region Place

    /// <inheritdoc />
    public override MetadataGuid OrderingID => ordering.ID;

    /// <inheritdoc />
    public override MetadataGuid? SeasonID => group.ID;

    /// <inheritdoc />
    public override int? SeasonNumber => group.SeasonNumber;

    /// <inheritdoc />
    public override int EpisodeNumber => episodeNumber;

    /// <inheritdoc />
    public override EpisodeType EpisodeType => group.IsSpecial ? EpisodeType.Special : EpisodeType.Episode;

    /// <inheritdoc />
    public override bool IsDefault => false;

    /// <inheritdoc />
    public override bool IsPreferred => ordering.IsPreferred;

    /// <inheritdoc />
    public override int? AirsBeforeSeasonNumber => airing?.AirsBeforeSeasonNumber;

    /// <inheritdoc />
    public override int? AirsBeforeEpisodeNumber => airing?.AirsBeforeEpisodeNumber;

    /// <inheritdoc />
    public override int? AirsAfterSeasonNumber => airing?.AirsAfterSeasonNumber;

    /// <inheritdoc />
    public override MetadataGuid? AirsAfterEpisodeID => airing?.AirsAfterEpisodeID;

    /// <inheritdoc />
    public override MetadataGuid? AirsBeforeEpisodeID => airing?.AirsBeforeEpisodeID;

    /// <inheritdoc />
    public override ISeries Series => group.Series;

    /// <inheritdoc />
    public override ISeason? Season => group;

    #endregion

    #region Dates

    /// <inheritdoc />
    public override DateTime CreatedAt => ordering.CreatedAt;

    /// <inheritdoc />
    public override DateTime LastUpdatedAt => ordering.LastUpdatedAt;

    #endregion
}

/// <summary>
///   An episode's place in a group of a stored ordering, with the series,
///   group and linked episode typed.
/// </summary>
/// <typeparam name="TSeries">The series' type.</typeparam>
/// <typeparam name="TEpisode">The episodes' type.</typeparam>
/// <param name="group">The group the episode is at home in.</param>
/// <param name="episode">The episode.</param>
/// <param name="episodeNumber">The episode's number in the group, from <c>1</c>, placed specials skipped.</param>
/// <param name="airing">Where a placed special airs, on its special group place.</param>
internal sealed class StoredEpisodeOrdering<TSeries, TEpisode>(StoredOrderingGroup<TSeries, TEpisode> group, TEpisode episode, int episodeNumber, OrderingAiring? airing)
    : StoredEpisodeOrdering(group.Ordering, group, episode, episodeNumber, airing), IEpisodeOrderingInformation<TSeries, TEpisode>
    where TSeries : class, ISeries
    where TEpisode : class, IEpisode
{
    private readonly StoredOrderingGroup<TSeries, TEpisode> _group = group;

    private readonly TEpisode _episode = episode;

    #region IEpisodeOrderingInformation<TSeries, TEpisode> Implementation

    /// <inheritdoc />
    public new TSeries Series => _group.Series;

    /// <inheritdoc />
    public new ISeason<TSeries, TEpisode>? Season => _group;

    /// <inheritdoc />
    public TEpisode Episode => _episode;

    #endregion
}
