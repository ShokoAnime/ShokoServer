using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;

namespace Shoko.Server.Utilities;

/// <summary>
///   The yearly seasons: where each one starts and ends, and the rule that
///   places a series, movie or anime in them. Every consumer of yearly
///   seasons goes through it, so they all agree.
/// </summary>
/// <remarks>
///   <para>
///     An entry starts in one season, by the whole-week boundaries of its
///     type: the week holding a season's first day (1 January, April, July
///     or October), the weeks after it, and the lead-in weeks of its type
///     before it (see <see cref="GetLeadInWeeks"/>). With dated regular
///     episodes, an early premiere or a batch drop changes that season
///     (see <see cref="GetSpan(AnimeType, IEnumerable{DateOnly}, PartialDateOnly?, PartialDateOnly?, IEnumerable{DateOnly}?, DateOnly?)"/>).
///   </para>
///   <para>
///     After its start, an entry is in every calendar quarter (see
///     <see cref="GetCalendarQuarter"/>) holding one of its regular
///     episodes, up to the fourth from the end, so the last three never
///     carry it into a season, and a quarter it took a break through is
///     left out. Without dated episodes, it is in every season from its
///     start to the quarter three weeks before its end date.
///   </para>
///   <para>
///     A user may set an AniDB anime's start season by hand. It then starts
///     there and keeps only the computed seasons after it
///     (see <see cref="WithStartSeason"/>).
///   </para>
/// </remarks>
public static class SeasonCalendar
{
    // How many episodes at the end never carry an entry into a season.
    private const int TrailingEpisodes = 3;

    // How far back from the end date an entry without dated episodes stops.
    private const int TrailingDays = TrailingEpisodes * 7;

    // How long before a complete start date a regular episode's date is
    // taken as a stray (a screening or a misdated entry) and left out.
    private const int StrayEpisodeDays = 21;

    // AniDB sends no episode air dates before this day.
    private static readonly DateOnly UndatedEpisodesBefore = new(1970, 1, 1);

    #region Lead-in

    /// <summary>
    ///   The lead-in weeks of TV series and TV shorts. Compared with
    ///   AniList's start seasons for about 1,800 anime of the last five
    ///   years, a long lead-in matched TV best (four weeks: 98% on AniDB's
    ///   dates and 100% on AniList's, none: 93%), since late March
    ///   premieres belong to Spring.
    /// </summary>
    public const int TelevisionLeadInWeeks = 4;

    /// <summary>
    ///   The lead-in weeks of web series. Against the same AniList sample,
    ///   web series matched best with about one week (89%), and AniDB's own
    ///   seasons for 94 of them agreed (93%, against 85% with none).
    /// </summary>
    public const int WebLeadInWeeks = 1;

    /// <summary>
    ///   The lead-in weeks of movies, OVAs, TV specials, music videos and
    ///   other or unknown types. AniDB's own seasons for 28 of them near a
    ///   quarter's start all matched one week (none: 19 of 28), as AniDB
    ///   counts releases from the last week or so of a quarter in the next.
    /// </summary>
    public const int ReleaseLeadInWeeks = 1;

    /// <summary>
    ///   How many weeks a season of a type starts before the week holding
    ///   the season's first day.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The weeks.</returns>
    public static int GetLeadInWeeks(AnimeType type)
        => type switch
        {
            AnimeType.TV or AnimeType.TVShort => TelevisionLeadInWeeks,
            AnimeType.Web => WebLeadInWeeks,
            _ => ReleaseLeadInWeeks,
        };

    #endregion

    #region Start rules

    /// <summary>
    ///   Early premiere: the most days a lone premiere date may come before
    ///   the next season's first day and still start the entry in it.
    /// </summary>
    public const int EarlyPremiereMaxDays = 14;

    /// <summary>
    ///   Early premiere: how many regular episodes on the premiere date make
    ///   it a premiere event on their own, without a gap after it.
    /// </summary>
    public const int EarlyPremiereMinEpisodes = 2;

    /// <summary>
    ///   Early premiere: a lone episode on the premiere date counts when the
    ///   next one comes more than this many days after it.
    /// </summary>
    public const int EarlyPremiereGapDays = 10;

    /// <summary>
    ///   Early premiere: how many other dates the run needs in the next
    ///   season to start in it.
    /// </summary>
    public const int EarlyPremiereNextSeasonDates = 2;

