using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Providers.AniDB;

/// <summary>
///   Moves the AniDB episodes dated by an early showing onto the regular run
///   the anime's description names, for matching against other sources.
/// </summary>
/// <remarks>
///   AniDB dates an episode by its first showing anywhere, so an episode
///   screened at an event, streamed ahead or aired early on one channel
///   carries that date, while other sources date it by the regular broadcast.
///   The note in the description usually says when that started ("The regular
///   TV broadcast started on April 7, 2016"). The stored dates are never
///   changed.
/// </remarks>
public static partial class AnidbRegularAirDates
{
    #region Types

    /// <summary>
    ///   What reading an anime's note came to.
    /// </summary>
    public enum Outcome
    {
        /// <summary>
        ///   The description says nothing about an early showing or a regular
        ///   start.
        /// </summary>
        NoNote,

        /// <summary>
        ///   The note names no regular start, and the count it states did not
        ///   place the early episodes either.
        /// </summary>
        NoRegularDate,

        /// <summary>
        ///   The note names a regular start that is not a whole date.
        /// </summary>
        RegularDateUnreadable,

        /// <summary>
        ///   The anime has no dated normal episodes.
        /// </summary>
        NoDatedEpisodes,

        /// <summary>
        ///   No episode is dated before the regular start.
        /// </summary>
        NoEarlyEpisodes,

        /// <summary>
        ///   More episodes are dated early than a note about the first few
        ///   explains, or every episode came out ahead.
        /// </summary>
        TooManyEarly,

        /// <summary>
        ///   The episodes after the early ones are dated too close to the
        ///   regular start, so they carry the early showing's dates as well.
        /// </summary>
        LaterEpisodesEarlyToo,

        /// <summary>
        ///   The early episodes were paced from the regular start.
        /// </summary>
        Corrected,

        /// <summary>
        ///   No regular start was named, and the early episodes the note
        ///   counts were paced back from the first regularly dated one.
        /// </summary>
        CorrectedFromCount,
    }

    /// <summary>
    ///   An episode moved onto the regular run.
    /// </summary>
    /// <param name="EpisodeNumber">The normal episode's number.</param>
    /// <param name="Stored">The date AniDB gives the episode.</param>
    /// <param name="Regular">The date of its regular broadcast.</param>
    public sealed record MovedEpisode(int EpisodeNumber, DateOnly Stored, DateOnly Regular);

    /// <summary>
    ///   What reading an anime's note came to.
    /// </summary>
    /// <param name="Outcome">Why the episodes were moved, or were not.</param>
    /// <param name="RegularStart">
    ///   When the regular run started, when the note or the count placed it.
    /// </param>
    /// <param name="Episodes">The episodes moved, first first.</param>
    public sealed record Reading(Outcome Outcome, DateOnly? RegularStart, IReadOnlyList<MovedEpisode> Episodes);

    /// <summary>
    ///   A date found in a note.
    /// </summary>
    /// <param name="year">The year, or <c>null</c> when the note leaves it out.</param>
    /// <param name="month">The month.</param>
    /// <param name="day">The day, or <c>null</c> for a month and year.</param>
    /// <param name="format">Which pattern matched, such as <c>mdy</c> or <c>dmy_dot</c>.</param>
    /// <param name="start">Where the date starts in the text.</param>
    /// <param name="end">Where the date ends in the text.</param>
    /// <param name="ordinal">Whether the day has an ordinal suffix.</param>
    public sealed class NoteDate(int? year, int month, int? day, string format, int start, int end, bool ordinal)
    {
        /// <summary>
        ///   The year, stated or inferred.
        /// </summary>
        public int? Year { get; internal set; } = year;

        /// <summary>
        ///   The month.
        /// </summary>
        public int Month { get; } = month;

        /// <summary>
        ///   The day, or <c>null</c> for a month and year.
        /// </summary>
        public int? Day { get; } = day;

        /// <summary>
        ///   Which pattern matched.
        /// </summary>
        public string Format { get; } = format;

        /// <summary>
        ///   Where the date starts in the text.
        /// </summary>
        public int Start { get; } = start;

        /// <summary>
        ///   Where the date ends in the text.
        /// </summary>
        public int End { get; } = end;

        /// <summary>
        ///   Whether the day has an ordinal suffix.
        /// </summary>
        public bool Ordinal { get; } = ordinal;

