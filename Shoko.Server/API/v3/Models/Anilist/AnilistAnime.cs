using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anilist.Enums;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.Anilist;
using Shoko.Server.Models.Anilist.Embedded;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Providers.Anilist;

using AnimeType = Shoko.Abstractions.Metadata.Enums.AnimeType;
using Resource = Shoko.Server.API.v3.Models.Common.Resource;

#nullable enable
namespace Shoko.Server.API.v3.Models.Anilist;

/// <summary>
/// APIv3 Anilist Anime Data Transfer Object (DTO).
/// </summary>
public class AnilistAnime
{
    /// <summary>
    /// Anilist Anime ID.
    /// </summary>
    [Required]
    public int ID { get; init; }

    /// <summary>
    /// English title.
    /// </summary>
    [Required]
    public string Title { get; init; }

    /// <summary>
    /// Main title. A transcription of the native title (romaji, pinyin, etc.),
    /// which AniList treats as the canonical title.
    /// </summary>
    [Required]
    public string MainTitle { get; init; }

    /// <summary>
    /// Native title.
    /// </summary>
    [Required]
    public string NativeTitle { get; init; }

    /// <summary>
    /// All synonyms/alternative titles.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<string>? Synonyms { get; init; }

    /// <summary>
    /// English overview/description.
    /// </summary>
    [Required]
    public string Overview { get; init; }

    /// <summary>
    /// Original language the anime was produced in.
    /// </summary>
    [Required]
    public string OriginalLanguage { get; init; }

    /// <summary>
    /// Indicates the anime is restricted to an age group above the legal age
    /// (adult content).
    /// </summary>
    [Required]
    public bool IsRestricted { get; init; }

    /// <summary>
    /// Indicates the anime has been licensed for release in at least one
    /// region outside its country of origin.
    /// </summary>
    [Required]
    public bool IsLicensed { get; init; }

    /// <summary>
    /// User rating of the anime from Anilist users (0-100 scale).
    /// </summary>
    [Required]
    public Rating UserRating { get; init; }

    /// <summary>
    /// Mean score (0-100 scale).
    /// </summary>
    [Required]
    public double MeanScore { get; init; }

    /// <summary>
    /// Popularity rank.
    /// </summary>
    [Required]
    public int Popularity { get; init; }

    /// <summary>
    /// Number of favorites.
    /// </summary>
    [Required]
    public int FavoriteCount { get; init; }

    /// <summary>
    /// The anime type (TV, Movie, OVA, etc.).
    /// </summary>
    [Required]
    [JsonConverter(typeof(StringEnumConverter))]
    public AnimeType Type { get; init; }

    /// <summary>
    /// Airing status.
    /// </summary>
    [Required]
    public AnilistMediaStatus Status { get; init; }

    /// <summary>
    /// Source material.
    /// </summary>
    [Required]
    public AnilistMediaSource Source { get; init; }

    /// <summary>
    /// Season of release.
    /// </summary>
    public YearlySeason? Season { get; init; }

    /// <summary>
    /// Year of the season.
    /// </summary>
    public int? SeasonYear { get; init; }

    /// <summary>
    /// Total episode count.
    /// </summary>
    [Required]
    public int EpisodeCount { get; init; }

    /// <summary>
    /// Default episode duration in minutes.
    /// </summary>
    public int? EpisodeDuration { get; init; }

    /// <summary>
    /// Genres.
    /// </summary>
    [Required]
    public IReadOnlyList<string> Genres { get; init; }

    /// <summary>
    /// Cover image URL.
    /// </summary>
    public string? CoverImage { get; init; }

    /// <summary>
    /// Banner image URL.
    /// </summary>
    public string? BannerImage { get; init; }

    /// <summary>
    /// Primary color from the cover image.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// MAL IDs linked to this anime.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<int>? MalIDs { get; init; }

    /// <summary>
    /// All titles for the anime, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Title>? Titles { get; init; }

    /// <summary>
    /// All overviews for the anime, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Overview>? Overviews { get; init; }

