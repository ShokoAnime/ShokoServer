using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Models.Enums;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Extensions;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;
using TMDbLib.Objects.Search;

#nullable enable
namespace Shoko.Server.API.v3.Models.TMDB;

/// <summary>
/// APIv3 The Movie DataBase (TMDB) Show Data Transfer Object (DTO)
/// </summary>
public class Show
{
    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int ID { get; init; }

    /// <summary>
    /// Preferred title based upon series title preference.
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// All available titles, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Title>? Titles { get; init; }

    /// <summary>
    /// Preferred overview based upon description preference.
    /// </summary>
    public string Overview { get; init; }

    /// <summary>
    /// All available overviews for the series, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Overview>? Overviews { get; init; }

    /// <summary>
    /// Original language the show was shot in.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public TitleLanguage OriginalLanguage { get; init; }

    /// <summary>
    /// Indicates the show is restricted to an age group above the legal age,
    /// because it's a pornography.
    /// </summary>
    public bool IsRestricted { get; init; }

    /// <summary>
    /// User rating of the show from TMDB users.
    /// </summary>
    public Rating UserRating { get; init; }

    /// <summary>
    /// Genres.
    /// </summary>
    public IReadOnlyList<string> Genres { get; init; }

    /// <summary>
    /// Content ratings for different countries for this show.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<ContentRating>? ContentRatings { get; init; }

    /// <summary>
    /// The production companies (studios) that produced the show.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Studio>? Studios { get; init; }

    /// <summary>
    /// The television networks that aired the show.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Network>? Networks { get; init; }

    /// <summary>
    /// Images associated with the show, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Images? Images { get; init; }

    /// <summary>
    /// The cast that have worked on this show across all episodes and all seasons.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Role>? Cast { get; init; }

    /// <summary>
    /// The crew that have worked on this show across all episodes and all seasons.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Role>? Crew { get; init; }

    /// <summary>
    /// Count of episodes associated with the show.
    /// </summary>
    public int EpisodeCount { get; init; }

    /// <summary>
    /// Count of seasons associated with the show.
    /// </summary>
    public int SeasonCount { get; init; }

    /// <summary>
    /// Count of locally alternate ordering schemes associated with the show.
    /// </summary>
    public int AlternateOrderingCount { get; init; }

    /// <summary>
    /// All available ordering for the show, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<OrderingInformation>? Ordering { get; init; }

    /// <summary>
    /// Show cross-references.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<CrossReference>? CrossReferences { get; init; }

    /// <summary>
    /// The date the first episode aired at, if it is known.
    /// </summary>
    public DateOnly? FirstAiredAt { get; init; }

    /// <summary>
    /// The date the last episode aired at, if it is known.
    /// </summary>
    public DateOnly? LastAiredAt { get; init; }

    /// <summary>
    /// When the local metadata was first created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the local metadata was last updated with new changes from the
    /// remote.
    /// </summary>
    public DateTime LastUpdatedAt { get; init; }

    public Show(TMDB_Show show, IncludeDetails? includeDetails = null, IReadOnlySet<TitleLanguage>? language = null) :
        this(show, null, includeDetails, language)
    { }