        /// <summary>
        ///   Whether the note stated the year.
        /// </summary>
        public bool HasStatedYear { get; } = year.HasValue;

        /// <summary>
        ///   The date, or <c>null</c> without a year or a day, or
        ///   for a day the month does not have.
        /// </summary>
        public DateOnly? Value
            => Year is { } y && Day is { } d && y is >= 1 and <= 9999 && d <= DateTime.DaysInMonth(y, Month) ? new DateOnly(y, Month, d) : null;

        /// <summary>
        ///   The pattern, marked <c>+noyear</c> and <c>+ordinal</c> where they
        ///   apply.
        /// </summary>
        public string Tag
            => Format + (HasStatedYear ? string.Empty : "+noyear") + (Ordinal ? "+ordinal" : string.Empty);
    }

    /// <summary>
    ///   A part of an early showing the note lists by episode and date
    ///   ("episodes 5-8 on 23 August 2024").
    /// </summary>
    /// <param name="First">The part's first episode.</param>
    /// <param name="Last">The part's last episode.</param>
    /// <param name="Date">When the part was shown.</param>
    public sealed record NotePart(int First, int Last, NoteDate Date);

    /// <summary>
    ///   What a description's note says.
    /// </summary>
    public sealed class Note
    {
        /// <summary>
        ///   The date the note names as the regular start.
        /// </summary>
        public NoteDate? Regular { get; internal set; }

        /// <summary>
        ///   A start counted from an early showing ("two days before the TV
        ///   broadcast"): the days, and the date they count from, if any.
        /// </summary>
        public (int Days, NoteDate? Anchor)? Relative { get; internal set; }

        /// <summary>
        ///   How many leading episodes the note says came out early.
        /// </summary>
        public int? Count { get; internal set; }

        /// <summary>
        ///   Whether the note says every episode came out ahead.
        /// </summary>
        public bool Each { get; internal set; }

        /// <summary>
        ///   The parts of the early showing the note lists by episode and
        ///   date, in the order it lists them.
        /// </summary>
        public List<NotePart> Parts { get; } = [];

        /// <summary>
        ///   The lines of the description the note was read from.
        /// </summary>
        public List<string> Lines { get; } = [];
    }

    #endregion

    #region Reading

    /// <summary>
    ///   Reads an anime's note and moves the early episodes onto the regular
    ///   run.
    /// </summary>
    /// <remarks>
    ///   Rule 1 paces the episodes dated more than a day before the regular
    ///   start the note names, by the cadence of the rest, at most three
    ///   unless the note counts more, or every one when they are exactly the
    ///   parts of the showing the note lists by date, the whole run included.
    ///   Rule 2, for a TV series whose note names no start but counts at most
    ///   three early episodes, paces those back from the first regularly
    ///   dated one.
    /// </remarks>
    /// <param name="description">The anime's description, AniDB markup included.</param>
    /// <param name="type">The anime's type.</param>
    /// <param name="normalEpisodes">The normal episodes with a date: number and stored date.</param>
    /// <param name="animeAirDate">
    ///   The anime's air date, to infer a missing year from when no episode is
    ///   dated.
    /// </param>
    /// <returns>What the note came to, with the moved episodes.</returns>
    public static Reading Read(
        string? description,
        Abstractions.Metadata.Enums.AnimeType type,
        IEnumerable<(int Number, DateOnly AirDate)> normalEpisodes,
        DateOnly? animeAirDate = null
    )
    {
        var note = ReadNote(description);
        if (note.Lines.Count == 0)
            return new(Outcome.NoNote, null, []);

        var episodes = normalEpisodes.OrderBy(episode => episode.Number).ToList();
        DateOnly? firstEpisode = episodes.Count > 0 ? episodes[0].AirDate : null;
        DateOnly? regular;
        if (note.Regular is null && note.Relative is { } relative && firstEpisode is { } first)
        {
            if (relative.Anchor is { } anchor)
                InferYear(anchor, first, forward: false);

            regular = AddDays(relative.Anchor?.Value ?? first, relative.Days);
        }
        else if (note.Regular is null)
        {
            return FromCount(note, type, episodes) ?? new(Outcome.NoRegularDate, null, []);
        }
        else
        {
            InferYear(note.Regular, firstEpisode ?? animeAirDate, forward: true);
            regular = note.Regular.Value;
        }

        if (regular is not { } start)
            return FromCount(note, type, episodes) ?? new(Outcome.RegularDateUnreadable, null, []);

        if (episodes.Count == 0)
            return new(Outcome.NoDatedEpisodes, start, []);

        // An episode stored at most a day before the start is the same
        // broadcast told by another time zone or channel, so it stays.
        var early = 0;
        while (early < episodes.Count && episodes[early].AirDate < start.AddDays(-1))
            early++;

        if (early == 0)
            return new(Outcome.NoEarlyEpisodes, start, []);

        var limit = Math.Max(3, note.Count ?? 0);
        if (note.Each || (!InParts(note, episodes, early) && (early > limit || early == episodes.Count || (note.Count ?? 0) >= episodes.Count)))
            return new(Outcome.TooManyEarly, start, []);

        // The first later episode must be a step after the start (or the step
        // before, for a double premiere), else it carries the early dates too.
        var rest = episodes.Skip(early).Select(episode => episode.AirDate).ToList();
        var (step, perDay) = Cadence(rest);
        if (rest.Count > 0 && rest[0].DayNumber - start.DayNumber < Math.Max(early - 1, 1) * Math.Min(step, 7) - 1 && perDay == 1)
            return new(Outcome.LaterEpisodesEarlyToo, start, []);

        var paced = Pace(start, rest.Count > 0 ? rest[0] : null, early, step, perDay);
        var moved = episodes.Take(early).Select((episode, index) => new MovedEpisode(episode.Number, episode.AirDate, paced[index])).ToList();
        return new(Outcome.Corrected, start, moved);
    }

