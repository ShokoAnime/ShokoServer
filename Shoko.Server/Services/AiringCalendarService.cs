using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.Shoko;

namespace Shoko.Server.Services;

/// <summary>
///   Builds the airing calendar's views from the anime catalog and two
///   batched reads of the airing schedule service per season.
/// </summary>
/// <param name="catalog">The cached anime catalog.</param>
/// <param name="airingScheduleService">The airing schedule service.</param>
/// <param name="timeProvider">The clock, or <c>null</c> for the system's.</param>
public class AiringCalendarService(
    AnidbAnimeCatalog catalog,
    IAiringScheduleService airingScheduleService,
    TimeProvider? timeProvider = null
) : IAiringCalendarService
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    #region Seasons

    /// <inheritdoc/>
    public IReadOnlyList<SeasonAnimeEntry> GetSeasonAnime(
        int year,
        YearlySeason season,
        AnidbAnimeListOptions? animeOptions = null,
        EpisodeAiringFilteringOptions? airingOptions = null
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 9999);

        // Taken once, so the anime and both airing reads agree on what now is.
        var at = airingOptions?.At is { } value ? ToUtc(value) : _timeProvider.GetUtcNow().UtcDateTime;
        var entries = catalog.GetAnime((animeOptions ?? new()) with { Seasons = [(year, season)], At = at });
        return BuildEntries(entries, airingOptions ?? new(), at);
    }

    /// <inheritdoc/>
    public IReadOnlyList<SeasonSection> GetSeasonSections(
        int year,
        YearlySeason season,
        IReadOnlyList<SeasonSectionDefinition>? sections = null,
        AnidbAnimeListOptions? animeOptions = null,
        EpisodeAiringFilteringOptions? airingOptions = null
    )
    {
        sections ??= SeasonSectionDefinition.DefaultLayout;
        if (sections.Any(section => section is null))
            throw new ArgumentException("A section is null.", nameof(sections));

        if (sections.DistinctBy(section => section.ID, StringComparer.Ordinal).Count() != sections.Count)
            throw new ArgumentException("Two sections share an ID.", nameof(sections));

        return GroupIntoSections(
            GetSeasonAnime(year, season, animeOptions, airingOptions),
            (year, season),
            sections,
            keepOrder: animeOptions?.Filter?.SortingExpression is not null
        );
    }

    /// <inheritdoc/>
    public IReadOnlyList<AnidbAnimeSeasonYear> GetSeasonsByYear(AnidbAnimeListOptions? options = null, bool includeImages = false)
        => GroupByYear(catalog.GetSeasons(options, includeImages));

    /// <summary>
    ///   Builds the entries for the anime of a season, in their order.
    /// </summary>
    /// <remarks>
    ///   The first read takes each anime's single next airing; the second,
    ///   only for anime whose next airing has a time, the next airing on each
    ///   channel, which gives that episode's other airings.
    /// </remarks>
    /// <param name="entries">The anime, each with its Shoko series when there is one.</param>
    /// <param name="airingOptions">The airing filters, read next-only on a copy.</param>
    /// <param name="at">The time the reads are as of, in UTC; its date decides whether an anime has finished.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> or <paramref name="airingOptions"/> is <c>null</c>.</exception>
    /// <returns>The entries.</returns>
    internal IReadOnlyList<SeasonAnimeEntry> BuildEntries(
        IReadOnlyList<(AniDB_Anime Anime, AnimeSeries? Series)> entries,
        EpisodeAiringFilteringOptions airingOptions,
        DateTime at
    )
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(airingOptions);

        var today = DateOnly.FromDateTime(at);
        var nextAirings = airingScheduleService.GetAiringsForSeries(
            entries.Select(GetReadEntity),
            WithNext(airingOptions, new HashSet<AiringNextGrouping>(), at)
        );
        var timedEntries = entries
            .Where(entry => GetNext(nextAirings, entry) is { IsDateOnly: false })
            .Select(GetReadEntity)
            .ToList();
        var channelAirings = timedEntries.Count > 0
            ? airingScheduleService.GetAiringsForSeries(
                timedEntries,
                WithNext(airingOptions, new HashSet<AiringNextGrouping> { AiringNextGrouping.Channel }, at)
            )
            : new Dictionary<MetadataGuid, IReadOnlyList<IEpisodeAiring>>();
        return
        [
            .. entries.Select(entry =>
            {
                var next = GetNext(nextAirings, entry);
                var others = next is { IsDateOnly: false } && channelAirings.TryGetValue(GetReadEntity(entry).ID, out var perChannel)
                    ? GetOtherAirings(next, perChannel)
                    : [];
                return new SeasonAnimeEntry
                {
                    Anime = entry.Anime,
                    Series = entry.Series,
                    Title = catalog.GetTitle(entry.Anime, entry.Series),
                    StartSeason = catalog.GetStartSeason(entry.Anime),
                    IsStartSeasonOverridden = catalog.IsStartSeasonOverridden(entry.Anime),
                    EpisodeDuration = catalog.GetEpisodeDuration(entry.Anime.AnimeID),
                    Status = GetStatus(entry.Anime, next, today),
                    NextAiring = next,
                    OtherAirings = others,
                };
            }),
        ];
    }

    /// <summary>
    ///   Groups seasons by year, leaving out the years with no
    ///   anime at all.
    /// </summary>
    /// <param name="seasons">The seasons.</param>
    /// <returns>The years, newest first, each with its seasons from winter to fall.</returns>
    internal static IReadOnlyList<AnidbAnimeSeasonYear> GroupByYear(IEnumerable<AnidbAnimeSeasonCount> seasons)
        =>
        [
            .. seasons
                .GroupBy(season => season.Year)
                .Where(group => group.Sum(season => season.Count) > 0)
                .OrderByDescending(group => group.Key)
                .Select(group => new AnidbAnimeSeasonYear(group.Key, [.. group.OrderBy(season => season.Season)])),
        ];

    #endregion

    #region Sections

    /// <summary>
    ///   Puts each anime in the first section that takes it, drops the empty
    ///   sections and sorts each by next airing, unless told to keep the
    ///   anime's order.
    /// </summary>
    /// <param name="anime">The season's anime.</param>
    /// <param name="season">The viewed season.</param>
    /// <param name="sections">The layout.</param>
    /// <param name="keepOrder">Whether each section keeps the anime's order, as a filter's sorting expression set it.</param>
    /// <returns>The non-empty sections, in the layout's order.</returns>
    internal static IReadOnlyList<SeasonSection> GroupIntoSections(
        IReadOnlyList<SeasonAnimeEntry> anime,
        (int Year, YearlySeason Season) season,
        IReadOnlyList<SeasonSectionDefinition> sections,
        bool keepOrder = false
    )
    {
        var members = sections.Select(_ => new List<SeasonAnimeEntry>()).ToList();
        foreach (var entry in anime)
        {
            for (var index = 0; index < sections.Count; index++)
            {
                if (!Takes(sections[index], entry, season))
                    continue;

                members[index].Add(entry);
                break;
            }
        }

        var comparer = Comparer<SeasonAnimeEntry>.Create(CompareByNextAiring);
        return
        [
            .. sections
                .Select((section, index) => (Section: section, Anime: members[index]))
                .Where(pair => pair.Anime.Count > 0)
                .Select(pair => new SeasonSection(pair.Section, keepOrder ? pair.Anime : [.. pair.Anime.Order(comparer)])),
        ];
    }

    /// <summary>
    ///   Whether a section takes an anime.
    /// </summary>
    /// <param name="section">The section.</param>
    /// <param name="entry">The anime.</param>
    /// <param name="season">The viewed season.</param>
    /// <returns><c>true</c> when the section takes it.</returns>
    private static bool Takes(SeasonSectionDefinition section, SeasonAnimeEntry entry, (int Year, YearlySeason Season) season)
    {
        if (section.Types is { } types && !types.Contains(entry.Anime.Type))
            return false;

        if (section.HalfLength is { } halfLength && halfLength != IsHalfLength(entry))
            return false;

        return section.Continuing is not { } continuing || continuing == IsContinuing(entry, season);
    }

    /// <summary>
    ///   Whether an anime started before the viewed season. One without a
    ///   known start counts as new.
    /// </summary>
    /// <param name="entry">The anime.</param>
    /// <param name="season">The viewed season.</param>
    /// <returns><c>true</c> when it started before the season.</returns>
    private static bool IsContinuing(SeasonAnimeEntry entry, (int Year, YearlySeason Season) season)
        => entry.StartSeason is { } start && start.CompareTo(season) < 0;

    /// <summary>
    ///   Whether an anime's episodes run shorter than the half-length limit.
    ///   One without a known length counts as full length.
    /// </summary>
    /// <param name="entry">The anime.</param>
    /// <returns><c>true</c> when it is half length.</returns>
    private static bool IsHalfLength(SeasonAnimeEntry entry)
        => entry.EpisodeDuration is { } duration && duration < SeasonSectionDefinition.HalfLengthLimit;

    /// <summary>
    ///   Orders two anime by next airing: a scheduled one first, soonest
    ///   first, then one known only by its AniDB air date, then none, by
    ///   premiere; each tier then by title.
    /// </summary>
    /// <param name="first">The first anime.</param>
    /// <param name="second">The second anime.</param>
    /// <returns>Less than zero when the first goes first.</returns>
    internal static int CompareByNextAiring(SeasonAnimeEntry first, SeasonAnimeEntry second)
    {
        var (firstTier, firstValue) = GetNextAiringRank(first.NextAiring);
        var (secondTier, secondValue) = GetNextAiringRank(second.NextAiring);
        if (firstTier != secondTier)
            return firstTier.CompareTo(secondTier);

        if (firstTier is 2)
            return CompareByPremiere(first, second);

        var byTime = firstValue.CompareTo(secondValue);
        return byTime is not 0 ? byTime : CompareTitles(first, second);
    }

    /// <summary>
    ///   Where an anime sorts by its next airing: the tier, and the time in
    ///   it. A missing time counts as the earliest.
    /// </summary>
    /// <param name="airing">The next airing, if any.</param>
    /// <returns>The tier, <c>0</c> scheduled, <c>1</c> date-only and <c>2</c> none, and the time in UTC ticks.</returns>
    private static (int Tier, long Value) GetNextAiringRank(IEpisodeAiring? airing)
    {
        if (airing is null)
            return (2, 0);

        if (airing.IsDateOnly)
            return (1, airing.AirDate is { } airDate ? airDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).Ticks : 0);

        return (0, (airing.AiredAt ?? airing.OriginalAiredAt) is { } time ? ToUtc(time).Ticks : 0);
    }

    /// <summary>
    ///   Orders two anime by premiere, an unknown one last, then by title.
    ///   Premieres compare as their <c>yyyy</c>, <c>yyyy-MM</c> or
    ///   <c>yyyy-MM-dd</c> text.
    /// </summary>
    /// <param name="first">The first anime.</param>
    /// <param name="second">The second anime.</param>
    /// <returns>Less than zero when the first goes first.</returns>
    private static int CompareByPremiere(SeasonAnimeEntry first, SeasonAnimeEntry second)
    {
        var firstDate = first.Anime.AirDate?.ToString();
        var secondDate = second.Anime.AirDate?.ToString();
        if (firstDate != secondDate)
        {
            if (firstDate is null)
                return 1;

            if (secondDate is null)
                return -1;

            return string.CompareOrdinal(firstDate, secondDate);
        }

        return CompareTitles(first, second);
    }

    /// <summary>
    ///   Orders two anime by title.
    /// </summary>
    /// <param name="first">The first anime.</param>
    /// <param name="second">The second anime.</param>
    /// <returns>Less than zero when the first goes first.</returns>
    private static int CompareTitles(SeasonAnimeEntry first, SeasonAnimeEntry second)
        => StringComparer.InvariantCulture.Compare(first.Title, second.Title);

    #endregion

    #region Season Airings

    /// <summary>
    ///   The entity an anime's airings are read through: its series when it
    ///   is in the collection, which walks the linked episodes, else itself.
    /// </summary>
    /// <param name="entry">The anime and its series.</param>
    /// <returns>The entity.</returns>
    private static ISeries GetReadEntity((AniDB_Anime Anime, AnimeSeries? Series) entry)
        => entry.Series is { } series ? series : entry.Anime;

    /// <summary>
    ///   The airing filters with a next-only read per the given grouping, for
    ///   one series at a time.
    /// </summary>
    /// <param name="options">The caller's filters.</param>
    /// <param name="nextPer">What to keep one airing per; empty for the single next airing.</param>
    /// <param name="at">The time the read is as of, in UTC.</param>
    /// <returns>The filters.</returns>
    private static EpisodeAiringFilteringOptions WithNext(EpisodeAiringFilteringOptions options, IReadOnlySet<AiringNextGrouping> nextPer, DateTime at)
        => new()
        {
            ProviderIDs = options.ProviderIDs,
            Kinds = options.Kinds,
            Languages = options.Languages,
            ChannelIDs = options.ChannelIDs,
            EpisodeTypes = options.EpisodeTypes,
            EpisodeKinds = options.EpisodeKinds,
            InCollection = options.InCollection,
            Filter = options.Filter,
            IncludeRestricted = options.IncludeRestricted,
            User = options.User,
            IncludeEstimates = options.IncludeEstimates,
            IncludeDateOnly = options.IncludeDateOnly,
            IncludeDisabled = options.IncludeDisabled,
            IncludeDelayedOriginalSlots = options.IncludeDelayedOriginalSlots,
            LinkedEntityAirings = options.LinkedEntityAirings,
            EntityAnchor = options.EntityAnchor,
            PreferredChannels = options.PreferredChannels,
            PreferredTracks = options.PreferredTracks,
            PreferredOnly = options.PreferredOnly,
            NextOnly = true,
            NextPer = nextPer,
            At = at,
        };

    /// <summary>
    ///   An anime's next airing from the first read.
    /// </summary>
    /// <param name="nextAirings">The first read's airings by series.</param>
    /// <param name="entry">The anime and its series.</param>
    /// <returns>The next airing, or <c>null</c>.</returns>
    private static IEpisodeAiring? GetNext(
        IReadOnlyDictionary<MetadataGuid, IReadOnlyList<IEpisodeAiring>> nextAirings,
        (AniDB_Anime Anime, AnimeSeries? Series) entry
    )
        => nextAirings.TryGetValue(GetReadEntity(entry).ID, out var airings) ? airings.FirstOrDefault() : null;

    /// <summary>
    ///   The other upcoming airings of the next airing's episode, from the
    ///   next airing on each channel, in airing order.
    /// </summary>
    /// <param name="next">The next airing.</param>
    /// <param name="perChannel">The next airing on each channel.</param>
    /// <returns>The other airings.</returns>
    private static IReadOnlyList<IEpisodeAiring> GetOtherAirings(IEpisodeAiring next, IReadOnlyList<IEpisodeAiring> perChannel)
    {
        var episodeID = GetEpisodeID(next);
        return
        [
            .. perChannel
                .Where(airing => airing.ID != next.ID && !airing.IsDateOnly && GetEpisodeID(airing) == episodeID)
                .OrderBy(airing => airing.AiredAt ?? DateTime.MaxValue)
                .ThenBy(airing => airing.Channel?.Name ?? string.Empty, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    ///   The episode an airing was read for: the Shoko episode, else the
    ///   AniDB one, else the stored one.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <returns>The episode's ID.</returns>
    private static MetadataGuid GetEpisodeID(IEpisodeAiring airing)
        => airing.ShokoEpisode?.ID ?? airing.AnidbEpisode?.ID ?? airing.EpisodeID;

    /// <summary>
    ///   Whether an anime has a next airing, has finished, or neither is
    ///   known. It has finished when the last day its end date can mean has
    ///   passed.
    /// </summary>
    /// <param name="anime">The anime.</param>
    /// <param name="next">Its next airing, if any.</param>
    /// <param name="today">The current date.</param>
    /// <returns>The status.</returns>
    internal static SeasonAnimeAiringStatus GetStatus(ISeries anime, IEpisodeAiring? next, DateOnly today)
    {
        if (next is not null)
            return SeasonAnimeAiringStatus.Upcoming;

        if (anime.EndDate is not { } endDate)
            return SeasonAnimeAiringStatus.Unknown;

        var lastDay = endDate switch
        {
            { Month: { } month, Day: { } day } => new DateOnly(endDate.Year, month, day),
            { Month: { } month } => new DateOnly(endDate.Year, month, DateTime.DaysInMonth(endDate.Year, month)),
            _ => new DateOnly(endDate.Year, 12, 31),
        };
        return lastDay < today ? SeasonAnimeAiringStatus.Finished : SeasonAnimeAiringStatus.Unknown;
    }

    #endregion

    #region Days

    /// <inheritdoc/>
    public IReadOnlyList<AiringCalendarDay> GetCalendarDays(
        DateTimeOffset from,
        DateTimeOffset to,
        TimeZoneInfo? timeZone = null,
        EpisodeAiringFilteringOptions? airingOptions = null,
        bool everyChannel = true
    )
    {
        if (to < from)
            throw new ArgumentException("The end of the range is before its start.", nameof(to));

        var zone = timeZone ?? TimeZoneInfo.Utc;
        // In the zone, so the date-only entries are read for its days.
        var start = TimeZoneInfo.ConvertTime(from, zone);
        var end = TimeZoneInfo.ConvertTime(to, zone);
        return GroupIntoDays(
            airingScheduleService.GetAiringsInRange(start, end, airingOptions),
            start,
            end,
            zone,
            everyChannel
        );
    }

    /// <summary>
    ///   Places the airings of a range on their local days and groups each
    ///   day's airings by episode.
    /// </summary>
    /// <param name="airings">The airings, in airing order.</param>
    /// <param name="start">The start of the range, inclusive.</param>
    /// <param name="end">The end of the range, exclusive.</param>
    /// <param name="zone">The time zone the days are in.</param>
    /// <param name="everyChannel">Whether to keep each episode's other airings, or only its lead.</param>
    /// <returns>The days with anything on them, in order.</returns>
    internal static IReadOnlyList<AiringCalendarDay> GroupIntoDays(
        IEnumerable<IEpisodeAiring> airings,
        DateTimeOffset start,
        DateTimeOffset end,
        TimeZoneInfo zone,
        bool everyChannel
    )
    {
        var days = new SortedDictionary<DateOnly, List<AiringCalendarEntry>>();
        foreach (var airing in airings)
        {
            if (Place(airing, start, end, zone) is not { } entry)
                continue;

            var date = DateOnly.FromDateTime(entry.Time.DateTime);
            if (!days.TryGetValue(date, out var list))
                days[date] = list = [];

            list.Add(entry);
        }

        return
        [
            .. days.Select(pair => new AiringCalendarDay(
                pair.Key,
                GroupByEpisode(
                    [.. pair.Value.OrderByDescending(entry => entry.IsAllDay).ThenBy(entry => entry.Time)],
                    everyChannel
                )
            )),
        ];
    }

    /// <summary>
    ///   Where an airing goes in a range: at its slot when that is in the
    ///   range, else at the slot it was moved out of, so the gap a delay
    ///   leaves is still shown. A date-only entry goes on its AniDB air date.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <param name="start">The start of the range, inclusive.</param>
    /// <param name="end">The end of the range, exclusive.</param>
    /// <param name="zone">The time zone the days are in.</param>
    /// <returns>The entry, or <c>null</c> when neither slot is in the range.</returns>
    private static AiringCalendarEntry? Place(IEpisodeAiring airing, DateTimeOffset start, DateTimeOffset end, TimeZoneInfo zone)
    {
        if (airing.IsDateOnly)
        {
            // The AniDB air date is a calendar date, so it stays on that day in every time zone.
            if (airing.AirDate is not { } airDate)
                return null;

            var day = GetStartOfDay(airDate, zone);
            return day >= start && day < end ? new(airing, day, true, null, null) : null;
        }

        var airedAt = ToZone(airing.AiredAt, zone);
        var originalAiredAt = ToZone(airing.OriginalAiredAt, zone);
        var isMoved = airing.IsDelayed && airedAt is not null && originalAiredAt is not null && airedAt != originalAiredAt;
        if (airedAt is { } time && time >= start && time < end)
            return new(airing, time, false, isMoved ? originalAiredAt : null, null);

        if (originalAiredAt is { } originalTime && originalTime >= start && originalTime < end)
            return new(airing, originalTime, false, null, isMoved ? airedAt : null);

        return null;
    }

    /// <summary>
    ///   Groups one day's airings by episode, keeping the order of the leads.
    ///   The lead is the preferred airing, else the first timed one, else the
    ///   date-only one; an airing of an unknown episode stays on its own.
    /// </summary>
    /// <param name="entries">The day's entries, the date-only ones first, then by time.</param>
    /// <param name="everyChannel">Whether to keep each episode's other airings, or only its lead.</param>
    /// <returns>The episodes, by their leads' order.</returns>
    private static IReadOnlyList<AiringCalendarEpisode> GroupByEpisode(IReadOnlyList<AiringCalendarEntry> entries, bool everyChannel)
    {
        var groups = new List<(List<int> Members, int Lead)>();
        var byEpisode = new Dictionary<MetadataGuid, int>();
        for (var index = 0; index < entries.Count; index++)
        {
            var airing = entries[index].Airing;
            var episodeID = airing.AnidbEpisode?.ID ?? airing.ShokoEpisode?.ID;
            if (episodeID is not { } key || !byEpisode.TryGetValue(key, out var groupIndex))
            {
                if (episodeID is { } newKey)
                    byEpisode[newKey] = groups.Count;

                groups.Add(([index], index));
                continue;
            }

            var (members, lead) = groups[groupIndex];
            members.Add(index);
            if (Outranks(airing, entries[lead].Airing))
                groups[groupIndex] = (members, index);
        }

        return
        [
            .. groups
                .OrderBy(group => group.Lead)
                .Select(group => new AiringCalendarEpisode(
                    entries[group.Lead],
                    everyChannel
                        ? [.. group.Members.Where(index => index != group.Lead).Select(index => entries[index])]
                        : []
                )),
        ];
    }

    /// <summary>
    ///   Whether an airing should lead its episode over the current lead.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <param name="lead">The current lead.</param>
    /// <returns><c>true</c> when it should lead.</returns>
    private static bool Outranks(IEpisodeAiring airing, IEpisodeAiring lead)
        => !lead.IsPreferred && (airing.IsPreferred || (lead.IsDateOnly && !airing.IsDateOnly));

    /// <summary>
    ///   The start of a date in a time zone.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="zone">The time zone.</param>
    /// <returns>Its local midnight.</returns>
    private static DateTimeOffset GetStartOfDay(DateOnly date, TimeZoneInfo zone)
    {
        var midnight = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new(midnight, zone.GetUtcOffset(midnight));
    }

    /// <summary>
    ///   A stored time in a time zone. A time without a kind is UTC.
    /// </summary>
    /// <param name="value">The time, if any.</param>
    /// <param name="zone">The time zone.</param>
    /// <returns>The time in the zone, or <c>null</c>.</returns>
    private static DateTimeOffset? ToZone(DateTime? value, TimeZoneInfo zone)
        => value is { } time ? TimeZoneInfo.ConvertTime(new DateTimeOffset(ToUtc(time)), zone) : null;

    /// <summary>
    ///   A stored time as UTC. A time without a kind is UTC already.
    /// </summary>
    /// <param name="value">The time.</param>
    /// <returns>The time, in UTC.</returns>
    private static DateTime ToUtc(DateTime value)
        => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => value.ToUniversalTime(),
        };

    #endregion
}