    /// <summary>
    /// Images associated with the anime, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Images? Images { get; init; }

    /// <summary>
    /// Tags associated with the anime.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<AnilistTag>? Tags { get; init; }

    /// <summary>
    /// Studios that produced the anime.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Studio>? Studios { get; init; }

    /// <summary>
    /// The characters and their voice actors, if included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Role>? Cast { get; init; }

    /// <summary>
    /// The staff that worked on the anime, if included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Role>? Crew { get; init; }

    /// <summary>
    /// Anime cross-references.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<CrossReference>? CrossReferences { get; init; }

    /// <summary>
    /// External resources for the anime, if they should be included. Covers
    /// the AniList page itself, the MyAnimeList cross-reference, the trailer
    /// and every external link AniList lists, such as the official site or a
    /// streaming service.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Resource>? Resources { get; init; }

    /// <summary>
    /// The date the anime started airing.
    /// </summary>
    public PartialDateOnly? FirstAiredAt { get; init; }

    /// <summary>
    /// The date the anime ended airing.
    /// </summary>
    public PartialDateOnly? LastAiredAt { get; init; }

    /// <summary>
    /// When the local metadata was first created.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the local metadata was last updated.
    /// </summary>
    [Required]
    public DateTime LastUpdatedAt { get; init; }

    public AnilistAnime(Anilist_Anime anime, IncludeDetails? includeDetails = null)
    {
        var include = includeDetails ?? default;

        ID = anime.AnilistAnimeID;
        Title = anime.PreferredTitle;
        MainTitle = anime.MainTitle;
        NativeTitle = anime.NativeTitle;
        if (include.HasFlag(IncludeDetails.Synonyms))
            Synonyms = anime.Synonyms;
        Overview = anime.EnglishOverview;
        OriginalLanguage = anime.OriginalLanguageCode;
        IsRestricted = anime.IsRestricted;
        IsLicensed = anime.IsLicensed;
        UserRating = new()
        {
            Value = anime.UserRating,
            MaxValue = 100,
            Votes = anime.UserVotes,
            Source = "Anilist",
            Type = "User",
        };
        MeanScore = anime.MeanScore;
        Popularity = anime.Popularity;
        FavoriteCount = anime.FavoriteCount;
        Type = anime.Type;
        Status = anime.ReleasingStatus;
        Source = anime.MediaSource;
        Season = anime.Season;
        SeasonYear = anime.SeasonYear;
        EpisodeCount = anime.EpisodeCount;
        EpisodeDuration = anime.EpisodeDuration;
        Genres = anime.Genres;
        CoverImage = AnilistImageService.ToImageUrl(anime.CoverImagePath);
        BannerImage = AnilistImageService.ToImageUrl(anime.BannerImagePath);
        Color = !string.IsNullOrEmpty(anime.Color) ? anime.Color : null;
        if (include.HasFlag(IncludeDetails.MalIDs))
            MalIDs = anime.MalID.HasValue ? [anime.MalID.Value] : [];
        if (include.HasFlag(IncludeDetails.Titles))
        {
            var withTitles = (IWithTitles)anime;
            var defaultTitle = withTitles.DefaultTitle;
            Titles = withTitles.Titles.Select(title => new Title(title, defaultTitle.Value, withTitles.PreferredTitle)).ToList();
        }
        if (include.HasFlag(IncludeDetails.Overviews))
        {
            var withDescriptions = (IWithDescriptions)anime;
            Overviews = withDescriptions.Descriptions.Select(overview => new Overview(overview, withDescriptions.DefaultDescription?.Value, withDescriptions.PreferredDescription)).ToList();
        }
        if (include.HasFlag(IncludeDetails.Images))
            Images = ((IWithImages)anime).GetImages().ToDto();
        if (include.HasFlag(IncludeDetails.Tags))
            Tags = anime.Tags
                .Select(animeTag => (animeTag, tag: animeTag.Tag))
                .Where(tuple => tuple.tag is not null)
                .Select(tuple => new AnilistTag(tuple.tag!, tuple.animeTag.Weight, tuple.animeTag.IsLocalSpoiler || tuple.tag!.IsSpoiler))
                .OrderByDescending(tag => tag.Rank)
                .ThenBy(tag => tag.Name)
                .ToList();
        if (include.HasFlag(IncludeDetails.Studios))
            Studios = anime.Studios
                .Select(animeStudio => animeStudio.Studio)
                .WhereNotNull()
                .Select(studio => new Studio(studio))
                .ToList();
        if (include.HasFlag(IncludeDetails.Cast))
            Cast = ((IWithCastAndCrew)anime).Cast
                .OfType<Anilist_Cast>()
                .Select(Role.FromAnilist)
                .ToList();
        if (include.HasFlag(IncludeDetails.Crew))
            Crew = ((IWithCastAndCrew)anime).Crew
                .OfType<Anilist_Crew>()
                .Select(Role.FromAnilist)
                .OfType<Role>()
                .ToList();
        if (include.HasFlag(IncludeDetails.CrossReferences))
            CrossReferences = anime.CrossReferences
                .Select(xref => new CrossReference(xref))
                .OrderBy(xref => xref.AnidbAnimeID)
                .ToList();
        if (include.HasFlag(IncludeDetails.Resources))
            Resources = anime.Resources
                .Select(resource => new Resource(resource))
                .ToList();
        FirstAiredAt = anime.FirstAiredAt;
        LastAiredAt = anime.LastAiredAt;
        CreatedAt = anime.CreatedAt.ToUniversalTime();
        LastUpdatedAt = anime.LastUpdatedAt.ToUniversalTime();
    }