    /// <summary>
    ///   Whether the parts the note lists hold exactly the early episodes:
    ///   one after another from the first, each dated as its part.
    /// </summary>
    /// <param name="note">The note.</param>
    /// <param name="episodes">The dated normal episodes, by number.</param>
    /// <param name="early">How many leading episodes are dated before the regular start.</param>
    /// <returns>Whether the parts hold every early episode and no other.</returns>
    private static bool InParts(Note note, List<(int Number, DateOnly AirDate)> episodes, int early)
    {
        if (note.Parts.Count == 0)
            return false;

        var next = 1;
        foreach (var part in note.Parts)
        {
            if (part.First != next)
                return false;

            InferYear(part.Date, episodes[0].AirDate, forward: true);
            for (; next <= part.Last; next++)
            {
                if (next > early || episodes[next - 1].Number != next || episodes[next - 1].AirDate != part.Date.Value)
                    return false;
            }
        }

        return next == early + 1;
    }

    /// <summary>
    ///   Rule 2: paces the early episodes the note counts back from the first
    ///   regularly dated one, for a TV series whose note names no start.
    /// </summary>
    /// <param name="note">The note.</param>
    /// <param name="type">The anime's type.</param>
    /// <param name="episodes">The dated normal episodes, by number.</param>
    /// <returns>The moved episodes, or <c>null</c> when the rule does not apply.</returns>
    private static Reading? FromCount(Note note, Abstractions.Metadata.Enums.AnimeType type, List<(int Number, DateOnly AirDate)> episodes)
    {
        if (type is not Abstractions.Metadata.Enums.AnimeType.TVSeries || note.Count is not { } count || count is 0 or > 3 || note.Each)
            return null;

        if (episodes.Count < count + 2)
            return null;

        // Only when exactly the counted episodes are set apart from the rest by
        // more than one step, and close together themselves.
        var dates = episodes.Select(episode => episode.AirDate).ToList();
        var (step, perDay) = Cadence(dates.Skip(count).ToList());
        if (dates[count].DayNumber - dates[count - 1].DayNumber <= step + 2 || step > 14 || perDay > 1)
            return null;

        for (var index = 0; index < count - 1; index++)
            if (dates[index + 1].DayNumber - dates[index].DayNumber > step + 2)
                return null;

        var moved = episodes.Take(count)
            .Select((episode, index) => new MovedEpisode(episode.Number, episode.AirDate, dates[count].AddDays(-step * (count - index))))
            .ToList();
        return new(Outcome.CorrectedFromCount, moved[0].Regular, moved);
    }

