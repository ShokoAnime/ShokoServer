using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;

using MatchRatingEnum = Shoko.Models.Enums.MatchRating;

#nullable enable
namespace Shoko.Server.API.v3.Models.TMDB;

/// <summary>
/// APIv3 The Movie DataBase (TMDB) Episode Data Transfer Object (DTO).
/// </summary>
public class Episode
{
    /// <summary>
    /// TMDB Episode ID.
    /// </summary>
    public int ID { get; init; }

    /// <summary>
    /// TMDB Season ID.
    /// </summary>
    public string SeasonID { get; init; }

    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int ShowID { get; init; }

    /// <summary>
    /// TVDB Episode ID, if available.
    /// </summary>
    public int? TvdbEpisodeID { get; init; }

    /// <summary>
    /// Preferred title based upon episode title preference.
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// All available titles for the episode, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Title>? Titles { get; init; }

    /// <summary>
    /// Preferred overview based upon episode title preference.
    /// </summary>
    public string Overview { get; init; }

    /// <summary>
    /// All available overviews for the episode, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Overview>? Overviews { get; init; }

    /// <summary>
    /// The episode number for the main ordering or alternate ordering in use.
    /// </summary>
    public int EpisodeNumber { get; init; }

    /// <summary>
    /// The season number for the main ordering or alternate ordering in use.
    /// </summary>
    public int SeasonNumber { get; init; }

    /// <summary>
    /// User rating of the episode from TMDB users.
    /// </summary>
    public Rating UserRating { get; init; }

    /// <summary>
    /// The episode run-time, if it is known.
    /// </summary>
    public TimeSpan? Runtime { get; init; }

    /// <summary>
    /// All images stored locally for this episode, if any.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Images? Images { get; init; }

    /// <summary>
    /// The cast that have worked on this episode.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Role>? Cast { get; init; }

    /// <summary>
    /// The crew that have worked on this episode.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Role>? Crew { get; init; }

    /// <summary>
    /// All available ordering for the episode, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<OrderingInformation>? Ordering { get; init; }

    /// <summary>
    /// Episode cross-references.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<CrossReference>? CrossReferences { get; init; }

    /// <summary>
    /// The date the episode first aired, if it is known.
    /// </summary>
    public DateOnly? AiredAt { get; init; }

    /// <summary>
    /// When the local metadata was first created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the local metadata was last updated with new changes from the
    /// remote.
    /// </summary>
    public DateTime LastUpdatedAt { get; init; }

    public Episode(TMDB_Episode episode, IncludeDetails? includeDetails = null, IReadOnlySet<TitleLanguage>? language = null) :
        this(episode, null, includeDetails, language)
    { }

    public Episode(TMDB_Episode episode, TMDB_AlternateOrdering_Episode? alternateOrderingEpisode, IncludeDetails? includeDetails = null, IReadOnlySet<TitleLanguage>? language = null)
    {
        var include = includeDetails ?? default;
        var preferredOverview = episode.GetPreferredOverview();
        var preferredTitle = episode.GetPreferredTitle();

        ID = episode.TmdbEpisodeID;
        SeasonID = alternateOrderingEpisode != null
         ? alternateOrderingEpisode.TmdbEpisodeGroupID
         : episode.TmdbSeasonID.ToString();
        ShowID = episode.TmdbShowID;
        TvdbEpisodeID = episode.TvdbEpisodeID;

        Title = preferredTitle!.Value;
        if (include.HasFlag(IncludeDetails.Titles))
            Titles = episode.GetAllTitles()
                .ToDto(episode.EnglishTitle, preferredTitle, language);

        Overview = preferredOverview!.Value;
        if (include.HasFlag(IncludeDetails.Overviews))
            Overviews = episode.GetAllOverviews()
                .ToDto(episode.EnglishOverview, preferredOverview, language);

        if (alternateOrderingEpisode != null)
        {
            EpisodeNumber = alternateOrderingEpisode.EpisodeNumber;
            SeasonNumber = alternateOrderingEpisode.SeasonNumber;
        }
        else
        {
            EpisodeNumber = episode.EpisodeNumber;
            SeasonNumber = episode.SeasonNumber;
        }
        UserRating = new()
        {
            Value = (decimal)episode.UserRating,
            MaxValue = 10,
            Votes = episode.UserVotes,
            Source = "TMDB",
        };
        Runtime = episode.Runtime;
        if (include.HasFlag(IncludeDetails.Images))
            Images = episode.GetImages()
                .InLanguage(language)
                .ToDto(includeThumbnails: true);
        if (include.HasFlag(IncludeDetails.Cast))
            Cast = episode.Cast
                .Select(cast => new Role(cast))
                .ToList();
        if (include.HasFlag(IncludeDetails.Crew))
            Crew = episode.Crew
                .Select(crew => new Role(crew))
                .ToList();
        if (include.HasFlag(IncludeDetails.Ordering))
        {
            var ordering = new List<OrderingInformation>
            {
                new(episode, alternateOrderingEpisode),
            };
            foreach (var altOrderEp in episode.TmdbAlternateOrderingEpisodes)
                ordering.Add(new(altOrderEp, alternateOrderingEpisode));
            Ordering = ordering
                .OrderByDescending(o => o.InUse)
                .ThenByDescending(o => string.IsNullOrEmpty(o.OrderingID))
                .ThenBy(o => o.OrderingName)
                .ToList();
        }
        if (include.HasFlag(IncludeDetails.CrossReferences))
            CrossReferences = episode.CrossReferences
                .Select(xref => new CrossReference(xref))
                .ToList();
        AiredAt = episode.AiredAt;
        CreatedAt = episode.CreatedAt.ToUniversalTime();
        LastUpdatedAt = episode.LastUpdatedAt.ToUniversalTime();
    }

