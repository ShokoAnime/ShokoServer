using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;
using TMDbLib.Objects.Movies;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// The Movie DataBase (TMDB) Movie Database Model.
/// </summary>
public class TMDB_Movie : TMDB_Base<int>, IEntityMetadata
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
    /// The original language this show was shot in, just as a title language
    /// enum instead.
    /// </summary>
    public TitleLanguage OriginalLanguage
    {
        get => string.IsNullOrEmpty(OriginalLanguageCode) ? TitleLanguage.None : OriginalLanguageCode.GetTitleLanguage();
    }

    /// <summary>
    /// The original language this show was shot in.
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
    public List<string> Genres { get; set; } = new();

    /// <summary>
    /// Content ratings for different countries for this show.
    /// </summary>
    public List<TMDB_ContentRating> ContentRatings { get; set; } = new();

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
    /// Number of users that cast a vote for a rating of this show.
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
    /// <returns>True if any of the fields have been updated.</returns>
    public bool Populate(Movie movie)
    {
        var translation = movie.Translations.Translations.FirstOrDefault(translation => translation.Iso_639_1 == "en");
        var updatedList = new[]
        {
            UpdateProperty(TmdbCollectionID, movie.BelongsToCollection?.Id, v => TmdbCollectionID = v),
            UpdateProperty(EnglishTitle, translation?.Data.Name ?? movie.Title, v => EnglishTitle = v),
            UpdateProperty(EnglishOverview, translation?.Data.Overview ?? movie.Overview, v => EnglishOverview = v),
            UpdateProperty(OriginalTitle, movie.OriginalTitle, v => OriginalTitle = v),
            UpdateProperty(OriginalLanguageCode, movie.OriginalLanguage, v => OriginalLanguageCode = v),
            UpdateProperty(IsRestricted, movie.Adult, v => IsRestricted = v),
            UpdateProperty(IsVideo, movie.Video, v => IsVideo = v),
            UpdateProperty(
                Genres,
                movie.Genres.SelectMany(genre => genre.Name.Split('&', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).OrderBy(s => s).ToList(),
                v => Genres = v,
                (a, b) => string.Equals(string.Join("|", a), string.Join("|", b))
            ),
            UpdateProperty(
                ContentRatings,
                movie.ReleaseDates.Results
                    .Where(releaseDate => releaseDate.ReleaseDates.Any(r => !string.IsNullOrEmpty(r.Certification)))
                    .Select(releaseDate => new TMDB_ContentRating(releaseDate.Iso_3166_1, releaseDate.ReleaseDates.Last(r => !string.IsNullOrEmpty(r.Certification)).Certification))
                    .OrderBy(c => c.CountryCode)
                    .ToList(),
                v => ContentRatings = v,
                (a, b) => string.Equals(string.Join(",", a.Select(a1 => a1.ToString())), string.Join(",", b.Select(b1 => b1.ToString())))
            ),
            UpdateProperty(Runtime, movie.Runtime.HasValue ? TimeSpan.FromMinutes(movie.Runtime.Value) : null, v => Runtime = v),
            UpdateProperty(UserRating, movie.VoteAverage, v => UserRating = v),
            UpdateProperty(UserVotes, movie.VoteCount, v => UserVotes = v),
            UpdateProperty(ReleasedAt, movie.ReleaseDate.HasValue ? DateOnly.FromDateTime(movie.ReleaseDate.Value) : null, v => ReleasedAt = v),
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
            var title = titles.FirstOrDefault(title => title.Language == preferredLanguage.Language);
            if (title != null)
                return title;
        }

        return useFallback ? new(ForeignEntityType.Movie, TmdbMovieID, EnglishTitle, "en", "US") : null;
    }

    /// <summary>
    /// Cached reference to all titles for the movie, so we won't have to hit
    /// the database twice to get all titles _and_ the preferred title.
    /// </summary>
    private IReadOnlyList<TMDB_Title>? AllTitles = null;

    /// <summary>
    /// Get all titles for the movie.
    /// </summary>
    /// <param name="force">Forcefully re-fetch all movie titles if they're
    /// already cached from a previous call. </param>
    /// <returns>All titles for the movie.</returns>
    public IReadOnlyList<TMDB_Title> GetAllTitles(bool force = false) => force
        ? AllTitles = RepoFactory.TMDB_Title.GetByParentTypeAndID(ForeignEntityType.Movie, TmdbMovieID)
        : AllTitles ??= RepoFactory.TMDB_Title.GetByParentTypeAndID(ForeignEntityType.Movie, TmdbMovieID);

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

        foreach (var preferredLanguage in Languages.PreferredEpisodeNamingLanguages)
        {
            var overview = overviews.FirstOrDefault(overview => overview.Language == preferredLanguage.Language);
            if (overview != null)
                return overview;
        }

        return useFallback ? new(ForeignEntityType.Movie, TmdbMovieID, EnglishOverview, "en", "US") : null;
    }

    /// <summary>
    /// Cached reference to all overviews for the movie, so we won't have to hit
    /// the database twice to get all overviews _and_ the preferred overview.
    /// </summary>
    private IReadOnlyList<TMDB_Overview>? AllOverviews = null;

    /// <summary>
    /// Get all overviews for the movie.
    /// </summary>
    /// <param name="force">Forcefully re-fetch all movie overviews if they're
    /// already cached from a previous call.</param>
    /// <returns>All overviews for the movie.</returns>
    public IReadOnlyList<TMDB_Overview> GetAllOverviews(bool force = false) => force
        ? AllOverviews = RepoFactory.TMDB_Overview.GetByParentTypeAndID(ForeignEntityType.Movie, TmdbMovieID)
        : AllOverviews ??= RepoFactory.TMDB_Overview.GetByParentTypeAndID(ForeignEntityType.Movie, TmdbMovieID);

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
    /// Get all TMDB company cross-references linked to the movie.
    /// </summary>
    /// <returns>All TMDB company cross-references linked to the movie.
    /// </returns>
    public IReadOnlyList<TMDB_Company_Entity> GetTmdbCompanyCrossReferences() =>
        RepoFactory.TMDB_Company_Entity.GetByTmdbEntityTypeAndID(ForeignEntityType.Movie, TmdbMovieID);

    /// <summary>
    /// Get all TMDB companies linked to the movie.
    /// </summary>
    /// <returns>All TMDB companies linked to the movie.</returns>
    public IReadOnlyList<TMDB_Company> GetTmdbCompanies() =>
        GetTmdbCompanyCrossReferences()
            .Select(xref => xref.GetTmdbCompany())
            .OfType<TMDB_Company>()
            .ToList();

    /// <summary>
    /// Get all cast members that have worked on this movie.
    /// </summary>
    /// <returns>All cast members that have worked on this movie.</returns>
    public IReadOnlyList<TMDB_Movie_Cast> GetCast() =>
        RepoFactory.TMDB_Movie_Cast.GetByTmdbMovieID(TmdbMovieID);

    /// <summary>
    /// Get all crew members that have worked on this movie.
    /// </summary>
    /// <returns>All crew members that have worked on this movie.</returns>
    public IReadOnlyList<TMDB_Movie_Crew> GetCrew() =>
        RepoFactory.TMDB_Movie_Crew.GetByTmdbMovieID(TmdbMovieID);

    /// <summary>
    /// Get the TMDB movie collection linked to the movie from the local
    /// database, if any. You need to have movie collections enabled in the
    /// settings file for this to be populated.
    /// </summary>
    /// <returns>The TMDB movie collection if found, or null.</returns>
    public TMDB_Collection? GetTmdbCollection() => TmdbCollectionID.HasValue
        ? RepoFactory.TMDB_Collection.GetByTmdbCollectionID(TmdbCollectionID.Value)
        : null;

    /// <summary>
    /// Get AniDB/TMDB cross-references for the movie.
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> GetCrossReferences() =>
        RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByTmdbMovieID(TmdbMovieID);

    #endregion

    #region IEntityMetadata

    ForeignEntityType IEntityMetadata.Type => ForeignEntityType.Movie;

    DataSourceType IEntityMetadata.DataSource => DataSourceType.TMDB;

    TitleLanguage? IEntityMetadata.OriginalLanguage => OriginalLanguage;

    #endregion
}