    /// <summary>
    ///   The cadence of a run: the days between air days and the episodes an
    ///   air day, the lower median of the first eight of each.
    /// </summary>
    /// <param name="dates">The run's dates, in episode order.</param>
    /// <returns>The step in days and the episodes a day, weekly and one when the run has fewer than two air days.</returns>
    internal static (int Step, int PerDay) Cadence(IReadOnlyList<DateOnly> dates)
    {
        var perDay = dates.GroupBy(date => date).ToDictionary(group => group.Key, group => group.Count());
        var days = perDay.Keys.Order().ToList();
        if (days.Count < 2)
            return (7, 1);

        var gaps = days.Zip(days.Skip(1), (left, right) => right.DayNumber - left.DayNumber).Take(8).ToList();
        var step = LowerMedian(gaps);
        var per = LowerMedian([.. days.Take(8).Select(day => perDay[day])]);
        return (Math.Max(step, 1), Math.Max(per, 1));
    }

    /// <summary>
    ///   Puts the early episodes on the regular schedule, <paramref name="perDay"/>
    ///   a day every <paramref name="step"/> days from the start. When the
    ///   slots before the first regular episode cannot hold them all, the
    ///   extra ones share the premiere slot, a double premiere.
    /// </summary>
    /// <param name="start">The regular start.</param>
    /// <param name="firstRegular">
    ///   The first regularly dated episode's date, or <c>null</c> when every
    ///   episode was shown early.
    /// </param>
    /// <param name="count">The early episodes.</param>
    /// <param name="step">The days between air days.</param>
    /// <param name="perDay">The episodes an air day.</param>
    /// <returns>A date for each early episode.</returns>
    private static List<DateOnly> Pace(DateOnly start, DateOnly? firstRegular, int count, int step, int perDay)
    {
        // A slot within a day of the first regular episode reaches it: the
        // note may give the broadcast day while AniDB gives the calendar date.
        List<DateOnly> slots = [start];
        while (slots.Count * perDay < count)
        {
            var next = slots[^1].AddDays(step);
            if (firstRegular is { } regular && next >= regular.AddDays(-1))
                break;

            slots.Add(next);
        }

        var extra = Math.Max(0, count - slots.Count * perDay);
        return [.. slots.SelectMany((slot, index) => Enumerable.Repeat(slot, perDay + (index == 0 ? extra : 0))).Take(count)];
    }

    /// <summary>
    ///   The lower of the two middle values, or the middle one.
    /// </summary>
    /// <param name="values">The values, at least one; sorted in place.</param>
    /// <returns>The lower median.</returns>
    private static int LowerMedian(List<int> values)
    {
        values.Sort();
        return values[(values.Count - 1) / 2];
    }

    /// <summary>
    ///   Adds days to a date, when the result is a date.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="days">The days to add.</param>
    /// <returns>The later date, or <c>null</c> past the last date.</returns>
    private static DateOnly? AddDays(DateOnly date, int days)
    {
        var number = (long)date.DayNumber + days;
        return number >= DateOnly.MinValue.DayNumber && number <= DateOnly.MaxValue.DayNumber ? DateOnly.FromDayNumber((int)number) : null;
    }

    #endregion

    #region Regular Start

    /// <summary>
    ///   When an anime's regular broadcast started, for matching it against
    ///   other sources: its first normal episode's <see cref="IEpisode.AirDate"/>
    ///   when that episode was shown early, and the anime's own date otherwise.
    /// </summary>
    /// <param name="airDate">The anime's own air date, partial or not.</param>
    /// <param name="episodes">The anime's episodes.</param>
    /// <returns>The date, or <c>null</c> when neither is dated.</returns>
    public static PartialDateOnly? RegularStartOf(PartialDateOnly? airDate, IEnumerable<IEpisode> episodes)
        => episodes.FirstOrDefault(episode => episode is { Type: EpisodeType.Episode, EpisodeNumber: 1 }) is { EarlyAirDate: not null, AirDate: { } regular }
            ? new PartialDateOnly(regular)
            : airDate;

    #endregion

    #region Note

    private static readonly Dictionary<string, int> _wordNumbers = new()
    {
        ["one"] = 1,
        ["two"] = 2,
        ["three"] = 3,
        ["four"] = 4,
        ["five"] = 5,
        ["six"] = 6,
        ["seven"] = 7,
        ["eight"] = 8,
        ["nine"] = 9,
        ["ten"] = 10,
        ["twelve"] = 12,
        ["both"] = 2,
    };

