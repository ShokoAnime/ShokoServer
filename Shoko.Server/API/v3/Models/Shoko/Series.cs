using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.API.Converters;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

using AniDBAnimeType = Shoko.Models.Enums.AnimeType;
using AniDBEpisodeType = Shoko.Models.Enums.EpisodeType;
using DataSource = Shoko.Server.API.v3.Models.Common.DataSource;
using InternalEpisodeType = Shoko.Models.Enums.EpisodeType;
using RelationType = Shoko.Plugin.Abstractions.DataModels.RelationType;
using TmdbMovie = Shoko.Server.API.v3.Models.TMDB.Movie;
using TmdbShow = Shoko.Server.API.v3.Models.TMDB.Show;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
/// Series object, stores all of the series info
/// </summary>
public class Series : BaseModel
{
    private static AniDBTitleHelper? _titleHelper = null;

    private static AniDBTitleHelper TitleHelper
        => _titleHelper ??= Utils.ServiceContainer.GetService<AniDBTitleHelper>()!;

    /// <summary>
    /// The relevant IDs for the series, Shoko Internal, AniDB, etc
    /// </summary>
    public SeriesIDs IDs { get; set; }

    /// <summary>
    /// Indicates that the series have a custom name set.
    /// </summary>
    public bool HasCustomName { get; set; }

    /// <summary>
    /// Preferred description for series.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// The default or random pictures for a series. This allows the client to not need to get all images and pick one.
    /// There should always be a poster, but no promises on the rest.
    /// </summary>
    public Images Images { get; set; }

    /// <summary>
    /// the user's rating
    /// </summary>
    public Rating? UserRating { get; set; }

