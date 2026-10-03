using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Search;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.Metadata;
using Shoko.Server.Extensions;
using Shoko.Server.Models.Metadata;

namespace Shoko.Server.API.v3.Models.TMDB;

/// <summary>
/// APIv3 The Movie DataBase (TMDB) Search Data Transfer Objects (DTOs).
/// </summary>
public static class Search
{
    /// <summary>
    /// Auto-magic AniDB to TMDB match result DTO.
    /// </summary>
    /// <remarks>
    /// The AniDB anime/episode metadata is not included since it's presumed
    /// it's already available to the client when it searches for the match.
    /// The <strong>remote</strong> TMDB information on the other hand is not
    /// necessarily available and thus included with the match results.
    /// </remarks>
    public class AutoMatchResult
    {
        /// <summary>
        /// AniDB Anime ID.
        /// </summary>
        [Required]
        public int AnimeID { get; set; }

        /// <summary>
        /// AniDB Episode ID, if it's an auto-magic movie match.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? EpisodeID { get; set; }

        /// <summary>
        /// Indicates that this is a local match using existing data instead of a
        /// remote match.
        /// </summary>
        [Required]
        public bool IsLocal { get; set; }

        /// <summary>
        /// Indicates that this is a remote match.
        /// </summary>
        [Required]
        public bool IsRemote { get; set; }

        /// <summary>
        /// Indicates that the result is for a movie auto-magic match.
        /// </summary>
        [Required]
        [MemberNotNullWhen(true, nameof(EpisodeID))]
        [MemberNotNullWhen(true, nameof(Movie))]
        [MemberNotNullWhen(false, nameof(Show))]
        public bool IsMovie { get; set; }

        /// <summary>
        /// Remote TMDB Movie information.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public RemoteSearchMovie? Movie { get; set; }

        /// <summary>
        /// Remote TMDB Show information.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public RemoteSearchShow? Show { get; set; }

        /// <summary>
        /// Where the match came from: the search, a prequel's link listed for
        /// context, which an automatic search never links, or an entry the
        /// anime's AniDB resources or its links on other sources name, linked
        /// only when the search takes nothing it competes with or only
        /// something rated below it.
        /// </summary>
        [Required, JsonConverter(typeof(StringEnumConverter))]
        public MetadataAutoLinkOrigin Origin { get; set; }

        /// <summary>
        /// Why the match is not linked, or <see langword="null"/> when an
        /// automatic search links it. A match turned down can still be linked
        /// by hand.
        /// </summary>
        public MetadataAutoLinkRejectionResult? Rejection { get; set; }

        /// <summary>
        /// Describes a match the auto-linker offered.
        /// </summary>
        /// <param name="candidate">The candidate, reviewed by the core.</param>
        public AutoMatchResult(MetadataAutoLinkCandidate candidate)
        {
            Rejection = candidate.Rejection is { } rejection ? new(rejection) : null;
            AnimeID = candidate.AnidbAnimeID;
            IsLocal = candidate.IsLocal;
            IsRemote = candidate.IsRemote;
            Origin = candidate.Origin;
            if (candidate.Result is MetadataMovieSearchResult movie)
            {
                IsMovie = true;
                EpisodeID = candidate.AnidbEpisodeID ?? 0;
                Movie = new(movie);
            }
            else if (candidate.Result is MetadataSeriesSearchResult show)
            {
                Show = new(show);
            }
        }
    }

    /// <summary>
    /// Remote search movie DTO.
    /// </summary>
    public class RemoteSearchMovie
    {
        /// <summary>
        /// TMDB Movie ID.
        /// </summary>
        [Required]
        public int ID { get; init; }

        /// <summary>
        /// English title.
        /// </summary>
        [Required]
        public string Title { get; init; }

        /// <summary>
        /// Title in the original language.
        /// </summary>
        [Required]
        public string OriginalTitle { get; init; }

        /// <summary>
        /// Original language the movie was shot in.
        /// </summary>
        [Required]
        public string OriginalLanguage { get; init; }

        /// <summary>
        /// Preferred overview based upon description preference.
        /// </summary>
        [Required]
        public string Overview { get; init; }

        /// <summary>
        /// Indicates the movie is restricted to an age group above the legal age,
        /// because it's a pornography.
        /// </summary>
        [Required]
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
        [Required]
        public bool IsVideo { get; init; }

        /// <summary>
        /// The date the movie first released, if it is known.
        /// </summary>
        public DateOnly? ReleasedAt { get; init; }

        /// <summary>
        /// Poster URL, if available.
        /// </summary>
        public string? Poster { get; init; }

        /// <summary>
        /// Backdrop URL, if available.
        /// </summary>
        public string? Backdrop { get; init; }

        /// <summary>
        /// User rating of the movie from TMDB users.
        /// </summary>
        [Required]
        public Rating UserRating { get; init; }

        /// <summary>
        /// Genres.
        /// </summary>
        [Required]
        public IReadOnlyList<string> Genres { get; init; }