    /// <summary>
    ///   Pulls the regular start, the stated early count and the
    ///   every-episode flag out of a description. A line is part of the note
    ///   when it talks about an early showing or a regular start.
    /// </summary>
    /// <param name="description">The description, AniDB markup included.</param>
    /// <returns>What the note says; no lines when there is none.</returns>
    public static Note ReadNote(string? description)
    {
        var note = new Note();
        foreach (var rawLine in AnidbDescriptionMarkup.ToPlainText(description).Split('\n'))
        {
            var line = rawLine.Trim();
            Match? regular = null;
            foreach (Match match in RegularStart().Matches(line))
            {
                var from = Math.Max(0, match.Index - 20);
                var to = Math.Min(line.Length, match.Index + match.Length + 10);
                if (!HomeVideo().IsMatch(line[from..to]))
                {
                    regular = match;
                    break;
                }
            }

            if (regular is null && !EarlyShowing().IsMatch(line))
                continue;

            note.Lines.Add(line);
            var dates = FindDates(line);
            var each = EveryEpisode().IsMatch(line);
            var relative = RelativeStart().Match(line);
            if (relative.Success && note.Relative is null && !each && ParseCount(relative.Groups["n"].Value) is { } amount)
            {
                var days = relative.Groups["unit"].Value.StartsWith("week", StringComparison.OrdinalIgnoreCase) ? amount * 7L : amount;
                if (days <= int.MaxValue)
                    note.Relative = ((int)days, dates.LastOrDefault(date => date.End <= relative.Index));
            }

            if (regular is not null && note.Regular is null)
            {
                // The start is the first date after the phrase in the same
                // sentence, else the one before it "on the same day".
                var regularEnd = regular.Index + regular.Length;
                var stop = SentenceStop().Match(line[regularEnd..]);
                var limit = regularEnd + (stop.Success ? stop.Index : line.Length);
                var after = dates.FirstOrDefault(date => regular.Index <= date.Start && date.Start < limit);
                var before = dates.LastOrDefault(date => date.End <= regular.Index);
                if (after is not null)
                    note.Regular = after;
                else if (before is not null && SameDay().IsMatch(line[regular.Index..]))
                    note.Regular = before;
            }

            note.Parts.AddRange(FindParts(line, dates));
            if (each)
                note.Each = true;

            if (note.Count is null && StatedCount(line) is { } count and not 0)
                note.Count = count;
        }

        return note;
    }

    /// <summary>
    ///   The parts of an early showing a line lists: each episode range with
    ///   the first date after it, before the next range or sentence.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <param name="dates">The dates in the line.</param>
    /// <returns>The parts, in the order the line lists them.</returns>
    private static IEnumerable<NotePart> FindParts(string line, IReadOnlyList<NoteDate> dates)
    {
        var ranges = EpisodeRange().Matches(line);
        for (var index = 0; index < ranges.Count; index++)
        {
            var range = ranges[index];
            var rangeEnd = range.Index + range.Length;
            var stop = SentenceStop().Match(line, rangeEnd);
            var limit = Math.Min(stop.Success ? stop.Index : line.Length, index + 1 < ranges.Count ? ranges[index + 1].Index : line.Length);
            var date = dates.FirstOrDefault(candidate => rangeEnd <= candidate.Start && candidate.Start < limit);
            var first = ParseDigits(range.Groups["first"].Value);
            var last = range.Groups["last"].Success ? ParseDigits(range.Groups["last"].Value) : first;
            if (date is not null && first is { } from && last is { } to && from <= to)
                yield return new(from, to, date);
        }
    }

    /// <summary>
    ///   How many leading episodes a line says came out early.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>The count, or <c>null</c> when the line states none.</returns>
    public static int? StatedCount(string line)
    {
        var match = FirstEpisodes().Match(line);
        if (!match.Success)
            return null;

        if (match.Groups["n"] is { Success: true, Value: { Length: > 0 } number })
            return ParseCount(number);

        if (match.Groups["one"].Success || match.Groups["e1"].Success)
            return 1;

        if (match.Groups["to"] is { Success: true, Value: { Length: > 0 } to })
            return ParseCount(to);

        if (match.Groups["and"].Success || match.Groups["fs"].Success || match.Groups["both"].Success)
            return 2;

        return null;
    }