    /// <summary>
    ///   Batch drop: how many regular episodes released on the opening date
    ///   make it a batch.
    /// </summary>
    public const int BatchDropMinEpisodes = 5;

    /// <summary>
    ///   Batch drop: no other regular episode may come within this many days
    ///   after the opening date. A batch drop takes no lead-in weeks.
    /// </summary>
    public const int BatchDropQuietDays = 28;

    #endregion

    #region Boundaries

    /// <summary>
    ///   The first day of a season for a type: the Monday of the week
    ///   holding the season's first day, less the type's lead-in weeks.
    /// </summary>
    /// <param name="season">The season.</param>
    /// <param name="type">The type whose boundaries apply, TV by default.</param>
    /// <returns>The Monday the season starts on.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The season's year is after 9999.</exception>
    public static DateOnly GetStart((int Year, YearlySeason Season) season, AnimeType type = AnimeType.TV)
        => GetStart(season, GetLeadInWeeks(type));

    /// <summary>
    ///   The season a day falls in for a type, by the whole-week boundaries
    ///   that decide where an entry starts. For today, it is the season
    ///   under way.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <param name="type">The type whose boundaries apply, TV by default.</param>
    /// <returns>The season.</returns>
    public static (int Year, YearlySeason Season) GetYearlySeason(DateOnly date, AnimeType type = AnimeType.TV)
        => GetYearlySeason(date, GetLeadInWeeks(type));

    /// <summary>
    ///   The calendar quarter a day falls in, as a season: January to March
    ///   is Winter, April to June Spring, July to September Summer and
    ///   October to December Fall. The seasons after an entry's start go by
    ///   these.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns>The season.</returns>
    public static (int Year, YearlySeason Season) GetCalendarQuarter(DateOnly date)
        => (date.Year, (YearlySeason)((date.Month - 1) / 3));

    /// <summary>
    ///   The season following another.
    /// </summary>
    /// <param name="season">The season.</param>
    /// <returns>The next season, in the next year after Fall.</returns>
    public static (int Year, YearlySeason Season) GetNextYearlySeason((int Year, YearlySeason Season) season)
        => season.Season is YearlySeason.Fall
            ? (season.Year + 1, YearlySeason.Winter)
            : (season.Year, season.Season + 1);

    /// <summary>
    ///   The season under way today for a type.
    /// </summary>
    /// <param name="type">The type whose boundaries apply, TV by default.</param>
    /// <returns>The season.</returns>
    public static (int Year, YearlySeason Season) GetCurrentYearlySeason(AnimeType type = AnimeType.TV)
        => GetYearlySeason(DateTime.Today.ToDateOnly(), type);

    /// <summary>
    ///   The first day of a season with some lead-in weeks.
    /// </summary>
    /// <param name="season">The season.</param>
    /// <param name="leadInWeeks">The lead-in weeks.</param>
    /// <returns>The Monday the season starts on.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The season's year is after 9999.</exception>
    private static DateOnly GetStart((int Year, YearlySeason Season) season, int leadInWeeks)
    {
        var firstDay = new DateOnly(season.Year, 1 + 3 * (int)season.Season, 1);
        var sinceMonday = ((int)firstDay.DayOfWeek + 6) % 7;
        return DateOnly.FromDayNumber(Math.Max(0, firstDay.DayNumber - sinceMonday - 7 * leadInWeeks));
    }

    /// <summary>
    ///   The season a day falls in with some lead-in weeks.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <param name="leadInWeeks">The lead-in weeks.</param>
    /// <returns>The season.</returns>
    private static (int Year, YearlySeason Season) GetYearlySeason(DateOnly date, int leadInWeeks)
    {
        if (date.Year < DateOnly.MaxValue.Year && GetStart((date.Year + 1, YearlySeason.Winter), leadInWeeks) <= date)
            return (date.Year + 1, YearlySeason.Winter);

        for (var season = YearlySeason.Fall; season > YearlySeason.Winter; season--)
        {
            if (GetStart((date.Year, season), leadInWeeks) <= date)
                return (date.Year, season);
        }

        return (date.Year, YearlySeason.Winter);
    }

    #endregion

    #region Placement

