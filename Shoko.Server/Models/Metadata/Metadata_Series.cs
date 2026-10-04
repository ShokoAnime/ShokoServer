using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Metadata.Embedded;
using Shoko.Server.Repositories;

#pragma warning disable CS0618
namespace Shoko.Server.Models.Metadata;

/// <summary>
///   A series a plugin source keeps in the series store.
/// </summary>
public class Metadata_Series : ISeries<ISeries, IEpisode>, IMetadataStoreRow<Metadata_Series>, IMetadataDefaultImageSource
{
    #region Database Columns

    /// <summary>
    ///   The row's ID.
    /// </summary>
    public int Metadata_SeriesID { get; set; }

    /// <summary>
    ///   The source the series belongs to.
    /// </summary>
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    ///   The source's own ID for the series.
    /// </summary>
    public string ProviderID { get; set; } = string.Empty;

    /// <summary>
    ///   What kind of series it is.
    /// </summary>
    public AnimeType Type { get; set; } = AnimeType.Unknown;

    /// <summary>
    ///   When the series first aired, if fully or partially known.
    /// </summary>
    public PartialDateOnly? AirDate { get; set; }

    /// <summary>
    ///   When the series ended, if fully or partially known.
    /// </summary>
    public PartialDateOnly? EndDate { get; set; }

    /// <summary>
    ///   The source's user rating, on a scale of 1 to 10.
    /// </summary>
    public double Rating { get; set; }

    /// <summary>
    ///   How many votes the rating is made from.
    /// </summary>
    public int RatingVotes { get; set; }

    /// <summary>
    ///   Whether the series is for adults only.
    /// </summary>
    public bool IsRestricted { get; set; }

    /// <summary>
    ///   Where the series is in its release.
    /// </summary>
    public ReleaseStatus ReleaseStatus { get; set; } = ReleaseStatus.Unknown;

    /// <summary>
    ///   What the series was adapted from.
    /// </summary>
    public SourceMaterial SourceMaterial { get; set; } = SourceMaterial.Unknown;

    /// <summary>
    ///   The language the series was first made in, when the source says.
    /// </summary>
    public string? OriginalLanguageCode { get; set; }

    /// <summary>
    ///   How popular the series is, on the source's own scale.
    /// </summary>
    public double? Popularity { get; set; }

    /// <summary>
    ///   How many of the source's users marked the series a favorite.
    /// </summary>
    public int? FavoriteCount { get; set; }

    /// <summary>
    ///   The links to the series elsewhere that its source gave.
    /// </summary>
    public List<Resource> Resources { get; set; } = [];

    /// <summary>
    ///   The IDs other sources gave the same series, as its source listed
    ///   them.
    /// </summary>
    public List<MetadataGuid> CrossSourceIDs { get; set; } = [];

    /// <summary>
    ///   What the source said of the series that needs no column of its
    ///   own, or <c>null</c> when it said none of it.
    /// </summary>
    public Metadata_SeriesExtra? ExtraData { get; set; }

    /// <summary>
    ///   When the store first wrote the series. Set once and never changed.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    ///   When the source last wrote the series.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>
    ///   When the core last refreshed the series in full without failing, in
    ///   local time, or <c>null</c> when it never did. Kept by the refresh job
    ///   alone; a save of the series keeps it.
    /// </summary>
    public DateTime? LastRefreshedAt { get; set; }

    /// <summary>
    ///   The ordering chosen for the series, of any source, or <c>null</c>
    ///   for its default one. A save of the series keeps it; set through
    ///   <see cref="IMetadataOrderingService.SetPreferredOrdering"/>.
    /// </summary>
    public MetadataGuid? PreferredOrderingID { get; set; }

    #endregion

    #region Helpers

    /// <summary>
    ///   The series' identifier.
    /// </summary>
    public MetadataGuid ID => new(Source, MetadataEntityType.Series, ProviderID);

    /// <summary>
    ///   Whether the stored columns are the same as another row's, leaving
    ///   out the row's ID and when it was written.
    /// </summary>
    /// <param name="other">The other row.</param>
    /// <returns><c>true</c> when nothing but the ID and time differ.</returns>
    internal bool SameAs(Metadata_Series other)
        => Source == other.Source &&
            ProviderID == other.ProviderID &&
            Type == other.Type &&
            AirDate == other.AirDate &&
            EndDate == other.EndDate &&
            Rating.Equals(other.Rating) &&
            RatingVotes == other.RatingVotes &&
            IsRestricted == other.IsRestricted &&
            ReleaseStatus == other.ReleaseStatus &&
            SourceMaterial == other.SourceMaterial &&
            OriginalLanguageCode == other.OriginalLanguageCode &&
            Nullable.Equals(Popularity, other.Popularity) &&
            FavoriteCount == other.FavoriteCount &&
            MetadataStoredEntry.SameResources(Resources, other.Resources) &&
            CrossSourceIDs.SequenceEqual(other.CrossSourceIDs) &&
            Equals(ExtraData, other.ExtraData);

