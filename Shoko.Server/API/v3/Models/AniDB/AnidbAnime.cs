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
    /// Indicates when the AniDB anime first started airing, if it's known. In the 'yyyy-MM-dd' format, or null.
    /// </summary>
    public PartialDateOnly? AirDate { get; set; }

    /// <summary>
    /// Indicates when the AniDB anime stopped airing. It will be null if it's still airing or haven't aired yet. In the 'yyyy-MM-dd' format, or null.
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
        else if ((result ??= TitleHelper.SearchAnimeID(animeId)) is not null)
        {
            Type = AnimeType.Unknown;
            Title = result.Title;
            Titles = includeTitles
                ? result.Titles.Select(
                    title => new Title(title, result.DefaultTitle.Value, Title)
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

    public AnidbAnime(AniDB_Anime anime, AnimeSeries? series = null, bool includeTitles = true)
        : this(anime.AnimeID, includeTitles, series, anime) { }

    public AnidbAnime(ResponseAniDBTitles.Anime result, AnimeSeries? series = null, bool includeTitles = true)
        : this(result.AnimeID, includeTitles, series) { }

    public AnidbAnime(IRelatedMetadata relation, AnimeSeries? series = null, bool includeTitles = true)
        : this(relation.RelatedID, includeTitles, series)
    {
        Relation = relation.RelationType;
        // If the other anime is present we assume they're of the same kind. Be it restricted or unrestricted.
        if (Type == AnimeType.Unknown && TitleHelper.SearchAnimeID(relation.RelatedID) is not null)
            Restricted = RepoFactory.AniDB_Anime.GetByAnimeID(relation.BaseID) is { IsRestricted: true };
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
}
