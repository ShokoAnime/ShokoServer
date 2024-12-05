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
    /// TvDB Show ID, if available.
    /// </summary>
    public int? TvdbID { get; init; }

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
    public string OriginalLanguage { get; init; }

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
    /// Keywords.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<string>? Keywords { get; init; }

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
    /// Production countries.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyDictionary<string, string>? ProductionCountries { get; init; }

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
        TvdbID = show.TvdbShowID;
        Title = preferredTitle!.Value;
        if (include.HasFlag(IncludeDetails.Titles))
            Titles = show.GetAllTitles()
                .ToDto(show.EnglishOverview, preferredTitle, language);

        Overview = preferredOverview!.Value;
        if (include.HasFlag(IncludeDetails.Overviews))
            Overviews = show.GetAllOverviews()
                .ToDto(show.EnglishTitle, preferredOverview, language);
        OriginalLanguage = show.OriginalLanguageCode;
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
                .ToDto(language, preferredPoster: show.DefaultPoster, preferredBackdrop: show.DefaultBackdrop);
        if (include.HasFlag(IncludeDetails.Cast))
            Cast = (alternateOrdering is null ? show.Cast : alternateOrdering.Cast)
                .Select(cast => new Role(cast))
                .ToList();
        if (include.HasFlag(IncludeDetails.Crew))
            Crew = (alternateOrdering is null ? show.Crew : alternateOrdering.Crew)
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
                ordering.Add(new(show, altOrder, alternateOrdering));
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
        if (include.HasFlag(IncludeDetails.Keywords))
            Keywords = show.Keywords;
        if (include.HasFlag(IncludeDetails.ProductionCountries))
            ProductionCountries = show.ProductionCountries
                .ToDictionary(country => country.CountryCode, country => country.CountryName);
        FirstAiredAt = show.FirstAiredAt;
        LastAiredAt = show.LastAiredAt;
        CreatedAt = show.CreatedAt.ToUniversalTime();
        LastUpdatedAt = show.LastUpdatedAt.ToUniversalTime();
    }

    public class OrderingInformation
    {
        /// <summary>
        /// The ordering ID.
        /// </summary>
        public string OrderingID { get; init; }

        /// <summary>
        /// The alternate ordering type. Will not be set if the main ordering is
        /// used.
        /// </summary>
        public AlternateOrderingType? OrderingType { get; init; }

        /// <summary>
        /// English name of the ordering scheme.
        /// </summary>
        public string OrderingName { get; init; }

        /// <summary>
        /// The number of episodes in the ordering scheme.
        /// </summary>
        public int EpisodeCount { get; init; }

        /// <summary>
        /// The number of seasons in the ordering scheme.
        /// </summary>
        public int SeasonCount { get; init; }

        /// <summary>
        /// Indicates the current ordering is the default ordering for the show.
        /// </summary>
        public bool IsDefault { get; init; }

        /// <summary>
        /// Indicates the current ordering is the preferred ordering for the show.
        /// </summary>
        public bool IsPreferred { get; init; }

        /// <summary>
        /// Indicates the current ordering is in use for the show.
        /// </summary>
        public bool InUse { get; init; }

        public OrderingInformation(TMDB_Show show, TMDB_AlternateOrdering? alternateOrderingInUse)
        {
            OrderingID = show.Id.ToString();
            OrderingName = "Seasons";
            OrderingType = null;
            EpisodeCount = show.EpisodeCount;
            SeasonCount = show.SeasonCount;
            IsDefault = true;
            IsPreferred = string.IsNullOrEmpty(show.PreferredAlternateOrderingID) || string.Equals(show.Id.ToString(), show.PreferredAlternateOrderingID);
            InUse = alternateOrderingInUse == null;
        }

        public OrderingInformation(TMDB_Show show, TMDB_AlternateOrdering ordering, TMDB_AlternateOrdering? alternateOrderingInUse)
        {
            OrderingID = ordering.TmdbEpisodeGroupCollectionID;
            OrderingName = ordering.EnglishTitle;
            OrderingType = ordering.Type;
            EpisodeCount = ordering.EpisodeCount;
            SeasonCount = ordering.SeasonCount;
            IsDefault = false;
            IsPreferred = string.Equals(ordering.TmdbEpisodeGroupCollectionID, show.PreferredAlternateOrderingID);
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
            Rating = xref.Source is CrossRefSource.User ? "User" : "Automatic";
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
        Keywords = 1024,
        ProductionCountries = 2048,
    }
}
