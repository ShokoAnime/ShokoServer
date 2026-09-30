using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Services;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   One group of a stored ordering, read back as a season of the ordering.
/// </summary>
/// <param name="ordering">The ordering the group is in.</param>
/// <param name="row">The group's row.</param>
/// <param name="seasonNumber">The group's season number: <c>0</c> for the special group, else its place among the others, from 1.</param>
/// <param name="places">The episodes' places in the group, in order, with each episode.</param>
public sealed class StoredOrderingGroup(
    StoredOrdering ordering,
    Metadata_Ordering_Group row,
    int seasonNumber,
    IReadOnlyList<(Metadata_Ordering_Entry Entry, IEpisode Episode)> places
) : ISeason, IInlineTextSource
{
    /// <summary>
    ///   The ordering the group is in.
    /// </summary>
    public StoredOrdering Ordering => ordering;

    /// <summary>
    ///   The episodes' places in the group, in order, with each episode.
    /// </summary>
    internal IReadOnlyList<(Metadata_Ordering_Entry Entry, IEpisode Episode)> Places => places;

    #region IMetadata Implementation

    /// <inheritdoc />
    public MetadataGuid ID => row.ID;

    #endregion

    #region ISeason Implementation

    /// <inheritdoc />
    public MetadataGuid SeriesID => ordering.SeriesID;

    /// <inheritdoc />
    public int SeasonNumber => seasonNumber;

    /// <inheritdoc />
    public bool IsSpecial => row.IsSpecial;

    /// <inheritdoc />
    public MetadataGuid? OrderingID => ordering.ID;

    /// <inheritdoc />
    public ISeries Series => ordering.Series ??
        throw new NullReferenceException($"Unable to find series {ordering.SeriesID} for ordering {ordering.ID}");

    /// <inheritdoc />
    public IReadOnlyList<IEpisode> Episodes => [.. places.Select(place => place.Episode)];

    /// <inheritdoc />
    public IReadOnlyList<IMetadataSeasonCrossReference> MetadataSeasonCrossReferences => MetadataService.GetSeasonCrossReferences(this, MetadataEpisodeCrossReferences);

    /// <inheritdoc />
    public IReadOnlyList<IMetadataEpisodeCrossReference> MetadataEpisodeCrossReferences
        => [.. places.SelectMany(place => place.Episode.MetadataEpisodeCrossReferences)];

    /// <inheritdoc />
    public IReadOnlyList<IMetadataMovieCrossReference> MetadataMovieCrossReferences
        => [.. places.SelectMany(place => place.Episode.MetadataMovieCrossReferences)];

    #endregion

    #region IInlineTextSource Implementation

    ITitle? IInlineTextSource.InlineTitle => InlineText.Title(row.Source, row.Name, TitleLanguage.Unknown, "unk");

    IText? IInlineTextSource.InlineOverview => InlineText.Overview(row.Source, row.Description, TitleLanguage.Unknown, "unk");

    #endregion

    #region IWithTitles Implementation

    /// <inheritdoc />
    public string Title => row.Name;

    /// <inheritdoc />
    public ITitle DefaultTitle => new TitleStub
    {
        Source = row.Source,
        Language = TitleLanguage.Unknown,
        LanguageCode = "unk",
        Value = row.Name,
        Type = TitleType.Main,
    };

    /// <inheritdoc />
    public ITitle? PreferredTitle => DefaultTitle;

    /// <inheritdoc />
    public IReadOnlyList<ITitle> Titles => [DefaultTitle];

    #endregion

    #region IWithOverviews Implementation

    /// <inheritdoc />
    public IText? DefaultOverview => string.IsNullOrEmpty(row.Description)
        ? null
        : new TextStub
        {
            Source = row.Source,
            Language = TitleLanguage.Unknown,
            LanguageCode = "unk",
            Value = row.Description,
        };

    /// <inheritdoc />
    public IText? PreferredOverview => DefaultOverview;

    /// <inheritdoc />
    public IReadOnlyList<IText> Overviews => DefaultOverview is { } overview ? [overview] : [];

    #endregion

    #region IWithCastAndCrew Implementation

    /// <inheritdoc />
    public IReadOnlyList<ICast> Cast => [];

    /// <inheritdoc />
    public IReadOnlyList<ICrew> Crew => [];

    #endregion

    #region IWithYearlySeasons Implementation

    /// <inheritdoc />
    public IReadOnlyList<(int Year, YearlySeason Season)> YearlySeasons
    {
        get
        {
            var aired = places.Select(place => place.Episode.AirDate).OfType<DateOnly>().ToList();
            return aired.Count is 0 ? [] : [.. ((DateOnly?)aired.Min()).GetYearlySeasons(aired.Max())];
        }
    }

    #endregion
}
