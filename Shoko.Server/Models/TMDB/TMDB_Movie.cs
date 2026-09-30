using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Video;
using Shoko.Server.Extensions;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.CrossReference.Embedded;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Models.Metadata;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using TMDbLib.Objects.Movies;

#pragma warning disable CS0618
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// The Movie DataBase (TMDB) Movie Database Model.
/// </summary>
public class TMDB_Movie : TMDB_Base<int>, IEntityMetadata, IMovie, ITmdbMovie, IInlineTextSource
{
    #region Properties

    /// <summary>
    /// IEntityMetadata.Id.
    /// </summary>
    public override int Id => TmdbMovieID;

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_MovieID { get; set; }

    /// <summary>
    /// TMDB Movie ID.
    /// </summary>
    public int TmdbMovieID { get; set; }

    /// <summary>
    /// TMDB Collection ID, if the movie is part of a collection.
    /// </summary>
    public int? TmdbCollectionID { get; set; }

    /// <summary>
    /// Linked Imdb movie ID.
    /// </summary>
    /// <remarks>
    /// Will be <code>null</code> if not linked. Will be <code>0</code> if no
    /// Imdb link is found in TMDB. Otherwise, it will be the Imdb movie ID.
    /// </remarks>
    public string? ImdbMovieID { get; set; }

    /// <summary>
    /// The default poster path. Used to determine the default poster for the
    /// movie.
    /// </summary>
    public string PosterPath { get; set; } = string.Empty;

    /// <summary>
    /// The default backdrop path. Used to determine the default backdrop for
    /// the movie.
    /// </summary>
    public string BackdropPath { get; set; } = string.Empty;

    /// <summary>
    /// The english title of the movie, used as a fallback for when no title
    /// is available in the preferred language.
    /// </summary>
    public string EnglishTitle { get; set; } = string.Empty;

    /// <summary>
    /// The english overview, used as a fallback for when no overview is
    /// available in the preferred language.
    /// </summary>
    public string EnglishOverview { get; set; } = string.Empty;

    /// <summary>
    ///   Whether TMDB lists <see cref="EnglishTitle"/> among the movie's
    ///   translations, where it is not stored a second time.
    /// </summary>
    public bool EnglishTitleListed { get; set; }

    /// <summary>
    ///   Whether TMDB lists <see cref="EnglishOverview"/> among the movie's
    ///   translations, where it is not stored a second time.
    /// </summary>
    public bool EnglishOverviewListed { get; set; }

    /// <summary>
    /// Original title in the original language.
    /// </summary>
    public string OriginalTitle { get; set; } = string.Empty;

    /// <summary>
    /// The original language this movie was shot in, just as a title language
    /// enum instead.
    /// </summary>
    public TitleLanguage OriginalLanguage
    {
        get => string.IsNullOrEmpty(OriginalLanguageCode) ? TitleLanguage.None : OriginalLanguageCode.GetTitleLanguage();
    }

    /// <summary>
    /// The original language this movie was shot in.
    /// </summary>
    public string OriginalLanguageCode { get; set; } = string.Empty;

    /// <summary>
    /// Indicates the movie is restricted to an age group above the legal age,
    /// because it's a pornography.
    /// </summary>
    public bool IsRestricted { get; set; }

    /// <summary>
    /// Indicates the entry is not truly a movie, including but not limited to
    /// the types:
    ///
    /// - official compilations,
    /// - best of,
    /// - filmed sport events,
    /// - music concerts,
    /// - plays or stand-up show,
    /// - fitness video,
    /// - health video,
    /// - live movie theater events (art, music),
    /// - and how-to DVDs,
    ///
    /// among others.
    /// </summary>
    public bool IsVideo { get; set; }

    /// <summary>
    /// Genres.
    /// </summary>
    public List<string> Genres { get; set; } = [];

    /// <summary>
    /// Keywords / Tags.
    /// </summary>
    public List<string> Keywords { get; set; } = [];