        public RemoteSearchMovie(Metadata_Movie movie)
        {
            ID = movie.TmdbMovieID;
            Title = movie.EnglishTitle;
            OriginalTitle = movie.OriginalTitle;
            OriginalLanguage = movie.OriginalLanguageCodeOrEmpty;
            Overview = movie.EnglishOverview;
            IsRestricted = movie.IsRestricted;
            IsVideo = movie.IsVideo;
            ReleasedAt = movie.ReleasedAt;
            Poster = TmdbCompatibility.DefaultImageUrl(movie, ImageEntityType.Primary);
            Backdrop = TmdbCompatibility.DefaultImageUrl(movie, ImageEntityType.Backdrop);
            UserRating = new Rating
            {
                Value = movie.Rating,
                MaxValue = 10,
                Source = "TMDB",
                Type = "User",
                Votes = movie.RatingVotes,
            };
            Genres = movie.Genres;
        }

        public RemoteSearchMovie(MetadataMovieSearchResult movie)
        {
            ID = TmdbCompatibility.Number(movie.ID.ID);
            Title = movie.Title;
            OriginalTitle = movie.OriginalTitle ?? movie.Title;
            OriginalLanguage = movie.OriginalLanguageCode ?? string.Empty;
            Overview = movie.Overview ?? string.Empty;
            IsRestricted = movie.IsRestricted;
            IsVideo = movie.IsStandaloneVideo;
            ReleasedAt = movie.ReleasedAt is { } releasedAt && releasedAt.TryConvertToDateOnly(out var date) ? date : null;
            Poster = movie.PosterUrl;
            Backdrop = movie.BackdropUrl;
            UserRating = new Rating
            {
                Value = (double)(movie.UserRating ?? 0),
                MaxValue = 10,
                Source = "TMDB",
                Type = "User",
                Votes = movie.UserVotes ?? 0,
            };
            Genres = movie.Genres;
        }
    }

    /// <summary>
    /// Remote search show DTO.
    /// </summary>
    public class RemoteSearchShow
    {
        /// <summary>
        /// TMDB Show ID.
        /// </summary>
        [Required]
        public int ID { get; init; }

        /// <summary>
        /// English title.
        /// </summary>
        [Required]
        public string Title { get; init; }

        /// <summary>
        /// Title in the original language.
        /// </summary>
        [Required]
        public string OriginalTitle { get; init; }

        /// <summary>
        /// Original language the show was shot in.
        /// </summary>
        [Required]
        public string OriginalLanguage { get; init; }

        /// <summary>
        /// Preferred overview based upon description preference.
        /// </summary>
        [Required]
        public string Overview { get; init; }

        /// <summary>
        /// The date the first episode aired at, if it is known.
        /// </summary>
        public DateOnly? FirstAiredAt { get; init; }

        /// <summary>
        /// Poster URL, if available.
        /// </summary>
        public string? Poster { get; init; }

        /// <summary>
        /// Backdrop URL, if available.
        /// </summary>
        public string? Backdrop { get; init; }

        /// <summary>
        /// User rating of the movie from TMDB users.
        /// </summary>
        [Required]
        public Rating UserRating { get; init; }

        /// <summary>
        /// Genres.
        /// </summary>
        [Required]
        public IReadOnlyList<string> Genres { get; init; }

        public RemoteSearchShow(Metadata_Series show)
        {
            ID = show.TmdbShowID;
            Title = show.EnglishTitle;
            OriginalTitle = show.OriginalTitle;
            OriginalLanguage = show.OriginalLanguageCodeOrEmpty;
            Overview = show.EnglishOverview;
            FirstAiredAt = show.FirstAiredAt;
            Poster = TmdbCompatibility.DefaultImageUrl(show, ImageEntityType.Primary);
            Backdrop = TmdbCompatibility.DefaultImageUrl(show, ImageEntityType.Backdrop);
            UserRating = new Rating
            {
                Value = show.Rating,
                MaxValue = 10,
                Source = "TMDB",
                Type = "User",
                Votes = show.RatingVotes,
            };
            Genres = show.Genres;
        }

        public RemoteSearchShow(MetadataSeriesSearchResult show)
        {
            ID = TmdbCompatibility.Number(show.ID.ID);
            Title = show.Title;
            OriginalTitle = show.OriginalTitle ?? show.Title;
            OriginalLanguage = show.OriginalLanguageCode ?? string.Empty;
            Overview = show.Overview ?? string.Empty;
            FirstAiredAt = show.FirstAiredAt is { } firstAiredAt && firstAiredAt.TryConvertToDateOnly(out var date) ? date : null;
            Poster = show.PosterUrl;
            Backdrop = show.BackdropUrl;
            UserRating = new Rating
            {
                Value = (double)(show.UserRating ?? 0),
                MaxValue = 10,
                Source = "TMDB",
                Type = "User",
                Votes = show.UserVotes ?? 0,
            };
            Genres = show.Genres;
        }
    }
}