    public class OrderingInformation
    {
        /// <summary>
        /// The ordering ID. Will be null for the main ordering, or a hex id for
        /// any alternate ordering.
        /// </summary>
        public string? OrderingID { get; init; }

        /// <summary>
        /// The alternate ordering type. Will not be set if the main ordering is
        /// used.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore), JsonConverter(typeof(StringEnumConverter))]
        public AlternateOrderingType? OrderingType { get; init; }

        /// <summary>
        /// English name of the alternate ordering scheme.
        /// </summary>
        public string OrderingName { get; init; }

        /// <summary>
        /// The season id. Will be a stringified integer for the main ordering,
        /// or a hex id any alternate ordering.
        /// </summary>
        public string SeasonID { get; init; }

        /// <summary>
        /// English name of the season.
        /// </summary>
        public string SeasonName { get; init; } = string.Empty;

        /// <summary>
        /// The season number for the ordering.
        /// </summary>
        public int SeasonNumber { get; init; }

        /// <summary>
        /// The episode number for the ordering.
        /// </summary>
        public int EpisodeNumber { get; init; }

        /// <summary>
        /// Indicates the current ordering is in use for the episode.
        /// </summary>
        public bool InUse { get; init; }

        public OrderingInformation(TMDB_Episode episode, TMDB_AlternateOrdering_Episode? alternateOrderingEpisodeInUse)
        {
            var season = episode.TmdbSeason;
            OrderingID = null;
            OrderingName = "Seasons";
            OrderingType = null;
            SeasonID = episode.TmdbSeasonID.ToString();
            SeasonName = season?.EnglishTitle ?? "<unknown name>";
            SeasonNumber = episode.SeasonNumber;
            EpisodeNumber = episode.EpisodeNumber;
            InUse = alternateOrderingEpisodeInUse == null;
        }

        public OrderingInformation(TMDB_AlternateOrdering_Episode episode, TMDB_AlternateOrdering_Episode? alternateOrderingEpisodeInUse)
        {
            var ordering = episode.TmdbAlternateOrdering;
            var season = episode.TmdbAlternateOrderingSeason;
            OrderingID = episode.TmdbEpisodeGroupCollectionID;
            OrderingName = ordering?.EnglishTitle ?? "<unknown name>";
            OrderingType = ordering?.Type ?? AlternateOrderingType.Unknown;
            SeasonID = episode.TmdbEpisodeGroupID;
            SeasonName = season?.EnglishTitle ?? "<unknown name>";
            SeasonNumber = episode.SeasonNumber;
            EpisodeNumber = episode.EpisodeNumber;
            InUse = alternateOrderingEpisodeInUse != null &&
                episode.TMDB_AlternateOrdering_EpisodeID == alternateOrderingEpisodeInUse.TMDB_AlternateOrdering_EpisodeID;
        }
    }

    /// <summary>
    /// APIv3 The Movie DataBase (TMDB) Episode Cross-Reference Data Transfer Object (DTO).
    /// </summary>
    public class CrossReference
    {
        /// <summary>
        /// AniDB Anime ID.
        /// </summary>
        public int AnidbAnimeID { get; init; }

        /// <summary>
        /// AniDB Episode ID.
        /// </summary>
        public int AnidbEpisodeID { get; init; }

        /// <summary>
        /// TMDB Show ID.
        /// </summary>
        public int TmdbShowID { get; init; }

        /// <summary>
        /// TMDB Episode ID. May be null if the <see cref="AnidbEpisodeID"/> is
        /// not mapped to a TMDB Episode yet.
        /// </summary>
        public int? TmdbEpisodeID { get; init; }

        /// <summary>
        /// The index to order the cross-references if multiple references
        /// exists for the same anidb or tmdb episode.
        /// </summary>
        public int Index { get; init; }

        /// <summary>
        /// The match rating.
        /// </summary>
        public string Rating { get; init; }

        public CrossReference(CrossRef_AniDB_TMDB_Episode xref)
        {
            AnidbAnimeID = xref.AnidbAnimeID;
            AnidbEpisodeID = xref.AnidbEpisodeID;
            TmdbShowID = xref.TmdbShowID;
            TmdbEpisodeID = xref.TmdbEpisodeID == 0 ? null : xref.TmdbEpisodeID;
            Index = xref.Ordering;
            Rating = "None";
            // NOTE: Internal easter-eggs stays internally.
            if (xref.MatchRating != MatchRatingEnum.SarahJessicaParker)
                Rating = xref.MatchRating.ToString();
        }
    }

    [Flags]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum IncludeDetails
    {
        None = 0,
        Titles = 1,
        Overviews = 2,
        Images = 4,
        Ordering = 8,
        CrossReferences = 16,
        Cast = 32,
        Crew = 64,
    }
}
