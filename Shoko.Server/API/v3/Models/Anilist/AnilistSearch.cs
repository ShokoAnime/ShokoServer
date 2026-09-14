using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anilist;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Providers.Anilist;

#nullable enable
namespace Shoko.Server.API.v3.Models.Anilist;

/// <summary>
/// APIv3 Anilist Search Data Transfer Objects (DTOs).
/// </summary>
public static class AnilistSearch
{
    /// <summary>
    /// Auto-magic AniDB to Anilist match result DTO.
    /// </summary>
    public class AutoMatchResult
    {
        /// <summary>
        /// AniDB Anime ID.
        /// </summary>
        public int AnimeID { get; set; }

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
        /// Remote Anilist Anime information.
        /// </summary>
        public RemoteSearchAnime Anime { get; set; }

        public AutoMatchResult(AnilistAutoSearchResult result)
        {
            AnimeID = result.AnidbAnime.AnimeID;
            IsLocal = result.IsLocal;
            IsRemote = result.IsRemote;
            Anime = new RemoteSearchAnime(result.AnilistAnime);
        }
    }

    /// <summary>
    /// Remote search anime DTO.
    /// </summary>
    public class RemoteSearchAnime
    {
        /// <summary>
        /// Anilist Anime ID.
        /// </summary>
        public int ID { get; init; }

        /// <summary>
        /// English title.
        /// </summary>
        public string Title { get; init; }

        /// <summary>
        /// Native/original title.
        /// </summary>
        public string OriginalTitle { get; init; }

        /// <summary>
        /// Original language the anime was produced in.
        /// </summary>
        public string OriginalLanguage { get; init; }

        /// <summary>
        /// Overview/description.
        /// </summary>
        public string Overview { get; init; }

        /// <summary>
        /// Indicates the anime is restricted to an age group above the legal age
        /// (adult content).
        /// </summary>
        public bool IsRestricted { get; init; }

        /// <summary>
        /// The anime type (TV, Movie, OVA, etc.).
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public AnimeType Type { get; init; }

        /// <summary>
        /// The date the anime started airing.
        /// </summary>
        public PartialDateOnly? FirstAiredAt { get; init; }

        /// <summary>
        /// Cover image URL.
        /// </summary>
        public string? CoverImage { get; init; }

        /// <summary>
        /// Banner image URL.
        /// </summary>
        public string? BannerImage { get; init; }

        /// <summary>
        /// User rating of the anime from Anilist users.
        /// </summary>
        public Rating UserRating { get; init; }

        public RemoteSearchAnime(Anilist_Anime anime)
        {
            ID = anime.AnilistAnimeID;
            Title = anime.PreferredTitle;
            OriginalTitle = anime.NativeTitle;
            OriginalLanguage = anime.OriginalLanguageCode;
            Overview = anime.EnglishOverview ?? string.Empty;
            IsRestricted = anime.IsRestricted;
            Type = anime.Type;
            FirstAiredAt = anime.FirstAiredAt;
            CoverImage = !string.IsNullOrEmpty(anime.CoverImagePath) ? anime.CoverImagePath : null;
            BannerImage = !string.IsNullOrEmpty(anime.BannerImagePath) ? anime.BannerImagePath : null;
            UserRating = new()
            {
                Value = anime.UserRating,
                MaxValue = 100,
                Source = "Anilist",
                Type = "User",
                Votes = anime.UserVotes,
            };
        }

        public RemoteSearchAnime(IAnilistAnimeSearchResult anime)
        {
            ID = anime.ID;
            Title = anime.Title;
            OriginalTitle = anime.OriginalTitle;
            OriginalLanguage = anime.OriginalLanguage;
            Overview = anime.Overview;
            IsRestricted = anime.IsRestricted;
            Type = anime.Type;
            FirstAiredAt = anime.FirstAiredAt;
            CoverImage = anime.CoverImageUrl;
            BannerImage = anime.BannerImageUrl;
            UserRating = new()
            {
                Value = (double)anime.UserRating,
                MaxValue = 100,
                Source = "Anilist",
                Type = "User",
                Votes = anime.UserVotes,
            };
        }
    }
}
