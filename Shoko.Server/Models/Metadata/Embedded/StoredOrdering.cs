using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Services;
using Shoko.Server.Services.Ordering;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   A stored ordering, read back with its groups.
/// </summary>
/// <remarks>
///   Read untyped, its series and episodes are presented in the ordering:
///   the series' seasons are the ordering's groups, and each episode is
///   numbered by its first place here. Read typed, they are the source's own.
/// </remarks>
/// <typeparam name="TSeries">The series' type.</typeparam>
/// <typeparam name="TEpisode">The episodes' type.</typeparam>
/// <param name="row">The ordering's row.</param>
/// <param name="service">The ordering service, which reads the groups and knows the choice and the hidden episodes.</param>
public sealed class StoredOrdering<TSeries, TEpisode>(Metadata_Ordering row, MetadataOrderingService service) : IOrdering<TSeries, TEpisode>, IPlacedOrdering
    where TSeries : class, ISeries
    where TEpisode : class, IEpisode
{
    private IReadOnlyList<StoredOrderingGroup<TSeries, TEpisode>>? _groups;

    private IReadOnlyList<TEpisode>? _episodes;

    private OrderingPlaces? _placement;

    private Dictionary<MetadataGuid, TEpisode>? _episodesByID;

    private SeriesInOrdering? _presentedSeries;

    private IReadOnlyList<IEpisode>? _presentedEpisodes;

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

    /// <summary>
    ///   Every place an episode has in the ordering: its special group place
    ///   for a placed special, else one for each group that lists it.
    /// </summary>
    /// <param name="episodeID">The episode.</param>
    /// <returns>The places, in the order of the groups.</returns>
    internal IEnumerable<StoredEpisodeOrdering<TSeries, TEpisode>> PlacesOf(MetadataGuid episodeID)
    {
        if (EpisodeByID(episodeID) is not { } episode)
            yield break;

        var airing = Placement.AiringOf(episodeID);
        foreach (var place in Placement.PlacesOf(episodeID))
        {
            if (Groups.FirstOrDefault(group => group.ID == place.GroupID) is { } group)
                yield return new(group, episode, place.EpisodeNumber, place.IsSpecial ? airing : null);
        }
    }

    #region IMetadata Implementation

    /// <inheritdoc />
    public MetadataGuid ID => row.ID;

    #endregion

    #region IOrdering<TSeries, TEpisode> Implementation

    /// <inheritdoc />
    public MetadataGuid SeriesID => row.SeriesGuid;

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

    #region IOrdering Implementation

    ISeries IOrdering.Series => _presentedSeries ??= new(Series, this);

    IReadOnlyList<ISeason> IOrdering.Seasons => Seasons;

    IReadOnlyList<IEpisode> IOrdering.Episodes
        => _presentedEpisodes ??= [.. Placement.ViewingOrder.Select(episodeID => PlacesOf(episodeID).FirstOrDefault()).OfType<IEpisode>()];

    #endregion

    #region IPlacedOrdering Implementation

    OrderingPlaces IPlacedOrdering.Placement => Placement;

    #endregion

    #region IWithTitles Implementation

    /// <inheritdoc />
    public string Title => PreferredTitle?.Value ?? DefaultTitle.Value;

    /// <inheritdoc />
    public ITitle DefaultTitle => MetadataStoredEntry.DefaultTitle(this);

    /// <inheritdoc />
    public ITitle? PreferredTitle => MetadataStoredEntry.PreferredTitle(this);

    /// <inheritdoc />
    public IReadOnlyList<ITitle> Titles => MetadataStoredEntry.Titles(this);

    #endregion

    #region IWithOverviews Implementation

    /// <inheritdoc />
    public IText? DefaultOverview => MetadataStoredEntry.DefaultOverview(this);

    /// <inheritdoc />
    public IText? PreferredOverview => MetadataStoredEntry.PreferredOverview(this);

    /// <inheritdoc />
    public IReadOnlyList<IText> Overviews => MetadataStoredEntry.Overviews(this);

    #endregion

    #region Dates

    /// <inheritdoc />
    public DateTime CreatedAt => row.CreatedAt.ToUniversalTime();

    /// <inheritdoc />
    public DateTime LastUpdatedAt => row.LastUpdatedAt.ToUniversalTime();

    #endregion
}