    /// <summary>
    ///   Where an entry is placed in the seasons.
    /// </summary>
    /// <param name="First">The first day counted: its first regular episode, or its start date.</param>
    /// <param name="Last">The last day counted, never before <paramref name="First"/>.</param>
    /// <param name="Type">The type whose boundaries apply.</param>
    /// <param name="StartSeason">The season the entry starts in.</param>
    /// <param name="Seasons">
    ///   Every season the entry is in, oldest first, from
    ///   <paramref name="StartSeason"/> on, with no cap.
    /// </param>
    /// <param name="IsStartOverridden">
    ///   Whether <paramref name="StartSeason"/> was set by hand rather than
    ///   by the rule (see <see cref="WithStartSeason"/>).
    /// </param>
    public sealed record SeasonSpan(
        DateOnly First,
        DateOnly Last,
        AnimeType Type,
        (int Year, YearlySeason Season) StartSeason,
        IReadOnlyList<(int Year, YearlySeason Season)> Seasons,
        bool IsStartOverridden = false
    );

    /// <summary>
    ///   The span of an entry. With dated regular episodes, leaving out
    ///   those dated more than three weeks before a complete start date, it
    ///   starts in the season of its first episode, after an early premiere
    ///   or batch drop is accounted for, and goes on in each calendar
    ///   quarter holding one of its episodes up to the fourth from the end.
    ///   Otherwise it runs from the start date to three weeks before the end
    ///   date, or to today without an end. A start date with only a year
    ///   places nothing.
    /// </summary>
    /// <remarks>
    ///   An early premiere is a lone date before the next season, at most
    ///   <see cref="EarlyPremiereMaxDays"/> days before it, holding
    ///   <see cref="EarlyPremiereMinEpisodes"/> episodes or followed by a
    ///   gap of more than <see cref="EarlyPremiereGapDays"/> days, with the
    ///   run going on for <see cref="EarlyPremiereNextSeasonDates"/> other
    ///   dates in that season, which the entry then starts in. A batch drop
    ///   is <see cref="BatchDropMinEpisodes"/> episodes or more on the
    ///   opening date and none in the <see cref="BatchDropQuietDays"/> days
    ///   after it, which places the opening with no lead-in.
    /// </remarks>
    /// <param name="type">The entry's type, which picks the season boundaries.</param>
    /// <param name="regularAirDates">The air dates of the entry's regular episodes, in any order.</param>
    /// <param name="startDate">When the entry started, used without dated regular episodes.</param>
    /// <param name="endDate">When the entry ended, or <c>null</c> when it is still airing.</param>
    /// <param name="otherAirDates">
    ///   The air dates of its other episodes, read only to stand in for a
    ///   start date missing its month or day.
    /// </param>
    /// <param name="today">The day to end a still airing entry on, today by default.</param>
    /// <returns>The span, or <c>null</c> when there are no dates to go by.</returns>
    public static SeasonSpan? GetSpan(
        AnimeType type,
        IEnumerable<DateOnly> regularAirDates,
        PartialDateOnly? startDate,
        PartialDateOnly? endDate,
        IEnumerable<DateOnly>? otherAirDates = null,
        DateOnly? today = null
    )
        => GetSpan(
            type,
            regularAirDates,
            startDate,
            endDate,
            otherAirDates,
            today,
            startsOn: null
        );

    /// <summary>
    ///   The span of an AniDB anime, by the general
    ///   <see cref="GetSpan(AnimeType, IEnumerable{DateOnly}, PartialDateOnly?, PartialDateOnly?, IEnumerable{DateOnly}?, DateOnly?)"/>
    ///   on the regular broadcast dates of its episodes. AniDB sends no
    ///   episode air dates before 1970, so an anime starting before then
    ///   starts on its own date, and without dated episodes or an end date
    ///   it is placed in its first season only.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <param name="episodes">Its episodes, from the cached per-anime lookup.</param>
    /// <param name="today">The day to end a still airing anime on, today by default.</param>
    /// <returns>The span, or <c>null</c> when there are no dates to go by.</returns>
    public static SeasonSpan? GetSpan(AniDB_Anime anime, IReadOnlyList<AniDB_Episode> episodes, DateOnly? today = null)
    {
        var regular = new List<DateOnly>();
        foreach (var episode in episodes)
        {
            if (episode.EpisodeType is EpisodeType.Episode && (anime.GetRegularAirDate(episode) ?? episode.GetAirDateAsDateOnly()) is { } date)
                regular.Add(date);
        }

        var animeStart = anime.AirDate?.ToDateOnly();
        var beforeEpisodeDates = animeStart < UndatedEpisodesBefore;
        return GetSpan(
            anime.AnimeType,
            regular,
            anime.AirDate,
            beforeEpisodeDates && regular.Count is 0 ? anime.EffectiveEndDateForSeasons ?? anime.AirDate : anime.EffectiveEndDateForSeasons,
            episodes.Select(episode => episode.GetAirDateAsDateOnly()).OfType<DateOnly>(),
            today,
            beforeEpisodeDates ? animeStart : null
        );
    }

