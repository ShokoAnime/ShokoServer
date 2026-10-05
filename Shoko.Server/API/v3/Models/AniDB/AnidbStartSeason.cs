using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Services;
using Shoko.Server.Utilities;

using AbstractOverride = Shoko.Abstractions.Metadata.Anidb.Models.AnidbStartSeasonOverride;

namespace Shoko.Server.API.v3.Models.AniDB;

/// <summary>
///   The season an AniDB anime starts in, and whether a user set it by hand.
/// </summary>
public class AnidbStartSeason
{
    /// <summary>
    ///   The year of the season the anime starts in, or <c>null</c> when it
    ///   has no dates to go by and no override.
    /// </summary>
    public required int? Year { get; init; }

    /// <summary>
    ///   The season the anime starts in, or <c>null</c> when it has no dates
    ///   to go by and no override.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public required YearlySeason? Season { get; init; }

    /// <summary>
    ///   Whether a user set the start season by hand.
    /// </summary>
    [Required]
    public required bool IsOverridden { get; init; }

    /// <summary>
    ///   The start season the rule works out on its own, or <c>null</c> when
    ///   the anime is not in the local cache or has no dates to go by.
    /// </summary>
    public required YearAndSeason? Computed { get; init; }

    /// <summary>
    ///   Builds the view of an anime's start season.
    /// </summary>
    /// <param name="computed">The span the rule works out, if any.</param>
    /// <param name="startOverride">The override, if any.</param>
    /// <returns>The view.</returns>
    public static AnidbStartSeason From(SeasonCalendar.SeasonSpan? computed, AbstractOverride? startOverride)
    {
        var start = startOverride is not null ? (startOverride.Year, startOverride.Season) : computed?.StartSeason;
        return new()
        {
            Year = start?.Year,
            Season = start?.Season,
            IsOverridden = startOverride is not null,
            Computed = computed is { StartSeason: var season } ? new(season.Year, season.Season) : null,
        };
    }

    /// <summary>
    ///   A yearly season.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="season">The season.</param>
    public class YearAndSeason(int year, YearlySeason season)
    {
        /// <summary>
        ///   The year.
        /// </summary>
        [Required]
        public int Year { get; } = year;

        /// <summary>
        ///   The season.
        /// </summary>
        [Required, JsonConverter(typeof(StringEnumConverter))]
        public YearlySeason Season { get; } = season;
    }

    /// <summary>
    ///   The body setting an anime's start season.
    /// </summary>
    public class Body
    {
        /// <summary>
        ///   The year, from 1900 to 9999.
        /// </summary>
        [Required, Range(AnidbStartSeasonOverrides.MinimumYear, AnidbStartSeasonOverrides.MaximumYear)]
        public int? Year { get; set; }

        /// <summary>
        ///   The season: <c>Winter</c>, <c>Spring</c>, <c>Summer</c> or
        ///   <c>Fall</c>.
        /// </summary>
        [Required, JsonConverter(typeof(StringEnumConverter))]
        public YearlySeason? Season { get; set; }
    }
}

/// <summary>
///   A start season a user set by hand for an AniDB anime.
/// </summary>
/// <param name="value">The override.</param>
/// <param name="title">The anime's preferred title, if known.</param>
public class AnidbStartSeasonOverride(AbstractOverride value, string? title)
{
    /// <summary>
    ///   The AniDB anime ID.
    /// </summary>
    [Required]
    public int AnidbAnimeID { get; } = value.AnidbAnimeID;

    /// <summary>
    ///   The anime's preferred title, its series' one when it is in the
    ///   collection, or <c>null</c> when the anime is not in the local cache
    ///   or the user may not see it.
    /// </summary>
    public string? Title { get; } = title;

    /// <summary>
    ///   The year of the season the anime starts in.
    /// </summary>
    [Required]
    public int Year { get; } = value.Year;

    /// <summary>
    ///   The season the anime starts in.
    /// </summary>
    [Required, JsonConverter(typeof(StringEnumConverter))]
    public YearlySeason Season { get; } = value.Season;

    /// <summary>
    ///   When the override was first set, in UTC.
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; } = value.CreatedAt;

    /// <summary>
    ///   When the override was last changed, in UTC.
    /// </summary>
    [Required]
    public DateTime UpdatedAt { get; } = value.UpdatedAt;

    /// <summary>
    ///   The ID of the user who last set it, or <c>null</c> when the system
    ///   did.
    /// </summary>
    public int? UserID { get; } = value.UserID;
}

/// <summary>
///   What a CSV import of start season overrides did.
/// </summary>
/// <param name="result">The import's result.</param>
public class AnidbStartSeasonOverrideImportSummary(AnidbStartSeasonOverrideImportResult result)
{
    /// <summary>
    ///   How many overrides were set for anime without one.
    /// </summary>
    [Required]
    public int Added { get; } = result.Added;

    /// <summary>
    ///   How many existing overrides were changed.
    /// </summary>
    [Required]
    public int Updated { get; } = result.Updated;

    /// <summary>
    ///   How many lines matched the override already set.
    /// </summary>
    [Required]
    public int Unchanged { get; } = result.Unchanged;

    /// <summary>
    ///   The lines that could not be imported, in file order.
    /// </summary>
    [Required]
    public List<RejectedLine> Rejected { get; } = [.. result.Rejected.Select(line => new RejectedLine(line))];

    /// <summary>
    ///   A line that could not be imported.
    /// </summary>
    /// <param name="line">The rejected line.</param>
    public class RejectedLine(AnidbStartSeasonOverrideImportResult.RejectedLine line)
    {
        /// <summary>
        ///   The line's number, from <c>1</c>.
        /// </summary>
        [Required]
        public int Line { get; } = line.Line;

        /// <summary>
        ///   The line as it was in the file.
        /// </summary>
        [Required]
        public string Text { get; } = line.Text;

        /// <summary>
        ///   Why it was rejected.
        /// </summary>
        [Required]
        public string Reason { get; } = line.Reason;
    }
}
