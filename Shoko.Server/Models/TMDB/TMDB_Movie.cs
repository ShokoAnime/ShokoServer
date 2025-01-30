using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using TMDbLib.Objects.Movies;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// The Movie DataBase (TMDB) Movie Database Model.
/// </summary>
public class TMDB_Movie : TMDB_Base<int>, IEntityMetadata, IMovie
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
        var translation = movie.Translations.Translations.FirstOrDefault(translation => translation.Iso_639_1 == "en");
        var lang = movie.ProductionCountries.FirstOrDefault()?.Iso_3166_1;
        var releaseDate = !string.IsNullOrEmpty(lang) && movie.ReleaseDates.Results.FirstOrDefault(obj0 => obj0.Iso_3166_1 == lang) is { } obj1
            ? obj1.ReleaseDates.FirstOrDefault(obj2 => obj2.Type is ReleaseDateType.TheatricalLimited or ReleaseDateType.Theatrical)?.ReleaseDate ??
                obj1.ReleaseDates.FirstOrDefault(obj2 => obj2.Type is ReleaseDateType.Premiere)?.ReleaseDate ??
                obj1.ReleaseDates.FirstOrDefault()?.ReleaseDate ??
                movie.ReleaseDate
            : movie.ReleaseDate;
        var updatedList = new[]
        {
            UpdateProperty(PosterPath, movie.PosterPath, v => PosterPath = v),
            UpdateProperty(BackdropPath, movie.BackdropPath, v => BackdropPath = v),
            UpdateProperty(TmdbCollectionID, movie.BelongsToCollection?.Id, v => TmdbCollectionID = v),
            UpdateProperty(EnglishTitle, !string.IsNullOrEmpty(translation?.Data.Name) ? translation.Data.Name : movie.Title, v => EnglishTitle = v),
            UpdateProperty(EnglishOverview, !string.IsNullOrEmpty(translation?.Data.Overview) ? translation.Data.Overview : movie.Overview, v => EnglishOverview = v),
            UpdateProperty(OriginalTitle, movie.OriginalTitle, v => OriginalTitle = v),
            UpdateProperty(OriginalLanguageCode, movie.OriginalLanguage, v => OriginalLanguageCode = v),
            UpdateProperty(IsRestricted, movie.Adult, v => IsRestricted = v),
            UpdateProperty(IsVideo, movie.Video, v => IsVideo = v),
            UpdateProperty(Genres, movie.GetGenres(), v => Genres = v, (a, b) => string.Equals(string.Join("|", a), string.Join("|", b))),
            UpdateProperty(Keywords, movie.Keywords.Keywords.Select(k => k.Name).ToList(), v => Keywords = v, (a, b) => string.Equals(string.Join("|", a), string.Join("|", b))),
            UpdateProperty(
                ContentRatings,
                movie.ReleaseDates.Results
                    .Where(releaseDate => releaseDate.ReleaseDates.Any(r => !string.IsNullOrEmpty(r.Certification)))
                    .Select(releaseDate => new TMDB_ContentRating(releaseDate.Iso_3166_1, releaseDate.ReleaseDates.Last(r => !string.IsNullOrEmpty(r.Certification)).Certification))
                    .WhereInLanguages(crLanguages?.Append(TitleLanguage.EnglishAmerican).ToHashSet())
                    .OrderBy(c => c.CountryCode)
                    .ToList(),
                v => ContentRatings = v,
                (a, b) => string.Equals(string.Join(",", a.Select(a1 => a1.ToString())), string.Join(",", b.Select(b1 => b1.ToString())))
            ),
            UpdateProperty(
                ProductionCountries,
                movie.ProductionCountries
                    .Select(country => new TMDB_ProductionCountry(country.Iso_3166_1, country.Name))
                    .OrderBy(c => c.CountryCode)
                    .ToList(),
                v => ProductionCountries = v,
                (a, b) => string.Equals(string.Join(",", a.Select(a1 => a1.ToString())), string.Join(",", b.Select(b1 => b1.ToString())))
            ),
            UpdateProperty(Runtime, movie.Runtime.HasValue ? TimeSpan.FromMinutes(movie.Runtime.Value) : null, v => Runtime = v),
            UpdateProperty(UserRating, movie.VoteAverage, v => UserRating = v),
            UpdateProperty(UserVotes, movie.VoteCount, v => UserVotes = v),
            UpdateProperty(ReleasedAt, releaseDate.HasValue ? DateOnly.FromDateTime(releaseDate.Value) : null, v => ReleasedAt = v),
        };

        return updatedList.Any(updated => updated);
    }

    /// <summary>
    /// Get the preferred title using the preferred series title preference
    /// from the application settings.
    /// </summary>
    /// <param name="useFallback">Use a fallback title if no title was found in
    /// any of the preferred languages.</param>
    /// <param name="force">Forcefully re-fetch all movie titles if they're
    /// already cached from a previous call to <seealso cref="GetAllTitles"/>.
    /// </param>
    /// <returns>The preferred movie title, or null if no preferred title was
    /// found.</returns>
    public TMDB_Title? GetPreferredTitle(bool useFallback = true, bool force = false)
    {
        var titles = GetAllTitles(force);

        foreach (var preferredLanguage in Languages.PreferredNamingLanguages)
        {
            if (preferredLanguage.Language == TitleLanguage.Main)
                return new(ForeignEntityType.Movie, TmdbMovieID, EnglishTitle, "en", "US");

            var title = titles.GetByLanguage(preferredLanguage.Language);
            if (title != null)
                return title;
        }

        return useFallback ? new(ForeignEntityType.Movie, TmdbMovieID, EnglishTitle, "en", "US") : null;
    }

    /// <summary>
    /// Cached reference to all titles for the movie, so we won't have to hit
    /// the database twice to get all titles _and_ the preferred title.
    /// </summary>
    private IReadOnlyList<TMDB_Title>? _allTitles = null;

    /// <summary>
    /// Get all titles for the movie.
    /// </summary>
    /// <param name="force">Forcefully re-fetch all movie titles if they're
    /// already cached from a previous call. </param>
    /// <returns>All titles for the movie.</returns>
    public IReadOnlyList<TMDB_Title> GetAllTitles(bool force = false) => force
        ? _allTitles = RepoFactory.TMDB_Title.GetByParentTypeAndID(ForeignEntityType.Movie, TmdbMovieID)
        : _allTitles ??= RepoFactory.TMDB_Title.GetByParentTypeAndID(ForeignEntityType.Movie, TmdbMovieID);

    /// <summary>
    /// Get the preferred overview using the preferred episode title preference
    /// from the application settings.
    /// </summary>
    /// <param name="useFallback">Use a fallback overview if no overview was
    /// found in any of the preferred languages.</param>
    /// <param name="force">Forcefully re-fetch all movie overviews if they're
    /// already cached from a previous call to
    /// <seealso cref="GetAllOverviews"/>.
    /// </param>
    /// <returns>The preferred movie overview, or null if no preferred overview
    /// was found.</returns>
    public TMDB_Overview? GetPreferredOverview(bool useFallback = true, bool force = false)
    {
        var overviews = GetAllOverviews(force);

        foreach (var preferredLanguage in Languages.PreferredDescriptionNamingLanguages)
        {
            var overview = overviews.GetByLanguage(preferredLanguage.Language);
            if (overview != null)
                return overview;
        }

        return useFallback ? new(ForeignEntityType.Movie, TmdbMovieID, EnglishOverview, "en", "US") : null;
    }

    /// <summary>
    /// Cached reference to all overviews for the movie, so we won't have to hit
    /// the database twice to get all overviews _and_ the preferred overview.
    /// </summary>
    private IReadOnlyList<TMDB_Overview>? _allOverviews = null;

    /// <summary>
    /// Get all overviews for the movie.
    /// </summary>
    /// <param name="force">Forcefully re-fetch all movie overviews if they're
    /// already cached from a previous call.</param>
    /// <returns>All overviews for the movie.</returns>
    public IReadOnlyList<TMDB_Overview> GetAllOverviews(bool force = false) => force
        ? _allOverviews = RepoFactory.TMDB_Overview.GetByParentTypeAndID(ForeignEntityType.Movie, TmdbMovieID)
        : _allOverviews ??= RepoFactory.TMDB_Overview.GetByParentTypeAndID(ForeignEntityType.Movie, TmdbMovieID);

    public TMDB_Image? DefaultPoster => RepoFactory.TMDB_Image.GetByRemoteFileName(PosterPath)?.GetImageMetadata(true, ImageEntityType.Poster);

    public TMDB_Image? DefaultBackdrop => RepoFactory.TMDB_Image.GetByRemoteFileName(BackdropPath)?.GetImageMetadata(true, ImageEntityType.Backdrop);

    /// <summary>
    /// Get all images for the movie, or all images for the given
    /// <paramref name="entityType"/> provided for the movie.
    /// </summary>
    /// <param name="entityType">If set, will restrict the returned list to only
    /// containing the images of the given entity type.</param>
    /// <returns>A read-only list of images that are linked to the movie.
    /// </returns>
    public IReadOnlyList<TMDB_Image> GetImages(ImageEntityType? entityType = null) => entityType.HasValue
        ? RepoFactory.TMDB_Image.GetByTmdbMovieIDAndType(TmdbMovieID, entityType.Value)
        : RepoFactory.TMDB_Image.GetByTmdbMovieID(TmdbMovieID);

    /// <summary>
    /// Get all images for the movie, or all images for the given
    /// <paramref name="entityType"/> provided for the movie.
    /// </summary>
    /// <param name="entityType">If set, will restrict the returned list to only
    /// containing the images of the given entity type.</param>
    /// <param name="preferredImages">The preferred images.</param>
    /// <returns>A read-only list of images that are linked to the movie.
    /// </returns>
    public IReadOnlyList<IImageMetadata> GetImages(ImageEntityType? entityType, IReadOnlyDictionary<ImageEntityType, IImageMetadata> preferredImages) =>
        GetImages(entityType)
            .GroupBy(i => i.ImageType)
            .SelectMany(gB => preferredImages.TryGetValue(gB.Key, out var pI) ? gB.Select(i => i.Equals(pI) ? pI : i) : gB)
            .ToList();

    /// <summary>
    /// Get all TMDB company cross-references linked to the movie.
    /// </summary>
    /// <returns>All TMDB company cross-references linked to the movie.
    /// </returns>
    public IReadOnlyList<TMDB_Company_Entity> TmdbCompanyCrossReferences =>
        RepoFactory.TMDB_Company_Entity.GetByTmdbEntityTypeAndID(ForeignEntityType.Movie, TmdbMovieID);

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
    public IReadOnlyList<SVR_CrossRef_File_Episode> FileCrossReferences =>
        CrossReferences
            .DistinctBy(xref => xref.AnidbEpisodeID)
            .SelectMany(xref => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(xref.AnidbEpisodeID))
            .WhereNotNull()
            .ToList();

    #endregion

    #region IEntityMetadata

    ForeignEntityType IEntityMetadata.Type => ForeignEntityType.Movie;

    DataSourceEnum IEntityMetadata.DataSource => DataSourceEnum.TMDB;

    TitleLanguage? IEntityMetadata.OriginalLanguage => OriginalLanguage;

    #endregion

    #region IMetadata

    DataSourceEnum IMetadata.Source => DataSourceEnum.TMDB;

    int IMetadata<int>.ID => Id;

    #endregion

    #region IWithTitles

    string IWithTitles.DefaultTitle => EnglishTitle;

    string IWithTitles.PreferredTitle => GetPreferredTitle()!.Value;

    IReadOnlyList<AnimeTitle> IWithTitles.Titles => GetAllTitles()
        .Select(title => new AnimeTitle()
        {
            Language = title.Language,
            LanguageCode = title.LanguageCode,
            Source = DataSourceEnum.TMDB,
            Title = title.Value,
            Type = TitleType.Official,
        })
        .ToList();

    #endregion

    #region IWithDescriptions

    string IWithDescriptions.DefaultDescription => EnglishOverview;

    string IWithDescriptions.PreferredDescription => GetPreferredOverview()!.Value;

    IReadOnlyList<TextDescription> IWithDescriptions.Descriptions => GetAllOverviews()
        .Select(overview => new TextDescription()
        {
            CountryCode = overview.CountryCode,
            Language = overview.Language,
            LanguageCode = overview.LanguageCode,
            Source = DataSourceEnum.TMDB,
            Value = overview.Value,
        })
        .ToList();

    #endregion

    #region IWithImages

    IImageMetadata? IWithImages.GetPreferredImageForType(ImageEntityType entityType) => null;

    IReadOnlyList<IImageMetadata> IWithImages.GetImages(ImageEntityType? entityType) => GetImages(entityType);

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast => Cast;

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew => Crew;

    #endregion

    #region IWithStudios Implementation

    IReadOnlyList<IStudio> IWithStudios.Studios => TmdbStudios;

    #endregion

    #region IMovie

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

    IImageMetadata? IMovie.DefaultPoster => DefaultPoster;

    IReadOnlyList<IShokoEpisode> IMovie.ShokoEpisodes => CrossReferences
        .Select(xref => xref.AnimeEpisode)
        .WhereNotNull()
        .ToList();

    IReadOnlyList<IShokoSeries> IMovie.ShokoSeries => CrossReferences
        .Select(xref => xref.AnimeSeries)
        .WhereNotNull()
        .ToList();

    IReadOnlyList<IRelatedMetadata<ISeries>> IMovie.RelatedSeries => [];

    IReadOnlyList<IRelatedMetadata<IMovie>> IMovie.RelatedMovies => [];

    IReadOnlyList<IVideoCrossReference> IMovie.CrossReferences => CrossReferences
        .SelectMany(xref => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(xref.AnidbEpisodeID))
        .ToList();

    IReadOnlyList<IVideo> IMovie.VideoList => CrossReferences
        .SelectMany(xref => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(xref.AnidbEpisodeID))
        .Select(xref => xref.VideoLocal)
        .WhereNotNull()
        .ToList();

    #endregion
}
