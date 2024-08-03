using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Server.API.Converters;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

using AniDBEpisodeType = Shoko.Models.Enums.EpisodeType;
using AniDBAnimeType = Shoko.Models.Enums.AnimeType;
using RelationType = Shoko.Plugin.Abstractions.DataModels.RelationType;
using DataSource = Shoko.Server.API.v3.Models.Common.DataSource;

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
    /// The default or random pictures for a series. This allows the client to not need to get all images and pick one.
    /// There should always be a poster, but no promises on the rest.
    /// </summary>
    public Images Images { get; set; }

    /// <summary>
    /// the user's rating
    /// </summary>
    public Rating UserRating { get; set; }

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

    /// <summary>
    /// The <see cref="Series.AniDB"/>, if <see cref="DataSource.AniDB"/> is
    /// included in the data to add.
    /// </summary>
    [JsonProperty("AniDB", NullValueHandling = NullValueHandling.Ignore)]
    public AniDB _AniDB { get; set; }

    /// <summary>
    /// The <see cref="Series.TvDB"/> entries, if <see cref="DataSource.TvDB"/>
    /// is included in the data to add.
    /// </summary>
    [JsonProperty("TvDB", NullValueHandling = NullValueHandling.Ignore)]
    public IEnumerable<TvDB> _TvDB { get; set; }

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
        [Required]
        public int ID { get; set; }

        /// <summary>
        /// <see cref="Series"/> ID if the series is available locally.
        /// </summary>
        /// <value></value>
        public int? ShokoID { get; set; }

        /// <summary>
        /// Series type. Series, OVA, Movie, etc
        /// </summary>
        [Required]
        [JsonConverter(typeof(StringEnumConverter))]
        public SeriesType Type { get; set; }

        /// <summary>
        /// Main Title, usually matches x-jat
        /// </summary>
        [Required]
        public string Title { get; set; }

        /// <summary>
        /// There should always be at least one of these, the <see cref="Title"/>.
        /// </summary>
        [Required]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<Title> Titles { get; set; }

        /// <summary>
        /// Description.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Description { get; set; }

        /// <summary>
        /// Air date (2013-02-27, shut up avael). Anything without an air date is going to be missing a lot of info.
        /// </summary>
        [Required]
        [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
        public DateTime? AirDate { get; set; }

        /// <summary>
        /// End date, can be omitted. Omitted means that it's still airing (2013-02-27)
        /// </summary>
        [JsonConverter(typeof(DateFormatConverter), "yyyy-MM-dd")]
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// Restricted content. Mainly porn.
        /// </summary>
        public bool Restricted { get; set; }

        /// <summary>
        /// The main or default poster.
        /// </summary>
        [Required]
        public Image Poster { get; set; }

        /// <summary>
        /// Number of <see cref="EpisodeType.Normal"/> episodes contained within the series if it's known.
        /// </summary>
        public int? EpisodeCount { get; set; }

        /// <summary>
        /// The average rating for the anime.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Rating Rating { get; set; }

        /// <summary>
        /// User approval rate for the similar submission. Only available for similar.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public Rating UserApproval { get; set; }

        /// <summary>
        /// Relation type. Only available for relations.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public RelationType? Relation { get; set; }
    }

    /// <summary>
    /// The result entries for the "Recommended For You" algorithm.
    /// </summary>
    public class AniDBRecommendedForYou
    {
        /// <summary>
        /// The recommended AniDB entry.
        /// </summary>
        public AniDB Anime;

        /// <summary>
        /// Number of similar anime that resulted in this recommendation.
        /// </summary>
        public int SimilarTo;
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
        /// Air date (2013-02-27, shut up avael)
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
        public Rating Rating { get; set; }
    }

    /// <summary>
    /// A site link, as in hyperlink.
    /// </summary>
    public class Resource
    {
        /// <summary>
        /// Resource type.
        /// </summary>
        [Required]
        public string Type { get; set; }

        /// <summary>
        /// site name
        /// </summary>
        [Required]
        public string Name { get; set; }

        /// <summary>
        /// the url to the series page
        /// </summary>
        [Required]
        public string URL { get; set; }
    }

    #region Inputs

    public static class Input
    {
        public class LinkCommonBody
        {
            /// <summary>
            /// Provider ID to add.
            /// </summary>
            [Required]
            public int ID;

            public bool Replace = false;
        }

        public class UnlinkCommonBody
        {
            /// <summary>
            /// Provider ID to remove.
            /// </summary>
            [Required]
            public int ID;
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
    public List<int> TvDB { get; set; } = new();

    // TODO Support for TvDB string IDs (like in the new URLs) one day maybe

    /// <summary>
    /// The Movie DB IDs
    /// </summary>
    public List<int> TMDB { get; set; } = new();

    /// <summary>
    /// The MyAnimeList IDs
    /// </summary>
    public List<int> MAL { get; set; } = new();

    /// <summary>
    /// The TraktTv IDs
    /// </summary>
    public List<string> TraktTv { get; set; } = new();

    /// <summary>
    /// The AniList IDs
    /// </summary>
    public List<int> AniList { get; set; } = new();

    #endregion
}

/// <summary>
/// An Extended Series Model with Values for Search Results
/// </summary>
public class SeriesSearchResult : Series
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
}

/// <summary>
/// An extended model for use with the soft duplicate endpoint.
/// </summary>
public class SeriesWithMultipleReleasesResult : Series
{
    /// <summary>
    /// Number of episodes in the series which have multiple releases.
    /// </summary>
    public int EpisodeCount { get; set; }
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
    /// Counts of each file source type available within the local colleciton
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
        public int Unknown;
        public int Other;
        public int TV;
        public int DVD;
        public int BluRay;
        public int Web;
        public int VHS;
        public int VCD;
        public int LaserDisc;
        public int Camera;
    }
}

public class SeriesTitleOverride
{
    /// <summary>
    /// New title to be set as override for the series
    /// </summary>
    [Required(AllowEmptyStrings = true)]
    public string Title { get; set; }
}
