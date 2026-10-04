using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories;

#pragma warning disable CS0618
namespace Shoko.Server.API.v3.Models.AniDB;

/// <summary>
/// Basic anidb data across all anidb types.
/// </summary>
public class AnidbAnime
{
    private static AniDBTitleHelper? _titleHelper;

    private static AniDBTitleHelper TitleHelper
        => _titleHelper ??= ISystemService.StaticServices.GetService<AniDBTitleHelper>()!;

    /// <summary>
    /// AniDB ID
    /// </summary>
    [Required]
    public int ID { get; set; }

    /// <summary>
    /// <see cref="Shoko.Series"/> ID if the series is available locally.
    /// </summary>
    public int? ShokoID { get; set; }

    /// <summary>
    /// Series type. Series, OVA, Movie, etc
    /// </summary>
    [Required]
    public AnimeType Type { get; set; }

    /// <summary>
    /// Preferred title.
    /// </summary>
    [Required]
    public string Title { get; set; }

    /// <summary>
    /// There should always be at least one of these, the <see cref="Title"/>.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<Title>? Titles { get; set; }

    /// <summary>
    /// Description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Indicates when the AniDB anime first started airing, if it's known. In the 'yyyy', 'yyyy-MM' or 'yyyy-MM-dd' format, or null.
    /// </summary>
    public PartialDateOnly? AirDate { get; set; }

    /// <summary>
    /// Indicates when the AniDB anime stopped airing. It will be null if it's still airing or haven't aired yet. In the 'yyyy', 'yyyy-MM' or 'yyyy-MM-dd' format, or null.
    /// </summary>
    public PartialDateOnly? EndDate { get; set; }

    /// <summary>
    /// Restricted content. Mainly porn.
    /// </summary>
    [Required]
    public bool Restricted { get; set; }

    /// <summary>
    /// The preferred poster for the anime.
    /// </summary>
    public Image? Poster { get; set; }

    /// <summary>
    /// Number of <see cref="EpisodeType.Episode"/> episodes contained within the series if it's known.
    /// </summary>
    public int? EpisodeCount { get; set; }

    /// <summary>
    /// The average rating for the anime.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Rating? Rating { get; set; }

    /// <summary>
    /// User approval rate for the similar submission. Only available for similar. Otherwise null.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Rating? UserApproval { get; set; }

    /// <summary>
    /// Relation type. Only available for relations. Otherwise null.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public RelationType? Relation { get; set; }

    /// <summary>
    /// Whether the relation has been verified. Only available for relations. Otherwise null.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? Verified { get; set; }

    /// <summary>
    /// The preferred overview. Only set with <see cref="IncludeDetails.Overview"/>,
    /// and left out when there is none.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Overview { get; set; }

    /// <summary>
    /// The animation studios, in AniDB's order. Only set with
    /// <see cref="IncludeDetails.Studios"/>.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<AnimeStudio>? Studios { get; set; }

    /// <summary>
    /// What the anime was adapted from. Only set with
    /// <see cref="IncludeDetails.SourceMaterial"/>.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public SourceMaterial? SourceMaterial { get; set; }

    /// <summary>
    /// The heaviest genre tags, without spoilers. Only set with
    /// <see cref="IncludeDetails.Tags"/>.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<AnimeTag>? Tags { get; set; }

    /// <summary>
    /// The local files of the anime's Shoko series. Only set with
    /// <see cref="IncludeDetails.Files"/>.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public AnimeFiles? Files { get; set; }

    /// <summary>
    /// The yearly season the first regular episode airs in. Only sent with
    /// <see cref="IncludeDetails.StartSeason"/>, and <c>null</c> when no
    /// regular episode has an air date.
    /// </summary>
    public SeasonWithYear? StartSeason
    {
        get => _startSeason;
        set
        {
            _startSeason = value;
            _hasStartSeason = true;
        }
    }

    private SeasonWithYear? _startSeason;

    private bool _hasStartSeason;

    /// <summary>
    /// Whether to send <see cref="StartSeason"/>: once set, even to <c>null</c>.
    /// </summary>
    /// <returns><c>true</c> when it was set.</returns>
    public bool ShouldSerializeStartSeason()
        => _hasStartSeason;

    /// <summary>
    /// The usual length of a regular episode: the median of the known
    /// lengths. Only sent with <see cref="IncludeDetails.EpisodeDuration"/>,
    /// and <c>null</c> when no regular episode has a known length.
    /// </summary>
    public TimeSpan? EpisodeDuration
    {
        get => _episodeDuration;
        set
        {
            _episodeDuration = value;
            _hasEpisodeDuration = true;
        }
    }

    private TimeSpan? _episodeDuration;

    private bool _hasEpisodeDuration;

    /// <summary>
    /// Whether to send <see cref="EpisodeDuration"/>: once set, even to <c>null</c>.
    /// </summary>
    /// <returns><c>true</c> when it was set.</returns>
    public bool ShouldSerializeEpisodeDuration()
        => _hasEpisodeDuration;

