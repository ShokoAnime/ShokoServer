using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.API.v3.Models.AniDB;

/// <summary>
/// Basic anidb data across all anidb types.
/// </summary>
public class AnidbAnime
{
    private static AniDBTitleHelper? _titleHelper = null;

    private static AniDBTitleHelper TitleHelper
        => _titleHelper ??= Utils.ServiceContainer.GetService<AniDBTitleHelper>()!;

    /// <summary>
    /// AniDB ID
    /// </summary>
    public int ID { get; set; }

    /// <summary>
    /// <see cref="Shoko.Series"/> ID if the series is available locally.
    /// </summary>
    public int? ShokoID { get; set; }

    /// <summary>
    /// Series type. Series, OVA, Movie, etc
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public AnimeType Type { get; set; }

    /// <summary>
    /// Main Title, usually matches x-jat
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// There should always be at least one of these, the <see cref="Title"/>.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<Title>? Titles { get; set; }

    /// <summary>
    /// Description.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Description { get; set; }

    /// <summary>
    /// Indicates when the AniDB anime first started airing, if it's known. In the 'yyyy-MM-dd' format, or null.
    /// </summary>
    public DateOnly? AirDate { get; set; }

    /// <summary>
    /// Indicates when the AniDB anime stopped airing. It will be null if it's still airing or haven't aired yet. In the 'yyyy-MM-dd' format, or null.
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Restricted content. Mainly porn.
    /// </summary>
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
    /// User approval rate for the similar submission. Only available for similar.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Rating? UserApproval { get; set; }

    /// <summary>
    /// Relation type. Only available for relations.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public RelationType? Relation { get; set; }

    private AnidbAnime(int animeId, bool includeTitles, SVR_AnimeSeries? series = null, SVR_AniDB_Anime? anime = null, ResponseAniDBTitles.Anime? result = null)
    {
        ID = animeId;
        if ((anime ??= (series is not null ? series.AniDB_Anime : RepoFactory.AniDB_Anime.GetByAnimeID(animeId))) is not null)
        {
            ArgumentNullException.ThrowIfNull(anime);
            series ??= RepoFactory.AnimeSeries.GetByAnimeID(animeId);
            var seriesTitle = series?.PreferredTitle ?? anime.PreferredTitle;
            ShokoID = series?.AnimeSeriesID;
            Type = anime.AbstractAnimeType.ToV3Dto();
            Title = seriesTitle;
            Titles = includeTitles
                ? anime.Titles.Select(title => new Title(title, anime.MainTitle, seriesTitle)).ToList()
                : null;
            Description = anime.Description;
            Restricted = anime.IsRestricted;
            Poster = new Image(anime.PreferredOrDefaultPoster);
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
            AirDate = anime.AirDate?.ToDateOnly();
            EndDate = anime.EndDate?.ToDateOnly();
        }
        else if ((result ??= TitleHelper.SearchAnimeID(animeId)) is not null)
        {
            Type = AnimeType.Unknown;
            Title = result.PreferredTitle;
            Titles = includeTitles
                ? result.Titles.Select(
                    title => new Title(title, result.MainTitle, Title)
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
            Poster = new Image(animeId, ImageEntityType.Poster, DataSourceType.AniDB);
        }
        else
        {
            Type = AnimeType.Unknown;
            Title = string.Empty;
            Titles = includeTitles ? [] : null;
            Poster = new Image(animeId, ImageEntityType.Poster, DataSourceType.AniDB);
        }
    }

    public AnidbAnime(SVR_AniDB_Anime anime, SVR_AnimeSeries? series = null, bool includeTitles = true)
        : this(anime.AnimeID, includeTitles, series, anime) { }

    public AnidbAnime(ResponseAniDBTitles.Anime result, SVR_AnimeSeries? series = null, bool includeTitles = true)
        : this(result.AnimeID, includeTitles, series) { }

    public AnidbAnime(IRelatedMetadata relation, SVR_AnimeSeries? series = null, bool includeTitles = true)
        : this(relation.RelatedID, includeTitles, series)
    {
        Relation = relation.RelationType;
        // If the other anime is present we assume they're of the same kind. Be it restricted or unrestricted.
        if (Type == AnimeType.Unknown && TitleHelper.SearchAnimeID(relation.RelatedID) is not null)
            Restricted = RepoFactory.AniDB_Anime.GetByAnimeID(relation.BaseID) is { IsRestricted: true };
    }

    public AnidbAnime(AniDB_Anime_Similar similar, SVR_AnimeSeries? series = null, bool includeTitles = true)
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