    /// <summary>
    ///   The series' seasons, by number.
    /// </summary>
    public IReadOnlyList<Metadata_Season> StoredSeasons
        => [.. RepoFactory.Metadata_Season.GetBySeriesID(Source, ProviderID).OrderBy(season => season.SeasonNumber).ThenBy(season => season.ProviderID, StringComparer.Ordinal)];

    /// <summary>
    ///   The series' episodes, by season, type and number.
    /// </summary>
    public IReadOnlyList<Metadata_Episode> StoredEpisodes
        => [.. Metadata_Episode.InOrder(RepoFactory.Metadata_Episode.GetBySeriesID(Source, ProviderID))];

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Series>.RowID
    {
        get => Metadata_SeriesID;
        set => Metadata_SeriesID = value;
    }

    Metadata_Series IMetadataStoreRow<Metadata_Series>.Clone()
        => (Metadata_Series)MemberwiseClone();

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.Title => ((IWithTitles)this).PreferredTitle?.Value ?? ((IWithTitles)this).DefaultTitle.Value;

    ITitle IWithTitles.DefaultTitle => MetadataStoredEntry.DefaultTitle(this);

    ITitle? IWithTitles.PreferredTitle => MetadataStoredEntry.PreferredTitle(this);

    IReadOnlyList<ITitle> IWithTitles.Titles => MetadataStoredEntry.Titles(this);

    #endregion

    #region IWithOverviews Implementation

    IText? IWithOverviews.DefaultOverview => MetadataStoredEntry.DefaultOverview(this);

    IText? IWithOverviews.PreferredOverview => MetadataStoredEntry.PreferredOverview(this);

    IReadOnlyList<IText> IWithOverviews.Overviews => MetadataStoredEntry.Overviews(this);

    #endregion

    #region IMetadataDefaultImageSource Implementation

    string? IMetadataDefaultImageSource.GetDefaultResourceID(ImageEntityType imageType)
        => ExtraData?.GetDefaultResourceID(imageType);

    #endregion

    #region IWithImages Implementation

    IImageCrossReference? IWithImages.DefaultPrimaryImageCrossReference => MetadataStoredEntry.DefaultImage(this, ImageEntityType.Primary);

    IImageCrossReference? IWithImages.DefaultBackdropImageCrossReference => MetadataStoredEntry.DefaultImage(this, ImageEntityType.Backdrop);

    IImageCrossReference? IWithImages.DefaultLogoImageCrossReference => MetadataStoredEntry.DefaultImage(this, ImageEntityType.Logo);

    IImageCrossReference? IWithImages.DefaultBannerImageCrossReference => MetadataStoredEntry.DefaultImage(this, ImageEntityType.Banner);

    IImageCrossReference? IWithImages.DefaultDiscImageCrossReference => MetadataStoredEntry.DefaultImage(this, ImageEntityType.Disc);

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => MetadataStoredEntry.Cast(ID);

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => MetadataStoredEntry.Crew(ID);

    #endregion

    #region IWithStudios Implementation

    IReadOnlyList<IStudio> IWithStudios.Studios => MetadataStoredEntry.Studios(ID);

    #endregion

    #region IWithContentRatings Implementation

    IReadOnlyList<IContentRating> IWithContentRatings.ContentRatings => RepoFactory.Metadata_ContentRating.GetByEntry(ID);

    #endregion

    #region IWithYearlySeasons Implementation

    IReadOnlyList<(int Year, YearlySeason Season)> IWithYearlySeasons.YearlySeasons => [.. AirDate.GetYearlySeasons(EndDate)];

    #endregion

    #region IWithResources Implementation

    IReadOnlyList<Resource> IWithResources.Resources
        => [.. Resources, .. ISystemService.StaticServices.GetRequiredService<IMetadataService>().GatherResourcesForEntity(this)];

    #endregion

    #region IWithCrossSources Implementation

    IReadOnlyList<MetadataGuid> IWithCrossSources.CrossSourceIDs => CrossSourceIDs;

    #endregion

    #region IWithTags Implementation

    IReadOnlyList<ITag> IWithTags.Tags => MetadataStoredEntry.Tags(ID);

    #endregion

    #region ISeries Implementation

    DateTime? ISeries.LastRefreshedAt => LastRefreshedAt?.ToUniversalTime();

    IReadOnlyList<INetwork> ISeries.Networks => MetadataStoredEntry.Networks(ID);

    IReadOnlyList<IOrdering<ISeries, IEpisode>> ISeries<ISeries, IEpisode>.Orderings => OrderingLookup.For<ISeries, IEpisode>(this);

