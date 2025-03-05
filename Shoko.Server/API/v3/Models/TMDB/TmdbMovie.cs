using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Shoko;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.API.v3.Models.TMDB;

/// <summary>
/// APIv3 The Movie DataBase (TMDB) Movie Data Transfer Object (DTO).
/// </summary>
public class TmdbMovie
{
    /// <summary>
    /// TMDB Movie ID.
    /// </summary>
    public int ID { get; init; }

    /// <summary>
    /// TMDB Movie Collection ID, if the movie is in a movie collection on TMDB.
    /// </summary>
    public int? CollectionID { get; init; }

    /// <summary>
    /// IMDB Movie ID, if available.
    /// </summary>
    public string? ImdbMovieID { get; init; }

    /// <summary>
    /// Preferred title based upon series title preference.
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// All available titles for the movie, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Title>? Titles { get; init; }

    /// <summary>
    /// Preferred overview based upon description preference.
    /// </summary>
    public string Overview { get; init; }

    /// <summary>
    /// All available overviews for the movie, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Overview>? Overviews { get; init; }

    /// <summary>
    /// Original language the movie was shot in.
    /// </summary>
    public string OriginalLanguage { get; init; }

    /// <summary>
    /// Indicates the movie is restricted to an age group above the legal age,
    /// because it's a pornography.
    /// </summary>
    public bool IsRestricted { get; init; }

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
    public bool IsVideo { get; init; }

    /// <summary>
    /// User rating of the movie from TMDB users.
    /// </summary>
    public Rating UserRating { get; init; }

    /// <summary>
    /// The movie run-time, if it is known.
    /// </summary>
    public TimeSpan? Runtime { get; init; }

    /// <summary>
    /// Genres.
    /// </summary>
    public IReadOnlyList<string> Genres { get; init; }

    /// <summary>
    /// Keywords.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<string>? Keywords { get; init; }

    /// <summary>
    /// Content ratings for different countries for this show.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<ContentRating>? ContentRatings { get; init; }

    /// <summary>
    /// The production companies (studios) that produced the movie.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Studio>? Studios { get; init; }

    /// <summary>
    /// Production countries.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyDictionary<string, string>? ProductionCountries { get; init; }

    /// <summary>
    /// Images associated with the movie, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Images? Images { get; init; }

    /// <summary>
    /// The cast that have worked on this movie.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Role>? Cast { get; init; }

    /// <summary>
    /// The crew that have worked on this movie.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Role>? Crew { get; init; }

    /// <summary>
    /// Movie cross-references.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<CrossReference>? CrossReferences { get; init; }

    /// <summary>
    /// TMDB movie to file cross-references.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<FileCrossReference>? FileCrossReferences { get; init; }

    /// <summary>
    /// The date the movie first released, if it is known.
    /// </summary>
    public DateOnly? ReleasedAt { get; init; }

    /// <summary>
    /// When the local metadata was first created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the local metadata was last updated with new changes from the
    /// remote.
    /// </summary>
    public DateTime LastUpdatedAt { get; init; }

    public TmdbMovie(TMDB_Movie movie, IncludeDetails? includeDetails = null, IReadOnlySet<TitleLanguage>? language = null)
    {
        var include = includeDetails ?? default;
        var preferredTitle = movie.GetPreferredTitle();
        var preferredOverview = movie.GetPreferredOverview();

        ID = movie.TmdbMovieID;
        CollectionID = movie.TmdbCollectionID;
        ImdbMovieID = movie.ImdbMovieID;
        Title = preferredTitle!.Value;
        if (include.HasFlag(IncludeDetails.Titles))
            Titles = movie.AllTitles
                .ToDto(movie.EnglishTitle, preferredTitle, language);
        Overview = preferredOverview!.Value;
        if (include.HasFlag(IncludeDetails.Overviews))
            Overviews = movie.AllOverviews
                .ToDto(movie.EnglishOverview, preferredOverview, language);
        OriginalLanguage = movie.OriginalLanguageCode;
        IsRestricted = movie.IsRestricted;
        IsVideo = movie.IsVideo;
        UserRating = new()
        {
            Value = (decimal)movie.UserRating,
            MaxValue = 10,
            Votes = movie.UserVotes,
            Source = "TMDB",
            Type = "User",
        };
        Runtime = movie.Runtime;
        Genres = movie.Genres;
        if (include.HasFlag(IncludeDetails.ContentRatings))
            ContentRatings = movie.ContentRatings
                .Select(contentRating => new ContentRating(contentRating))
                .ToList();
        if (include.HasFlag(IncludeDetails.Studios))
            Studios = movie.Companies
                .Select(company => new Studio(company))
                .ToList();
        if (include.HasFlag(IncludeDetails.Images))
            Images = movie.Images
                .ToDto(language, preferredPoster: movie.DefaultPoster, preferredBackdrop: movie.DefaultBackdrop);
        if (include.HasFlag(IncludeDetails.Cast))
            Cast = movie.Cast
                .Select(cast => new Role(cast))
                .ToList();
        if (include.HasFlag(IncludeDetails.Crew))
            Crew = movie.Crew
                .Select(crew => new Role(crew))
                .ToList();
        if (include.HasFlag(IncludeDetails.CrossReferences))
            CrossReferences = movie.CrossReferences
                .Select(xref => new CrossReference(xref))
                .OrderBy(xref => xref.AnidbAnimeID)
                .ThenBy(xref => xref.AnidbEpisodeID)
                .ThenBy(xref => xref.TmdbMovieID)
                .ToList();
        if (include.HasFlag(IncludeDetails.FileCrossReferences))
            FileCrossReferences = FileCrossReference.From(movie.FileCrossReferences);
        if (include.HasFlag(IncludeDetails.Keywords))
            Keywords = movie.Keywords;
        if (include.HasFlag(IncludeDetails.ProductionCountries))
            ProductionCountries = movie.ProductionCountries
                .ToDictionary(country => country.CountryCode, country => country.CountryName);
        ReleasedAt = movie.ReleasedAt;
        CreatedAt = movie.CreatedAt.ToUniversalTime();
        LastUpdatedAt = movie.LastUpdatedAt.ToUniversalTime();
    }

