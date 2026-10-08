using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Storage;

using MovieCast = TMDbLib.Objects.Movies.Cast;
using TmdbCrew = TMDbLib.Objects.General.Crew;
using TvCast = TMDbLib.Objects.TvShows.Cast;

namespace Shoko.Plugin.Tmdb.Mapping;

/// <summary>
///   Turns TMDB's credits into the core's, and works out a show's and a
///   season's credits from its episodes'.
/// </summary>
/// <remarks>
///   TMDB has no characters of its own, so a cast credit names the person
///   and the role they are credited under. Each credit carries the person's
///   name, so the core keeps a stub for anyone not fetched yet. The language
///   is the work's original one, as TMDB only lists the original cast.
/// </remarks>
public static class TmdbCredits
{
    #region Constants

    /// <summary>
    ///   The notes on a guest star's credit.
    /// </summary>
    public const string GuestStarNotes = "Guest star";

    #endregion

    #region Episodes & Movies

    /// <summary>
    ///   An episode's cast, the regular cast first and its guest stars after.
    /// </summary>
    /// <param name="cast">The regular cast.</param>
    /// <param name="guestStars">The guest stars.</param>
    /// <param name="languageCode">The show's original language.</param>
    /// <returns>The credits, in TMDB's order.</returns>
    public static IReadOnlyList<MetadataCastData> EpisodeCast(IEnumerable<TvCast>? cast, IEnumerable<TvCast>? guestStars, string? languageCode)
        => [
            .. (cast ?? []).Where(credit => credit.Id > 0).Select(credit => Cast(credit.Id, credit.Name, credit.Character, languageCode, null)),
            .. (guestStars ?? []).Where(credit => credit.Id > 0).Select(credit => Cast(credit.Id, credit.Name, credit.Character, languageCode, GuestStarNotes)),
        ];

    /// <summary>
    ///   A movie's cast.
    /// </summary>
    /// <param name="cast">The cast.</param>
    /// <param name="languageCode">The movie's original language.</param>
    /// <returns>The credits, in TMDB's order.</returns>
    public static IReadOnlyList<MetadataCastData> MovieCast(IEnumerable<MovieCast>? cast, string? languageCode)
        => [.. (cast ?? []).Where(credit => credit.Id > 0).OrderBy(credit => credit.Order).Select(credit => Cast(credit.Id, credit.Name, credit.Character, languageCode, null))];

    /// <summary>
    ///   An episode's or a movie's crew.
    /// </summary>
    /// <param name="crew">The crew.</param>
    /// <param name="languageCode">The work's original language.</param>
    /// <returns>The credits, by department, job and TMDB's credit ID, each job of a person once.</returns>
    public static IReadOnlyList<MetadataCrewData> Crew(IEnumerable<TmdbCrew>? crew, string? languageCode)
        => [
            .. (crew ?? [])
                .Where(credit => credit.Id > 0)
                .OrderBy(credit => credit.Department, StringComparer.Ordinal)
                .ThenBy(credit => credit.Job, StringComparer.Ordinal)
                .ThenBy(credit => credit.CreditId, StringComparer.Ordinal)
                .Select(credit => new MetadataCrewData
                {
                    CreatorID = TmdbIds.Creator(credit.Id),
                    CreatorName = TmdbTexts.Clean(credit.Name),
                    Name = JobName(credit.Department, credit.Job),
                    RoleType = CrewRoleOf(credit.Job),
                    LanguageCode = TmdbTexts.Clean(languageCode),
                })
                .DistinctBy(credit => (credit.CreatorID, credit.Name)),
        ];

    /// <summary>
    ///   What a crew credit is called: TMDB's department and job.
    /// </summary>
    /// <param name="department">The department, e.g. <c>Directing</c>.</param>
    /// <param name="job">The job, e.g. <c>Director</c>.</param>
    /// <returns>The name, e.g. <c>Directing, Director</c>.</returns>
    public static string JobName(string? department, string? job)
        => (TmdbTexts.Clean(department), TmdbTexts.Clean(job)) switch
        {
            ({ } dept, { } work) => $"{dept}, {work}",
            (null, { } work) => work,
            ({ } dept, null) => dept,
            _ => string.Empty,
        };

    /// <summary>
    ///   The kind of a crew job, where TMDB's wording maps onto one.
    /// </summary>
    /// <param name="job">The job.</param>
    /// <returns>The kind, or <see cref="CrewRoleType.None"/>.</returns>
    public static CrewRoleType CrewRoleOf(string? job)
        => job?.Trim().ToLowerInvariant() switch
        {
            "director" or "series director" => CrewRoleType.Director,
            "producer" or "executive producer" or "animation producer" => CrewRoleType.Producer,
            "series composition" => CrewRoleType.SeriesComposer,
            "character designer" or "original character design" => CrewRoleType.CharacterDesign,
            "original music composer" or "music" or "composer" => CrewRoleType.Music,
            "novel" or "original story" or "comic book" or "author" or "original concept" or "original series creator" => CrewRoleType.SourceWork,
            _ => CrewRoleType.None,
        };