    /// <summary>
    ///   A span with its start season set by hand. It starts in
    ///   <paramref name="start"/> and goes on in each computed season after
    ///   it, so the computed seasons up to the override are dropped when it
    ///   is later, and every computed season is kept when it is earlier,
    ///   with no season filled in between.
    /// </summary>
    /// <remarks>
    ///   Without a computed span, the entry is in the overridden season
    ///   only, counted from the first day of its calendar quarter.
    /// </remarks>
    /// <param name="span">The computed span, or <c>null</c> when there were no dates to go by.</param>
    /// <param name="start">The season the entry starts in.</param>
    /// <param name="type">The entry's type, used without a computed span.</param>
    /// <returns>The span, flagged as overridden.</returns>
    public static SeasonSpan WithStartSeason(SeasonSpan? span, (int Year, YearlySeason Season) start, AnimeType type)
    {
        if (span is null)
        {
            var first = new DateOnly(start.Year, 1 + 3 * (int)start.Season, 1);
            return new(first, first, type, start, [start], true);
        }

        return span with
        {
            StartSeason = start,
            Seasons = [start, .. span.Seasons.Where(season => season.CompareTo(start) > 0)],
            IsStartOverridden = true,
        };
    }

    /// <summary>
    ///   The seasons a span is in, oldest first, up to a last season.
    /// </summary>
    /// <param name="span">The span, if any.</param>
    /// <param name="last">The last season to list, by default the one under way for the span's type.</param>
    /// <returns>The seasons, empty without a span.</returns>
    public static IReadOnlyList<(int Year, YearlySeason Season)> GetSeasons(SeasonSpan? span, (int Year, YearlySeason Season)? last = null)
    {
        if (span is null)
            return [];

        var cap = last ?? GetCurrentYearlySeason(span.Type);
        return [.. span.Seasons.TakeWhile(season => season.CompareTo(cap) <= 0)];
    }

    /// <summary>
    ///   The span of an entry, starting no later than a day when one is
    ///   given.
    /// </summary>
    /// <param name="type">The entry's type, which picks the season boundaries.</param>
    /// <param name="regularAirDates">The air dates of the entry's regular episodes, in any order.</param>
    /// <param name="startDate">When the entry started, used without dated regular episodes.</param>
    /// <param name="endDate">When the entry ended, or <c>null</c> when it is still airing.</param>
    /// <param name="otherAirDates">The air dates of its other episodes, for a partial start date.</param>
    /// <param name="today">The day to end a still airing entry on, today by default.</param>
    /// <param name="startsOn">A day the entry is known to start on, before its dated episodes.</param>
    /// <returns>The span, or <c>null</c> when there are no dates to go by.</returns>
    private static SeasonSpan? GetSpan(
        AnimeType type,
        IEnumerable<DateOnly> regularAirDates,
        PartialDateOnly? startDate,
        PartialDateOnly? endDate,
        IEnumerable<DateOnly>? otherAirDates,
        DateOnly? today,
        DateOnly? startsOn
    )
    {
        var dates = startDate is { IsComplete: true } completeStart
            ? regularAirDates.Where(date => date >= completeStart.ToDateOnly().AddDays(-StrayEpisodeDays)).ToList()
            : regularAirDates.ToList();
        if (dates.Count > 0)
        {
            dates.Sort();
            return FromEpisodes(type, dates, startsOn);
        }

        if (startDate is not { } start)
            return null;

        var first = start.IsComplete ? start.ToDateOnly() : GetFirstDayWithin(start, otherAirDates) ?? (start.Month is null ? null : start.ToDateOnly());
        if (first is null)
            return null;

        var end = endDate is { } known ? known.ToDateOnly().AddDays(-TrailingDays) : today ?? DateTime.Today.ToDateOnly();
        return FromDates(type, first.Value, end > first ? end : first.Value);
    }