    /// <summary>
    /// Content ratings for different countries for this movie.
    /// </summary>
    public List<TMDB_ContentRating> ContentRatings { get; set; } = [];

    /// <summary>
    /// Production countries.
    /// </summary>
    public List<TMDB_ProductionCountry> ProductionCountries { get; set; } = [];

    /// <summary>
    /// Movie run-time in minutes.
    /// </summary>
    public int? RuntimeMinutes
    {
        get => Runtime.HasValue ? (int)Math.Floor(Runtime.Value.TotalMinutes) : null;
        set => Runtime = value.HasValue ? TimeSpan.FromMinutes(value.Value) : null;
    }

    /// <summary>
    /// Movie run-time.
    /// </summary>
    public TimeSpan? Runtime { get; set; }

    /// <summary>
    /// Average user rating across all <see cref="UserVotes"/>.
    /// </summary>
    public double UserRating { get; set; }

    /// <summary>
    /// Number of users that cast a vote for a rating of this movie.
    /// </summary>
    /// <value></value>
    public int UserVotes { get; set; }

    /// <summary>
    /// When the movie aired, or when it will air in the future if it's known.
    /// </summary>
    public DateOnly? ReleasedAt { get; set; }

    /// <summary>
    /// When the metadata was first downloaded.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the metadata was last synchronized with the remote.
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor for NHibernate to work correctly while hydrating the rows
    /// from the database.
    /// </summary>
    public TMDB_Movie() { }