    private static MetadataCastData Cast(int personID, string? personName, string? character, string? languageCode, string? notes)
        => new()
        {
            CreatorID = TmdbIds.Creator(personID),
            CreatorName = TmdbTexts.Clean(personName),
            Name = TmdbTexts.Clean(character?.Replace(" (voice)", string.Empty, StringComparison.Ordinal)) ?? string.Empty,
            LanguageCode = TmdbTexts.Clean(languageCode),
            RoleNotes = notes,
        };

    #endregion

    #region Aggregation

    /// <summary>
    ///   A show's or a season's cast, from the cast of its episodes: each
    ///   person in each role once, at their place on the first episode, by
    ///   TMDB's ID for the episode, they appear in.
    /// </summary>
    /// <remarks>
    ///   The order is the one the core gave TMDB's shows and seasons: a
    ///   show's by place, then person, and a season's by person, then place.
    /// </remarks>
    /// <param name="episodes">Each episode's cast, in the order of TMDB's IDs for the episodes.</param>
    /// <param name="season">Whether the credits are a season's rather than a show's.</param>
    /// <returns>The credits.</returns>
    public static IReadOnlyList<MetadataCastData> AggregateCast(IEnumerable<IReadOnlyList<MetadataCastData>> episodes, bool season = false)
    {
        var credits = new List<(MetadataCastData Credit, int Place, int Person)>();
        var seen = new HashSet<(MetadataGuid?, string, string?)>();
        foreach (var cast in episodes)
        {
            for (var place = 0; place < cast.Count; place++)
            {
                var credit = cast[place];
                if (seen.Add((credit.CreatorID, credit.Name, credit.RoleNotes)))
                    credits.Add((credit, place, PersonNumber(credit.CreatorID)));
            }
        }

        var ordered = season
            ? credits.OrderBy(credit => credit.Person).ThenBy(credit => credit.Place)
            : credits.OrderBy(credit => credit.Place).ThenBy(credit => credit.Person);
        return [.. ordered.Select(credit => credit.Credit)];
    }

    /// <summary>
    ///   A show's or a season's crew, from the crew of its episodes: each
    ///   person in each job once.
    /// </summary>
    /// <remarks>
    ///   The order is the one the core gave TMDB's shows and seasons: a
    ///   show's by department, job and person, and a season's by person, job
    ///   and department.
    /// </remarks>
    /// <param name="episodes">Each episode's crew.</param>
    /// <param name="season">Whether the credits are a season's rather than a show's.</param>
    /// <returns>The credits.</returns>
    public static IReadOnlyList<MetadataCrewData> AggregateCrew(IEnumerable<IReadOnlyList<MetadataCrewData>> episodes, bool season = false)
    {
        var credits = episodes
            .SelectMany(crew => crew)
            .DistinctBy(credit => (credit.CreatorID, credit.Name))
            .Select(credit => (Credit: credit, Job: SplitJobName(credit.Name), Person: PersonNumber(credit.CreatorID)))
            .ToList();
        var ordered = season
            ? credits.OrderBy(credit => credit.Person)
                .ThenBy(credit => credit.Job.Job, StringComparer.InvariantCulture)
                .ThenBy(credit => credit.Job.Department, StringComparer.InvariantCulture)
            : credits.OrderBy(credit => credit.Job.Department, StringComparer.InvariantCulture)
                .ThenBy(credit => credit.Job.Job, StringComparer.InvariantCulture)
                .ThenBy(credit => credit.Person);
        return [.. ordered.Select(credit => credit.Credit)];
    }

    /// <summary>
    ///   Splits a crew credit's name, as <see cref="JobName"/> makes it, into
    ///   the department and the job.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The department and the job, either of which may be empty.</returns>
    private static (string Department, string Job) SplitJobName(string name)
        => name.IndexOf(", ", StringComparison.Ordinal) is var index and >= 0 ? (name[..index], name[(index + 2)..]) : (string.Empty, name);

    /// <summary>
    ///   TMDB's ID for a credited person, as a number.
    /// </summary>
    /// <param name="creatorID">The person.</param>
    /// <returns>The ID, or <c>0</c> when there is none.</returns>
    private static int PersonNumber(MetadataGuid? creatorID)
        => creatorID is { } id && int.TryParse(id.ID, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;

    /// <summary>
    ///   A stored cast credit, as it would be written again.
    /// </summary>
    /// <param name="cast">The stored credit.</param>
    /// <returns>The credit.</returns>
    public static MetadataCastData FromStored(ICast cast)
        => new()
        {
            CreatorID = cast.CreatorID,
            CreatorName = cast.Creator?.Name,
            CharacterID = cast.CharacterID,
            Name = cast.Name,
            RoleType = cast.RoleType,
            LanguageCode = string.IsNullOrEmpty(cast.LanguageCode) ? null : cast.LanguageCode,
            RoleNotes = cast.Description,
            DubGroup = cast.DubGroup,
        };

    /// <summary>
    ///   A stored crew credit, as it would be written again.
    /// </summary>
    /// <param name="crew">The stored credit.</param>
    /// <returns>The credit.</returns>
    public static MetadataCrewData FromStored(ICrew crew)
        => new()
        {
            CreatorID = crew.CreatorID,
            CreatorName = crew.Creator?.Name,
            Name = crew.Name,
            RoleType = crew.RoleType,
            LanguageCode = string.IsNullOrEmpty(crew.LanguageCode) ? null : crew.LanguageCode,
        };

    #endregion
}
