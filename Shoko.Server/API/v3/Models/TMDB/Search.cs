
using System;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Providers.TMDB;
using TMDbLib.Objects.Search;

#nullable enable
namespace Shoko.Server.API.v3.Models.TMDB;

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
        /// <value></value>
        public int? EpisodeID { get; set; }

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
        [JsonConverter(typeof(StringEnumConverter))]
        public TitleLanguage OriginalLanguage { get; init; }

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

        public RemoteSearchMovie(SearchMovie movie)
        {
            ID = movie.Id;
            Title = movie.Title;
            OriginalTitle = movie.OriginalTitle;
            OriginalLanguage = movie.OriginalLanguage.GetTitleLanguage();
            Overview = movie.Overview ?? string.Empty;
            IsRestricted = movie.Adult;
            IsVideo = movie.Video;
            ReleasedAt = movie.ReleaseDate.HasValue ? DateOnly.FromDateTime(movie.ReleaseDate.Value) : null;
            Poster = !string.IsNullOrEmpty(movie.PosterPath)
                ? $"{TmdbMetadataService.ImageServerUrl}/original/{movie.PosterPath}"
                : null;
            Backdrop = !string.IsNullOrEmpty(movie.BackdropPath)
                ? $"{TmdbMetadataService.ImageServerUrl}/original/{movie.BackdropPath}"
                : null;
            UserRating = new Rating()
            {
                Value = (decimal)movie.VoteAverage,
                MaxValue = 10,
                Source = "TMDB",
                Type = "User",
                Votes = movie.VoteCount,
            };
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
        [JsonConverter(typeof(StringEnumConverter))]
        public TitleLanguage OriginalLanguage { get; init; }

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

        public RemoteSearchShow(SearchTv show)
        {
            ID = show.Id;
            Title = show.Name;
            OriginalTitle = show.OriginalName;
            OriginalLanguage = show.OriginalLanguage.GetTitleLanguage();
            Overview = show.Overview ?? string.Empty;
            FirstAiredAt = show.FirstAirDate.HasValue ? DateOnly.FromDateTime(show.FirstAirDate.Value) : null;
            Poster = !string.IsNullOrEmpty(show.PosterPath)
                ? $"{TmdbMetadataService.ImageServerUrl}/original/{show.PosterPath}"
                : null;
            Backdrop = !string.IsNullOrEmpty(show.BackdropPath)
                ? $"{TmdbMetadataService.ImageServerUrl}/original/{show.BackdropPath}"
                : null;
            UserRating = new Rating()
            {
                Value = (decimal)show.VoteAverage,
                MaxValue = 10,
                Source = "TMDB",
                Type = "User",
                Votes = show.VoteCount,
            };
        }
    }
}