    /// <summary>
    /// The inferred days of the week this series airs on.
    /// </summary>
    /// <remarks>
    /// Will only be set for series of type <see cref="SeriesType.TV"/> and
    /// <see cref="SeriesType.Web"/>.
    /// </remarks>
    /// <value>Each weekday</value>
    [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
    public List<DayOfWeek> AirsOn { get; set; }

    /// <summary>
    /// links to series pages on various sites
    /// </summary>
    public List<Resource> Links { get; set; }

    /// <summary>
    /// Sizes object, has totals
    /// </summary>
    public SeriesSizes Sizes { get; set; }

    /// <summary>
    /// The time when the series was created, during the process of the first file being added
    /// </summary>
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime Created { get; set; }

    /// <summary>
    /// The time when the series was last updated
    /// </summary>
    [JsonConverter(typeof(IsoDateTimeConverter))]
    public DateTime Updated { get; set; }

#pragma warning disable IDE1006
    /// <summary>
    /// The <see cref="Series.AniDB"/>, if <see cref="DataSource.AniDB"/> is
    /// included in the data to add.
    /// </summary>
    [JsonProperty("AniDB", NullValueHandling = NullValueHandling.Ignore)]
    public AniDB? _AniDB { get; set; }

    /// <summary>
    /// The <see cref="Series.TvDB"/> entries, if <see cref="DataSource.TvDB"/>
    /// is included in the data to add.
    /// </summary>
    [JsonProperty("TvDB", NullValueHandling = NullValueHandling.Ignore)]
    public IEnumerable<TvDB>? _TvDB { get; set; }
#pragma warning restore IDE1006

    /// <summary>
    /// The <see cref="TmdbData"/> entries, if <see cref="DataSource.TMDB"/>
    /// is included in the data to add.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public TmdbData? TMDB { get; set; }

    public Series(SVR_AnimeSeries ser, int userId = 0, bool randomizeImages = false, HashSet<DataSource>? includeDataFrom = null)
    {
        var anime = ser.AniDB_Anime ??
            throw new NullReferenceException($"Unable to get AniDB Anime {ser.AniDB_ID} for AnimeSeries {ser.AnimeSeriesID}");
        var animeType = (AniDBAnimeType)anime.AnimeType;
        var allEpisodes = ser.AllAnimeEpisodes;
        var vote = RepoFactory.AniDB_Vote.GetByEntityAndType(anime.AnimeID, AniDBVoteType.Anime) ??
                   RepoFactory.AniDB_Vote.GetByEntityAndType(anime.AnimeID, AniDBVoteType.AnimeTemp);
        var tmdbMovieXRefs = ser.TmdbMovieCrossReferences;
        var tmdbShowXRefs = ser.TmdbShowCrossReferences;
        var sizes = ModelHelper.GenerateSeriesSizes(allEpisodes, userId);
        IDs = new()
        {
            ID = ser.AnimeSeriesID,
            ParentGroup = ser.AnimeGroupID,
            TopLevelGroup = ser.TopLevelAnimeGroup?.AnimeGroupID ?? 0,
            AniDB = ser.AniDB_ID,
            TvDB = ser.TvdbSeriesCrossReferences.Select(a => a.TvDBID).Concat(tmdbShowXRefs.Select(xref => xref.TmdbShow?.TvdbShowID).WhereNotNull()).Distinct().ToList(),
            IMDB = tmdbMovieXRefs
                .Select(xref => xref.TmdbMovie?.ImdbMovieID)
                .WhereNotNull()
                .Distinct()
                .ToList(),
            TMDB = new()
            {
                Movie = tmdbMovieXRefs.Select(a => a.TmdbMovieID).Distinct().ToList(),
                Show = tmdbShowXRefs.Select(a => a.TmdbShowID).Distinct().ToList(),
            },
            TraktTv = ser.TraktShowCrossReferences.Select(a => a.TraktID).Distinct().ToList(),
            MAL = ser.MALCrossReferences.Select(a => a.MALID).Distinct().ToList()
        };
        Links = anime.Resources
            .Select(tuple => new Resource(tuple))
            .ToList();
        Name = ser.PreferredTitle;
        HasCustomName = !string.IsNullOrEmpty(ser.SeriesNameOverride);
        Description = ser.PreferredOverview;
        Images = ser.GetImages().ToDto(preferredImages: true, randomizeImages: randomizeImages);
        AirsOn = animeType == AniDBAnimeType.TVSeries || animeType == AniDBAnimeType.Web ? GetAirsOnDaysOfWeek(allEpisodes) : [];
        Sizes = sizes;
        Created = ser.DateTimeCreated.ToUniversalTime();
        Updated = ser.DateTimeUpdated.ToUniversalTime();
        Size = sizes.Local.Credits + sizes.Local.Episodes + sizes.Local.Others + sizes.Local.Parodies + sizes.Local.Specials + sizes.Local.Trailers;
        if (vote is not null)
            UserRating = new()
            {
                Value = (decimal)Math.Round(vote.VoteValue / 100D, 1),
                MaxValue = 10,
                Type = (AniDBVoteType)vote.VoteType == AniDBVoteType.Anime ? "Permanent" : "Temporary",
                Source = "User"
            };
        if (includeDataFrom?.Contains(DataSource.AniDB) ?? false)
            _AniDB = new(anime, ser);
        if (includeDataFrom?.Contains(DataSource.TvDB) ?? false)
            _TvDB = ser.TvDBSeries.Select(tvdb => new TvDB(tvdb, allEpisodes));
        if (includeDataFrom?.Contains(DataSource.TMDB) ?? false)
            TMDB = new()
            {
                Movies = ser.TmdbMovies.Select(movie => new TmdbMovie(movie)),
                Shows = ser.TmdbShows.Select(show => new TmdbShow(show)),
            };
    }

    /// <summary>
    /// Get the most recent days in the week the show airs on.
    /// </summary>
    /// <param name="animeEpisodes">Optionally pass in the episodes so we don't have to fetch them.</param>
    /// <param name="includeThreshold">Threshold of episodes to include in the calculation.</param>
    /// <returns></returns>
    private static List<DayOfWeek> GetAirsOnDaysOfWeek(IEnumerable<SVR_AnimeEpisode> animeEpisodes, int includeThreshold = 24)
    {
        var now = DateTime.Now;
        var filteredEpisodes = animeEpisodes
            .Select(episode =>
            {
                var aniDB = episode.AniDB_Episode;
                var airDate = aniDB.GetAirDateAsDate();
                return (episode, aniDB, airDate);
            })
            .Where(tuple =>
            {
                // Shouldn't happen, but the compiler want us to check so we check.
                if (tuple.aniDB is null)
                    return false;

                // We ignore all other types except the "normal" type.
                if ((AniDBEpisodeType)tuple.aniDB.EpisodeType != AniDBEpisodeType.Episode)
                    return false;

                // We ignore any unknown air dates and dates in the future.
                if (!tuple.airDate.HasValue || tuple.airDate.Value > now)
                    return false;

                return true;
            })
            .ToList();

        // Threshold used to filter out outliers, e.g. a weekday that only happens
        // once or twice for whatever reason, or when a show gets an early preview,
        // an episode moving, etc...
        var outlierThreshold = Math.Min((int)Math.Ceiling(filteredEpisodes.Count / 12D), 4);
        return filteredEpisodes
            .OrderByDescending(tuple => tuple.aniDB!.EpisodeNumber)
        //   We check up to the `x` last aired episodes to get a grasp on which days
        //   it airs on. This helps reduce variance in days for long-running
        //   shows, such as One Piece, etc...
            .Take(includeThreshold)
            .Select(tuple => tuple.airDate!.Value.DayOfWeek)
            .GroupBy(weekday => weekday)
            .Where(list => list.Count() > outlierThreshold)
            .Select(list => list.Key)
            .OrderBy(weekday => weekday)
            .ToList();
    }

    /// <summary>
    /// Cast is aggregated, and therefore not in each provider
    /// </summary>
    /// <param name="animeID"></param>
    /// <param name="roleTypes"></param>
    /// <returns></returns>
    public static List<Role> GetCast(int animeID, HashSet<Role.CreatorRoleType>? roleTypes = null)
    {
        var roles = new List<Role>();
        var xrefAnimeStaff = RepoFactory.CrossRef_Anime_Staff.GetByAnimeID(animeID);
        foreach (var xref in xrefAnimeStaff)
        {
            // Filter out any roles that are not of the desired type.
            if (roleTypes != null && !roleTypes.Contains((Role.CreatorRoleType)xref.RoleType))
                continue;

            var character = xref.RoleID.HasValue ? RepoFactory.AnimeCharacter.GetByID(xref.RoleID.Value) : null;
            var staff = RepoFactory.AnimeStaff.GetByID(xref.StaffID);
            if (staff == null)
                continue;

            var role = new Role(xref, staff, character);
            roles.Add(role);
        }

        return roles;
    }

    public static List<Tag> GetTags(SVR_AniDB_Anime anime, TagFilter.Filter filter, bool excludeDescriptions = false, bool orderByName = false, bool onlyVerified = true)
    {
        // Only get the user tags if we don't exclude it (false == false), or if we invert the logic and want to include it (true == true).
        IEnumerable<Tag> userTags = new List<Tag>();
        if (filter.HasFlag(TagFilter.Filter.User) == filter.HasFlag(TagFilter.Filter.Invert))
        {
            userTags = RepoFactory.CustomTag.GetByAnimeID(anime.AnimeID)
                .Select(tag => new Tag(tag, excludeDescriptions));
        }

        var selectedTags = anime.GetAniDBTags(onlyVerified)
            .DistinctBy(a => a.TagName)
            .ToList();
        var tagFilter = new TagFilter<AniDB_Tag>(name => RepoFactory.AniDB_Tag.GetByName(name).FirstOrDefault(), tag => tag.TagName,
            name => new AniDB_Tag { TagNameSource = name });
        var anidbTags = tagFilter
            .ProcessTags(filter, selectedTags)
            .Select(tag =>
            {
                var xref = RepoFactory.AniDB_Anime_Tag.GetByTagID(tag.TagID).FirstOrDefault(xref => xref.AnimeID == anime.AnimeID);
                return new Tag(tag, excludeDescriptions) { Weight = xref?.Weight ?? 0, IsLocalSpoiler = xref?.LocalSpoiler };
            });

        if (orderByName)
            return userTags.Concat(anidbTags)
                .OrderByDescending(tag => tag.Source)
                .ThenBy(tag => tag.Name)
                .ToList();

        return userTags.Concat(anidbTags)
            .OrderByDescending(tag => tag.Source)
            .ThenByDescending(tag => tag.Weight)
            .ThenBy(tag => tag.Name)
            .ToList();
    }

    /// <summary>
    /// Auto-matching settings for the series.
    /// </summary>
    public class AutoMatchSettings
    {
        public AutoMatchSettings()
        {
            TvDB = false;
            TMDB = false;
            Trakt = false;
            // MAL = false;
            // AniList = false;
            // Animeshon = false;
            // Kitsu = false;
        }

        public AutoMatchSettings(SVR_AnimeSeries series)
        {
            TvDB = !series.IsTvDBAutoMatchingDisabled;
            TMDB = !series.IsTMDBAutoMatchingDisabled;
            Trakt = !series.IsTraktAutoMatchingDisabled;
            // MAL = !series.IsMALAutoMatchingDisabled;
            // AniList = !series.IsAniListAutoMatchingDisabled;
            // Animeshon = !series.IsAnimeshonAutoMatchingDisabled;
            // Kitsu = !series.IsKitsuAutoMatchingDisabled;
        }

        public AutoMatchSettings MergeWithExisting(SVR_AnimeSeries series)
        {
            series.IsTvDBAutoMatchingDisabled = !TvDB;
            series.IsTMDBAutoMatchingDisabled = !TMDB;
            series.IsTraktAutoMatchingDisabled = !Trakt;
            // series.IsMALAutoMatchingDisabled = !MAL;
            // series.IsAniListAutoMatchingDisabled = !AniList;
            // series.IsAnimeshonAutoMatchingDisabled = !Animeshon;
            // series.IsKitsuAutoMatchingDisabled = !Kitsu;

            RepoFactory.AnimeSeries.Save(series, false, true);

            return new AutoMatchSettings(series);
        }

        /// <summary>
        /// Auto-match against TvDB.
        /// </summary>
        [Required]
        public bool TvDB { get; set; }

        /// <summary>
        /// Auto-match against The Movie Database (TMDB).
        /// </summary>
        [Required]
        public bool TMDB { get; set; }

        /// <summary>
        /// Auto-match against Trakt.
        /// </summary>
        [Required]
        public bool Trakt { get; set; }

        // /// <summary>
        // /// Auto-match against My Anime List (MAL).
        // /// </summary>
        // [Required]
        // public bool MAL { get; set; }

        // /// <summary>
        // /// Auto-match against AniList.
        // /// </summary>
        // [Required]
        // public bool AniList { get; set; }

        // /// <summary>
        // /// Auto-match against Animeshon.
        // /// </summary>
        // [Required]
        // public bool Animeshon { get; set; }

        // /// <summary>
        // /// Auto-match against Kitsu.
        // /// </summary>
        // [Required]
        // public bool Kitsu { get; set; }
    }

    /// <summary>
    /// Basic anidb data across all anidb types.
    /// </summary>
    public class AniDB
    {
        /// <summary>
        /// AniDB ID
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// <see cref="Series"/> ID if the series is available locally.
        /// </summary>
        /// <value></value>
        public int? ShokoID { get; set; }

        /// <summary>
        /// Series type. Series, OVA, Movie, etc
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        public SeriesType Type { get; set; }

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
        [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
        public DateTime? AirDate { get; set; }

        /// <summary>
        /// Indicates when the AniDB anime stopped airing. It will be null if it's still airing or haven't aired yet. In the 'yyyy-MM-dd' format, or null.
        /// </summary>
        [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Restricted content. Mainly porn.
        /// </summary>
        public bool Restricted { get; set; }

        /// <summary>
        /// The preferred poster for the anime.
        /// </summary>
        public Image? Poster { get; set; }

        /// <summary>
        /// Number of <see cref="EpisodeType.Normal"/> episodes contained within the series if it's known.
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


        private AniDB(int animeId, bool includeTitles, SVR_AnimeSeries? series = null, SVR_AniDB_Anime? anime = null, ResponseAniDBTitles.Anime? result = null)
        {
            ID = animeId;
            if ((anime ??= (series is not null ? series.AniDB_Anime : RepoFactory.AniDB_Anime.GetByAnimeID(animeId))) is not null)
            {
                ArgumentNullException.ThrowIfNull(anime);
                series ??= RepoFactory.AnimeSeries.GetByAnimeID(animeId);
                var seriesTitle = series?.PreferredTitle ?? anime.PreferredTitle;
                ShokoID = series?.AnimeSeriesID;
                Type = anime.AnimeType.ToAniDBSeriesType();
                Title = seriesTitle;
                Titles = includeTitles
                    ? anime.Titles.Select(title => new Title(title, anime.MainTitle, seriesTitle)).ToList()
                    : null;
                Description = anime.Description;
                Restricted = anime.Restricted == 1;
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
                AirDate = anime.AirDate;
                EndDate = anime.EndDate;
            }
            else if ((result ??= TitleHelper.SearchAnimeID(animeId)) is not null)
            {
                Type = SeriesType.Unknown;
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
                Type = SeriesType.Unknown;
                Title = string.Empty;
                Titles = includeTitles ? [] : null;
                Poster = new Image(animeId, ImageEntityType.Poster, DataSourceType.AniDB);
            }
        }

        public AniDB(SVR_AniDB_Anime anime, SVR_AnimeSeries? series = null, bool includeTitles = true)
            : this(anime.AnimeID, includeTitles, series, anime) { }

        public AniDB(ResponseAniDBTitles.Anime result, SVR_AnimeSeries? series = null, bool includeTitles = true)
            : this(result.AnimeID, includeTitles, series) { }

        public AniDB(SVR_AniDB_Anime_Relation relation, SVR_AnimeSeries? series = null, bool includeTitles = true)
            : this(relation.RelatedAnimeID, includeTitles, series)
        {
            Relation = ((IRelatedMetadata)relation).RelationType;
            // If the other anime is present we assume they're of the same kind. Be it restricted or unrestricted.
            if (Type == SeriesType.Unknown && TitleHelper.SearchAnimeID(relation.RelatedAnimeID) is not null)
                Restricted = RepoFactory.AniDB_Anime.GetByAnimeID(relation.AnimeID) is { Restricted: 1 };
        }

        public AniDB(AniDB_Anime_Similar similar, SVR_AnimeSeries? series = null, bool includeTitles = true)
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

    /// <summary>
    /// The result entries for the "Recommended For You" algorithm.
    /// </summary>
    public class AniDBRecommendedForYou
    {
        /// <summary>
        /// The recommended AniDB entry.
        /// </summary>
        public AniDB Anime { get; init; }

        /// <summary>
        /// Number of similar anime that resulted in this recommendation.
        /// </summary>
        public int SimilarTo { get; init; }

        public AniDBRecommendedForYou(AniDB anime, int similarCount)
        {
            Anime = anime;
            SimilarTo = similarCount;
        }
    }

    /// <summary>
    /// The TvDB Data model for series
    /// </summary>
    public class TvDB
    {
        /// <summary>
        /// TvDB ID
        /// </summary>
        [Required]
        public int ID { get; set; }

        /// <summary>
        /// Air date.
        /// </summary>
        [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
        public DateTime? AirDate { get; set; }

        /// <summary>
        /// End date, can be null. Null means that it's still airing (2013-02-27)
        /// </summary>
        [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// TvDB only supports one title
        /// </summary>
        [Required]
        public string Title { get; set; }

        /// <summary>
        /// Description
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// TvDB Season. This value is not guaranteed to be even kind of accurate
        /// TvDB matchings and links affect this. Null means no match. 0 means specials
        /// </summary>
        public int? Season { get; set; }

        /// <summary>
        /// Posters
        /// </summary>
        public List<Image> Posters { get; set; }

        /// <summary>
        /// Fanarts
        /// </summary>
        public List<Image> Fanarts { get; set; }

        /// <summary>
        /// Banners
        /// </summary>
        public List<Image> Banners { get; set; }

        /// <summary>
        /// The rating object
        /// </summary>
        public Rating? Rating { get; set; }

        public TvDB(TvDB_Series tvdbSeries, IReadOnlyList<SVR_AnimeEpisode> episodeList)
        {
            var images = tvdbSeries.GetImages().ToDto();
            var firstEp = episodeList
                .FirstOrDefault(a =>
                    a.AniDB_Episode is not null &&
                    a.AniDB_Episode.EpisodeTypeEnum is InternalEpisodeType.Episode &&
                    a.AniDB_Episode.EpisodeNumber == 1)
                ?.TvDBEpisode;
            var lastEp = episodeList
                .Where(a => a.AniDB_Episode is not null && a.AniDB_Episode.EpisodeTypeEnum is InternalEpisodeType.Episode)
                .OrderBy(a => a.AniDB_Episode!.EpisodeType)
                .ThenBy(a => a.AniDB_Episode!.EpisodeNumber)
                .LastOrDefault()
                ?.TvDBEpisode;
            ID = tvdbSeries.SeriesID;
            Description = tvdbSeries.Overview;
            Title = tvdbSeries.SeriesName;
            Posters = images.Posters;
            Fanarts = images.Backdrops;
            Banners = images.Banners;
            Season = firstEp?.SeasonNumber;
            AirDate = firstEp?.AirDate;
            EndDate = lastEp?.AirDate;
            if (tvdbSeries.Rating is not null)
                Rating = new Rating { Source = "TvDB", Value = tvdbSeries.Rating.Value, MaxValue = 10 };
        }
    }

    public class SeriesIDs : IDs
    {
        #region Groups

        /// <summary>
        /// The ID of the direct parent group, if it has one.
        /// </summary>
        public int ParentGroup { get; set; }

        /// <summary>
        /// The ID of the top-level (ancestor) group this series belongs to.
        /// </summary>
        public int TopLevelGroup { get; set; }

        #endregion

        #region XRefs

        // These are useful for many things, but for clients, it is mostly auxiliary

        /// <summary>
        /// The AniDB ID
        /// </summary>
        [Required]
        public int AniDB { get; set; }

        /// <summary>
        /// The TvDB IDs
        /// </summary>
        public List<int> TvDB { get; set; } = [];

        /// <summary>
        /// The IMDB Movie IDs.
        /// </summary>
        public List<string> IMDB { get; set; } = [];

        /// <summary>
        /// The Movie Database (TMDB) IDs.
        /// </summary>
        public TmdbSeriesIDs TMDB { get; set; } = new();

        /// <summary>
        /// The MyAnimeList IDs
        /// </summary>
        public List<int> MAL { get; set; } = [];

        /// <summary>
        /// The TraktTv IDs
        /// </summary>
        public List<string> TraktTv { get; set; } = [];

        #endregion

        public class TmdbSeriesIDs
        {
            public List<int> Movie { get; init; } = [];

            public List<int> Show { get; init; } = [];
        }
    }

    public class TmdbData
    {
        public IEnumerable<TmdbMovie> Movies { get; init; } = [];

        public IEnumerable<TmdbShow> Shows { get; init; } = [];
    }

    #region Inputs

    public static class Input
    {
        public class LinkCommonBody
        {
            /// <summary>
            /// Provider ID to add.
            /// </summary>
            [Required, Range(1, int.MaxValue)]
            public int ID { get; set; }

            /// <summary>
            /// Replace all existing links.
            /// </summary>
            public bool Replace { get; set; } = false;

            /// <summary>
            /// Forcefully refresh metadata even if we recently did a refresh.
            /// </summary>
            public bool Refresh { get; set; } = false;
        }

        public class LinkShowBody : LinkCommonBody
        {
        }

        public class LinkMovieBody : LinkCommonBody
        {
            /// <summary>
            /// Also link to the given AniDB episode by ID.
            /// </summary>
            [Required, Range(1, int.MaxValue)]
            public int EpisodeID { get; set; }
        }

        public class UnlinkCommonBody
        {
            /// <summary>
            /// Provider ID to remove.
            /// </summary>
            [DefaultValue(0)]
            [Range(0, int.MaxValue)]
            public int ID { get; set; }

            /// <summary>
            /// Purge the provider metadata from the database.
            /// </summary>
            public bool Purge { get; set; } = false;
        }

        public class UnlinkMovieBody : UnlinkCommonBody
        {
            /// <summary>
            /// Only unlink to the given AniDB episode by ID.
            /// </summary>
            [DefaultValue(0)]
            [Range(0, int.MaxValue)]
            public int EpisodeID { get; set; }
        }

        public class OverrideEpisodeMappingBody
        {
            /// <summary>
            /// Reset all existing links.
            /// </summary>
            public bool ResetAll { get; set; } = false;

            /// <summary>
            /// Replacing existing links or add new additional links.
            /// </summary>
            [Required]
            public IReadOnlyList<OverrideEpisodeLinkBody> Mapping { get; set; } = [];
        }

        public class OverrideEpisodeLinkBody
        {
            /// <summary>
            /// AniDB Episode ID.
            /// </summary>
            [Required, Range(1, int.MaxValue)]
            public int AniDBID { get; set; }

            /// <summary>
            /// TMDB Episode ID.
            /// </summary>
            [Required, Range(0, int.MaxValue)]
            public int TmdbID { get; set; }

            /// <summary>
            /// Replace existing episode links.
            /// </summary>
            public bool Replace { get; set; } = false;
        }

        public class TitleOverrideBody
        {
            /// <summary>
            /// New title to be set as override for the series
            /// </summary>
            [Required(AllowEmptyStrings = true)]
            public string Title { get; set; } = string.Empty;
        }

        public class AddOrRemoveUserTagsBody
        {
            /// <summary>
            /// User Tag IDs to add/remove from the series.
            /// </summary>
            [Required]
            [MinLength(1)]
            public int[] IDs { get; set; } = [];
        }
    }

    #endregion

    /// <summary>
    /// An Extended Series Model with Values for Search Results
    /// </summary>
    public class SearchResult : Series
    {
        /// <summary>
        /// Indicates whether the search result is an exact match to the query.
        /// </summary>
        public bool ExactMatch { get; set; }

        /// <summary>
        /// Represents the position of the match within the sanitized string.
        /// This property is only applicable when ExactMatch is set to true.
        /// A lower value indicates a match that occurs earlier in the string.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Represents the similarity measure between the sanitized query and the sanitized matched result.
        /// This may be the sorensen-dice distance or the tag weight when comparing tags for a series.
        /// A lower value indicates a more similar match.
        /// </summary>
        public double Distance { get; set; }

        /// <summary>
        /// Represents the absolute difference in length between the sanitized query and the sanitized matched result.
        /// A lower value indicates a match with a more similar length to the query.
        /// </summary>
        public int LengthDifference { get; set; }

        /// <summary>
        /// Contains the original matched substring from the original string.
        /// </summary>
        public string Match { get; set; } = string.Empty;

        public SearchResult(SeriesSearch.SearchResult<SVR_AnimeSeries> result, int userId = 0, bool randomizeImages = false, HashSet<DataSource>? includeDataFrom = null)
            : base(result.Result, userId, randomizeImages, includeDataFrom)
        {
            ExactMatch = result.ExactMatch;
            Index = result.Index;
            Distance = result.Distance;
            LengthDifference = result.LengthDifference;
            Match = result.Match;
        }
    }

    /// <summary>
    /// An extended model for use with the soft duplicate endpoint.
    /// </summary>
    public class WithMultipleReleasesResult : Series
    {
        /// <summary>
        /// Number of episodes in the series which have multiple releases.
        /// </summary>
        public int EpisodeCount { get; set; }

        public WithMultipleReleasesResult(SVR_AnimeSeries ser, int userId = 0, HashSet<DataSource>? includeDataFrom = null, bool ignoreVariations = true)
            : base(ser, userId, false, includeDataFrom)
        {
            EpisodeCount = RepoFactory.AnimeEpisode.GetWithMultipleReleases(ignoreVariations, ser.AniDB_ID).Count;
        }
    }
}

public enum SeriesType
{
    /// <summary>
    /// The series type is unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// A catch-all type for future extensions when a provider can't use a current episode type, but knows what the future type should be.
    /// </summary>
    Other = 1,

    /// <summary>
    /// Standard TV series.
    /// </summary>
    TV = 2,

    /// <summary>
    /// TV special.
    /// </summary>
    TVSpecial = 3,

    /// <summary>
    /// Web series.
    /// </summary>
    Web = 4,

    /// <summary>
    /// All movies, regardless of source (e.g. web or theater)
    /// </summary>
    Movie = 5,

    /// <summary>
    /// Original Video Animations, AKA standalone releases that don't air on TV or the web.
    /// </summary>
    OVA = 6
}

/// <summary>
/// Downloaded, Watched, Total, etc
/// </summary>
public class SeriesSizes
{
    public SeriesSizes() : base()
    {
        Hidden = 0;
        FileSources = new FileSourceCounts();
        Local = new EpisodeTypeCounts();
        Watched = new EpisodeTypeCounts();
        Missing = new ReducedEpisodeTypeCounts();
        Total = new EpisodeTypeCounts();
    }

    /// <summary>
    /// Count of hidden episodes, be it available or missing.
    /// </summary>
    public int Hidden { get; set; }

    /// <summary>
    /// Counts of each file source type available within the local collection
    /// </summary>
    [Required]
    public FileSourceCounts FileSources { get; set; }

    /// <summary>
    /// What is downloaded and available
    /// </summary>
    [Required]
    public EpisodeTypeCounts Local { get; set; }

    /// <summary>
    /// What is local and watched.
    /// </summary>
    public EpisodeTypeCounts Watched { get; set; }

    /// <summary>
    /// Count of missing episodes that are not hidden.
    /// </summary>
    public ReducedEpisodeTypeCounts Missing { get; set; }

    /// <summary>
    /// Total count of each type
    /// </summary>
    [Required]
    public EpisodeTypeCounts Total { get; set; }

    /// <summary>
    /// Lists the count of each type of episode.
    /// </summary>
    public class ReducedEpisodeTypeCounts
    {
        public int Episodes { get; set; }

        public int Specials { get; set; }
    }

    /// <summary>
    /// Lists the count of each type of episode.
    /// </summary>
    public class EpisodeTypeCounts
    {
        public int Unknown { get; set; }

        public int Episodes { get; set; }

        public int Specials { get; set; }

        public int Credits { get; set; }

        public int Trailers { get; set; }

        public int Parodies { get; set; }

        public int Others { get; set; }
    }

    public class FileSourceCounts
    {
        public int Unknown { get; set; }

        public int Other { get; set; }

        public int TV { get; set; }

        public int DVD { get; set; }

        public int BluRay { get; set; }

        public int Web { get; set; }

        public int VHS { get; set; }

        public int VCD { get; set; }

        public int LaserDisc { get; set; }

        public int Camera { get; set; }
    }
}
