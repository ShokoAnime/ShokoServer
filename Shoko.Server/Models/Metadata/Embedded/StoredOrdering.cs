using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Services;
using Shoko.Server.Services.Ordering;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   A stored ordering, read back with its groups.
/// </summary>
/// <typeparam name="TSeries">The series' type.</typeparam>
/// <typeparam name="TEpisode">The episodes' type.</typeparam>
/// <param name="row">The ordering's row.</param>
/// <param name="service">The ordering service, which reads the groups and knows the choice and the hidden episodes.</param>
public sealed class StoredOrdering<TSeries, TEpisode>(Metadata_Ordering row, MetadataOrderingService service) : IOrdering<TSeries, TEpisode>, IInlineTextSource, IPlacedOrdering
    where TSeries : class, ISeries
    where TEpisode : class, IEpisode
{
    private IReadOnlyList<StoredOrderingGroup<TSeries, TEpisode>>? _groups;

    private IReadOnlyList<TEpisode>? _episodes;

    private OrderingPlaces? _placement;

    private Dictionary<MetadataGuid, TEpisode>? _episodesByID;

    /// <summary>
    ///   The ordering's row.
    /// </summary>
    internal Metadata_Ordering Row => row;

    /// <summary>
    ///   The ordering's groups, in viewing order.
    /// </summary>
    public IReadOnlyList<StoredOrderingGroup<TSeries, TEpisode>> Groups => _groups ??= service.ReadGroups(this);

    /// <summary>
    ///   The ordering's places, its specials placed.
    /// </summary>
    internal OrderingPlaces Placement => _placement ??= OrderingPlaces.ByPlace(
        [.. Groups.Select(group => new OrderingPlacesGroup(group.ID, group.SeasonNumber, group.IsSpecial, [.. group.Places.Select(place => place.Episode.ID)]))]
    );

    /// <summary>
    ///   Finds one of the ordering's episodes.
    /// </summary>
    /// <param name="episodeID">The episode.</param>
    /// <returns>The episode, or <c>null</c> when no group lists it.</returns>
    internal TEpisode? EpisodeByID(MetadataGuid episodeID)
    {
        _episodesByID ??= Groups.SelectMany(group => group.Places).DistinctBy(place => place.Episode.ID).ToDictionary(place => place.Episode.ID, place => place.Episode);
        return _episodesByID.GetValueOrDefault(episodeID);
    }

    #region IMetadata Implementation

    /// <inheritdoc />
    public MetadataGuid ID => row.ID;

    #endregion

    #region IOrdering<TSeries, TEpisode> Implementation

    /// <inheritdoc />
    public MetadataGuid SeriesID => row.SeriesGuid;

    /// <inheritdoc />
    public string Name => row.Name;

    /// <inheritdoc />
    public string Overview => row.Description ?? string.Empty;

    /// <inheritdoc />
    public OrderingType Type => row.Type;

    /// <inheritdoc />
    public bool IsDefault => false;

    /// <inheritdoc />
    public bool IsPreferred => service.IsChosen(SeriesID, ID);

    /// <inheritdoc />
    public int EpisodeCount => Placement.EpisodeCount;

    /// <inheritdoc />
    public int HiddenEpisodeCount => service.CountHidden(Episodes);

    /// <inheritdoc />
    public int SeasonCount => Groups.Count;

    /// <inheritdoc />
    public IReadOnlyList<INetwork> Networks => service.GetNetworks(ID);

    /// <inheritdoc />
    public TSeries Series => service.GetSeries(SeriesID) as TSeries ??
        throw new NullReferenceException($"Unable to find series {SeriesID} for ordering {ID}");

    /// <inheritdoc />
    public IReadOnlyList<ISeason<TSeries, TEpisode>> Seasons => Groups;

    /// <inheritdoc />
    public IReadOnlyList<TEpisode> Episodes => _episodes ??= [.. Placement.ViewingOrder.Select(EpisodeByID).OfType<TEpisode>()];

    #endregion

    #region IPlacedOrdering Implementation

    OrderingPlaces IPlacedOrdering.Placement => Placement;

    #endregion

    #region IInlineTextSource Implementation

    ITitle? IInlineTextSource.InlineTitle => InlineText.Title(row.Source, row.Name, TitleLanguage.Unknown, "unk");

    IText? IInlineTextSource.InlineOverview => InlineText.Overview(row.Source, row.Description, TitleLanguage.Unknown, "unk");

    #endregion

    #region Dates

    /// <inheritdoc />
    public DateTime CreatedAt => row.CreatedAt.ToUniversalTime();

    /// <inheritdoc />
    public DateTime LastUpdatedAt => row.LastUpdatedAt.ToUniversalTime();

    #endregion
}