    /// <summary>
    ///   The span of an entry with dated regular episodes: its start season,
    ///   then each calendar quarter after it holding a counted episode.
    /// </summary>
    /// <param name="type">The entry's type.</param>
    /// <param name="dates">The regular air dates, sorted, at least one.</param>
    /// <param name="startsOn">A day the entry is known to start on, before its dated episodes.</param>
    /// <returns>The span.</returns>
    private static SeasonSpan FromEpisodes(AnimeType type, List<DateOnly> dates, DateOnly? startsOn)
    {
        var lastCounted = dates.Count > TrailingEpisodes ? dates.Count - TrailingEpisodes - 1 : dates.Count - 1;
        var first = startsOn is { } earlier && earlier < dates[0] ? earlier : dates[0];
        var start = first < dates[0] ? GetYearlySeason(first, type) : GetStartSeason(type, dates);
        var seasons = new List<(int Year, YearlySeason Season)> { start };
        for (var index = 0; index <= lastCounted; index++)
        {
            var quarter = GetCalendarQuarter(dates[index]);
            if (quarter.CompareTo(seasons[^1]) > 0)
                seasons.Add(quarter);
        }

        return new(first, dates[lastCounted], type, start, seasons);
    }

    /// <summary>
    ///   The span of an entry known only by its dates: every season from
    ///   the one it starts in to the calendar quarter of its last day.
    /// </summary>
    /// <param name="type">The entry's type.</param>
    /// <param name="first">Its first day.</param>
    /// <param name="last">Its last day, never before <paramref name="first"/>.</param>
    /// <returns>The span.</returns>
    private static SeasonSpan FromDates(AnimeType type, DateOnly first, DateOnly last)
    {
        var start = GetYearlySeason(first, type);
        var end = GetCalendarQuarter(last);
        var seasons = new List<(int Year, YearlySeason Season)> { start };
        for (var season = GetNextYearlySeason(start); season.CompareTo(end) <= 0; season = GetNextYearlySeason(season))
            seasons.Add(season);

        return new(first, last, type, start, seasons);
    }

    /// <summary>
    ///   The season an entry with dated regular episodes starts in: the one
    ///   its opening date falls in, with no lead-in after a batch drop, or
    ///   the next one after an early premiere.
    /// </summary>
    /// <param name="type">The entry's type.</param>
    /// <param name="dates">The regular air dates, sorted, at least one.</param>
    /// <returns>The season.</returns>
    private static (int Year, YearlySeason Season) GetStartSeason(AnimeType type, List<DateOnly> dates)
    {
        var opening = dates[0];
        var openingCount = 1;
        while (openingCount < dates.Count && dates[openingCount] == opening)
            openingCount++;

        DateOnly? next = openingCount < dates.Count ? dates[openingCount] : null;
        var daysToNext = next is { } day ? day.DayNumber - opening.DayNumber : int.MaxValue;
        var leadInWeeks = openingCount >= BatchDropMinEpisodes && daysToNext > BatchDropQuietDays ? 0 : GetLeadInWeeks(type);
        var season = GetYearlySeason(opening, leadInWeeks);

        // The last years of the calendar have no next season to move to.
        if (season.Year >= DateOnly.MaxValue.Year - 1 || openingCount < EarlyPremiereMinEpisodes && daysToNext <= EarlyPremiereGapDays)
            return season;

        var nextSeason = GetNextYearlySeason(season);
        var boundary = GetStart(nextSeason, leadInWeeks);
        if (next < boundary || boundary.DayNumber - opening.DayNumber > EarlyPremiereMaxDays)
            return season;

        var afterNext = GetStart(GetNextYearlySeason(nextSeason), leadInWeeks);
        var datesInNext = dates.Skip(openingCount).TakeWhile(date => date < afterNext).Distinct().Count();
        return datesInNext >= EarlyPremiereNextSeasonDates ? nextSeason : season;
    }

    /// <summary>
    ///   The earliest of some days falling within a partial date.
    /// </summary>
    /// <param name="date">The partial date.</param>
    /// <param name="days">The days, if any.</param>
    /// <returns>The day, or <c>null</c> when none falls within it.</returns>
    private static DateOnly? GetFirstDayWithin(PartialDateOnly date, IEnumerable<DateOnly>? days)
    {
        DateOnly? first = null;
        foreach (var day in days ?? [])
        {
            if (day.Year == date.Year && (date.Month is not { } month || day.Month == month) && (first is null || day < first))
                first = day;
        }

        return first;
    }

    #endregion
}
