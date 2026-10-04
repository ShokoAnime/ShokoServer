using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Services;
using Shoko.Server.Utilities;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   One group of a stored ordering, read back as a season of the ordering.
/// </summary>
/// <remarks>
///   Read untyped, its series and episodes are presented in the ordering,
///   numbered by their places here. Read typed, they are the source's own.
/// </remarks>
/// <typeparam name="TSeries">The series' type.</typeparam>
/// <typeparam name="TEpisode">The episodes' type.</typeparam>
/// <param name="ordering">The ordering the group is in.</param>
/// <param name="row">The group's row.</param>
/// <param name="seasonNumber">The group's season number: <c>0</c> for the special group, else its source's number or its place among the others.</param>
/// <param name="places">The episodes' places in the group, in order, with each episode.</param>
public sealed class StoredOrderingGroup<TSeries, TEpisode>(
    StoredOrdering<TSeries, TEpisode> ordering,
    Metadata_Ordering_Group row,
    int seasonNumber,
    IReadOnlyList<(Metadata_Ordering_Entry Entry, TEpisode Episode)> places
) : ISeason<TSeries, TEpisode>
    where TSeries : class, ISeries
    where TEpisode : class, IEpisode
{
    /// <summary>
    ///   The ordering the group is in.
    /// </summary>
    public StoredOrdering<TSeries, TEpisode> Ordering => ordering;

    /// <summary>
    ///   The episodes' places in the group, in order, with each episode.
    /// </summary>
    internal IReadOnlyList<(Metadata_Ordering_Entry Entry, TEpisode Episode)> Places => places;

    #region IMetadata Implementation

    /// <inheritdoc />
    public MetadataGuid ID => row.ID;

    #endregion

    #region ISeason<TSeries, TEpisode> Implementation

    /// <inheritdoc />
    public MetadataGuid SeriesID => ordering.SeriesID;

    /// <inheritdoc />
    public int SeasonNumber => seasonNumber;

    /// <inheritdoc />
    public bool IsSpecial => row.IsSpecial;

    /// <inheritdoc />
    public MetadataGuid OrderingID => ordering.ID;

    /// <inheritdoc />
    public TSeries Series => ordering.Series;

    /// <inheritdoc />
    /// <remarks>
    ///   Only the episodes at home in the group: a placed special is left out
    ///   of a regular group, where it only airs.
    /// </remarks>
    public IReadOnlyList<TEpisode> Episodes => [.. ordering.Placement.HomeEpisodes(ID).Select(ordering.EpisodeByID).OfType<TEpisode>()];

    /// <inheritdoc />
    IOrdering<TSeries, TEpisode> ISeason<TSeries, TEpisode>.Ordering => ordering;

    ISeries ISeason.Series => ((IOrdering)ordering).Series;

    IReadOnlyList<IEpisode> ISeason.Episodes
        => [.. ordering.Placement.HomeEpisodes(ID).Select(episodeID => ordering.PlacesOf(episodeID).FirstOrDefault(place => place.SeasonID == ID)).OfType<IEpisode>()];

    /// <inheritdoc />
    public IReadOnlyList<IMetadataSeasonCrossReference> MetadataSeasonCrossReferences => MetadataService.GetSeasonCrossReferences(this, MetadataEpisodeCrossReferences);

    /// <inheritdoc />
    public IReadOnlyList<IMetadataEpisodeCrossReference> MetadataEpisodeCrossReferences
        => [.. places.SelectMany(place => place.Episode.MetadataEpisodeCrossReferences)];

    /// <inheritdoc />
    public IReadOnlyList<IMetadataMovieCrossReference> MetadataMovieCrossReferences
        => [.. places.SelectMany(place => place.Episode.MetadataMovieCrossReferences)];

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

    #region IWithCastAndCrew Implementation

    /// <inheritdoc />
    public IReadOnlyList<ICast> Cast => [];

    /// <inheritdoc />
    public IReadOnlyList<ICrew> Crew => [];

    #endregion

    #region IWithCreationDate Implementation

    /// <inheritdoc />
    public DateTime CreatedAt => ordering.CreatedAt;

    #endregion

    #region IWithUpdateDate Implementation

    /// <inheritdoc />
    public DateTime LastUpdatedAt => ordering.LastUpdatedAt;

    #endregion

    #region IWithYearlySeasons Implementation

    /// <inheritdoc />
    public IReadOnlyList<(int Year, YearlySeason Season)> YearlySeasons
    {
        get
        {
            var aired = places.Select(place => place.Episode.AirDate).OfType<DateOnly>();
            return SeasonCalendar.GetSeasons(SeasonCalendar.GetSpan(((ISeason)this).Series.Type, aired, null, null));
        }
    }

    #endregion
}
