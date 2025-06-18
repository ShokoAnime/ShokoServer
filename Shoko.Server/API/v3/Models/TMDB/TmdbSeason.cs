using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Extensions;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.API.v3.Models.TMDB;

/// <summary>
/// APIv3 The Movie DataBase (TMDB) Season Data Transfer Object (DTO).
/// </summary>
public class TmdbSeason
{
    /// <summary>
    /// TMDB Season ID.
    /// </summary>
    public string ID { get; init; }

    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int ShowID { get; init; }

    /// <summary>
    /// The ID of the alternate ordering this season is associated with.
    /// </summary>
    public string AlternateOrderingID { get; init; }

    /// <summary>
    /// Preferred title based upon episode title preference.
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// All available titles for the season, if they should be included.
    /// /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Title>? Titles { get; init; }

    /// <summary>
    /// Preferred overview based upon episode title preference.
    /// </summary>
    public string Overview { get; init; }

    /// <summary>
    /// All available overviews for the season, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Overview>? Overviews { get; init; }

    /// <summary>
    /// Images associated with the season, if they should be included.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Images? Images { get; init; }

    /// <summary>
    /// The cast that have worked on this season across all episodes.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Role>? Cast { get; init; }

    /// <summary>
    /// The crew that have worked on this season across all episodes.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public IReadOnlyList<Role>? Crew { get; init; }

    /// <summary>
    /// The inferred days of the week this season airs on.
    /// </summary>
    /// <value>Each weekday</value>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore, ItemConverterType = typeof(StringEnumConverter))]
    public List<DayOfWeek>? DaysOfWeek { get; set; }

    /// <summary>
    /// The yearly seasons this season belongs to.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<YearlySeason>? YearlySeasons { get; set; }

    /// <summary>
    /// The season number for the main ordering or alternate ordering in use.
    /// </summary>
    public int SeasonNumber { get; init; }

    /// <summary>
    /// Count of episodes associated with the season.
    /// </summary>
    public int EpisodeCount { get; init; }

    /// <summary>
    /// Count of hidden episodes associated with the season.
    /// </summary>
    public int HiddenEpisodeCount { get; init; }

    /// <summary>
    /// Indicates the season is locked for edits in TMDB.
    /// </summary>
    public bool IsLocked { get; init; }

    /// <summary>
    /// When the local metadata was first created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the local metadata was last updated with new changes from the
    /// remote.
    /// </summary>
    public DateTime LastUpdatedAt { get; init; }

    public TmdbSeason(TMDB_Season season, IncludeDetails? includeDetails = null, IReadOnlySet<TitleLanguage>? language = null)
    {
        var include = includeDetails ?? default;
        var preferredOverview = season.GetPreferredOverview();
        var preferredTitle = season.GetPreferredTitle();

        ID = season.TmdbSeasonID.ToString();
        ShowID = season.TmdbShowID;
        AlternateOrderingID = season.TmdbShowID.ToString();
        Title = preferredTitle!.Value;
        if (include.HasFlag(IncludeDetails.Titles))
            Titles = season.GetAllTitles()
                .ToDto(season.EnglishTitle, preferredTitle, language);
        Overview = preferredOverview!.Value;
        if (include.HasFlag(IncludeDetails.Overviews))
            Overviews = season.GetAllOverviews()
                .ToDto(season.EnglishOverview, preferredOverview, language);
        if (include.HasFlag(IncludeDetails.Images))
            Images = season.GetImages()
                .ToDto(language, preferredPoster: season.DefaultPoster);
        if (include.HasFlag(IncludeDetails.Cast))
            Cast = season.Cast
                .Select(cast => new Role(cast))
                .ToList();
        if (include.HasFlag(IncludeDetails.Crew))
            Crew = season.Crew
                .Select(crew => new Role(crew))
                .ToList();
        if (include.HasFlag(IncludeDetails.YearlySeasons))
            YearlySeasons = season.Seasons.ToV3Dto();
        if (include.HasFlag(IncludeDetails.DaysOfWeek))
            DaysOfWeek = season.TmdbEpisodes
                .Select(e => e.AiredAt?.DayOfWeek)
                .WhereNotDefault()
                .Distinct()
                .Order()
                .ToList();
        SeasonNumber = season.SeasonNumber;
        EpisodeCount = season.EpisodeCount;
        HiddenEpisodeCount = season.HiddenEpisodeCount;
        IsLocked = false;
        CreatedAt = season.CreatedAt.ToUniversalTime();
        LastUpdatedAt = season.LastUpdatedAt.ToUniversalTime();
    }

    public TmdbSeason(TMDB_AlternateOrdering_Season season, IncludeDetails? includeDetails = null)
    {
        var include = includeDetails ?? default;

        ID = season.TmdbEpisodeGroupID;
        ShowID = season.TmdbShowID;
        AlternateOrderingID = season.TmdbEpisodeGroupCollectionID;
        Title = season.EnglishTitle;
        if (include.HasFlag(IncludeDetails.Titles))
            Titles = Array.Empty<Title>();
        Overview = string.Empty;
        if (include.HasFlag(IncludeDetails.Overviews))
            Overviews = Array.Empty<Overview>();
        if (include.HasFlag(IncludeDetails.Images))
            Images = new();
        if (include.HasFlag(IncludeDetails.YearlySeasons))
            YearlySeasons = season.Seasons.ToV3Dto();
        if (include.HasFlag(IncludeDetails.DaysOfWeek))
            DaysOfWeek = season.TmdbAlternateOrderingEpisodes
                .Select(e => e.TmdbEpisode?.AiredAt?.DayOfWeek)
                .WhereNotDefault()
                .Distinct()
                .Order()
                .ToList();
        SeasonNumber = season.SeasonNumber;
        EpisodeCount = season.EpisodeCount;
        HiddenEpisodeCount = season.HiddenEpisodeCount;
        IsLocked = season.IsLocked;
        CreatedAt = season.CreatedAt.ToUniversalTime();
        LastUpdatedAt = season.LastUpdatedAt.ToUniversalTime();
    }

    [Flags]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum IncludeDetails
    {
        None = 0,
        Titles = 1 << 0,
        Overviews = 1 << 1,
        Images = 1 << 2,
        Cast = 1 << 3,
        Crew = 1 << 4,
        YearlySeasons = 1 << 5,
        DaysOfWeek = 1 << 6,
    }
}
