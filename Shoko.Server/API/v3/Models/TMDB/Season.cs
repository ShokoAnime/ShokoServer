
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.API.v3.Models.Common;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.API.v3.Models.TMDB;

/// <summary>
/// APIv3 The Movie DataBase (TMDB) Season Data Transfer Object (DTO).
/// </summary>
public class Season
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
    /// The alternate ordering this season is associated with. Will be null
    /// for main series seasons.
    /// </summary>
    public string? AlternateOrderingID { get; init; }

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
    /// The season number for the main ordering or alternate ordering in use.
    /// </summary>
    public int SeasonNumber { get; init; }

    /// <summary>
    /// Count of episodes associated with the season.
    /// </summary>
    public int EpisodeCount { get; init; }

    /// <summary>
    /// Indicates the alternate ordering season is locked. Will not be set if
    /// <seealso cref="AlternateOrderingID"/> is not set.
    /// </summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public bool? IsLocked { get; init; }

    /// <summary>
    /// When the local metadata was first created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the local metadata was last updated with new changes from the
    /// remote.
    /// </summary>
    public DateTime LastUpdatedAt { get; init; }

    public Season(TMDB_Season season, IncludeDetails? includeDetails = null, IReadOnlySet<TitleLanguage>? language = null)
    {
        var include = includeDetails ?? default;
        var preferredOverview = season.GetPreferredOverview();
        var preferredTitle = season.GetPreferredTitle();

        ID = season.TmdbSeasonID.ToString();
        ShowID = season.TmdbShowID;
        AlternateOrderingID = null;
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
                .ToDto(language);
        if (include.HasFlag(IncludeDetails.Cast))
            Cast = season.GetCast()
                .Select(cast => new Role(cast))
                .ToList();
        if (include.HasFlag(IncludeDetails.Crew))
            Crew = season.GetCrew()
                .Select(crew => new Role(crew))
                .ToList();
        SeasonNumber = season.SeasonNumber;
        EpisodeCount = season.EpisodeCount;
        IsLocked = null;
        CreatedAt = season.CreatedAt.ToUniversalTime();
        LastUpdatedAt = season.LastUpdatedAt.ToUniversalTime();
    }

    public Season(TMDB_AlternateOrdering_Season season, IncludeDetails? includeDetails = null)
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
        SeasonNumber = season.SeasonNumber;
        EpisodeCount = season.EpisodeCount;
        IsLocked = season.IsLocked;
        CreatedAt = season.CreatedAt.ToUniversalTime();
        LastUpdatedAt = season.LastUpdatedAt.ToUniversalTime();
    }

    [Flags]
    [JsonConverter(typeof(StringEnumConverter))]
    public enum IncludeDetails
    {
        None = 0,
        Titles = 1,
        Overviews = 2,
        Images = 4,
        Cast = 8,
        Crew = 16,
    }
}