    /// <summary>
    /// APIv3 Anilist Anime Tag Data Transfer Object (DTO).
    /// </summary>
    public class AnilistTag
    {
        /// <summary>
        /// Tag ID.
        /// </summary>
        [Required]
        public int ID { get; init; }

        /// <summary>
        /// Tag name.
        /// </summary>
        [Required]
        public string Name { get; init; }

        /// <summary>
        /// Tag description.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// Tag rank/relevance (percentage).
        /// </summary>
        [Required]
        public int Rank { get; init; }

        /// <summary>
        /// Whether the tag is a spoiler.
        /// </summary>
        [Required]
        public bool IsSpoiler { get; init; }

        public AnilistTag(Anilist_Tag tag, int rank, bool isSpoiler)
        {
            ID = tag.AnilistTagID;
            Name = tag.Name;
            Description = !string.IsNullOrEmpty(tag.Description) ? tag.Description : null;
            Rank = rank;
            IsSpoiler = isSpoiler;
        }
    }

    /// <summary>
    /// APIv3 Anilist Anime Cross-Reference Data Transfer Object (DTO).
    /// </summary>
    public class CrossReference
    {
        /// <summary>
        /// AniDB Anime ID.
        /// </summary>
        [Required]
        public int AnidbAnimeID { get; init; }

        /// <summary>
        /// Anilist Anime ID.
        /// </summary>
        [Required]
        public int AnilistAnimeID { get; init; }

        /// <summary>
        /// The match rating.
        /// </summary>
        [Required]
        public string Rating { get; init; }

        public CrossReference(CrossRef_AniDB_Anilist_Anime xref)
        {
            AnidbAnimeID = xref.AnidbAnimeID;
            AnilistAnimeID = xref.AnilistAnimeID;
            Rating = xref.MatchRating.ToString();
        }
    }

    [Flags]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum IncludeDetails
    {
        None = 0,
        Synonyms = 1 << 0,
        MalIDs = 1 << 1,
        Tags = 1 << 2,
        Studios = 1 << 3,
        CrossReferences = 1 << 4,
        Titles = 1 << 5,
        Overviews = 1 << 6,
        Images = 1 << 7,
        Cast = 1 << 8,
        Crew = 1 << 9,
        Resources = 1 << 10,
    }
}