    IOrdering<ISeries, IEpisode> ISeries<ISeries, IEpisode>.PreferredOrdering => OrderingLookup.PreferredFor<ISeries, IEpisode>(this);

    IOrdering<ISeries, IEpisode> ISeries<ISeries, IEpisode>.CurrentOrdering => OrderingLookup.DefaultFor<ISeries, IEpisode>(this);

    IReadOnlyList<ISeason> ISeries.Seasons => ((ISeries<ISeries, IEpisode>)this).Seasons;

    IReadOnlyList<IEpisode> ISeries.Episodes => ((ISeries<ISeries, IEpisode>)this).Episodes;

    IReadOnlyList<IOrdering> ISeries.Orderings => ((ISeries<ISeries, IEpisode>)this).Orderings;

    IOrdering ISeries.PreferredOrdering => ((ISeries<ISeries, IEpisode>)this).PreferredOrdering;

    IOrdering ISeries.CurrentOrdering => ((ISeries<ISeries, IEpisode>)this).CurrentOrdering;

    IReadOnlyList<ISuggestedMetadata<ISeries, ISeries>> ISeries.Suggestions => ((ISeries<ISeries, IEpisode>)this).Suggestions;

    IReadOnlyList<ISuggestedMetadata<ISeries, ISeries>> ISeries.SuggestedBy => ((ISeries<ISeries, IEpisode>)this).SuggestedBy;

    IReadOnlyList<int> ISeries.ShokoSeriesIDs => [.. ((ISeries)this).ShokoSeries.Select(series => series.LocalID)];

    bool ISeries.Restricted => IsRestricted;

    IReadOnlyList<IShokoSeries> ISeries.ShokoSeries => MetadataStoredEntry.ShokoSeries(MetadataStoredEntry.SeriesLinksTo(ID));

    IReadOnlyList<IRelatedMetadata<ISeries, ISeries>> ISeries.RelatedSeries => MetadataStoredEntry.Relations<ISeries, ISeries>(ID);

    IReadOnlyList<IRelatedMetadata<ISeries, IMovie>> ISeries.RelatedMovies => MetadataStoredEntry.Relations<ISeries, IMovie>(ID);

    IReadOnlyList<string> ISeries.ProductionCountries => ExtraData?.ProductionCountries ?? [];

    IReadOnlyList<ISuggestedMetadata<ISeries, ISeries>> ISeries<ISeries, IEpisode>.Suggestions => MetadataStoredEntry.Suggestions<ISeries>(ID);

    IReadOnlyList<ISuggestedMetadata<ISeries, ISeries>> ISeries<ISeries, IEpisode>.SuggestedBy => MetadataStoredEntry.SuggestedBy<ISeries>(ID);

    IReadOnlyList<IVideoCrossReference> ISeries.VideoCrossReferences
        => MetadataStoredEntry.VideoLinksForAnime(MetadataStoredEntry.SeriesLinksTo(ID).Select(link => link.AnidbAnimeID));

    IReadOnlyList<IMetadataSeriesCrossReference> ISeries.MetadataSeriesCrossReferences => MetadataStoredEntry.SeriesLinksTo(ID);

    IReadOnlyList<IMetadataEpisodeCrossReference> ISeries.MetadataEpisodeCrossReferences
        => MetadataStoredEntry.EpisodeLinksTo(StoredEpisodes.Select(episode => episode.ID));

    IReadOnlyList<IMetadataSeasonCrossReference> ISeries.MetadataSeasonCrossReferences
        => MetadataStoredEntry.SeasonLinks(((ISeries)this).MetadataEpisodeCrossReferences);

    // A film sits in no series, so nothing links one to a provider's series.
    IReadOnlyList<IMetadataMovieCrossReference> ISeries.MetadataMovieCrossReferences => [];

    IReadOnlyList<ISeason<ISeries, IEpisode>> ISeries<ISeries, IEpisode>.Seasons => StoredSeasons;

    IReadOnlyList<IEpisode> ISeries<ISeries, IEpisode>.Episodes => StoredEpisodes;

    IReadOnlyList<IVideo> ISeries.Videos => MetadataStoredEntry.Videos(((ISeries)this).VideoCrossReferences);

    EpisodeCounts ISeries.EpisodeCounts
    {
        get
        {
            var episodes = StoredEpisodes;
            return new()
            {
                Episodes = episodes.Count(episode => episode.Type is EpisodeType.Episode),
                Specials = episodes.Count(episode => episode.Type is EpisodeType.Special),
                Credits = episodes.Count(episode => episode.Type is EpisodeType.Credits),
                Trailers = episodes.Count(episode => episode.Type is EpisodeType.Trailer),
                Parodies = episodes.Count(episode => episode.Type is EpisodeType.Parody),
                Others = episodes.Count(episode => episode.Type is EpisodeType.Other),
            };
        }
    }

    #endregion
}
