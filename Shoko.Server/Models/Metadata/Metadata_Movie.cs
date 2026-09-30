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
using Shoko.Server.Repositories;

#pragma warning disable CS0618
namespace Shoko.Server.Models.Metadata;

/// <summary>
///   A movie a plugin source keeps in the movie store.
/// </summary>
public class Metadata_Movie : IMovie, IMetadataStoreRow<Metadata_Movie>
{
    #region Database Columns

    /// <summary>
    ///   The row's ID.
    /// </summary>
    public int Metadata_MovieID { get; set; }

    /// <summary>
    ///   The source the movie belongs to.
    /// </summary>
    public MetadataSource Source { get; set; } = null!;

    /// <summary>
    ///   The source's own ID for the movie.
    /// </summary>
    public string ProviderID { get; set; } = string.Empty;

    /// <summary>
    ///   The day the movie was first released, when known.
    /// </summary>
    public DateOnly? ReleasedAt { get; set; }

    /// <summary>
    ///   Whether the movie is for adults only.
    /// </summary>
    public bool IsRestricted { get; set; }

    /// <summary>
    ///   Whether it is a standalone video rather than a movie.
    /// </summary>
    public bool IsVideo { get; set; }

    /// <summary>
    ///   The language the movie was first made in, as a language code, when
    ///   the source says.
    /// </summary>
    public string? OriginalLanguageCode { get; set; }

    /// <summary>
    ///   The source's user rating, on a scale of 1 to 10.
    /// </summary>
    public double Rating { get; set; }

    /// <summary>
    ///   How many votes the rating is made from.
    /// </summary>
    public int RatingVotes { get; set; }

    /// <summary>
    ///   The links to the movie elsewhere that its source gave.
    /// </summary>
    public List<Resource> Resources { get; set; } = [];

    /// <summary>
    ///   The IDs other sources gave the same movie, as its source listed
    ///   them.
    /// </summary>
    public List<MetadataGuid> CrossSourceIDs { get; set; } = [];

    /// <summary>
    ///   When the source last wrote the movie.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Helpers

    /// <summary>
    ///   The movie's identifier.
    /// </summary>
    public MetadataGuid ID => new(Source, MetadataEntityType.Movie, ProviderID);

    /// <summary>
    ///   Whether the stored columns are the same as another row's, leaving
    ///   out the row's ID and when it was written.
    /// </summary>
    /// <param name="other">The other row.</param>
    /// <returns><c>true</c> when nothing but the ID and time differ.</returns>
    internal bool SameAs(Metadata_Movie other)
        => Source == other.Source &&
            ProviderID == other.ProviderID &&
            ReleasedAt == other.ReleasedAt &&
            IsRestricted == other.IsRestricted &&
            IsVideo == other.IsVideo &&
            OriginalLanguageCode == other.OriginalLanguageCode &&
            Rating.Equals(other.Rating) &&
            RatingVotes == other.RatingVotes &&
            MetadataStoredEntry.SameResources(Resources, other.Resources) &&
            CrossSourceIDs.SequenceEqual(other.CrossSourceIDs);

    /// <summary>
    ///   The links naming the movie, at its own level and as a whole anime.
    /// </summary>
    private IReadOnlyList<IMetadataCrossReference> Links => MetadataStoredEntry.LinksTo(ID);

    /// <summary>
    ///   The AniDB episodes the movie's own links stand on.
    /// </summary>
    private IEnumerable<int> LinkedEpisodeIDs => Links.OfType<IMetadataMovieCrossReference>().Select(link => link.AnidbEpisodeID);

    #endregion

    #region Store Row Implementation

    int IMetadataStoreRow<Metadata_Movie>.RowID
    {
        get => Metadata_MovieID;
        set => Metadata_MovieID = value;
    }

    Metadata_Movie IMetadataStoreRow<Metadata_Movie>.Clone()
        => (Metadata_Movie)MemberwiseClone();

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

    IReadOnlyList<(int Year, YearlySeason Season)> IWithYearlySeasons.YearlySeasons => [.. ReleasedAt.GetYearlySeasons(ReleasedAt)];

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

    #region IMovie Implementation

    IReadOnlyList<int> IMovie.ShokoSeriesIDs => [.. ((IMovie)this).ShokoSeries.Select(series => series.LocalID)];

    IReadOnlyList<int> IMovie.ShokoEpisodeIDs => [.. ((IMovie)this).ShokoEpisodes.Select(episode => episode.LocalID)];

    DateTime? IMovie.ReleaseDate => ReleasedAt?.ToDateTime(TimeOnly.MinValue);

    bool IMovie.Restricted => IsRestricted;

    bool IMovie.Video => IsVideo;

    IReadOnlyList<IShokoEpisode> IMovie.ShokoEpisodes => MetadataStoredEntry.ShokoEpisodes(LinkedEpisodeIDs);

    IReadOnlyList<IShokoSeries> IMovie.ShokoSeries => MetadataStoredEntry.ShokoSeries(Links);

    IReadOnlyList<IRelatedMetadata<IMovie, ISeries>> IMovie.RelatedSeries => MetadataStoredEntry.Relations<IMovie, ISeries>(ID);

    IReadOnlyList<IRelatedMetadata<IMovie, IMovie>> IMovie.RelatedMovies => MetadataStoredEntry.Relations<IMovie, IMovie>(ID);

    IReadOnlyList<ISuggestedMetadata<IMovie, IMovie>> IMovie.Suggestions => MetadataStoredEntry.Suggestions<IMovie>(ID);

    IReadOnlyList<ISuggestedMetadata<IMovie, IMovie>> IMovie.SuggestedBy => MetadataStoredEntry.SuggestedBy<IMovie>(ID);

    IReadOnlyList<IVideoCrossReference> IMovie.VideoCrossReferences => MetadataStoredEntry.VideoLinksForEpisodes(LinkedEpisodeIDs);

    IReadOnlyList<IMetadataMovieCrossReference> IMovie.MetadataMovieCrossReferences => [.. Links.OfType<IMetadataMovieCrossReference>()];

    IReadOnlyList<IVideo> IMovie.Videos => MetadataStoredEntry.Videos(((IMovie)this).VideoCrossReferences);

    #endregion
}
