using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Services;
using Shoko.Server.Services.Ordering;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   The default ordering of a series of any source, made from its own
///   seasons and never stored. Its specials stay in season 0, but a placed
///   one is watched where it airs.
/// </summary>
/// <param name="series">The series.</param>
/// <param name="service">The ordering service, which knows the choice, the hidden episodes and the placed specials, if there is one.</param>
public class DefaultOrdering(ISeries series, MetadataOrderingService? service) : IOrdering, IPlacedOrdering
{
    private IReadOnlyList<IEpisode>? _episodes;

    private OrderingPlaces? _placement;

    /// <summary>
    ///   The ordering's places, its specials placed. Kept by the service
    ///   when there is one, else built here.
    /// </summary>
    internal OrderingPlaces Placement => _placement ??= service?.GetDefaultPlacement(series) ?? DefaultOrderingPlacement.Build(series);

    #region IMetadata Implementation

    /// <inheritdoc />
    public MetadataGuid ID { get; } = MetadataOrderingService.DefaultOrderingID(series.ID);

    #endregion

    #region IOrdering Implementation

    /// <inheritdoc />
    public MetadataGuid SeriesID => series.ID;

    /// <inheritdoc />
    public string Name => "Seasons";

    /// <inheritdoc />
    public string Overview => "Default ordering for the series.";

    /// <inheritdoc />
    public OrderingType Type => OrderingType.Default;

    /// <inheritdoc />
    public bool IsDefault => true;

    /// <inheritdoc />
    public bool IsPreferred => service?.IsPreferred(series, ID) ?? true;

    /// <inheritdoc />
    public int EpisodeCount => Episodes.Count;

    /// <inheritdoc />
    public int HiddenEpisodeCount => service?.CountHidden(Episodes) ?? 0;

    /// <inheritdoc />
    public int SeasonCount => Seasons.Count;

    /// <inheritdoc />
    public IReadOnlyList<INetwork> Networks => series.Networks;

    /// <inheritdoc />
    public ISeries Series => series;

    /// <inheritdoc />
    public IReadOnlyList<ISeason> Seasons => series.Seasons;

    /// <inheritdoc />
    /// <remarks>
    ///   The episodes of each season in the order of the seasons, each by
    ///   number and placed specials where they air, then those in no season.
    /// </remarks>
    public IReadOnlyList<IEpisode> Episodes => _episodes ??= InViewingOrder(series, Placement);

    #endregion

    #region Dates

    /// <inheritdoc />
    public DateTime CreatedAt => series.CreatedAt;

    /// <inheritdoc />
    public DateTime LastUpdatedAt => series.LastUpdatedAt;

    #endregion

    #region IPlacedOrdering Implementation

    OrderingPlaces IPlacedOrdering.Placement => Placement;

    #endregion

    #region Helpers

    /// <summary>
    ///   A series' episodes in viewing order.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <param name="placement">The places of its default ordering.</param>
    /// <returns>The episodes.</returns>
    private static IReadOnlyList<IEpisode> InViewingOrder(ISeries series, OrderingPlaces placement)
    {
        var episodes = series.Episodes.DistinctBy(episode => episode.ID).ToDictionary(episode => episode.ID);
        return [.. placement.ViewingOrder.Select(episodes.GetValueOrDefault).OfType<IEpisode>()];
    }

    #endregion
}

/// <summary>
///   The default ordering of a series whose seasons and episodes are typed,
///   read with them typed.
/// </summary>
/// <typeparam name="TSeries">The series' type.</typeparam>
/// <typeparam name="TEpisode">The episodes' type.</typeparam>
/// <param name="series">The series, which must be an <see cref="ISeries{TSeries,TEpisode}"/>.</param>
/// <param name="service">The ordering service, which knows the choice and the hidden episodes, if there is one.</param>
public sealed class DefaultOrdering<TSeries, TEpisode>(TSeries series, MetadataOrderingService? service)
    : DefaultOrdering(series, service), IOrdering<TSeries, TEpisode>
    where TSeries : class, ISeries
    where TEpisode : class, IEpisode
{
    private readonly TSeries _series = series;

    private IReadOnlyList<TEpisode>? _typedEpisodes;

    #region IOrdering<TSeries, TEpisode> Implementation

    /// <inheritdoc />
    public new TSeries Series => _series;

    /// <inheritdoc />
    public new IReadOnlyList<ISeason<TSeries, TEpisode>> Seasons => ((ISeries<TSeries, TEpisode>)_series).Seasons;

    /// <inheritdoc />
    public new IReadOnlyList<TEpisode> Episodes => _typedEpisodes ??= [.. base.Episodes.Cast<TEpisode>()];

    #endregion
}