    /// <summary>
    /// Constructor to create a new movie in the provider.
    /// </summary>
    /// <param name="movieId">The TMDB Movie id.</param>
    public TMDB_Movie(int movieId)
    {
        TmdbMovieID = movieId;
        CreatedAt = DateTime.Now;
        LastUpdatedAt = CreatedAt;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Populate the fields from the raw data.
    /// </summary>
    /// <param name="movie">The raw TMDB Movie object.</param>
    /// <param name="crLanguages">Content rating languages.</param>
    /// <returns>True if any of the fields have been updated.</returns>
    public bool Populate(Movie movie, HashSet<TitleLanguage>? crLanguages)
    {
        var translation = movie.Translations?.Translations?.FirstOrDefault(translation => translation.Iso_639_1 == "en");
        var lang = movie.ProductionCountries!.FirstOrDefault()?.Iso_3166_1;
        var releaseDate = !string.IsNullOrEmpty(lang) && movie.ReleaseDates!.Results!.FirstOrDefault(obj0 => obj0.Iso_3166_1 == lang) is { ReleaseDates: { } } obj1
            ? obj1.ReleaseDates.FirstOrDefault(obj2 => obj2.Type is ReleaseDateType.TheatricalLimited or ReleaseDateType.Theatrical)?.ReleaseDate ??
                obj1.ReleaseDates.FirstOrDefault(obj2 => obj2.Type is ReleaseDateType.Premiere)?.ReleaseDate ??
                obj1.ReleaseDates.FirstOrDefault()?.ReleaseDate ??
                movie.ReleaseDate
            : movie.ReleaseDate;
        var updatedList = new[]
        {
            UpdateProperty(PosterPath, movie.PosterPath!, v => PosterPath = v),
            UpdateProperty(BackdropPath, movie.BackdropPath!, v => BackdropPath = v),
            UpdateProperty(TmdbCollectionID, movie.BelongsToCollection?.Id, v => TmdbCollectionID = v),
            UpdateProperty(EnglishTitle, !string.IsNullOrEmpty(translation?.Data?.Name) ? translation.Data.Name : movie.Title!, v => EnglishTitle = v),
            UpdateProperty(EnglishOverview, !string.IsNullOrEmpty(translation?.Data?.Overview) ? translation.Data.Overview : movie.Overview!, v => EnglishOverview = v),
            UpdateProperty(OriginalTitle, movie.OriginalTitle!, v => OriginalTitle = v),
            UpdateProperty(OriginalLanguageCode, movie.OriginalLanguage!, v => OriginalLanguageCode = v),
            UpdateProperty(IsRestricted, movie.Adult, v => IsRestricted = v),
            UpdateProperty(IsVideo, movie.Video, v => IsVideo = v),
            UpdateProperty(Genres, movie.GetGenres(), v => Genres = v, (a, b) => string.Equals(string.Join("|", a), string.Join("|", b))),
            UpdateProperty(Keywords, movie.Keywords!.Keywords!.Select(k => k.Name!).ToList(), v => Keywords = v, (a, b) => string.Equals(string.Join("|", a), string.Join("|", b))),
            UpdateProperty(
                ContentRatings,
                movie.ReleaseDates!.Results!
                    .Where(releaseDate => releaseDate.ReleaseDates?.Any(r => !string.IsNullOrEmpty(r.Certification)) ?? false)
                    .Select(releaseDate => new TMDB_ContentRating(releaseDate.Iso_3166_1!, releaseDate.ReleaseDates!.Last(r => !string.IsNullOrEmpty(r.Certification)).Certification!))
                    .WhereInLanguages(crLanguages?.Append(TitleLanguage.EnglishAmerican).ToHashSet())
                    .OrderBy(c => c.CountryCode)
                    .ToList(),
                v => ContentRatings = v,
                (a, b) => string.Equals(string.Join(",", a.Select(a1 => a1.ToString())), string.Join(",", b.Select(b1 => b1.ToString())))
            ),
            UpdateProperty(
                ProductionCountries,
                movie.ProductionCountries!
                    .Select(country => new TMDB_ProductionCountry(country.Iso_3166_1!, country.Name!))
                    .OrderBy(c => c.CountryCode)
                    .ToList(),
                v => ProductionCountries = v,
                (a, b) => string.Equals(string.Join(",", a.Select(a1 => a1.ToString())), string.Join(",", b.Select(b1 => b1.ToString())))
            ),
            UpdateProperty(Runtime, movie.Runtime.HasValue ? TimeSpan.FromMinutes(movie.Runtime.Value) : null, v => Runtime = v),
            UpdateProperty(UserRating, movie.VoteAverage, v => UserRating = v),
            UpdateProperty(UserVotes, movie.VoteCount, v => UserVotes = v),
            UpdateProperty(ReleasedAt, releaseDate?.ToDateOnly(), v => ReleasedAt = v),
        };

        return updatedList.Any(updated => updated);
    }

    /// <summary>
    ///   The title the user's picks and language settings choose for the
    ///   movie.
    /// </summary>
    /// <returns>The title, or the English one when none is in a preferred language.</returns>
    public ITitle GetPreferredTitle()
        => TextAccess.Manager.PreferredTitleFor(this) ?? TmdbInlineText.TitleOrEmpty(EnglishTitle);

    /// <summary>
    ///   The movie's titles: the ones TMDB lists, then any other source's.
    /// </summary>
    /// <returns>The titles.</returns>
    public IReadOnlyList<ITitle> GetAllTitles()
        => TextAccess.Manager.ListTitles(this);

    /// <summary>
    ///   The overview the user's picks and language settings choose for the
    ///   movie.
    /// </summary>
    /// <returns>The overview, or the English one when none is in a preferred language.</returns>
    public IText GetPreferredOverview()
        => TextAccess.Manager.PreferredOverviewFor(this) ?? TmdbInlineText.OverviewOrEmpty(EnglishOverview);

    /// <summary>
    ///   The movie's overviews: the ones TMDB lists, then any other source's.
    /// </summary>
    /// <returns>The overviews.</returns>
    public IReadOnlyList<IText> GetAllOverviews()
        => TextAccess.Manager.ListOverviews(this);

    /// <summary>
    /// Get all TMDB company cross-references linked to the movie.
    /// </summary>
    /// <returns>All TMDB company cross-references linked to the movie.
    /// </returns>
    public IReadOnlyList<TMDB_Company_Entity> TmdbCompanyCrossReferences =>
        RepoFactory.TMDB_Company_Entity.GetByTmdbEntityTypeAndID(MetadataEntityType.Movie, TmdbMovieID);

    /// <summary>
    /// Get all TMDB companies linked to the movie.
    /// </summary>
    /// <returns>All TMDB companies linked to the movie.</returns>
    public IReadOnlyList<TMDB_Company> TmdbCompanies =>
        TmdbCompanyCrossReferences
            .Select(xref => xref.GetTmdbCompany())
            .WhereNotNull()
            .ToList();

    /// <summary>
    /// Get all TMDB studios linked to the movie.
    /// </summary>
    /// <returns>All TMDB studios linked to the movie.</returns>
    public IReadOnlyList<TMDB_Studio<TMDB_Movie>> TmdbStudios =>
        TmdbCompanyCrossReferences
            .Select(xref => xref.GetTmdbCompany() is { } company ? new TMDB_Studio<TMDB_Movie>(company, this) : null)
            .WhereNotNull()
            .ToList();

    /// <summary>
    /// The movies TMDB suggests to someone looking at this one, both its
    /// recommendations and its similar titles, best first. Most of them are
    /// not in the collection, so their <c>Suggested</c> is usually
    /// <c>null</c>.
    /// </summary>
    public IReadOnlyList<TMDB_Movie_Suggestion> TmdbSuggestions =>
        RepoFactory.TMDB_Suggestion.GetByTmdbEntityID(MetadataEntityType.Movie, TmdbMovieID)
            .Select(suggestion => new TMDB_Movie_Suggestion(suggestion))
            .ToList();

    /// <summary>
    /// The movies in the collection that TMDB suggests this one from.
    /// </summary>
    public IReadOnlyList<TMDB_Movie_Suggestion> TmdbSuggestedBy =>
        RepoFactory.TMDB_Suggestion.GetBySuggestedTmdbEntityID(MetadataEntityType.Movie, TmdbMovieID)
            .Select(suggestion => new TMDB_Movie_Suggestion(suggestion))
            .ToList();

    /// <summary>
    ///   External resources/links associated with the movie.
    /// </summary>
    public IReadOnlyList<Resource> Resources
    {
        get
        {
            var list = new List<Resource>();
            if (!string.IsNullOrEmpty(ImdbMovieID) && ImdbMovieID != "0")
                list.Add(new() { Type = ResourceType.CrossReference, Name = "IMDb", Url = $"https://www.imdb.com/title/{ImdbMovieID}/", ID = ImdbMovieID });
            list.AddRange(ISystemService.StaticServices.GetRequiredService<IMetadataService>().GatherResourcesForEntity(this));
            return list;
        }
    }

    /// <summary>
    /// Get all cast members that have worked on this movie.
    /// </summary>
    /// <returns>All cast members that have worked on this movie.</returns>
    public IReadOnlyList<TMDB_Movie_Cast> Cast =>
        RepoFactory.TMDB_Movie_Cast.GetByTmdbMovieID(TmdbMovieID);

    /// <summary>
    /// Get all crew members that have worked on this movie.
    /// </summary>
    /// <returns>All crew members that have worked on this movie.</returns>
    public IReadOnlyList<TMDB_Movie_Crew> Crew =>
        RepoFactory.TMDB_Movie_Crew.GetByTmdbMovieID(TmdbMovieID);

    /// <summary>
    /// Get all yearly seasons the movie was released in.
    /// </summary>
    public IReadOnlyList<(int Year, YearlySeason Season)> YearlySeasons
        => [.. ReleasedAt.GetYearlySeasons(ReleasedAt)];

    /// <summary>
    /// Get the TMDB movie collection linked to the movie from the local
    /// database, if any. You need to have movie collections enabled in the
    /// settings file for this to be populated.
    /// </summary>
    /// <returns>The TMDB movie collection if found, or null.</returns>
    public TMDB_Collection? TmdbCollection => TmdbCollectionID.HasValue
        ? RepoFactory.TMDB_Collection.GetByTmdbCollectionID(TmdbCollectionID.Value)
        : null;

    /// <summary>
    /// Get AniDB/TMDB cross-references for the movie.
    /// </summary>
    /// <returns>A read-only list of AniDB/TMDB cross-references for the movie.</returns>
    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> CrossReferences =>
        RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByTmdbMovieID(TmdbMovieID);

    /// <summary>
    /// Get all file cross-references associated with the movie.
    /// </summary>
    /// <returns>A read-only list of file cross-references associated with the
    /// movie.</returns>
    public IReadOnlyList<CrossRef_File_Episode> FileCrossReferences =>
        CrossReferences
            .DistinctBy(xref => xref.AnidbEpisodeID)
            .SelectMany(xref => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(xref.AnidbEpisodeID))
            .WhereNotNull()
            .ToList();

    #endregion

    #region IEntityMetadata

    MetadataEntityType IEntityMetadata.Type => MetadataEntityType.Movie;

    MetadataSource IEntityMetadata.DataSource => MetadataSource.TMDB;

    TitleLanguage? IEntityMetadata.OriginalLanguage => OriginalLanguage;

    #endregion

    #region IMetadata

    MetadataGuid IMetadata.ID => new(MetadataSource.TMDB, MetadataEntityType.Movie, TmdbMovieID.ToString());

    int ITmdbMovie.TmdbID => TmdbMovieID;

    #endregion

    #region IInlineTextSource Implementation

    ITitle? IInlineTextSource.InlineTitle => TmdbInlineText.Title(EnglishTitle);

    IText? IInlineTextSource.InlineOverview => TmdbInlineText.Overview(EnglishOverview);

    InlineTextPlacement IInlineTextSource.InlineTitlePlacement => TmdbInlineText.Placement(EnglishTitleListed);

    InlineTextPlacement IInlineTextSource.InlineOverviewPlacement => TmdbInlineText.Placement(EnglishOverviewListed);

    #endregion

    #region IWithTitles

    string IWithTitles.Title => GetPreferredTitle().Value;

    ITitle IWithTitles.DefaultTitle => TmdbInlineText.TitleOrEmpty(EnglishTitle);

    ITitle? IWithTitles.PreferredTitle => GetPreferredTitle();

    IReadOnlyList<ITitle> IWithTitles.Titles => GetAllTitles();

    #endregion

    #region IWithOverviews

    IText? IWithOverviews.DefaultOverview => TmdbInlineText.OverviewOrEmpty(EnglishOverview);

    IText? IWithOverviews.PreferredOverview => GetPreferredOverview();

    IReadOnlyList<IText> IWithOverviews.Overviews => GetAllOverviews();

    #endregion

    #region IWithCreationDate Implementation

    DateTime IWithCreationDate.CreatedAt => CreatedAt.ToUniversalTime();

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => LastUpdatedAt.ToUniversalTime();

    #endregion

    #region IWithImages

    public IImageCrossReference? DefaultPrimaryImageCrossReference => !string.IsNullOrEmpty(PosterPath) && IImageManager.GetIDForImageSourceAndResourceID(MetadataSource.TMDB, PosterPath) is { } imageID
        ? ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = MetadataSource.TMDB, ImageType = ImageEntityType.Primary }).FirstOrDefault(xref => xref.ImageID == imageID)
        : null;

    public IImageCrossReference? DefaultBackdropImageCrossReference => !string.IsNullOrEmpty(BackdropPath) && IImageManager.GetIDForImageSourceAndResourceID(MetadataSource.TMDB, BackdropPath) is { } imageID
        ? ((IWithImages)this).GetImageCrossReferences(new() { ImageSource = MetadataSource.TMDB, ImageType = ImageEntityType.Backdrop }).FirstOrDefault(xref => xref.ImageID == imageID)
        : null;

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => Cast;

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => Crew;

    #endregion

    #region IWithStudios Implementation

    IReadOnlyList<IStudio> IWithStudios.Studios => TmdbStudios;

    #endregion

    #region IWithContentRatings Implementation

    IReadOnlyList<IContentRating> IWithContentRatings.ContentRatings => ContentRatings;

    #endregion

    #region IWithTags Implementation

    /// <summary>
    ///   TMDB's genres, then its keywords, as tags.
    /// </summary>
    IReadOnlyList<ITag> IWithTags.Tags => TMDB_Tag.For(Genres, Keywords);

    #endregion

    #region IWithCrossSources Implementation

    IReadOnlyList<MetadataGuid> IWithCrossSources.CrossSourceIDs => CrossSourceID.For("imdb", MetadataEntityType.Movie, ImdbMovieID) is { } imdbID ? [imdbID] : [];

    #endregion

    #region IMovie Implementation

    IReadOnlyList<IMetadataMovieCrossReference> IMovie.MetadataMovieCrossReferences =>
        RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByTmdbMovieID(TmdbMovieID);

    bool IMovie.Restricted => IsRestricted;

    bool IMovie.Video => IsVideo;

    IReadOnlyList<int> IMovie.ShokoEpisodeIDs => CrossReferences
        .Select(xref => xref.AnimeEpisode?.AnimeEpisodeID)
        .WhereNotNull()
        .ToList();

    IReadOnlyList<int> IMovie.ShokoSeriesIDs => CrossReferences
        .Select(xref => xref.AnimeSeries?.AnimeSeriesID)
        .WhereNotNull()
        .ToList();

    DateTime? IMovie.ReleaseDate => ReleasedAt?.ToDateTime();

    double IMovie.Rating => UserRating;

    int IMovie.RatingVotes => UserVotes;

    IReadOnlyList<IShokoEpisode> IMovie.ShokoEpisodes => CrossReferences
        .Select(xref => xref.AnimeEpisode)
        .WhereNotNull()
        .ToList();

    IReadOnlyList<IShokoSeries> IMovie.ShokoSeries => CrossReferences
        .Select(xref => xref.AnimeSeries)
        .WhereNotNull()
        .ToList();

    IReadOnlyList<IRelatedMetadata<IMovie, ISeries>> IMovie.RelatedSeries => [];

    IReadOnlyList<IRelatedMetadata<IMovie, IMovie>> IMovie.RelatedMovies => [];

    IReadOnlyList<ISuggestedMetadata<IMovie, IMovie>> IMovie.Suggestions => TmdbSuggestions;

    IReadOnlyList<ISuggestedMetadata<IMovie, IMovie>> IMovie.SuggestedBy => TmdbSuggestedBy;

    IReadOnlyList<IVideoCrossReference> IMovie.VideoCrossReferences => CrossReferences
        .SelectMany(xref => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(xref.AnidbEpisodeID))
        .ToList();

    IReadOnlyList<IVideo> IMovie.Videos => CrossReferences
        .SelectMany(xref => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(xref.AnidbEpisodeID))
        .Select(xref => xref.VideoLocal)
        .WhereNotNull()
        .ToList();

    #endregion

    #region ITmdbMovie Implementation

    MetadataGuid? ITmdbMovie.CollectionID
        => TmdbCollectionID is { } collectionID ? new(MetadataSource.TMDB, MetadataEntityType.Collection, collectionID.ToString()) : null;

    IReadOnlyList<string> ITmdbMovie.ProductionCountries => ProductionCountries
        .Select(country => country.CountryCode)
        .Order()
        .ToList();

    IReadOnlyList<string> ITmdbMovie.Keywords => Keywords;

    IReadOnlyList<string> ITmdbMovie.Genres => Genres;

    ITmdbCollection? ITmdbMovie.Collection => TmdbCollection;

    IReadOnlyList<ITmdbMovieSuggestion> ITmdbMovie.Suggestions => TmdbSuggestions;

    IReadOnlyList<ITmdbMovieSuggestion> ITmdbMovie.SuggestedBy => TmdbSuggestedBy;

    #endregion
}
