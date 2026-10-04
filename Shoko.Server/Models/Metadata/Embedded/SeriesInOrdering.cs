using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   A series as one of its stored orderings presents it: its seasons are
///   the ordering's groups and its episodes the ordering's, numbered there,
///   with everything else from the series.
/// </summary>
/// <param name="series">The series.</param>
/// <param name="ordering">The ordering the series is presented in.</param>
internal sealed class SeriesInOrdering(ISeries series, IOrdering ordering) : ISeries
{
    /// <summary>
    ///   The series itself.
    /// </summary>
    public ISeries LinkedSeries => series;

    #region In the Ordering

    /// <inheritdoc />
    public IReadOnlyList<ISeason> Seasons => ordering.Seasons;

    /// <inheritdoc />
    public IReadOnlyList<IEpisode> Episodes => ordering.Episodes;

    /// <inheritdoc />
    public IOrdering CurrentOrdering => ordering;

    #endregion

    #region ISeries Implementation

    /// <inheritdoc />
    public MetadataGuid ID => series.ID;

    /// <inheritdoc />
    public DateTime? LastRefreshedAt => series.LastRefreshedAt;

    /// <inheritdoc />
    public IReadOnlyList<int> ShokoSeriesIDs => series.ShokoSeriesIDs;

    /// <inheritdoc />
    public AnimeType Type => series.Type;

    /// <inheritdoc />
    public PartialDateOnly? AirDate => series.AirDate;

    /// <inheritdoc />
    public PartialDateOnly? EndDate => series.EndDate;

    /// <inheritdoc />
    public double Rating => series.Rating;

    /// <inheritdoc />
    public int RatingVotes => series.RatingVotes;

    /// <inheritdoc />
    public bool Restricted => series.Restricted;

    /// <inheritdoc />
    public ReleaseStatus ReleaseStatus => series.ReleaseStatus;

    /// <inheritdoc />
    public SourceMaterial SourceMaterial => series.SourceMaterial;

    /// <inheritdoc />
    public string? OriginalLanguageCode => series.OriginalLanguageCode;

    /// <inheritdoc />
    public double? Popularity => series.Popularity;

    /// <inheritdoc />
    public int? FavoriteCount => series.FavoriteCount;

    /// <inheritdoc />
    public IReadOnlyList<IShokoSeries> ShokoSeries => series.ShokoSeries;

    /// <inheritdoc />
    public IReadOnlyList<INetwork> Networks => series.Networks;

    /// <inheritdoc />
    public IReadOnlyList<string> ProductionCountries => series.ProductionCountries;

    /// <inheritdoc />
    public IReadOnlyList<IRelatedMetadata<ISeries, ISeries>> RelatedSeries => series.RelatedSeries;

    /// <inheritdoc />
    public IReadOnlyList<IRelatedMetadata<ISeries, IMovie>> RelatedMovies => series.RelatedMovies;

    /// <inheritdoc />
    public IReadOnlyList<ISuggestedMetadata<ISeries, ISeries>> Suggestions => series.Suggestions;

    /// <inheritdoc />
    public IReadOnlyList<ISuggestedMetadata<ISeries, ISeries>> SuggestedBy => series.SuggestedBy;

    /// <inheritdoc />
    public IReadOnlyList<IVideoCrossReference> VideoCrossReferences => series.VideoCrossReferences;

    /// <inheritdoc />
    public IReadOnlyList<IMetadataSeriesCrossReference> MetadataSeriesCrossReferences => series.MetadataSeriesCrossReferences;

    /// <inheritdoc />
    public IReadOnlyList<IMetadataEpisodeCrossReference> MetadataEpisodeCrossReferences => series.MetadataEpisodeCrossReferences;

    /// <inheritdoc />
    public IReadOnlyList<IMetadataSeasonCrossReference> MetadataSeasonCrossReferences => series.MetadataSeasonCrossReferences;

    /// <inheritdoc />
    public IReadOnlyList<IMetadataMovieCrossReference> MetadataMovieCrossReferences => series.MetadataMovieCrossReferences;

    /// <inheritdoc />
    public IReadOnlyList<IOrdering> Orderings => series.Orderings;

    /// <inheritdoc />
    public IOrdering PreferredOrdering => series.PreferredOrdering;

    /// <inheritdoc />
    public IReadOnlyList<IVideo> Videos => series.Videos;

    /// <inheritdoc />
    public EpisodeCounts EpisodeCounts => series.EpisodeCounts;

    #endregion

    #region IWithTitles Implementation

    /// <inheritdoc />
    public string Title => series.Title;

    /// <inheritdoc />
    public ITitle DefaultTitle => series.DefaultTitle;

    /// <inheritdoc />
    public ITitle? PreferredTitle => series.PreferredTitle;

    /// <inheritdoc />
    public IReadOnlyList<ITitle> Titles => series.Titles;

    #endregion

    #region IWithOverviews Implementation

    /// <inheritdoc />
    public IText? DefaultOverview => series.DefaultOverview;

    /// <inheritdoc />
    public IText? PreferredOverview => series.PreferredOverview;

    /// <inheritdoc />
    public IReadOnlyList<IText> Overviews => series.Overviews;

    #endregion

    #region Other Containers

    /// <inheritdoc />
    public IImageCrossReference? DefaultPrimaryImageCrossReference => series.DefaultPrimaryImageCrossReference;

    /// <inheritdoc />
    public IImageCrossReference? DefaultBackdropImageCrossReference => series.DefaultBackdropImageCrossReference;

    /// <inheritdoc />
    public IImageCrossReference? DefaultLogoImageCrossReference => series.DefaultLogoImageCrossReference;

    /// <inheritdoc />
    public IImageCrossReference? DefaultBannerImageCrossReference => series.DefaultBannerImageCrossReference;

    /// <inheritdoc />
    public IImageCrossReference? DefaultDiscImageCrossReference => series.DefaultDiscImageCrossReference;

    /// <inheritdoc />
    public IReadOnlyList<ICast> Cast => series.Cast;

    /// <inheritdoc />
    public IReadOnlyList<ICrew> Crew => series.Crew;

    /// <inheritdoc />
    public IReadOnlyList<IStudio> Studios => series.Studios;

    /// <inheritdoc />
    public IReadOnlyList<IContentRating> ContentRatings => series.ContentRatings;

    /// <inheritdoc />
    public IReadOnlyList<(int Year, YearlySeason Season)> YearlySeasons => series.YearlySeasons;

    /// <inheritdoc />
    public IReadOnlyList<Resource> Resources => series.Resources;

    /// <inheritdoc />
    public IReadOnlyList<MetadataGuid> CrossSourceIDs => series.CrossSourceIDs;

    /// <inheritdoc />
    public IReadOnlyList<ITag> Tags => series.Tags;

    #endregion

    #region Dates

    /// <inheritdoc />
    public DateTime CreatedAt => series.CreatedAt;

    /// <inheritdoc />
    public DateTime LastUpdatedAt => series.LastUpdatedAt;

    #endregion
}