    /// <summary>
    ///   Reads a count written in digits or words.
    /// </summary>
    /// <param name="value">The count, such as <c>3</c>, <c>three</c> or <c>a</c>.</param>
    /// <returns>The count, or <c>null</c> when it is neither.</returns>
    private static int? ParseCount(string value)
    {
        var lower = value.ToLowerInvariant();
        if (lower == "a")
            return 1;

        if (_wordNumbers.TryGetValue(lower, out var word))
            return word;

        return ParseDigits(value);
    }

    /// <summary>
    ///   Rule 1: a phrase naming when the regular (TV or web) run started.
    /// </summary>
    [GeneratedRegex(
        @"\b(?:regular|actual|weekly|earliest|official)\b(?:\s+\w+){0,3}?\s+(?:broadcast|airing|distribution|streaming|run)" +
        @"|\b(?:TV|television|Japanese)?\s*(?:broadcast|airing)\b(?:\s+[\w-]+){0,4}?\s+(?:started|began|restarted)" +
        @"|\bregular\s+(?:TV\s+|television\s+)?(?:broadcast|airing)\b" +
        @"|\b(?:official|regular)\s+screening\s+(?:began|started)" +
        @"|\b(?:before|ahead\s+of|prior\s+to)\s+the\s+(?:TV|television)\s+broadcast\s+on\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RegularStart();

    /// <summary>
    ///   Rule 1b: a start counted from the early showing ("two days before the
    ///   TV broadcast").
    /// </summary>
    [GeneratedRegex(
        @"\b(?<n>\d+|one|a|two|three|four|five|six|seven)\s+(?<unit>day|days|week|weeks)\s+(?:before|prior\s+to|ahead\s+of)\s+" +
        @"(?:the|its)\s+(?:regular\s+|first\s+)?(?:TV\s+|television\s+)?(?:broadcast|airing)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RelativeStart();

    /// <summary>
    ///   A home video or theatrical run next to a start phrase, which is then
    ///   not the broadcast.
    /// </summary>
    [GeneratedRegex(@"\b(?:DVD|BD|Blu-?ray|disc|theatrical|cinematic|roadshow|opening)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HomeVideo();

    [GeneratedRegex(@"\b(?:later\s+)?on\s+the\s+same\s+day\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SameDay();

    [GeneratedRegex(@"\.\s", RegexOptions.CultureInvariant)]
    private static partial Regex SentenceStop();

    /// <summary>
    ///   Anything that talks about an early or out-of-band showing.
    /// </summary>
    [GeneratedRegex(
        @"\b(?:early|advance[d]?|preview|pre-?|special|sneak|premiere)\s*(?:(?:web|TV|online|stream|premiere|limited)\s+)?" +
        @"(?:screening|airing|broadcast(?:ed)?|release|stream(?:ing)?|distribution|premiere|screened|aired|showing|delivery)s?\b" +
        @"|\bpre-?(?:air|broadcast|screen|stream|release)" +
        @"|\bpremiered?\b|\b(?:shown|screened)\s+(?:at|during|in|as)\b|\baired\s+(?:as\s+part\s+of|during|within|in\s+\S+'s\s+timeslot)" +
        @"|\bas\s+part\s+of\s+(?:the|a|an)\b[^.]{0,60}\b(?:special|program|programme|show)\b|New\s+Year'?s?\s+(?:Eve\s+)?(?:TV\s+)?special" +
        @"|\b(?:one|a|two|three|\d+)\s+(?:day|days|week|weeks)\s+(?:before|prior\s+to|ahead\s+of|earlier)" +
        @"|\bahead\s+of\s+(?:the|its)\s+(?:regular\s+)?(?:TV|television|broadcast)|\bdigitally\s+distributed\b" +
        @"|\b(?:distributed|streamed|released|delivered|premiered)\s+(?:on|by|online|via|exclusively|in)\b" +
        @"|\bbundled\s+with\b|\btheat(?:re|er|rical)\s+(?:release|run)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EarlyShowing();

    /// <summary>
    ///   A count of leading episodes: "the first two episodes", "episodes 1-3",
    ///   "the first episode".
    /// </summary>
    [GeneratedRegex(
        @"\bfirst\s+(?<n>\d+|one|two|three|four|five|six|seven|eight|nine|ten|twelve)\s+episodes\b" +
        @"|\bfirst\s+(?<one>episode)\b|\bepisodes?\s+(?:1|one)\s*(?:-|–|to|through)\s*(?<to>\d+)\b" +
        @"|\bepisodes?\s+1\s+and\s+(?<and>2)\b|\bfirst\s+and\s+second\s+episodes\b(?<fs>)" +
        @"|\bepisode\s+(?:1|one)\b(?<e1>)|\b(?<both>both)\s+episodes\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FirstEpisodes();

    /// <summary>
    ///   An episode or a range of them: "episode 3", "episodes 5-8",
    ///   "episodes 2 and 3".
    /// </summary>
    [GeneratedRegex(
        @"\bepisodes?\s+(?<first>\d{1,4})(?:\s*(?:-|–|to|through|and)\s*(?<last>\d{1,4}))?\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EpisodeRange();

    /// <summary>
    ///   Every episode came out ahead, weekly: the whole run is early, not
    ///   the first few.
    /// </summary>
    [GeneratedRegex(
        @"\b(?:each|every|all)\s+(?:[\w-]+\s+){0,2}episodes?\b[^.]{0,100}?\b(?:ahead|advance|early|prior|before|weekly)\b" +
        @"|(?:^|[.:]\s+)(?:The\s+)?episodes\s+(?:were|received)\s+(?:\w+\s+){0,2}(?:streamed|distributed|released|an\s+early)" +
        @"|\bweekly\s+(?:stream|screening|release|distribution)|\badvance\s+stream\s+delivery\s+started" +
        @"|\bIt\s+was\s+(?:\w+\s+)?(?:streamed|distributed)\b[^.]*\b(?:starting|from|ahead)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EveryEpisode();

    #endregion

    #region Dates

    private const string MonthName =
        @"(?<mon>January|February|March|April|May|June|July|August|September|October|November|December" +
        @"|Jan|Feb|Mar|Apr|Jun|Jul|Aug|Sept|Sep|Oct|Nov|Dec)\.?";

    private const string DayOfMonth = @"(?<day>\d{1,2})(?<ord>st|nd|rd|th)?";

    private const string FullYear = @"(?<year>(?:19|20)\d\d)";

    private static readonly Dictionary<string, int> _months = BuildMonths();

    /// <summary>
    ///   The date patterns, tried in order: the first to match a stretch of
    ///   text takes it.
    /// </summary>
    private static readonly (string Format, Regex Pattern)[] _dateFormats =
    [
        ("iso", IsoDate()),
        ("dmy_dot", DottedDate()),
        ("range_dmy", DayRangeMonthYear()),
        ("range_mdy", MonthDayRangeYear()),
        ("dmy", DayMonthYear()),
        ("mdy", MonthDayYear()),
        ("my", MonthYear()),
    ];

    /// <summary>
    ///   Every date in a line, left to right, without overlaps.
    /// </summary>
    /// <remarks>
    ///   Reads <c>2001.08.08</c>, <c>09.01.2019</c>, <c>24 September 2019</c>,
    ///   <c>6th of April, 2012</c>, <c>November 8, 2020</c>,
    ///   <c>October 2nd, 2015</c>, <c>Jan 7</c>, <c>October 2020</c>, and the
    ///   ranges <c>between 24 and 29 June 2019</c> and
    ///   <c>July 11 and 12, 2020</c>, which give their first day. A weekday
    ///   before the date is left alone.
    /// </remarks>
    /// <param name="text">The line.</param>
    /// <returns>The dates, by position.</returns>
    public static IReadOnlyList<NoteDate> FindDates(string text)
    {
        var found = new List<NoteDate>();
        var taken = new bool[text.Length + 1];
        foreach (var (format, pattern) in _dateFormats)
        {
            foreach (Match match in pattern.Matches(text))
            {
                var start = match.Index;
                var end = match.Index + match.Length;
                if (match.Length > 0 && Array.IndexOf(taken, true, start, match.Length) >= 0)
                    continue;

                int? month = match.Groups["mon"].Success
                    ? _months.TryGetValue(match.Groups["mon"].Value.ToLowerInvariant(), out var named) ? named : null
                    : ParseDigits(match.Groups["m"].Value);
                if (month is not { } monthValue)
                    continue;

                int? day = match.Groups["day"].Success ? ParseDigits(match.Groups["day"].Value) : null;
                int? year = match.Groups["year"].Success ? ParseDigits(match.Groups["year"].Value) : null;
                if (day is { } dayValue && dayValue is < 1 or > 31 || monthValue is < 1 or > 12)
                    continue;

                found.Add(new(year, monthValue, day, format, start, end, match.Groups["ord"].Success));
                Array.Fill(taken, true, start, match.Length);
            }
        }

        return [.. found.OrderBy(date => date.Start)];
    }

    /// <summary>
    ///   Gives a date without a year the year of its occurrence nearest the
    ///   anchor: the first on or after the day before it, or the last on or
    ///   before it.
    /// </summary>
    /// <param name="date">The date, changed in place.</param>
    /// <param name="anchor">The date to look from.</param>
    /// <param name="forward">Whether to look forward from the anchor.</param>
    public static void InferYear(NoteDate date, DateOnly? anchor, bool forward)
    {
        if (date.Year is not null || date.Day is not { } day || anchor is not { } from)
            return;

        foreach (var year in new[] { from.Year - 1, from.Year, from.Year + 1 })
        {
            if (year is < 1 or > 9999 || day > DateTime.DaysInMonth(year, date.Month))
                continue;

            var value = new DateOnly(year, date.Month, day);
            if (forward && value >= from.AddDays(-1))
            {
                date.Year = year;
                return;
            }

            if (!forward && value <= from)
                date.Year = year;
        }
    }

    /// <summary>
    ///   Reads decimal digits of any script.
    /// </summary>
    /// <param name="digits">The digits.</param>
    /// <returns>The number, or <c>null</c> for an empty, too long or non-digit value.</returns>
    private static int? ParseDigits(string digits)
    {
        if (digits.Length is 0 or > 9)
            return null;

        var value = 0;
        foreach (var character in digits)
        {
            var digit = CharUnicodeInfo.GetDecimalDigitValue(character);
            if (digit < 0)
                return null;

            value = value * 10 + digit;
        }

        return value;
    }

    /// <summary>
    ///   Maps month names and their abbreviations, lowercase, to their numbers.
    /// </summary>
    /// <returns>The map.</returns>
    private static Dictionary<string, int> BuildMonths()
    {
        string[] names = ["january", "february", "march", "april", "may", "june", "july", "august", "september", "october", "november", "december"];
        var months = new Dictionary<string, int>();
        for (var index = 0; index < names.Length; index++)
        {
            months[names[index]] = index + 1;
            months[names[index][..3]] = index + 1;
        }

        months["sept"] = 9;
        return months;
    }

    [GeneratedRegex(@"\b(?<year>(?:19|20)\d\d)[-.](?<m>\d\d)[-.](?<day>\d\d)\b", RegexOptions.CultureInvariant)]
    private static partial Regex IsoDate();

    [GeneratedRegex(@"\b(?<day>\d{1,2})\.(?<m>\d{1,2})\.(?<year>(?:19|20)\d\d)\b", RegexOptions.CultureInvariant)]
    private static partial Regex DottedDate();

    /// <summary>
    ///   "between 24 and 29 June 2019", "19-21 October 2012": the end carries
    ///   the month and year.
    /// </summary>
    [GeneratedRegex(
        @"\b(?<day>\d{1,2})(?:st|nd|rd|th)?\s*(?:-|–|~|and|to|until)\s*(?<day2>\d{1,2})(?:st|nd|rd|th)?\s+" + MonthName + @",?\s*" + FullYear + "?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DayRangeMonthYear();

    /// <summary>
    ///   "October 19-21, 2012", "July 11 and 12, 2020".
    /// </summary>
    [GeneratedRegex(
        @"\b" + MonthName + @"\s+(?<day>\d{1,2})(?:st|nd|rd|th)?\s*(?:-|–|~|and)\s*(?<day2>\d{1,2})(?:st|nd|rd|th)?\b,?\s*" + FullYear + "?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MonthDayRangeYear();

    [GeneratedRegex(@"\b" + DayOfMonth + @"\s+(?:of\s+)?" + MonthName + @",?\s*" + FullYear + @"?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DayMonthYear();

    [GeneratedRegex(
        @"\b" + MonthName + @",?\s+" + DayOfMonth + @"\b(?!:)(?:,?\s*" + FullYear + @"\b)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MonthDayYear();

    [GeneratedRegex(@"\b" + MonthName + @",?\s+(?:of\s+)?" + FullYear + @"\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MonthYear();

    #endregion
}
