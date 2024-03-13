using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.API.v3.Models.TMDB;

/// <summary>
/// APIv3 The Movie DataBase (TMDB) Movie Data Transfer Object (DTO).
/// </summary>
public class Movie
{
    /// <summary>
    /// TMDB Movie ID.
    /// </summary>
    public int ID;

    /// <summary>
    /// TMDB Movie Collection ID, if the movie is in a movie collection on TMDB.
    /// </summary>
    public int? CollectionID;

    /// <summary>
    /// Preferred title based upon series title preference.
    /// </summary>
    public string Title;

    /// <summary>
    /// All available titles for the movie, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Title>? Titles;

    /// <summary>
    /// Preferred overview based upon episode title preference.
    /// </summary>
    public string Overview;

    /// <summary>
    /// All available overviews for the movie, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Overview>? Overviews;

    /// <summary>
    /// Original language the movie was shot in.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public TitleLanguage OriginalLanguage;

    /// <summary>
    /// Indicates the movie is restricted to an age group above the legal age,
    /// because it's a pornography.
    /// </summary>
    public bool IsRestricted;

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
    public bool IsVideo;

    /// <summary>
    /// User rating of the episode from TMDB users.
    /// </summary>
    public Rating UserRating;

    /// <summary>
    /// The episode run-time, if it is known.
    /// </summary>
    public TimeSpan? Runtime;

    /// <summary>
    /// Genres.
    /// </summary>
    public IReadOnlyList<string> Genres;

    /// <summary>
    /// Content ratings for different countries for this show.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<ContentRating>? ContentRatings;

    /// <summary>
    /// The production companies (studios) that produced the movie.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Studio>? Studios;

    /// <summary>
    /// Images assosiated with the movie, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Images? Images;

    /// <summary>
    /// The cast that have worked on this seasom across all episodes.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Role>? Cast;

    /// <summary>
    /// The crew that have worked on this seasom across all episodes.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Role>? Crew;

    /// <summary>
    /// Movie cross-references.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<CrossReference>? CrossReferences;

    /// <summary>
    /// The date the episode first released, if it is known.
    /// </summary>
    public DateOnly? ReleasedAt;

    /// <summary>
    /// When the local metadata was first created.
    /// </summary>
    public DateTime CreatedAt;

    /// <summary>
    /// When the local metadata was last updated with new changes from the
    /// remote.
    /// </summary>
    public DateTime LastUpdatedAt;

    public Movie(TMDB_Movie movie, IncludeDetails? includeDetails = null, IReadOnlySet<TitleLanguage>? language = null)
    {
        var include = includeDetails ?? default;
        var preferredTitle = movie.GetPreferredTitle();
        var preferredOverview = movie.GetPreferredOverview();

        ID = movie.TmdbMovieID;
        CollectionID = movie.TmdbCollectionID;
        Title = preferredTitle!.Value;
        if (include.HasFlag(IncludeDetails.Titles))
            Titles = movie.GetAllTitles()
                .ToDto(movie.EnglishTitle, preferredTitle, language);
        Overview = preferredOverview!.Value;
        if (include.HasFlag(IncludeDetails.Overviews))
            Overviews = movie.GetAllOverviews()
                .ToDto(movie.EnglishOverview, preferredOverview, language);
        OriginalLanguage = movie.OriginalLanguage;
        IsRestricted = movie.IsRestricted;
        IsVideo = movie.IsVideo;
        UserRating = new()
        {
            Value = (decimal)movie.UserRating,
            MaxValue = 10,
            Votes = movie.UserVotes,
            Source = "TMDB",
        };
        Runtime = movie.Runtime;
        Genres = movie.Genres;
        if (include.HasFlag(IncludeDetails.ContentRatings))
            ContentRatings = movie.ContentRatings
                .Select(contentRating => new ContentRating(contentRating))
                .ToList();
        if (include.HasFlag(IncludeDetails.Studios))
            Studios = movie.GetTmdbCompanies()
                .Select(company => new Studio(company))
                .ToList();
        if (include.HasFlag(IncludeDetails.Images))
            Images = movie.GetImages()
                .ToDto(language);
        if (include.HasFlag(IncludeDetails.Cast))
            Cast = movie.GetCast()
                .Select(cast => new Role(cast))
                .ToList();
        if (include.HasFlag(IncludeDetails.Crew))
            Crew = movie.GetCrew()
                .Select(crew => new Role(crew))
                .ToList();
        if (include.HasFlag(IncludeDetails.CrossReferences))
            CrossReferences = movie.GetCrossReferences()
                .Select(xref => new CrossReference(xref))
                .OrderBy(xref => xref.AnidbAnimeID)
                .ThenBy(xref => xref.AnidbEpisodeID)
                .ThenBy(xref => xref.TmdbMovieID)
                .ToList();
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
        public int ID;

        /// <summary>
        /// Preferred title based upon episode title preference.
        /// </summary>
        public string Title;

        /// <summary>
        /// All available titles for the movie collection, if they should be included.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyList<Title>? Titles;

        /// <summary>
        /// Preferred overview based upon episode title preference.
        /// </summary>
        public string Overview;

        /// <summary>
        /// All available overviews for the movie collection, if they should be included.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyList<Overview>? Overviews;

        public int MovieCount;

        /// <summary>
        /// Images assosiated with the movie collection, if they should be included.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Images? Images;

        /// <summary>
        /// When the local metadata was first created.
        /// </summary>
        public DateTime CreatedAt;

        /// <summary>
        /// When the local metadata was last updated with new changes from the
        /// remote.
        /// </summary>
        public DateTime LastUpdatedAt;

        public Collection(TMDB_Collection collection, IncludeDetails? includeDetails = null, IReadOnlySet<TitleLanguage>? language = null)
        {
            var include = includeDetails ?? default;
            var preferredTitle = collection.GetPreferredTitle();
            var preferredOverview = collection.GetPreferredOverview();

            ID = collection.TmdbCollectionID;
            Title = preferredTitle!.Value;
            if (include.HasFlag(IncludeDetails.Titles))
                Titles = collection.GetAllTitles()
                    .ToDto(collection.EnglishTitle, preferredTitle, language);
            Overview = preferredOverview!.Value;
            if (include.HasFlag(IncludeDetails.Overviews))
                Overviews = collection.GetAllOverviews()
                    .ToDto(collection.EnglishOverview, preferredOverview, language);
            MovieCount = collection.MovieCount;
            if (include.HasFlag(IncludeDetails.Images))
                Images = collection.GetImages()
                    .ToDto(language);
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
        public int AnidbAnimeID;

        /// <summary>
        /// AniDB Episode ID.
        /// </summary>
        public int? AnidbEpisodeID;

        /// <summary>
        /// TMDB Show ID.
        /// </summary>
        public int TmdbMovieID;

        /// <summary>
        /// The match rating.
        /// </summary>
        public string Rating;

        public CrossReference(CrossRef_AniDB_TMDB_Movie xref)
        {
            AnidbAnimeID = xref.AnidbAnimeID;
            AnidbEpisodeID = xref.AnidbEpisodeID;
            TmdbMovieID = xref.TmdbMovieID;
            Rating = xref.Source != CrossRefSource.User ? "User" : "Automatic";
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
    }
}
