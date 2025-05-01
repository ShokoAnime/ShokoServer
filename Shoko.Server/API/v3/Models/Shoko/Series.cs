using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.Server;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.AniDB;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.API.v3.Models.TMDB;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

using AniDBVoteType = Shoko.Models.Enums.AniDBVoteType;
using AniDBEpisodeType = Shoko.Models.Enums.EpisodeType;
using DataSource = Shoko.Server.API.v3.Models.Common.DataSource;

#nullable enable
namespace Shoko.Server.API.v3.Models.Shoko;

/// <summary>
/// Series object, stores all of the series info
/// </summary>
public class Series : BaseModel
{

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
    /// Will only be set for series of type <see cref="AnimeType.TV"/> and
    /// <see cref="AnimeType.Web"/>.
    /// </remarks>
    /// <value>Each weekday</value>
    [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
    public List<DayOfWeek> AirsOn { get; set; }

    /// <summary>
    /// The yearly seasons this series belongs to.
    /// </summary>
    public List<YearlySeason> YearlySeasons { get; set; }

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

    /// <summary>
    /// The <see cref="Series.AniDB"/>, if <see cref="DataSource.AniDB"/> is
    /// included in the data to add.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public AnidbAnime? AniDB { get; set; }

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
        var animeType = anime.AbstractAnimeType.ToV3Dto();
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
            TvDB = tmdbShowXRefs.Select(xref => xref.TmdbShow?.TvdbShowID).WhereNotNull().Distinct().ToList(),
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
            MAL = ser.MalCrossReferences.Select(a => a.MALID).Distinct().ToList()
        };
        Links = anime.Resources
            .Select(tuple => new Resource(tuple))
            .ToList();
        Name = ser.PreferredTitle;
        HasCustomName = !string.IsNullOrEmpty(ser.SeriesNameOverride);
        Description = ser.PreferredOverview;
        Images = ser.GetImages().ToDto(preferredImages: true, randomizeImages: randomizeImages);
        AirsOn = animeType == AnimeType.TV || animeType == AnimeType.Web ? GetAirsOnDaysOfWeek(allEpisodes) : [];
        YearlySeasons = anime.Seasons
            .Select(x => new YearlySeason(x.Year, x.Season))
            .ToList();
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
            AniDB = new(anime, ser);
        if (includeDataFrom?.Contains(DataSource.TMDB) ?? false)
            TMDB = new()
            {
                Movies = tmdbMovieXRefs
                    .Select(tmdbEpisodeXref =>
                    {
                        var movie = tmdbEpisodeXref.TmdbMovie;
                        if (movie is not null && (TmdbMetadataService.Instance?.WaitForMovieUpdate(movie.TmdbMovieID) ?? false))
                            movie = RepoFactory.TMDB_Movie.GetByTmdbMovieID(movie.TmdbMovieID);
                        return movie;
                    })
                    .WhereNotNull()
                    .Select(tmdbMovie => new TmdbMovie(tmdbMovie))
                    .ToList(),
                Shows = tmdbShowXRefs
                    .Select(tmdbEpisodeXref =>
                    {
                        var show = tmdbEpisodeXref.TmdbShow;
                        if (show is not null && (TmdbMetadataService.Instance?.WaitForShowUpdate(show.TmdbShowID) ?? false))
                            show = RepoFactory.TMDB_Show.GetByTmdbShowID(show.TmdbShowID);
                        return show;
                    })
                    .WhereNotNull()
                    .Select(show => new TmdbShow(show, show.PreferredAlternateOrdering))
                    .ToList(),
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
                var airDate = aniDB?.GetAirDateAsDate();
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
    public static List<Role> GetCast(int animeID, HashSet<CreatorRoleType>? roleTypes = null)
    {
        var roles = new List<Role>();
        if (roleTypes == null || roleTypes.Contains(CreatorRoleType.Actor))
        {
            var characterXrefs = RepoFactory.AniDB_Anime_Character.GetByAnimeID(animeID);
            foreach (var xref in characterXrefs.OrderBy(x => x.Ordering))
            {
                if (xref.Character is not { } character)
                    continue;

                if (character.Type is CharacterType.Organization)
                    roles.Add(new(xref, character));
                else
                    foreach (var creator in xref.Creators)
                        roles.Add(new(xref, character, creator));
            }
        }

        var staff = RepoFactory.AniDB_Anime_Staff.GetByAnimeID(animeID);
        foreach (var xref in staff.OrderBy(x => x.Ordering))
        {
            // Filter out any roles that are not of the desired type.
            if (roleTypes != null && !roleTypes.Contains(xref.RoleType))
                continue;

            if (xref.Creator is not { } creator)
                continue;

            roles.Add(new(xref, creator));
        }

        return roles;
    }

    public static List<Tag> GetTags(
        SVR_AniDB_Anime anime,
        TagFilter.Filter filter,
        bool excludeDescriptions = false,
        bool orderByName = false,
        bool onlyVerified = true,
        bool includeCount = false)
    {
        // Only get the user tags if we don't exclude it (false == false), or if we invert the logic and want to include it (true == true).
        IEnumerable<Tag> userTags = new List<Tag>();
        if (filter.HasFlag(TagFilter.Filter.User) == filter.HasFlag(TagFilter.Filter.Invert))
        {
            userTags = RepoFactory.CustomTag.GetByAnimeID(anime.AnimeID)
                .Select(tag => new Tag(tag, excludeDescriptions, includeCount ? RepoFactory.CrossRef_CustomTag.GetByCustomTagID(tag.CustomTagID).Count : null));
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
                int? count = includeCount ? RepoFactory.AniDB_Anime_Tag.GetByTagID(tag.TagID).Count : null;
                return new Tag(tag, excludeDescriptions, count) { Weight = xref?.Weight ?? 0, IsLocalSpoiler = xref?.LocalSpoiler };
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
            TMDB = false;
            Trakt = false;
            // MAL = false;
            // AniList = false;
            // Animeshon = false;
            // Kitsu = false;
        }

        public AutoMatchSettings(SVR_AnimeSeries series)
        {
            TMDB = !series.IsTMDBAutoMatchingDisabled;
            Trakt = !series.IsTraktAutoMatchingDisabled;
            // MAL = !series.IsMALAutoMatchingDisabled;
            // AniList = !series.IsAniListAutoMatchingDisabled;
            // Animeshon = !series.IsAnimeshonAutoMatchingDisabled;
            // Kitsu = !series.IsKitsuAutoMatchingDisabled;
        }

        public AutoMatchSettings MergeWithExisting(SVR_AnimeSeries series)
        {
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

        /// <summary>
        /// Body for auto-matching AniDB episodes to TMDB episodes.
        /// </summary>
        public class AutoMatchTmdbEpisodesBody
        {
            /// <summary>
            /// The specified TMDB Show ID to search for links. This parameter is used to select a specific show.
            /// </summary>
            [Range(1, int.MaxValue)]
            public int? TmdbShowID { get; set; }

            /// <summary>
            /// The specified TMDB Season ID to search for links. If not provided, links are searched for any season of the selected or first linked show.
            /// </summary>
            [Range(1, int.MaxValue)]
            public int? TmdbSeasonID { get; set; }

            /// <summary>
            /// Determines whether to retain existing links for the current series.
            /// </summary>
            [DefaultValue(true)]
            public bool KeepExisting { get; set; } = true;

            /// <summary>
            /// Determines whether to consider existing links for other series when picking episodes.
            /// </summary>
            public bool? ConsiderExistingOtherLinks { get; set; }
        }

        public class OverrideTmdbEpisodeMappingBody
        {
            /// <summary>
            /// Unset all existing links before applying the overrides.
            /// </summary>
            /// <remarks>
            /// This will ensure the auto-links won't override the new unset
            /// links, unlink if you had reset them through the DELETE endpoint.
            /// </remarks>
            public bool UnsetAll { get; set; } = false;

            /// <summary>
            /// Replacing existing links or add new additional links.
            /// </summary>
            [Required]
            public IReadOnlyList<OverrideTmdbEpisodeLinkBody> Mapping { get; set; } = [];
        }

        public class OverrideTmdbEpisodeLinkBody
        {
            /// <summary>
            /// AniDB Episode ID.
            /// </summary>
            [Required, Range(1, int.MaxValue)]
            public int AniDBID { get; set; }

            /// <summary>
            /// TMDB Episode ID. Set to <c>0</c> to not link to any episode.
            /// </summary>
            [Required, Range(0, int.MaxValue)]
            public int TmdbID { get; set; }

            /// <summary>
            /// Replace existing episode links.
            /// </summary>
            public bool Replace { get; set; } = false;

            /// <summary>
            /// Episode index. Set to <c>null</c> to automatically calculate the
            /// index.
            /// </summary>
            [Range(0, int.MaxValue)]
            public int? Index { get; set; } = null;
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
    public class WithEpisodeCount : Series
    {
        /// <summary>
        /// Number of episodes in the series which have multiple releases.
        /// </summary>
        public int EpisodeCount { get; set; }

        public WithEpisodeCount(int episodeCount, SVR_AnimeSeries ser, int userId = 0, HashSet<DataSource>? includeDataFrom = null)
            : base(ser, userId, false, includeDataFrom)
        {
            EpisodeCount = episodeCount;
        }
    }
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
