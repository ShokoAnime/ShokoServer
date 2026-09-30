using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Services;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   The default ordering of a series of any source, made from its own
///   seasons and never stored.
/// </summary>
/// <param name="series">The series.</param>
/// <param name="service">The ordering service, which knows the choice and the hidden episodes, if there is one.</param>
public class DefaultOrdering(ISeries series, MetadataOrderingService? service) : IOrdering
{
    private IReadOnlyList<IEpisode>? _episodes;

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
    public ISeries? Series => series;

    /// <inheritdoc />
    public IReadOnlyList<ISeason> Seasons => series.Seasons;

    /// <inheritdoc />
    /// <remarks>
    ///   The episodes of each season in the order of the seasons, then those
    ///   in no season, each by number.
    /// </remarks>
    public IReadOnlyList<IEpisode> Episodes => _episodes ??= InViewingOrder(series);

    #endregion

    #region Dates

    /// <inheritdoc />
    public DateTime CreatedAt => series switch
    {
        IWithCreationDate created => created.CreatedAt,
        IWithUpdateDate updated => updated.LastUpdatedAt,
        _ => DateTime.UnixEpoch,
    };

    /// <inheritdoc />
    public DateTime LastUpdatedAt => series is IWithUpdateDate updated ? updated.LastUpdatedAt : CreatedAt;

    #endregion

    #region Helpers

    /// <summary>
    ///   A series' episodes in the order of its seasons, then by number.
    /// </summary>
    /// <param name="series">The series.</param>
    /// <returns>The episodes.</returns>
    private static IReadOnlyList<IEpisode> InViewingOrder(ISeries series)
    {
        var seasonOrder = series.Seasons
            .Select((season, index) => (season.ID, index))
            .DistinctBy(season => season.ID)
            .ToDictionary(season => season.ID, season => season.index);
        return
        [
            .. series.Episodes
                .OrderBy(episode => episode.SeasonID is { } seasonID && seasonOrder.TryGetValue(seasonID, out var index) ? index : int.MaxValue)
                .ThenBy(episode => episode.EpisodeNumber),
        ];
    }

    #endregion
}
