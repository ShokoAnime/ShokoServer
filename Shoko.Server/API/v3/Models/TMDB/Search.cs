
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Shoko.Server.Extensions;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;
using TMDbLib.Objects.Search;

using RemoteMovie = TMDbLib.Objects.Movies.Movie;
using RemoteShow = TMDbLib.Objects.TvShows.TvShow;

#nullable enable
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
        public bool IsLocal { get; set; }

        /// <summary>
        /// Indicates that this is a remote match.
        /// </summary>
        public bool IsRemote { get; set; }

        /// <summary>
        /// Indicates that the result is for a movie auto-magic match.
        /// </summary>
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

        public AutoMatchResult(TmdbAutoSearchResult result)
        {
            AnimeID = result.AnidbAnime.AnimeID;
            IsLocal = result.IsLocal;
            IsRemote = result.IsRemote;
            if (result.IsMovie)
            {
                IsMovie = true;
                EpisodeID = result.AnidbEpisode.EpisodeID;
                Movie = new(result.TmdbMovie);
            }
            else
            {
                Show = new(result.TmdbShow);
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
        public int ID { get; init; }

        /// <summary>
        /// English title.
        /// </summary>
        public string Title { get; init; }

        /// <summary>
        /// Title in the original language.
        /// </summary>
        public string OriginalTitle { get; init; }

        /// <summary>
        /// Original language the movie was shot in.
        /// </summary>
        public string OriginalLanguage { get; init; }

        /// <summary>
        /// Preferred overview based upon description preference.
        /// </summary>
        public string Overview { get; init; }

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
        public Rating UserRating { get; init; }

        /// <summary>
        /// Genres.
        /// </summary>
        public IReadOnlyList<string> Genres { get; init; }

        public RemoteSearchMovie(TMDB_Movie movie)
        {
            ID = movie.Id;
            Title = movie.EnglishTitle;
            OriginalTitle = movie.OriginalTitle;
            OriginalLanguage = movie.OriginalLanguageCode;
            Overview = movie.EnglishOverview ?? string.Empty;
            IsRestricted = movie.IsRestricted;
            IsVideo = movie.IsVideo;
            ReleasedAt = movie.ReleasedAt;
            Poster = !string.IsNullOrEmpty(movie.PosterPath) ? $"{TmdbMetadataService.ImageServerUrl}original{movie.PosterPath}"
                : null;
            Backdrop = !string.IsNullOrEmpty(movie.BackdropPath) ? $"{TmdbMetadataService.ImageServerUrl}original{movie.BackdropPath}"
                : null;
            UserRating = new Rating()
            {
                Value = movie.UserRating,
                MaxValue = 10,
                Source = "TMDB",
                Type = "User",
                Votes = movie.UserVotes,
            };
            Genres = movie.Genres;
        }

        public RemoteSearchMovie(RemoteMovie movie)
        {
            ID = movie.Id;
            Title = movie.Title!;
            OriginalTitle = movie.OriginalTitle!;
            OriginalLanguage = movie.OriginalLanguage!;
            Overview = movie.Overview ?? string.Empty;
            IsRestricted = movie.Adult;
            IsVideo = movie.Video;
            ReleasedAt = movie.ReleaseDate?.ToDateOnly();
            Poster = !string.IsNullOrEmpty(movie.PosterPath) ? $"{TmdbMetadataService.ImageServerUrl}original{movie.PosterPath}"
                : null;
            Backdrop = !string.IsNullOrEmpty(movie.BackdropPath) ? $"{TmdbMetadataService.ImageServerUrl}original{movie.BackdropPath}"
                : null;
            UserRating = new Rating()
            {
                Value = movie.VoteAverage,
                MaxValue = 10,
                Source = "TMDB",
                Type = "User",
                Votes = movie.VoteCount,
            };
            Genres = movie.GetGenres();
        }

        public RemoteSearchMovie(SearchMovie movie)
        {
            ID = movie.Id;
            Title = movie.Title!;
            OriginalTitle = movie.OriginalTitle!;
            OriginalLanguage = movie.OriginalLanguage!;
            Overview = movie.Overview ?? string.Empty;
            IsRestricted = movie.Adult;
            IsVideo = movie.Video;
            ReleasedAt = movie.ReleaseDate?.ToDateOnly();
            Poster = !string.IsNullOrEmpty(movie.PosterPath) ? $"{TmdbMetadataService.ImageServerUrl}original{movie.PosterPath}"
                : null;
            Backdrop = !string.IsNullOrEmpty(movie.BackdropPath) ? $"{TmdbMetadataService.ImageServerUrl}original{movie.BackdropPath}"
                : null;
            UserRating = new Rating()
            {
                Value = movie.VoteAverage,
                MaxValue = 10,
                Source = "TMDB",
                Type = "User",
                Votes = movie.VoteCount,
            };
            Genres = movie.GetGenres();
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
        public int ID { get; init; }

        /// <summary>
        /// English title.
        /// </summary>
        public string Title { get; init; }

        /// <summary>
        /// Title in the original language.
        /// </summary>
        public string OriginalTitle { get; init; }

        /// <summary>
        /// Original language the show was shot in.
        /// </summary>
        public string OriginalLanguage { get; init; }

        /// <summary>
        /// Preferred overview based upon description preference.
        /// </summary>
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
        public Rating UserRating { get; init; }

        /// <summary>
        /// Genres.
        /// </summary>
        public IReadOnlyList<string> Genres { get; init; }

        public RemoteSearchShow(TMDB_Show show)
        {
            ID = show.Id;
            Title = show.EnglishTitle;
            OriginalTitle = show.OriginalTitle;
            OriginalLanguage = show.OriginalLanguageCode;
            Overview = show.EnglishOverview ?? string.Empty;
            FirstAiredAt = show.FirstAiredAt;
            Poster = !string.IsNullOrEmpty(show.PosterPath)
                ? $"{TmdbMetadataService.ImageServerUrl}original{show.PosterPath}"
                : null;
            Backdrop = !string.IsNullOrEmpty(show.BackdropPath)
                ? $"{TmdbMetadataService.ImageServerUrl}original{show.BackdropPath}"
                : null;
            UserRating = new Rating()
            {
                Value = show.UserRating,
                MaxValue = 10,
                Source = "TMDB",
                Type = "User",
                Votes = show.UserVotes,
            };
            Genres = show.Genres;
        }

        public RemoteSearchShow(RemoteShow show)
        {
            ID = show.Id;
            Title = show.Name!;
            OriginalTitle = show.OriginalName!;
            OriginalLanguage = show.OriginalLanguage!;
            Overview = show.Overview ?? string.Empty;
            FirstAiredAt = show.FirstAirDate?.ToDateOnly();
            Poster = !string.IsNullOrEmpty(show.PosterPath)
                ? $"{TmdbMetadataService.ImageServerUrl}original{show.PosterPath}"
                : null;
            Backdrop = !string.IsNullOrEmpty(show.BackdropPath)
                ? $"{TmdbMetadataService.ImageServerUrl}original{show.BackdropPath}"
                : null;
            UserRating = new Rating()
            {
                Value = show.VoteAverage,
                MaxValue = 10,
                Source = "TMDB",
                Type = "User",
                Votes = show.VoteCount,
            };
            Genres = show.GetGenres();
        }

        public RemoteSearchShow(SearchTv show)
        {
            ID = show.Id;
            Title = show.Name!;
            OriginalTitle = show.OriginalName!;
            OriginalLanguage = show.OriginalLanguage!;
            Overview = show.Overview ?? string.Empty;
            FirstAiredAt = show.FirstAirDate?.ToDateOnly();
            Poster = !string.IsNullOrEmpty(show.PosterPath)
                ? $"{TmdbMetadataService.ImageServerUrl}original{show.PosterPath}"
                : null;
            Backdrop = !string.IsNullOrEmpty(show.BackdropPath)
                ? $"{TmdbMetadataService.ImageServerUrl}original{show.BackdropPath}"
                : null;
            UserRating = new Rating()
            {
                Value = show.VoteAverage,
                MaxValue = 10,
                Source = "TMDB",
                Type = "User",
                Votes = show.VoteCount,
            };
            Genres = show.GetGenres();
        }
    }
}