    /// <summary>
    /// APIv3 The Movie DataBase (TMDB) Movie Collection Data Transfer Object (DTO).
    /// </summary>
    public class Collection
    {
        /// <summary>
        /// TMDB Movie Collection ID.
        /// </summary>
        public int ID { get; init; }

        /// <summary>
        /// Preferred title based upon series title preference.
        /// </summary>
        public string Title { get; init; }

        /// <summary>
        /// All available titles for the movie collection, if they should be included.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyList<Title>? Titles { get; init; }

        /// <summary>
        /// Preferred overview based upon description preference.
        /// </summary>
        public string Overview { get; init; }

        /// <summary>
        /// All available overviews for the movie collection, if they should be included.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyList<Overview>? Overviews { get; init; }

        public int MovieCount { get; init; }

        /// <summary>
        /// Images associated with the movie collection, if they should be included.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Images? Images { get; init; }

        /// <summary>
        /// When the local metadata was first created.
        /// </summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>
        /// When the local metadata was last updated with new changes from the
        /// remote.
        /// </summary>
        public DateTime LastUpdatedAt { get; init; }

        public Collection(TMDB_Collection collection, IncludeDetails? includeDetails = null, IReadOnlySet<TitleLanguage>? language = null)
        {
            var include = includeDetails ?? default;
            var preferredTitle = collection.GetPreferredTitle()!;
            var preferredOverview = collection.GetPreferredOverview();

            ID = collection.TmdbCollectionID;
            Title = preferredTitle.Value;
            if (include.HasFlag(IncludeDetails.Titles))
                Titles = collection.Titles
                    .ToDto(collection.EnglishTitle, preferredTitle, language);
            Overview = preferredOverview!.Value;
            if (include.HasFlag(IncludeDetails.Overviews))
                Overviews = collection.Overviews
                    .ToDto(collection.EnglishOverview, preferredOverview, language);
            MovieCount = collection.MovieCount;
            if (include.HasFlag(IncludeDetails.Images))
                Images = collection.Images
                    .ToDto(language, includeThumbnails: true);
            CreatedAt = collection.CreatedAt.ToUniversalTime();
            LastUpdatedAt = collection.LastUpdatedAt.ToUniversalTime();
        }

        [Flags]
        [JsonConverter(typeof(StringEnumConverter))]
        public enum IncludeDetails
        {
            None = 0,
            Titles = 1,
            Overviews = 2,
            Images = 4,
        }
    }


    /// <summary>
    /// APIv3 The Movie DataBase (TMDB) Movie Cross-Reference Data Transfer Object (DTO).
    /// </summary>
    public class CrossReference
    {
        /// <summary>
        /// AniDB Anime ID.
        /// </summary>
        public int AnidbAnimeID { get; init; }

        /// <summary>
        /// AniDB Episode ID.
        /// </summary>
        public int AnidbEpisodeID { get; init; }

        /// <summary>
        /// TMDB Show ID.
        /// </summary>
        public int TmdbMovieID { get; init; }

        /// <summary>
        /// The match rating.
        /// </summary>
        public string Rating { get; init; }

        public CrossReference(CrossRef_AniDB_TMDB_Movie xref)
        {
            AnidbAnimeID = xref.AnidbAnimeID;
            AnidbEpisodeID = xref.AnidbEpisodeID;
            TmdbMovieID = xref.TmdbMovieID;
            Rating = xref.Source is CrossRefSource.User ? "User" : "Automatic";
        }
    }

    [Flags]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum IncludeDetails
    {
        None = 0,
        Titles = 1,
        Overviews = 2,
        Images = 4,
        CrossReferences = 8,
        Cast = 16,
        Crew = 32,
        Studios = 64,
        ContentRatings = 128,
        FileCrossReferences = 256,
        Keywords = 512,
        ProductionCountries = 1024,
    }
}
