using System;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Services;
using Shoko.Server.Services.Ordering;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   An episode's place in the default ordering of its series, and the
///   episode as that ordering presents it: its own season and number, and
///   where it airs for a placed special.
/// </summary>
/// <param name="episode">The episode.</param>
/// <param name="series">The episode's series, if it is available.</param>
/// <param name="service">The ordering service, which knows the choice and the placed specials, if there is one.</param>
public class DefaultEpisodeOrdering(IEpisode episode, ISeries? series, MetadataOrderingService? service) : EpisodeInOrdering(episode)
{
    private OrderingAiring? _airing;

    private bool _airingRead;

    /// <summary>
    ///   Where the episode airs, when it is a placed special.
    /// </summary>
    private OrderingAiring? Airing
    {
        get
        {
            if (!_airingRead)
            {
                _airing = series is not null && service is not null ? service.GetDefaultPlacement(series).AiringOf(EpisodeID) : null;
                _airingRead = true;
            }

            return _airing;
        }
    }

    #region Place

    /// <inheritdoc />
    public override MetadataGuid OrderingID { get; } = MetadataOrderingService.DefaultOrderingID(episode.SeriesID);

    /// <inheritdoc />
    public override MetadataGuid? SeasonID => LinkedEpisode.SeasonID;

    /// <inheritdoc />
    public override int? SeasonNumber => LinkedEpisode.SeasonNumber ?? Season?.SeasonNumber;

    /// <inheritdoc />
    public override int EpisodeNumber => LinkedEpisode.EpisodeNumber;

    /// <inheritdoc />
    public override EpisodeType EpisodeType => LinkedEpisode.Type;

    /// <inheritdoc />
    public override bool IsDefault => true;

    /// <inheritdoc />
    public override bool IsPreferred => series is null || service is null || service.IsPreferred(series, OrderingID);

    /// <inheritdoc />
    public override int? AirsBeforeSeasonNumber => Airing?.AirsBeforeSeasonNumber;

    /// <inheritdoc />
    public override int? AirsBeforeEpisodeNumber => Airing?.AirsBeforeEpisodeNumber;

    /// <inheritdoc />
    public override int? AirsAfterSeasonNumber => Airing?.AirsAfterSeasonNumber;

    /// <inheritdoc />
    public override MetadataGuid? AirsAfterEpisodeID => Airing?.AirsAfterEpisodeID;

    /// <inheritdoc />
    public override MetadataGuid? AirsBeforeEpisodeID => Airing?.AirsBeforeEpisodeID;

    /// <inheritdoc />
    public override ISeries Series => series ?? LinkedEpisode.Series;

    /// <inheritdoc />
    public override ISeason? Season => SeasonID is { } seasonID ? series?.Seasons.FirstOrDefault(season => season.ID == seasonID) : null;

    #endregion

    #region Dates

    /// <inheritdoc />
    public override DateTime CreatedAt => LinkedEpisode.CreatedAt;

    /// <inheritdoc />
    public override DateTime LastUpdatedAt => LinkedEpisode.LastUpdatedAt;

    #endregion
}

/// <summary>
///   An episode's place in the default ordering of its series, with the
///   series, season and linked episode typed.
/// </summary>
/// <typeparam name="TSeries">The series' type.</typeparam>
/// <typeparam name="TEpisode">The episodes' type.</typeparam>
/// <param name="episode">The episode, which must be an <see cref="IEpisode{TSeries,TEpisode}"/>.</param>
/// <param name="series">The episode's series, if it is available.</param>
/// <param name="service">The ordering service, which knows the choice, if there is one.</param>
public sealed class DefaultEpisodeOrdering<TSeries, TEpisode>(TEpisode episode, TSeries? series, MetadataOrderingService? service)
    : DefaultEpisodeOrdering(episode, series, service), IEpisodeOrderingInformation<TSeries, TEpisode>
    where TSeries : class, ISeries
    where TEpisode : class, IEpisode
{
    private readonly TEpisode _episode = episode;

    private readonly TSeries? _series = series;

    #region IEpisodeOrderingInformation<TSeries, TEpisode> Implementation

    /// <inheritdoc />
    public new TSeries Series => _series ?? ((IEpisode<TSeries, TEpisode>)_episode).Series;

    /// <inheritdoc />
    public new ISeason<TSeries, TEpisode>? Season => SeasonID is { } seasonID && _series is not null
        ? ((ISeries<TSeries, TEpisode>)_series).Seasons.FirstOrDefault(season => season.ID == seasonID)
        : null;

    /// <inheritdoc />
    public TEpisode Episode => _episode;

    #endregion
}