    private AnidbAnime(int animeId, bool includeTitles, AnimeSeries? series = null, AniDB_Anime? anime = null, ResponseAniDBTitles.Anime? result = null)
    {
        ID = animeId;
        if ((anime ??= (series is not null ? series.AniDB_Anime : RepoFactory.AniDB_Anime.GetByAnimeID(animeId))) is not null)
        {
            ArgumentNullException.ThrowIfNull(anime);
            series ??= RepoFactory.AnimeSeries.GetByAnimeID(animeId);
            ShokoID = series?.AnimeSeriesID;
            Type = anime.AnimeType;
            Title = series?.Title ?? anime.Title;
            Titles = includeTitles
                ? anime.Titles.Select(title => new Title(title, anime.MainTitle, Title)).ToList()
                : null;
            Description = anime.Description;
            Restricted = anime.IsRestricted;
            Poster = (anime as IWithPrimaryImage).PrimaryImage is { } img ? new Image(img) : null;
            EpisodeCount = anime.EpisodeCountNormal;
            Rating = new Rating
            {
                Source = "AniDB",
                Value = anime.Rating,
                MaxValue = 1000,
                Votes = anime.VoteCount,
            };
            UserApproval = null;
            Relation = null;
            AirDate = anime.AirDate;
            EndDate = anime.EndDate;
        }
        else if ((result ??= TitleHelper.SearchAnimeID(animeId)) is { } titleResult)
        {
            Type = AnimeType.Unknown;
            Title = titleResult.Title;
            Titles = includeTitles
                ? titleResult.Titles.Select(
                    title => new Title(title, titleResult.DefaultTitle.Value, Title)
                    {
                        Language = title.LanguageCode,
                        Name = title.Title,
                        Type = title.TitleType,
                        Default = string.Equals(title.Title, Title),
                        Source = "AniDB"
                    }
                ).ToList()
                : null;
            Description = null;
            Poster = null;
        }
        else
        {
            Type = AnimeType.Unknown;
            Title = string.Empty;
            Titles = includeTitles ? [] : null;
            Poster = null;
        }
    }

    /// <summary>
    /// An empty model, for the caller to fill in.
    /// </summary>
    internal AnidbAnime()
    {
        Title = string.Empty;
    }

    public AnidbAnime(AniDB_Anime anime, AnimeSeries? series = null, bool includeTitles = true)
        : this(anime.AnimeID, includeTitles, series, anime) { }

    public AnidbAnime(ResponseAniDBTitles.Anime result, AnimeSeries? series = null, bool includeTitles = true)
        : this(result.AnimeID, includeTitles, series) { }

    public AnidbAnime(IRelatedMetadata relation, AnimeSeries? series = null, bool includeTitles = true)
        : this(relation.RelatedID.GetNumericID<int>(), includeTitles, series)
    {
        Relation = relation.RelationType;
        Verified = relation.Verified;
        // If the other anime is present we assume they're of the same kind. Be it restricted or unrestricted.
        if (Type == AnimeType.Unknown && TitleHelper.SearchAnimeID(relation.RelatedID.GetNumericID<int>()) is not null)
            Restricted = RepoFactory.AniDB_Anime.GetByAnimeID(relation.BaseID.GetNumericID<int>()) is { IsRestricted: true };
    }

    public AnidbAnime(AniDB_Anime_Similar similar, AnimeSeries? series = null, bool includeTitles = true)
        : this(similar.SimilarAnimeID, includeTitles, series)
    {
        UserApproval = new()
        {
            Value = new Vote(similar.Approval, similar.Total).GetRating(100),
            MaxValue = 100,
            Votes = similar.Total,
            Source = "AniDB",
            Type = "User Approval"
        };
    }

    #region Nested Types

    /// <summary>
    /// The extra details an AniDB anime list can be sent with.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum IncludeDetails
    {
        /// <summary>
        /// The animation studios, in <see cref="AnidbAnime.Studios"/>.
        /// </summary>
        Studios,

        /// <summary>
        /// What the anime was adapted from, in <see cref="AnidbAnime.SourceMaterial"/>.
        /// </summary>
        SourceMaterial,

        /// <summary>
        /// The top genre tags, in <see cref="AnidbAnime.Tags"/>.
        /// </summary>
        Tags,

        /// <summary>
        /// The preferred overview, in <see cref="AnidbAnime.Overview"/>.
        /// </summary>
        Overview,

        /// <summary>
        /// The local file count, in <see cref="AnidbAnime.Files"/>.
        /// </summary>
        Files,

        /// <summary>
        /// The season the anime starts in, in <see cref="AnidbAnime.StartSeason"/>.
        /// </summary>
        StartSeason,

        /// <summary>
        /// The usual length of a regular episode, in <see cref="AnidbAnime.EpisodeDuration"/>.
        /// </summary>
        EpisodeDuration,
    }

    /// <summary>
    /// An animation studio of an AniDB anime.
    /// </summary>
    public class AnimeStudio
    {
        /// <summary>
        /// The AniDB creator ID.
        /// </summary>
        [Required]
        public required int ID { get; init; }

        /// <summary>
        /// The name.
        /// </summary>
        [Required]
        public required string Name { get; init; }
    }

    /// <summary>
    /// A tag of an AniDB anime.
    /// </summary>
    public class AnimeTag
    {
        /// <summary>
        /// The AniDB tag ID.
        /// </summary>
        [Required]
        public required int ID { get; init; }

        /// <summary>
        /// The name.
        /// </summary>
        [Required]
        public required string Name { get; init; }
    }

    /// <summary>
    /// The local files of an AniDB anime.
    /// </summary>
    public class AnimeFiles
    {
        /// <summary>
        /// How many local files are linked to the anime's Shoko series, each
        /// counted once; <c>0</c> when there is no series.
        /// </summary>
        [Required]
        public required int VideoCount { get; init; }
    }

    #endregion
}