    public Show(TMDB_Show show, TMDB_AlternateOrdering? alternateOrdering, IncludeDetails? includeDetails = null, IReadOnlySet<TitleLanguage>? language = null)
    {
        var include = includeDetails ?? default;
        var preferredOverview = show.GetPreferredOverview();
        var preferredTitle = show.GetPreferredTitle();

        ID = show.TmdbShowID;
        Title = preferredTitle!.Value;
        if (include.HasFlag(IncludeDetails.Titles))
            Titles = show.GetAllTitles()
                .ToDto(show.EnglishOverview, preferredTitle, language);

        Overview = preferredOverview!.Value;
        if (include.HasFlag(IncludeDetails.Overviews))
            Overviews = show.GetAllOverviews()
                .ToDto(show.EnglishTitle, preferredOverview, language);
        OriginalLanguage = show.OriginalLanguage;
        IsRestricted = show.IsRestricted;
        UserRating = new()
        {
            Value = (decimal)show.UserRating,
            MaxValue = 10,
            Votes = show.UserVotes,
            Source = "TMDB",
        };
        Genres = show.Genres;
        if (include.HasFlag(IncludeDetails.ContentRatings))
            ContentRatings = show.ContentRatings.ToDto(language);
        if (include.HasFlag(IncludeDetails.Studios))
            Studios = show.TmdbCompanies
                .Select(company => new Studio(company))
                .ToList();
        if (include.HasFlag(IncludeDetails.Networks))
            Networks = show.TmdbNetworks
                .Select(network => new Network(network))
                .ToList();
        if (include.HasFlag(IncludeDetails.Images))
            Images = show.GetImages()
                .ToDto(language);
        if (include.HasFlag(IncludeDetails.Cast))
            Cast = show.Cast
                .Select(cast => new Role(cast))
                .ToList();
        if (include.HasFlag(IncludeDetails.Crew))
            Crew = show.Crew
                .Select(cast => new Role(cast))
                .ToList();
        if (alternateOrdering != null)
        {
            EpisodeCount = alternateOrdering.EpisodeCount;
            SeasonCount = alternateOrdering.SeasonCount;
        }
        else
        {
            EpisodeCount = show.EpisodeCount;
            SeasonCount = show.SeasonCount;
        }
        AlternateOrderingCount = show.AlternateOrderingCount;
        if (include.HasFlag(IncludeDetails.Ordering))
        {
            var ordering = new List<OrderingInformation>
            {
                new(show, alternateOrdering),
            };
            foreach (var altOrder in show.TmdbAlternateOrdering)
                ordering.Add(new(altOrder, alternateOrdering));
            Ordering = ordering
                .OrderByDescending(o => o.InUse)
                .ThenByDescending(o => string.IsNullOrEmpty(o.OrderingID))
                .ThenBy(o => o.OrderingName)
                .ToList();
        }
        if (include.HasFlag(IncludeDetails.CrossReferences))
            CrossReferences = show.CrossReferences
                .Select(xref => new CrossReference(xref))
                .OrderBy(xref => xref.AnidbAnimeID)
                .ThenBy(xref => xref.TmdbShowID)
                .ToList();
        FirstAiredAt = show.FirstAiredAt;
        LastAiredAt = show.LastAiredAt;
        CreatedAt = show.CreatedAt.ToUniversalTime();
        LastUpdatedAt = show.LastUpdatedAt.ToUniversalTime();
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

    public class OrderingInformation
    {
        public string? OrderingID { get; init; }

        public AlternateOrderingType? OrderingType { get; init; }

        public string OrderingName { get; init; }

        public int EpisodeCount { get; init; }

        public int SeasonCount { get; init; }

        public bool InUse { get; init; }

        public OrderingInformation(TMDB_Show show, TMDB_AlternateOrdering? alternateOrderingInUse)
        {
            OrderingID = null;
            OrderingName = "Seasons";
            OrderingType = null;
            EpisodeCount = show.EpisodeCount;
            SeasonCount = show.SeasonCount;
            InUse = alternateOrderingInUse == null;
        }

        public OrderingInformation(TMDB_AlternateOrdering ordering, TMDB_AlternateOrdering? alternateOrderingInUse)
        {
            OrderingID = ordering.TmdbEpisodeGroupCollectionID;
            OrderingName = ordering.EnglishTitle;
            OrderingType = ordering.Type;
            EpisodeCount = ordering.EpisodeCount;
            SeasonCount = ordering.SeasonCount;
            InUse = alternateOrderingInUse != null &&
                string.Equals(ordering.TmdbEpisodeGroupCollectionID, alternateOrderingInUse.TmdbEpisodeGroupCollectionID);
        }
    }

    /// <summary>
    /// APIv3 The Movie DataBase (TMDB) Show Cross-Reference Data Transfer Object (DTO).
    /// </summary>
    public class CrossReference
    {
        /// <summary>
        /// AniDB Anime ID.
        /// </summary>
        public int AnidbAnimeID { get; init; }

        /// <summary>
        /// TMDB Show ID.
        /// </summary>
        public int TmdbShowID { get; init; }

        /// <summary>
        /// The match rating.
        /// </summary>
        public string Rating { get; init; }

        public CrossReference(CrossRef_AniDB_TMDB_Show xref)
        {
            AnidbAnimeID = xref.AnidbAnimeID;
            TmdbShowID = xref.TmdbShowID;
            Rating = xref.Source != CrossRefSource.User ? "User" : "Automatic";
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
        Studios = 128,
        Networks = 256,
        ContentRatings = 512,
    }
}
