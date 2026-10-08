using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Models.Airing;
using Shoko.Server.Repositories;
using Shoko.Server.Services.Airing;
using Shoko.Server.Utilities;
using Shoko.Server.Utilities.Airing;

namespace Shoko.Server.Services;

public partial class AiringScheduleService
{
    /// <summary>
    /// How far either side of a range read the day buckets are taken, so an
    /// airing that lands just outside a day boundary in UTC is still seen.
    /// </summary>
    private const int RangeBucketSlackDays = 1;

    /// <summary>
    /// How long a schedule with no recent airing is still considered running
    /// for a range read's estimates. Past that, a dormant schedule costs a
    /// range read nothing.
    /// </summary>
    private static readonly TimeSpan _dormantScheduleWindow = TimeSpan.FromDays(90);

    /// <summary>
    /// How far before a range a stored AniDB date is still looked at for a
    /// date-only entry: the stored date is the earliest showing, and the air
    /// date of an episode shown early lands up to this much later.
    /// </summary>
    private const int DateOnlyEarlyShowingSlackDays = 366;

    /// <summary>
    /// What a next-only read keeps one airing per when the caller says nothing.
    /// </summary>
    private static readonly IReadOnlySet<AiringNextGrouping> _defaultNextPer = new HashSet<AiringNextGrouping> { AiringNextGrouping.Series };

    /// <summary>
    /// The kinds of showing that make an episode premiered for a next-only read.
    /// </summary>
    private static readonly IReadOnlySet<EpisodeAiringKind> _premiereKinds = new HashSet<EpisodeAiringKind> { EpisodeAiringKind.Normal };

    #region Episode Airings | Reading

    /// <inheritdoc/>
    public IReadOnlyList<IEpisodeAiring> GetAiringsForEpisode(IEpisode episode, EpisodeAiringFilteringOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(episode);
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        options ??= new EpisodeAiringFilteringOptions();
        var context = new AiringReadContext(this, options.IncludeDisabled, at: options.At);
        context.Remember(episode);
        return ReadEntityAirings(context, GetEpisodeTargets(context, [episode], options), options, ResolveAnchor(options.EntityAnchor, episode));
    }

    /// <inheritdoc/>
    public IEpisodeAiring? GetAiringForEpisode(IEpisode episode, EpisodeAiringFilteringOptions? options = null)
        => GetAiringsForEpisode(episode, options).FirstOrDefault();

    /// <inheritdoc/>
    public IReadOnlyList<IEpisodeAiring> GetAiringsForSeries(ISeries series, EpisodeAiringFilteringOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(series);
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        options ??= new EpisodeAiringFilteringOptions();
        var context = new AiringReadContext(this, options.IncludeDisabled, at: options.At);
        var anchor = ResolveAnchor(options.EntityAnchor, series);
        return ReadEntityAirings(context, GetSeriesTargets(context, series, options), options, anchor);
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<MetadataGuid, IReadOnlyList<IEpisodeAiring>> GetAiringsForSeries(
        IEnumerable<ISeries> series,
        EpisodeAiringFilteringOptions? options = null
    )
    {
        ArgumentNullException.ThrowIfNull(series);
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        options ??= new EpisodeAiringFilteringOptions();
        // One context for every series, so the schedules, channels and linked keys they share are resolved once.
        var context = new AiringReadContext(this, options.IncludeDisabled, at: options.At);
        var airings = new Dictionary<MetadataGuid, IReadOnlyList<IEpisodeAiring>>();
        foreach (var entry in series)
        {
            if (entry is null)
                throw new ArgumentException("The series hold a null.", nameof(series));
            if (airings.ContainsKey(entry.ID))
                continue;

            var anchor = ResolveAnchor(options.EntityAnchor, entry);
            airings[entry.ID] = ReadEntityAirings(context, GetSeriesTargets(context, entry, options), options, anchor);
        }

        return airings;
    }

    /// <inheritdoc/>
    public IReadOnlyList<IEpisodeAiring> GetAiringsForSeason(ISeason season, EpisodeAiringFilteringOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(season);
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        options ??= new EpisodeAiringFilteringOptions();
        var context = new AiringReadContext(this, options.IncludeDisabled, at: options.At);
        var anchor = ResolveAnchor(options.EntityAnchor, season);
        return ReadEntityAirings(context, GetSeasonTargets(context, season, options), options, anchor);
    }

    /// <inheritdoc/>
    public IReadOnlyList<IEpisodeAiring> GetAiringsForSchedule(Guid scheduleID, EpisodeAiringFilteringOptions? options = null)
    {
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        options ??= new EpisodeAiringFilteringOptions();
        var context = new AiringReadContext(this, options.IncludeDisabled, at: options.At);
        if (RepoFactory.AiringSchedule.GetByScheduleID(scheduleID) is not { } row)
            return [];

        // No entity was passed in, so there is nothing to infer an anchor from.
        var anchor = ResolveAnchor(options.EntityAnchor, null);
        // Naming the schedule names its channel, so a hidden one is still read.
        var scheduleView = context.GetSchedule(row);
        if (!MatchesFilters(context, scheduleView, options, honourHiddenChannels: false))
            return [];

        var now = context.Now;
        var airings = RepoFactory.EpisodeAiring.GetByScheduleID(row.AiringScheduleID);
        var views = airings
            .Where(entry => !IsHidden(context, row, entry, now))
            .Select(entry => new EpisodeAiringView(context, scheduleView, entry))
            .ToList();
        if (options.IncludeEstimates)
        {
            var coverage = GetCoverage(context, row, airings);
            foreach (var episode in GetScheduleEpisodes(context, row))
            {
                var key = GetEntityKey(episode);
                if (coverage.Covers(key, episode))
                    continue;
                if (Estimate(context, row, scheduleView, episode, key) is { } estimate)
                    views.Add(estimate);
            }

            foreach (var (episode, number) in GetUnlinkedEpisodes(context, row))
            {
                var key = GetEntityKey(episode);
                if (!coverage.Covers(key, number) && Estimate(context, row, scheduleView, episode, key, scheduleEpisodeNumber: number) is { } estimate)
                    views.Add(estimate);
            }
        }

        ApplyAnchor(views, anchor);
        views.RemoveAll(view => !PassesAiringFilters(context, view, options));
        if (!options.NextOnly)
            return MarkPreferred(Order(context, views, options));

        var premiered = new Dictionary<AiringEpisodeKey, bool>();
        bool IsNew(EpisodeAiringView airing)
        {
            var target = airing.Target;
            if (!premiered.TryGetValue(target.Key, out var value))
                premiered[target.Key] = value = HasPremieredBefore(context, target, options, now);

            return !value;
        }

        var upcoming = MarkPreferred(
            Order(context, views, options, preferredOnly: false)
                .Where(airing => GetNextTime(airing, now, DateOnly.FromDateTime(now), TimeSpan.Zero) is not null && IsNew((EpisodeAiringView)airing))
        );
        return ReduceToNext(
            options.PreferredOnly ? upcoming.Where(airing => airing.IsPreferred) : upcoming,
            options,
            now,
            DateOnly.FromDateTime(now),
            TimeSpan.Zero
        );
    }

    /// <inheritdoc/>
    public IReadOnlyList<IEpisodeAiring> GetAiringsInRange(DateTimeOffset from, DateTimeOffset to, EpisodeAiringFilteringOptions? options = null)
        => GetAiringsInRange(from, to, options, includeHiddenChannels: false);

    /// <summary>
    /// The range read, optionally taking in the hidden channels when the read
    /// names no channels, which the notification horizon needs: each
    /// subscriber's own filter leaves them out again.
    /// </summary>
    /// <param name="from">The inclusive start of the range.</param>
    /// <param name="to">The exclusive end of the range.</param>
    /// <param name="options">How to filter and order the airings.</param>
    /// <param name="includeHiddenChannels">Whether hidden channels are read when no channels are named.</param>
    /// <exception cref="InvalidOperationException">Parts have not been added yet.</exception>
    /// <exception cref="ArgumentException"><paramref name="to"/> is before <paramref name="from"/>.</exception>
    /// <returns>The airings.</returns>
    internal IReadOnlyList<IEpisodeAiring> GetAiringsInRange(
        DateTimeOffset from,
        DateTimeOffset to,
        EpisodeAiringFilteringOptions? options,
        bool includeHiddenChannels
    )
    {
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");
        if (to < from)
            throw new ArgumentException("The end of the range is before its start.", nameof(to));

        options ??= new EpisodeAiringFilteringOptions();
        var context = new AiringReadContext(this, options.IncludeDisabled, includeHiddenChannels, options.At);
        // No entity was passed in, so there is nothing to infer an anchor from.
        var anchor = ResolveAnchor(options.EntityAnchor, null);
        var fromUtc = from.UtcDateTime;
        var toUtc = to.UtcDateTime;
        // A delayed airing's original slot is a gap, not an airing, so it is never next.
        var includeGaps = options.IncludeDelayedOriginalSlots && !options.NextOnly;
        var firstBucket = AddDaysClamped(DateOnly.FromDateTime(fromUtc), -RangeBucketSlackDays);
        var lastBucket = AddDaysClamped(DateOnly.FromDateTime(toUtc), RangeBucketSlackDays);
        var scheduleMatches = new Dictionary<int, bool>();
        bool MatchesSchedule(int scheduleID)
        {
            if (scheduleMatches.TryGetValue(scheduleID, out var matches))
                return matches;

            return scheduleMatches[scheduleID] =
                RepoFactory.AiringSchedule.GetByID(scheduleID) is { } row && MatchesFilters(context, context.GetSchedule(row), options);
        }

        // Everything already stored in the window, plus the airings that were
        // delayed out of it, which is what a calendar draws its gap from. Every
        // hard filter is schedule-level, so a candidate whose only airings are
        // on schedules the read filtered out is dropped here rather than after
        // it has been through the whole per-target pipeline.
        var candidates = RepoFactory.EpisodeAiring.GetByDayRange(firstBucket, lastBucket)
            .Concat(includeGaps ? RepoFactory.EpisodeAiring.GetDelayedByOriginalDayRange(firstBucket, lastBucket) : [])
            .DistinctBy(entry => entry.EpisodeAiringID)
            .Where(entry => MatchesSchedule(entry.AiringScheduleID))
            .ToList();

        // One airing linked to two AniDB episodes is two targets, but the same
        // episode or place reached through two airings is still one.
        var linked = options.LinkedEntityAirings ?? true;
        var targets = new Dictionary<AiringEpisodeKey, AiringReadTarget>();
        var targetEpisodes = new HashSet<(MetadataSource Source, string ID)>();
        void AddTarget(AiringReadTarget target)
        {
            if (target.Episode is { } episode)
                targetEpisodes.Add(GetEntityKey(episode));
            if (!targets.TryGetValue(target.Key, out var existing) || GetTargetRank(target) < GetTargetRank(existing))
                targets[target.Key] = target;
        }

        foreach (var entry in candidates)
        {
            var scheduleView = context.GetSchedule(entry.AiringScheduleID)!;
            var view = new EpisodeAiringView(context, scheduleView, entry);
            if (view.Episode is not { } episode)
            {
                AddTarget(new(null, linked ? AiringEpisodeKey.For(view) : AiringEpisodeKey.ForUnlinked(view)));
                continue;
            }

            foreach (var target in GetLinkedTargets(episode, linked))
            {
                var key = linked
                    ? AiringEpisodeKey.For(new EpisodeAiringView(context, scheduleView, entry, target))
                    : AiringEpisodeKey.ForEpisode(target.Source, target.ID.ID);
                AddTarget(new(target, key));
            }
        }

        if (options.IncludeEstimates)
        {
            foreach (var (source, id) in GetEstimableEpisodeKeys(context, fromUtc, toUtc, options))
            {
                if (context.GetEpisode(source, id) is not { } episode)
                    continue;

                foreach (var target in GetLinkedTargets(episode, linked))
                    if (!targetEpisodes.Contains(GetEntityKey(target)))
                        AddTarget(new(target, GetEpisodeTargetKey(context, target, linked)));
            }
        }

        // The window narrows first and the preference reduces afterwards, so a
        // target is answered with the best airing it has in the window. The
        // other way round a target whose best airing is elsewhere would drop
        // out of the day it actually airs on.
        var airings = targets.Values
            .SelectMany(target =>
            {
                var inRange = MarkPreferred(
                    ReadAirings(context, target, options, anchor, preferredOnly: false)
                        .Where(airing => IsInRange(airing, fromUtc, toUtc, includeGaps, includeOnAir: options.NextOnly))
                );
                if (options.NextOnly && inRange.Count > 0 && HasPremieredBefore(context, target, options, fromUtc))
                    return [];

                return options.PreferredOnly ? inRange.Take(1) : inRange;
            })
            .ToList();
        if (AllowsDateOnly(options))
        {
            var firstDate = DateOnly.FromDateTime(from.DateTime);
            var lastDate = DateOnly.FromDateTime(to > from ? to.AddTicks(-1).DateTime : from.DateTime);
            airings.AddRange(GetDateOnlyEntriesInRange(context, firstDate, lastDate, options, anchor));
        }

        var reduced = options.NextOnly
            ? ReduceToNext(airings, options, fromUtc, DateOnly.FromDateTime(from.DateTime), from.Offset)
            : airings;
        return reduced
            .OrderBy(airing => GetRangeSortTime(airing, fromUtc, toUtc, from.Offset))
            .ThenBy(airing => airing.LinkID)
            .ThenBy(airing => airing.Channel?.Name ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(airing => airing.Key, StringComparer.Ordinal)
            .ToList();
    }

    #endregion

    #region Episode Airings | Pipeline

    /// <summary>
    /// The one read every airing read runs: gather what is stored for the
    /// target, filter, add the estimates the surviving schedules produce,
    /// de-duplicate channels and order by preference.
    /// </summary>
    /// <param name="context">The read the views belong to.</param>
    /// <param name="target">The episode or place the read is for.</param>
    /// <param name="options">The filters and preference to read with.</param>
    /// <param name="anchor">The resolved entity anchor, never <see cref="AiringEntityAnchor.Auto"/>.</param>
    /// <param name="preferredOnly">Whether to reduce the answer to the target's best airing, overriding <see cref="EpisodeAiringFilteringOptions.PreferredOnly"/>.</param>
    /// <param name="honourHiddenChannels">Whether a schedule on a hidden channel is left out when the read names no channels.</param>
    /// <returns>The target's airings, best first.</returns>
    private List<IEpisodeAiring> ReadAirings(
        AiringReadContext context,
        AiringReadTarget target,
        EpisodeAiringFilteringOptions options,
        AiringEntityAnchor anchor,
        bool? preferredOnly = null,
        bool honourHiddenChannels = true
    )
    {
        var now = context.Now;
        var episodeLinked = options.LinkedEntityAirings ?? target.Episode is IShokoEpisode;
        var positionLinked = options.LinkedEntityAirings ?? true;
        var views = new List<EpisodeAiringView>();
        var covered = new HashSet<int>();
        foreach (var (row, entry) in context.GetStoredAirings(target, episodeLinked, positionLinked))
        {
            covered.Add(entry.AiringScheduleID);
            var scheduleView = context.GetSchedule(row);
            if (!MatchesFilters(context, scheduleView, options, honourHiddenChannels) || IsHidden(context, row, entry, now))
                continue;

            views.Add(new EpisodeAiringView(context, scheduleView, entry, target.Episode, target.Key));
        }

        if (options.IncludeEstimates && target.Episode is { } episode)
        {
            var keys = episodeLinked ? context.GetLinkedEpisodeKeys(episode) : [GetEntityKey(episode)];
            views.AddRange(GetEstimates(context, target, keys, covered, options, honourHiddenChannels));
        }

        // Before the ordering, so a preferred-only read is handed the best of
        // what the anchor and the filters kept rather than nothing at all.
        ApplyAnchor(views, anchor);
        views.RemoveAll(view => !PassesAiringFilters(context, view, options));
        return Order(context, views, options, preferredOnly).ToList();
    }

    /// <summary>
    /// Every stored airing a target counts: the ones pinned to the episode or
    /// its linked ones, the ones whose number on a schedule of those episodes'
    /// series resolves to them, the ones an <see cref="AiringEpisodeOffset"/>
    /// places on an AniDB episode, and, for a place no episode stands for, the
    /// ones at that place.
    /// </summary>
    /// <param name="context">The read the lookups are cached in.</param>
    /// <param name="target">The target.</param>
    /// <param name="episodeLinked">Whether the episode's links are followed.</param>
    /// <param name="positionLinked">Whether a place is looked up on the schedules of other sources too.</param>
    /// <returns>The stored airings, each with its schedule.</returns>
    internal static IReadOnlyList<(AiringSchedule Schedule, EpisodeAiring Entry)> CollectStoredAirings(
        AiringReadContext context,
        AiringReadTarget target,
        bool episodeLinked,
        bool positionLinked
    )
    {
        var found = new Dictionary<int, (AiringSchedule Schedule, EpisodeAiring Entry)>();
        void AddAt(AiringSchedule row, int episodeNumber, Func<EpisodeAiring, bool>? predicate = null)
        {
            var sequenceNumber = episodeNumber - row.FirstEpisodeNumber + 1;
            if (sequenceNumber < 1)
                return;

            foreach (var entry in RepoFactory.EpisodeAiring.GetByScheduleIDAndSequenceNumber(row.AiringScheduleID, sequenceNumber))
                if (!entry.IsPinned && (predicate is null || predicate(entry)))
                    found.TryAdd(entry.EpisodeAiringID, (row, entry));
        }

        if (target.Episode is { } episode)
        {
            var keys = episodeLinked ? context.GetLinkedEpisodeKeys(episode) : [GetEntityKey(episode)];
            foreach (var (source, id) in keys)
            {
                foreach (var entry in RepoFactory.EpisodeAiring.GetByEpisodeID(source, id))
                    if (RepoFactory.AiringSchedule.GetByID(entry.AiringScheduleID) is { } row)
                        found.TryAdd(entry.EpisodeAiringID, (row, entry));

                if (context.GetEpisode(source, id) is not { Type: EpisodeType.Episode } keyed)
                    continue;

                foreach (var row in RepoFactory.AiringSchedule.GetBySeriesID(source, keyed.SeriesID.ID))
                    if (context.GetScheduleEpisodeByNumber(row, keyed.EpisodeNumber) is { } atNumber && GetEntityKey(atNumber) == (source, id))
                        AddAt(row, keyed.EpisodeNumber);

                // The schedules of a linked series whose source does not list
                // the episode yet, placed by the offset the anime's linked
                // episodes give, the way its estimates are.
                if (keyed is IAnidbEpisode anidbEpisode)
                    foreach (var (row, number) in GetUnlinkedEpisodeSchedules(context, anidbEpisode))
                        AddAt(row, number);
            }
        }

        // A place an AniDB or shoko episode stands for is already gathered
        // through that episode. A series place is only ever the unlinked key.
        if (target.Key.Kind is AiringEpisodeKeyKind.AnidbPosition or AiringEpisodeKeyKind.SeriesPosition &&
            (target.Episode is null || context.GetEpisodeViews(target.Episode) is (null, null)))
        {
            foreach (var (row, number) in GetPositionSchedules(context, target.Key, positionLinked))
            {
                var scheduleView = context.GetSchedule(row);
                AddAt(row, number, entry =>
                {
                    var view = new EpisodeAiringView(context, scheduleView, entry);
                    return AiringEpisodeKey.For(view) == target.Key || AiringEpisodeKey.ForUnlinked(view) == target.Key;
                });
            }
        }

        return [.. found.Values];
    }

    /// <summary>
    /// The schedules a place on a line can have airings on, each with the
    /// number the place has on its line: for an AniDB place, the anime's own
    /// schedules and, when linked, its shoko series' and the ones an
    /// <see cref="AiringEpisodeOffset"/> places it on; for a series place, the
    /// series' schedules on that season.
    /// </summary>
    /// <param name="context">The read the lookups are cached in.</param>
    /// <param name="key">The place.</param>
    /// <param name="linked">Whether the schedules of other sources are looked at too.</param>
    /// <returns>The schedules, with the place's number on each.</returns>
    private static IEnumerable<(AiringSchedule Schedule, int Number)> GetPositionSchedules(AiringReadContext context, AiringEpisodeKey key, bool linked)
    {
        if (key.Kind is AiringEpisodeKeyKind.SeriesPosition)
        {
            foreach (var row in RepoFactory.AiringSchedule.GetBySeriesIDAndSeasonID(key.Source, key.ID, key.SeasonID))
                yield return (row, key.Number);

            yield break;
        }

        if (key.Kind is not AiringEpisodeKeyKind.AnidbPosition || !int.TryParse(key.ID, out var animeID))
            yield break;

        foreach (var row in RepoFactory.AiringSchedule.GetBySeriesID(MetadataSource.AniDB, key.ID))
            yield return (row, key.Number);

        if (!linked)
            yield break;

        if (context.GetShokoSeriesByAnimeID(animeID) is { } shokoSeries)
        {
            var (source, id) = GetEntityKey(shokoSeries);
            foreach (var row in RepoFactory.AiringSchedule.GetBySeriesID(source, id))
                yield return (row, key.Number);
        }

        foreach (var link in context.GetSeriesLinks(animeID))
        {
            if (link.Source.IsCore || link.ProviderID is not { } seriesID || seriesID.EntityType != MetadataEntityType.Series)
                continue;

            foreach (var row in RepoFactory.AiringSchedule.GetBySeriesID(link.Source, seriesID.ID))
            {
                if (context.GetEpisodeOffset(animeID, row) is not { } offset || key.Number <= offset.LastLinkedEpisodeNumber)
                    continue;

                var number = key.Number + offset.Offset;
                if (number > offset.LastLinkedScheduleNumber && number > 0)
                    yield return (row, number);
            }
        }
    }

    /// <summary>
    /// The AniDB anime and regular episode number a number on a schedule's
    /// line stands for: on an AniDB or shoko schedule the series' own anime at
    /// the same number, and on a plugin source's schedule the first anime
    /// linked to its series whose <see cref="AiringEpisodeOffset"/> places the
    /// number past its linked episodes and within the anime's episode count.
    /// </summary>
    /// <param name="context">The read the lookups are cached in.</param>
    /// <param name="row">The schedule.</param>
    /// <param name="episodeNumber">The number on the line.</param>
    /// <returns>The AniDB anime and episode number, or <c>null</c> when the series leads to none.</returns>
    internal static (int AnimeID, int EpisodeNumber)? FindAnidbPosition(AiringReadContext context, AiringSchedule row, int episodeNumber)
    {
        if (row.SeriesSource == MetadataSource.AniDB)
            return int.TryParse(row.SeriesID, out var anidbAnimeID) ? (anidbAnimeID, episodeNumber) : null;

        if (row.SeriesSource.IsCore)
            return context.GetSeries(row.SeriesSource, row.SeriesID) is IShokoSeries shokoSeries ? (shokoSeries.AnidbAnimeID, episodeNumber) : null;

        foreach (var animeID in context.GetLinkedAnimeIDs(row.SeriesSource, row.SeriesID))
        {
            if (context.GetEpisodeOffset(animeID, row) is not { } offset || episodeNumber <= offset.LastLinkedScheduleNumber)
                continue;

            var number = episodeNumber - offset.Offset;
            if (number <= offset.LastLinkedEpisodeNumber)
                continue;

            if (RepoFactory.AniDB_Anime.GetByAnimeID(animeID) is { EpisodeCountNormal: > 0 } anime && number > anime.EpisodeCountNormal)
                continue;

            return (animeID, number);
        }

        return null;
    }

    /// <summary>
    /// The targets an episode read is for, each under the key its airings are
    /// merged by.
    /// </summary>
    /// <param name="context">The read the keys belong to.</param>
    /// <param name="episodes">The episodes.</param>
    /// <param name="options">The read's options, for the links it walks.</param>
    /// <returns>The targets.</returns>
    private static IEnumerable<AiringReadTarget> GetEpisodeTargets(AiringReadContext context, IEnumerable<IEpisode> episodes, EpisodeAiringFilteringOptions options)
        => episodes.Select(episode => new AiringReadTarget(episode, GetEpisodeTargetKey(context, episode, options.LinkedEntityAirings ?? episode is IShokoEpisode)));

    /// <summary>
    /// The targets a series read is for: its episodes, and, unless the read
    /// leaves them out, the places on the lines of its schedules that no
    /// episode stands for yet.
    /// </summary>
    /// <param name="context">The read the keys belong to.</param>
    /// <param name="series">The series.</param>
    /// <param name="options">The read's options.</param>
    /// <returns>The targets, the episodes first.</returns>
    private List<AiringReadTarget> GetSeriesTargets(AiringReadContext context, ISeries series, EpisodeAiringFilteringOptions options)
    {
        var targets = GetEpisodeTargets(context, series.Episodes, options).ToList();
        if (!options.IncludeUnresolved)
            return targets;

        var linked = options.LinkedEntityAirings ?? series is IShokoSeries;
        var seriesKeys = linked ? GetInvalidationKeys(series) : [GetEntityKey(series)];
        var schedules = seriesKeys.SelectMany(key => RepoFactory.AiringSchedule.GetBySeriesID(key.Source, key.ID));
        return AddUnresolvedTargets(context, targets, schedules, linked);
    }

    /// <summary>
    /// The targets a season read is for: its episodes, and, unless the read
    /// leaves them out, the places on the lines of the schedules narrowed to
    /// it that no episode stands for yet.
    /// </summary>
    /// <param name="context">The read the keys belong to.</param>
    /// <param name="season">The season.</param>
    /// <param name="options">The read's options.</param>
    /// <returns>The targets, the episodes first.</returns>
    private static List<AiringReadTarget> GetSeasonTargets(AiringReadContext context, ISeason season, EpisodeAiringFilteringOptions options)
    {
        var targets = GetEpisodeTargets(context, season.Episodes, options).ToList();
        if (!options.IncludeUnresolved)
            return targets;

        var linked = options.LinkedEntityAirings ?? season is ISeason<IShokoSeries, IShokoEpisode>;
        var seasons = linked ? season.LinkedSeasons.Prepend(season) : [season];
        var schedules = seasons
            .DistinctBy(GetEntityKey)
            .SelectMany(entry => RepoFactory.AiringSchedule.GetBySeriesIDAndSeasonID(entry.Source, entry.SeriesID.ID, entry.ID.ID));
        return AddUnresolvedTargets(context, targets, schedules, linked);
    }

    /// <summary>
    /// Add a target for each unresolved airing on the schedules that no target
    /// already stands for.
    /// </summary>
    /// <param name="context">The read the keys belong to.</param>
    /// <param name="targets">The targets so far, added to in place.</param>
    /// <param name="schedules">The schedules to look at, which may repeat.</param>
    /// <param name="linked">Whether the read follows links, which decides the keys.</param>
    /// <returns>The targets.</returns>
    private static List<AiringReadTarget> AddUnresolvedTargets(
        AiringReadContext context,
        List<AiringReadTarget> targets,
        IEnumerable<AiringSchedule> schedules,
        bool linked
    )
    {
        var known = targets.Select(target => target.Key).ToHashSet();
        foreach (var row in schedules.DistinctBy(row => row.AiringScheduleID))
        {
            var scheduleView = context.GetSchedule(row);
            foreach (var entry in RepoFactory.EpisodeAiring.GetByScheduleID(row.AiringScheduleID))
            {
                if (entry.IsPinned)
                    continue;

                var view = new EpisodeAiringView(context, scheduleView, entry);
                if (view.Episode is not null)
                    continue;

                var key = linked ? AiringEpisodeKey.For(view) : AiringEpisodeKey.ForUnlinked(view);
                if (known.Add(key))
                    targets.Add(new(null, key));
            }
        }

        return targets;
    }

    /// <summary>
    /// The key an episode's airings are merged by when no airing is in hand:
    /// its regular AniDB episode's place, else its AniDB or shoko episode,
    /// else itself.
    /// </summary>
    /// <param name="context">The read the lookups are cached in.</param>
    /// <param name="episode">The episode.</param>
    /// <param name="linked">Whether the read follows links; without them the episode is its own key.</param>
    /// <returns>The key.</returns>
    private static AiringEpisodeKey GetEpisodeTargetKey(AiringReadContext context, IEpisode episode, bool linked)
    {
        if (!linked)
            return AiringEpisodeKey.ForEpisode(episode.Source, episode.ID.ID);

        var (anidbEpisode, shokoEpisode) = context.GetEpisodeViews(episode);
        if (anidbEpisode is not null)
            return ((IEpisode)anidbEpisode).Type is EpisodeType.Episode
                ? AiringEpisodeKey.ForAnidbPosition(anidbEpisode.AnidbAnimeID, anidbEpisode.EpisodeNumber)
                : AiringEpisodeKey.ForEpisode(anidbEpisode.ID);

        return shokoEpisode is not null ? AiringEpisodeKey.ForEpisode(shokoEpisode.ID) : AiringEpisodeKey.ForEpisode(episode.ID);
    }

    /// <summary>
    /// The episodes an episode stands for in a read: its shoko episodes when
    /// the read follows links and it has any, else itself.
    /// </summary>
    /// <param name="episode">The episode.</param>
    /// <param name="linked">Whether the read follows links.</param>
    /// <returns>The episodes.</returns>
    private static IReadOnlyList<IEpisode> GetLinkedTargets(IEpisode episode, bool linked)
        => linked && episode is not IShokoEpisode && episode.ShokoEpisodes.Count > 0 ? [.. episode.ShokoEpisodes] : [episode];

    /// <summary>
    /// Which of two targets under one key a read keeps: one with a shoko
    /// episode, then one with an AniDB episode, then any episode, then a place
    /// alone.
    /// </summary>
    /// <param name="target">The target.</param>
    /// <returns>The rank, lower being better.</returns>
    private static int GetTargetRank(AiringReadTarget target)
        => target.Episode switch
        {
            IShokoEpisode => 0,
            IAnidbEpisode => 1,
            null => 3,
            _ => 2,
        };

    /// <summary>
    /// The estimates every schedule that covers a target's episode, and has no
    /// airing for it, contributes, the schedules of a linked series whose
    /// source does not list the episode yet among them. Filtering runs first,
    /// so no estimate is computed only to be thrown away.
    /// </summary>
    /// <param name="context">The read the views belong to.</param>
    /// <param name="target">The target the read is for.</param>
    /// <param name="keys">The episode's own key and its linked ones.</param>
    /// <param name="covered">The schedules that already have an airing for the target.</param>
    /// <param name="options">The filters to apply.</param>
    /// <param name="honourHiddenChannels">Whether a schedule on a hidden channel is left out when the read names no channels.</param>
    /// <returns>One estimate per schedule that can produce one.</returns>
    private IEnumerable<EpisodeAiringView> GetEstimates(
        AiringReadContext context,
        AiringReadTarget target,
        IReadOnlyList<(MetadataSource Source, string ID)> keys,
        HashSet<int> covered,
        EpisodeAiringFilteringOptions options,
        bool honourHiddenChannels
    )
    {
        var estimated = new HashSet<int>();
        foreach (var key in keys)
        {
            if (context.GetEpisode(key.Source, key.ID) is not { Type: EpisodeType.Episode } episode)
                continue;

            foreach (var row in RepoFactory.AiringSchedule.GetBySeriesID(key.Source, episode.SeriesID.ID))
            {
                if (covered.Contains(row.AiringScheduleID))
                    continue;

                var scheduleView = context.GetSchedule(row);
                if (!MatchesFilters(context, scheduleView, options, honourHiddenChannels))
                    continue;

                if (Estimate(context, row, scheduleView, episode, key, target) is { } estimate)
                {
                    estimated.Add(row.AiringScheduleID);
                    yield return estimate;
                }
            }
        }

        // The schedules of a linked series whose source does not list the
        // episode yet, placed by the offset the anime's linked episodes give.
        foreach (var key in keys)
        {
            if (key.Source != MetadataSource.AniDB || context.GetEpisode(key.Source, key.ID) is not IAnidbEpisode anidbEpisode)
                continue;

            foreach (var (row, number) in GetUnlinkedEpisodeSchedules(context, anidbEpisode))
            {
                if (covered.Contains(row.AiringScheduleID) || !estimated.Add(row.AiringScheduleID))
                    continue;

                var scheduleView = context.GetSchedule(row);
                if (!MatchesFilters(context, scheduleView, options, honourHiddenChannels))
                    continue;

                if (Estimate(context, row, scheduleView, anidbEpisode, key, target, number) is { } estimate)
                    yield return estimate;
            }
        }
    }

    /// <summary>
    /// The estimate one schedule makes for one episode, or <c>null</c>
    /// when its coverage, its season or its own hiatus says it makes none.
    /// </summary>
    /// <param name="context">The read the view belongs to.</param>
    /// <param name="row">The schedule doing the estimating.</param>
    /// <param name="scheduleView">The schedule's view.</param>
    /// <param name="episode">The episode being estimated.</param>
    /// <param name="key">The stored key of the episode being estimated.</param>
    /// <param name="target">The target the read ran for, if any.</param>
    /// <param name="scheduleEpisodeNumber">
    /// The episode's number on the schedule when an <see cref="AiringEpisodeOffset"/>
    /// placed it there, which already satisfied the season; <c>null</c> for
    /// an episode of the schedule's own series.
    /// </param>
    /// <returns>The estimate, or <c>null</c>.</returns>
    private static EpisodeAiringView? Estimate(
        AiringReadContext context,
        AiringSchedule row,
        AiringScheduleView scheduleView,
        IEpisode episode,
        (MetadataSource Source, string ID) key,
        AiringReadTarget? target = null,
        int? scheduleEpisodeNumber = null
    )
    {
        var episodeNumber = scheduleEpisodeNumber ?? episode.EpisodeNumber;
        if (episode.Type is not EpisodeType.Episode)
            return null;
        if (episodeNumber < row.FirstEpisodeNumber)
            return null;
        if (scheduleEpisodeNumber is null && !string.IsNullOrEmpty(row.SeasonID) && !IsInSeason(context, row, key))
            return null;

        var sequenceNumber = episodeNumber - row.FirstEpisodeNumber + 1;
        var estimate = AiringScheduleUtility.EstimateAiring(context.GetProfile(row), new AiringEstimateTarget()
        {
            EpisodeKey = AiringScheduleUtility.GetDerivedSequenceAiringKey(sequenceNumber),
            EpisodeNumber = episodeNumber,
            IsNormalEpisode = true,
            AnidbAirDate = context.GetAnidbAirDate(episode),
            FirstOriginalAiringAt = context.GetFirstOriginalAiringAt(target ?? new(episode, GetEpisodeTargetKey(context, episode, linked: true))),
        });
        if (estimate is null || (estimate.AiredAt is null && estimate.OriginalAiredAt is null))
            return null;

        return new EpisodeAiringView(
            context,
            scheduleView,
            key.Source,
            key.ID,
            sequenceNumber,
            estimate.EpisodeKey,
            estimate.AiredAt,
            estimate.OriginalAiredAt,
            target?.Episode ?? episode,
            target?.Key
        );
    }

    /// <summary>
    /// De-duplicate the channels two providers both report, then order by the
    /// preference the caller asked for: the track, then the channel, then a
    /// real airing over an estimate, then time, then provider priority, then
    /// the channel's name.
    /// </summary>
    /// <param name="context">The read the views belong to.</param>
    /// <param name="views">The airings that survived the filters.</param>
    /// <param name="options">The preference to order by.</param>
    /// <param name="preferredOnly">
    /// Whether to reduce the answer to the best airing of each episode,
    /// overriding <see cref="EpisodeAiringFilteringOptions.PreferredOnly"/>. A
    /// range read passes <c>false</c> here and reduces after it has
    /// narrowed the airings to its window, so an episode keeps the best airing
    /// it has <em>in the window</em> rather than dropping out of it over a
    /// better one somewhere else.
    /// </param>
    /// <returns>The airings, best first.</returns>
    private IEnumerable<IEpisodeAiring> Order(
        AiringReadContext context,
        IReadOnlyList<EpisodeAiringView> views,
        EpisodeAiringFilteringOptions options,
        bool? preferredOnly = null
    )
    {
        // Ordering runs once per episode, and loading the settings goes through
        // the configuration provider, so the read holds on to them instead.
        var settings = context.Settings;
        var preferredChannels = options.PreferredChannels ?? settings.PreferredChannels;
        var preferredTracks = options.PreferredTracks ?? settings.PreferredTracks;
        var deduplicated = views
            // De-duplication is per episode per channel: it collapses the copies
            // two providers make of one slot, never two episodes that share a
            // channel, and never a channel's own repeat broadcasts of an episode.
            // Airings without a channel are never de-duplicated at all: there is
            // nothing to say they are the same slot.
            .GroupBy(view => (view.ScheduleView.Row.ChannelID, Episode: AiringEpisodeKey.For(view)))
            .SelectMany(group => group.Key.ChannelID is null ? group : DeduplicateChannel(context, group))
            .ToList();

        var ordered = deduplicated
            .OrderBy(view => GetTrackRank(view, preferredTracks))
            .ThenBy(view => GetChannelRank(view, preferredChannels))
            .ThenBy(view => view.IsEstimated)
            .ThenBy(view => view.AiredAt ?? view.OriginalAiredAt ?? DateTime.MaxValue)
            .ThenBy(view => context.GetProvider(view.ProviderID)?.Priority ?? int.MaxValue)
            .ThenBy(view => view.Channel?.Name ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(view => view.Key, StringComparer.Ordinal);
        if (!(preferredOnly ?? options.PreferredOnly))
            return ordered.Cast<IEpisodeAiring>();

        // One per episode rather than one overall, so a read that spans several
        // episodes keeps the best of each.
        var seen = new HashSet<AiringEpisodeKey>();
        return ordered.Where(view => seen.Add(AiringEpisodeKey.For(view))).Cast<IEpisodeAiring>();
    }

    /// <summary>
    /// Collapse the airings several providers report for one episode on one
    /// channel down to one schedule's, keeping the best of them: a real airing
    /// over an estimate, then the higher-priority provider.
    /// </summary>
    /// <remarks>
    /// Whole schedules are collapsed against each other rather than single
    /// airings, because a schedule is one provider's line for one channel and
    /// everything on it is that provider's own account of the slots. Two rows
    /// on one schedule are therefore two broadcasts, a rerun or a late-night
    /// repeat, and both are kept, while another provider's copy of the same
    /// release is dropped whole.
    /// </remarks>
    /// <param name="context">The read the views belong to.</param>
    /// <param name="views">The airings of one episode on a single channel.</param>
    /// <returns>The airings of the best schedule per release the channel carries.</returns>
    private static IEnumerable<EpisodeAiringView> DeduplicateChannel(AiringReadContext context, IEnumerable<EpisodeAiringView> views)
    {
        var kept = new List<EpisodeAiringView>();
        var keptSchedules = new HashSet<int>();
        foreach (var view in views
            .OrderBy(entry => entry.IsEstimated)
            .ThenBy(entry => context.GetProvider(entry.ProviderID)?.Priority ?? int.MaxValue)
            .ThenBy(entry => entry.AiredAt ?? entry.OriginalAiredAt ?? DateTime.MaxValue)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal))
        {
            // Another slot on a schedule the channel already kept is a second
            // broadcast, not a second provider's copy of the first.
            if (keptSchedules.Contains(view.ScheduleView.Row.AiringScheduleID))
            {
                kept.Add(view);
                continue;
            }

            // A release already kept wins, so the first match ends it; nothing
            // else can promote a view once a better one covers it.
            if (kept.Any(entry => IsSameRelease(entry, view)))
                continue;

            keptSchedules.Add(view.ScheduleView.Row.AiringScheduleID);
            kept.Add(view);
        }

        return kept;
    }

    /// <summary>
    /// Whether a schedule's airings are part of a read at all.
    /// </summary>
    /// <param name="context">The read the view belongs to.</param>
    /// <param name="view">The schedule's view.</param>
    /// <param name="options">The filters to apply.</param>
    /// <param name="honourHiddenChannels">
    /// Whether a schedule on a hidden channel is left out when the read names
    /// no channels and its context does not take hidden channels in.
    /// </param>
    /// <returns><c>true</c> when the schedule passes every hard filter.</returns>
    private static bool MatchesFilters(
        AiringReadContext context,
        AiringScheduleView view,
        EpisodeAiringFilteringOptions options,
        bool honourHiddenChannels = true
    )
    {
        if (!options.IncludeDisabled && (!context.IsProviderVisible(view.ProviderID) || !view.HasVisibleTracks))
            return false;
        if (options.ProviderIDs is { } providerIDs && !providerIDs.Contains(view.ProviderID))
            return false;
        if (options.ChannelIDs is { } channelIDs)
        {
            if (view.Row.ChannelID is not { } channelID || !channelIDs.Contains(channelID))
                return false;
        }
        else if (honourHiddenChannels && !context.IncludeHiddenChannels && view.Row.ChannelID is { } hiddenID && context.HiddenChannels.Contains(hiddenID))
        {
            return false;
        }

        if (options.Kinds is { } kinds && !view.Tracks.Any(track => kinds.Contains(track.Kind)))
            return false;
        if (options.Languages is { } languages && !view.Tracks.Any(track => languages.Contains(track.Language)))
            return false;

        return true;
    }

    /// <summary>
    /// The anchor a read runs with: the caller's, or the one inferred from the
    /// entity it was given. A read that takes no entity infers
    /// <see cref="AiringEntityAnchor.Raw"/>, which is what every read did
    /// before the anchor existed.
    /// </summary>
    /// <param name="anchor">The anchor the caller asked for.</param>
    /// <param name="entity">The entity the read was given, or <c>null</c> when it takes none.</param>
    /// <param name="alreadySatisfied">
    /// Whether everything this read can find is anchored to shoko by the way it
    /// was found, which collapses a shoko anchor back to
    /// <see cref="AiringEntityAnchor.Raw"/> so it drops nothing. A read that
    /// started from a shoko-resolvable entity only ever reaches that entity's
    /// own schedules and the ones its links lead to, so there is nothing left
    /// for the anchor to filter and the read answers exactly what it always
    /// did.
    /// </param>
    /// <returns>The resolved anchor, never <see cref="AiringEntityAnchor.Auto"/>.</returns>
    internal static AiringEntityAnchor ResolveAnchor(AiringEntityAnchor anchor, IMetadata? entity, bool alreadySatisfied = false)
    {
        var resolved = anchor switch
        {
            AiringEntityAnchor.Raw => AiringEntityAnchor.Raw,
            AiringEntityAnchor.Shoko => AiringEntityAnchor.Shoko,
            _ => entity is IShokoSeries or ISeason<IShokoSeries, IShokoEpisode> or IShokoEpisode ? AiringEntityAnchor.Shoko : AiringEntityAnchor.Raw,
        };
        return alreadySatisfied && resolved is AiringEntityAnchor.Shoko ? AiringEntityAnchor.Raw : resolved;
    }

    /// <summary>
    /// Drop the airings a shoko anchor does not want: the ones that resolve to
    /// no shoko episode at all, unless they are unresolved airings of a shoko
    /// series. A raw anchor keeps everything as the provider gave it.
    /// </summary>
    /// <remarks>
    /// This composes with <see cref="EpisodeAiringFilteringOptions.LinkedEntityAirings"/>
    /// rather than overriding it: the links decide what the read <em>finds</em>,
    /// and the anchor decides what survives. A read that started from a shoko
    /// episode has already resolved every view for it, so the anchor it infers
    /// has nothing left to drop and the read is unchanged; one that started from
    /// a provider episode only reaches a shoko episode through the links, so
    /// anchoring to shoko without them correctly answers nothing.
    /// </remarks>
    /// <param name="views">The airings that survived the filters, narrowed in place.</param>
    /// <param name="anchor">The resolved entity anchor.</param>
    private static void ApplyAnchor(List<EpisodeAiringView> views, AiringEntityAnchor anchor)
    {
        if (anchor is not AiringEntityAnchor.Shoko)
            return;

        views.RemoveAll(view => !IsAnchoredToShoko(view));
    }

    /// <summary>
    /// Whether an airing survives a shoko anchor: it has a shoko episode, or it
    /// is unresolved and its place belongs to a shoko series.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <returns><c>true</c> when it survives.</returns>
    internal static bool IsAnchoredToShoko(IEpisodeAiring airing)
        => airing.ShokoEpisode is not null || (airing.EpisodeID is null && airing.ShokoSeries is not null);

    /// <summary>
    /// Whether a stored airing is hidden from reads, which only happens to a
    /// slotless airing another airing of the same channel has since overtaken.
    /// </summary>
    /// <param name="context">The read the lookups are cached in.</param>
    /// <param name="row">The airing's schedule.</param>
    /// <param name="entry">The stored airing.</param>
    /// <param name="now">The current time, in UTC.</param>
    /// <returns><c>true</c> when the airing is hidden.</returns>
    private bool IsHidden(AiringReadContext context, AiringSchedule row, EpisodeAiring entry, DateTime now)
        => entry.AiredAt is null && GetSupersededEpisodeKeys(context, row, [entry], now).Count > 0;

    /// <summary>
    /// Whether an episode belongs to the season a schedule narrows to.
    /// </summary>
    /// <param name="context">The read the resolution belongs to.</param>
    /// <param name="row">The schedule.</param>
    /// <param name="key">The stored key of the episode.</param>
    /// <returns><c>true</c> when the episode is in the season, or the season can't be resolved.</returns>
    private static bool IsInSeason(AiringReadContext context, AiringSchedule row, (MetadataSource Source, string ID) key)
    {
        // An unresolvable season is not fatal, so an episode isn't dropped over it.
        if (context.GetSeason(row.SeriesSource, row.SeasonID) is null)
            return true;

        return context.GetSeasonEpisodeKeys(row.SeriesSource, row.SeasonID).Contains(key);
    }

    /// <summary>
    /// The episodes a schedule can hold airings for: its season's when it
    /// narrows to one, and its series' otherwise.
    /// </summary>
    /// <param name="context">The read the resolution belongs to.</param>
    /// <param name="row">The schedule.</param>
    /// <returns>The episodes, or nothing when the entity can't be resolved.</returns>
    internal static IReadOnlyList<IEpisode> GetScheduleEpisodes(AiringReadContext context, AiringSchedule row)
    {
        if (!string.IsNullOrEmpty(row.SeasonID))
            return context.GetSeason(row.SeriesSource, row.SeasonID)?.Episodes ?? [];

        return context.GetSeries(row.SeriesSource, row.SeriesID)?.Episodes ?? [];
    }

    /// <summary>
    /// What a schedule's stored airings already stand for, so a read estimates
    /// none of it again: the episodes they resolve to and their numbers on the
    /// line.
    /// </summary>
    /// <param name="context">The read the resolutions belong to.</param>
    /// <param name="row">The schedule.</param>
    /// <param name="airings">The schedule's stored airings.</param>
    /// <returns>The coverage.</returns>
    private static AiringCoverage GetCoverage(AiringReadContext context, AiringSchedule row, IReadOnlyList<EpisodeAiring> airings)
    {
        var episodes = new HashSet<(MetadataSource Source, string ID)>();
        var numbers = new HashSet<int>();
        foreach (var entry in airings)
        {
            if (context.ResolveEpisode(row, entry) is { } episode)
                episodes.Add(GetEntityKey(episode));
            if (entry.SequenceNumber is { } sequenceNumber)
                numbers.Add(row.FirstEpisodeNumber + sequenceNumber - 1);
        }

        return new(episodes, numbers);
    }

    /// <summary>
    /// What a schedule's stored airings stand for.
    /// </summary>
    /// <param name="Episodes">The episodes they resolve to.</param>
    /// <param name="Numbers">Their numbers on the line.</param>
    private readonly record struct AiringCoverage(IReadOnlySet<(MetadataSource Source, string ID)> Episodes, IReadOnlySet<int> Numbers)
    {
        /// <summary>
        /// Whether an episode of the schedule's own series or season is
        /// covered.
        /// </summary>
        /// <param name="key">The stored key of the episode.</param>
        /// <param name="episode">The episode.</param>
        /// <returns><c>true</c> when it is covered.</returns>
        public bool Covers((MetadataSource Source, string ID) key, IEpisode episode)
            => Episodes.Contains(key) || (episode.Type is EpisodeType.Episode && Numbers.Contains(episode.EpisodeNumber));

        /// <summary>
        /// Whether an episode the schedule places at a number is covered.
        /// </summary>
        /// <param name="key">The stored key of the episode.</param>
        /// <param name="number">Its number on the line.</param>
        /// <returns><c>true</c> when it is covered.</returns>
        public bool Covers((MetadataSource Source, string ID) key, int number)
            => Episodes.Contains(key) || Numbers.Contains(number);
    }

    /// <summary>
    /// Whether an airing falls in a range, by its current slot or, for the one
    /// airing that caused a delay, by the slot it was moved out of.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <param name="fromUtc">The start of the range.</param>
    /// <param name="toUtc">The end of the range.</param>
    /// <param name="includeDelayedOriginalSlots">Whether a delayed airing matches by its original slot too.</param>
    /// <param name="includeOnAir">Whether an airing still on air at the start of a non-empty range matches too.</param>
    /// <returns><c>true</c> when the airing is part of the range.</returns>
    private static bool IsInRange(IEpisodeAiring airing, DateTime fromUtc, DateTime toUtc, bool includeDelayedOriginalSlots, bool includeOnAir)
    {
        if ((airing.AiredAt ?? airing.OriginalAiredAt) is { } slot && slot >= fromUtc && slot < toUtc)
            return true;

        if (includeOnAir && fromUtc < toUtc && airing.IsAiringAt(fromUtc))
            return true;

        return includeDelayedOriginalSlots && airing.IsDelayed && airing.OriginalAiredAt is { } original && original >= fromUtc && original < toUtc;
    }

    /// <summary>
    /// The time a range read orders an airing by: the earlier of its current
    /// and its original slot that falls in the range, so a delayed airing sorts
    /// where its gap is drawn. A date-only entry sorts at the start of its day.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <param name="fromUtc">The inclusive start of the range.</param>
    /// <param name="toUtc">The exclusive end of the range.</param>
    /// <param name="offset">The offset a date-only entry's day is read in.</param>
    /// <returns>The time to order by, in UTC.</returns>
    private static DateTime GetRangeSortTime(IEpisodeAiring airing, DateTime fromUtc, DateTime toUtc, TimeSpan offset)
    {
        if (airing is EpisodeAirDateView dateOnly)
            return dateOnly.GetDayStart(offset);

        var airedAt = airing.AiredAt is { } aired && aired >= fromUtc && aired < toUtc ? aired : (DateTime?)null;
        var originalAiredAt = airing.OriginalAiredAt is { } original && original >= fromUtc && original < toUtc ? original : (DateTime?)null;
        if (airedAt is { } first && originalAiredAt is { } second)
            return first <= second ? first : second;

        return airedAt ?? originalAiredAt ?? airing.AiredAt ?? airing.OriginalAiredAt ?? DateTime.MaxValue;
    }

    /// <summary>
    /// Shift a date by a number of days without running off either end of the
    /// calendar, so a range read at the very edge of <see cref="DateOnly"/>
    /// widens up to the bound instead of throwing.
    /// </summary>
    /// <param name="date">The date to shift.</param>
    /// <param name="days">The number of days to shift it by. A negative count moves it back.</param>
    /// <returns>The shifted date, clamped to the representable range.</returns>
    private static DateOnly AddDaysClamped(DateOnly date, int days)
        => date.AddDays(int.Clamp(days, DateOnly.MinValue.DayNumber - date.DayNumber, DateOnly.MaxValue.DayNumber - date.DayNumber));

    /// <summary>
    /// Shift a point in time by an offset without running off either end of the
    /// calendar, so the slack a range read widens its window by is clamped
    /// rather than throwing at the edges of <see cref="DateTime"/>.
    /// </summary>
    /// <param name="value">The point in time to shift.</param>
    /// <param name="offset">The offset to shift it by. A negative offset moves it back.</param>
    /// <returns>The shifted time, clamped to the representable range.</returns>
    private static DateTime AddClamped(DateTime value, TimeSpan offset)
    {
        if (offset > TimeSpan.Zero)
            return DateTime.MaxValue - value < offset ? DateTime.MaxValue : value + offset;
        if (offset < TimeSpan.Zero)
            return value - DateTime.MinValue < -offset ? DateTime.MinValue : value + offset;

        return value;
    }

    /// <summary>
    /// The episodes a range read may need an estimate for: the ones a running
    /// schedule has no airing for, its own and the AniDB episodes it places by
    /// an <see cref="AiringEpisodeOffset"/>, whose AniDB date, or whose Original anchor
    /// when it has no AniDB date, lands near the range, which is what puts a
    /// not-yet-scheduled episode on a calendar at all.
    /// </summary>
    /// <param name="context">The read the resolutions belong to.</param>
    /// <param name="fromUtc">The start of the range.</param>
    /// <param name="toUtc">The end of the range.</param>
    /// <param name="options">The filters to apply.</param>
    /// <returns>The candidate episode keys.</returns>
    private IEnumerable<(MetadataSource Source, string ID)> GetEstimableEpisodeKeys(
        AiringReadContext context,
        DateTime fromUtc,
        DateTime toUtc,
        EpisodeAiringFilteringOptions options
    )
    {
        var dormantBefore = AddClamped(fromUtc, -_dormantScheduleWindow);
        foreach (var row in RepoFactory.AiringSchedule.GetAll())
        {
            if (row.IsFinished)
                continue;

            var scheduleView = context.GetSchedule(row);
            if (!MatchesFilters(context, scheduleView, options))
                continue;

            var airings = RepoFactory.EpisodeAiring.GetByScheduleID(row.AiringScheduleID);
            // A schedule nothing has touched in a season isn't running any more,
            // whatever it says, so it costs a range read nothing.
            if (airings.Count > 0 && airings.Max(entry => entry.AiredAt ?? entry.OriginalAiredAt) is { } latest && latest < dormantBefore)
                continue;

            var profile = context.GetProfile(row);
            if ((profile.Offset ?? profile.AnidbOffset) is not { } offset)
                continue;

            // The window is widened by the slot itself and by any trailing
            // shift, since an estimate lands that far from the AniDB date.
            var slack = offset.Duration() + TimeSpan.FromDays(1 + Math.Abs(profile.TrailingShiftDays));
            var windowStart = AddClamped(fromUtc, -slack);
            var windowEnd = AddClamped(toUtc, slack);
            var coverage = GetCoverage(context, row, airings);
            var episodes = GetScheduleEpisodes(context, row)
                .Select(episode => (Episode: episode, Number: (int?)null))
                .Concat(GetUnlinkedEpisodes(context, row).Select(entry => (Episode: (IEpisode)entry.Episode, Number: (int?)entry.Number)));
            foreach (var (episode, number) in episodes)
            {
                if (episode.Type is not EpisodeType.Episode)
                    continue;

                var key = GetEntityKey(episode);
                if (number is { } placed ? coverage.Covers(key, placed) : coverage.Covers(key, episode))
                    continue;

                if (context.GetAnidbAirDate(episode) is { } airDate)
                {
                    if (airDate < windowStart || airDate > windowEnd)
                        continue;
                }
                // An episode with no AniDB date at all is only estimable off a
                // real Original airing, and then the anchor is what says where
                // the estimate lands. It is only looked up for the handful of
                // episodes that have no date, so it costs a range read nothing.
                else if (profile.Anchor is not AiringAnchor.FirstOriginalAiring ||
                    context.GetFirstOriginalAiringAt(new(episode, GetEpisodeTargetKey(context, episode, linked: true))) is not { } anchor ||
                    anchor < windowStart || anchor > windowEnd)
                    continue;

                yield return key;
            }
        }
    }

    #endregion

    #region Episode Airings | Filters, Date-Only and Next

    /// <summary>
    /// The read every entity read runs: each target's airings, a date-only
    /// entry for an episode with none when one was asked for, and the next
    /// airing of each group when only that was asked for.
    /// </summary>
    /// <param name="context">The read the views belong to.</param>
    /// <param name="targets">The episodes and places the read is for.</param>
    /// <param name="options">The filters and preference to read with.</param>
    /// <param name="anchor">The resolved entity anchor.</param>
    /// <returns>The airings, each target's best first.</returns>
    private List<IEpisodeAiring> ReadEntityAirings(
        AiringReadContext context,
        IEnumerable<AiringReadTarget> targets,
        EpisodeAiringFilteringOptions options,
        AiringEntityAnchor anchor
    )
    {
        var now = context.Now;
        var today = DateOnly.FromDateTime(now);
        var airings = new List<IEpisodeAiring>();
        foreach (var target in targets)
        {
            // A next-only read reduces after it has dropped what already aired,
            // so a target keeps the best airing it has still to come.
            IEnumerable<IEpisodeAiring> targetAirings = MarkPreferred(
                options.NextOnly
                    ? ReadAirings(context, target, options, anchor, preferredOnly: false)
                        .Where(airing => GetNextTime(airing, now, today, TimeSpan.Zero) is not null)
                    : ReadAirings(context, target, options, anchor)
            );
            if (options.NextOnly && targetAirings.Any() && HasPremieredBefore(context, target, options, now))
                continue;

            if (options.NextOnly && options.PreferredOnly)
                targetAirings = targetAirings.Take(1);

            var count = airings.Count;
            airings.AddRange(targetAirings);
            if (airings.Count == count && target.Episode is { } episode && AllowsDateOnly(options) &&
                GetDateOnlyEntry(context, episode, options, anchor, null, null) is { } entry)
                airings.Add(entry);
        }

        return options.NextOnly ? ReduceToNext(airings, options, now, today, TimeSpan.Zero) : airings;
    }

    /// <summary>
    /// Flag the first airing of each episode, the one a preferred-only read
    /// keeps, out of a list ordered by preference within each episode and
    /// already narrowed to the read's window and filters.
    /// </summary>
    /// <param name="airings">The airings, each episode's best first.</param>
    /// <returns>Every airing, in the same order.</returns>
    private static List<IEpisodeAiring> MarkPreferred(IEnumerable<IEpisodeAiring> airings)
    {
        var seen = new HashSet<AiringEpisodeKey>();
        var marked = new List<IEpisodeAiring>();
        foreach (var airing in airings)
        {
            if (airing is EpisodeAiringView view)
                view.IsPreferred = seen.Add(AiringEpisodeKey.For(airing));

            marked.Add(airing);
        }

        return marked;
    }

    /// <summary>
    /// Whether an airing passes the filters on itself, its episode and its
    /// series: the kinds of showing, the episode types, the user, the
    /// restricted and collection filters, and the filter. The series is only
    /// resolved when one of them asks for it.
    /// </summary>
    /// <param name="context">The read the airing belongs to.</param>
    /// <param name="airing">The airing.</param>
    /// <param name="options">The filters to apply.</param>
    /// <returns><c>true</c> when the airing passes every one of them.</returns>
    internal static bool PassesAiringFilters(AiringReadContext context, IEpisodeAiring airing, EpisodeAiringFilteringOptions options)
    {
        if (options.EpisodeKinds is { } episodeKinds && !episodeKinds.Contains(airing.Kind))
            return false;

        var isUnresolved = airing.EpisodeID is null;
        if (!options.IncludeUnresolved && isUnresolved)
            return false;

        if (options.EpisodeTypes is { } episodeTypes)
        {
            // An unresolved airing is on the numbered line, which only regular episodes are.
            var episodeType = ((IEpisode?)airing.AnidbEpisode ?? (IEpisode?)airing.ShokoEpisode ?? airing.Episode)?.Type ??
                (isUnresolved ? EpisodeType.Episode : null);
            if (episodeType is not { } type || !episodeTypes.Contains(type))
                return false;
        }

        if (options is { User: null, IncludeRestricted: InclusionFilter.True, InCollection: InclusionFilter.True, Filter: null })
            return true;

        var filteredAnimeIDs = options.Filter is { } filter ? context.GetFilteredAnimeIDs(filter, options.User) : null;
        return PassesSeriesFilters(context.GetSeriesState(airing), options, filteredAnimeIDs);
    }

    /// <summary>
    /// Whether a series passes the filters on it: the user, the restricted
    /// and collection filters, and the filter's results.
    /// </summary>
    /// <param name="state">The series' state.</param>
    /// <param name="options">The filters to apply.</param>
    /// <param name="filteredAnimeIDs">The anime the filter passed, or <c>null</c> without a filter.</param>
    /// <returns><c>true</c> when the series passes every one of them.</returns>
    internal static bool PassesSeriesFilters(AiringSeriesState state, EpisodeAiringFilteringOptions options, IReadOnlySet<int>? filteredAnimeIDs = null)
    {
        if (options.User is { } user && state.AnidbAnime is { } anime && !user.IsAllowedToSee(anime))
            return false;

        if (filteredAnimeIDs is not null && (!state.IsInCollection || state.AnidbAnimeID is not { } animeID || !filteredAnimeIDs.Contains(animeID)))
            return false;

        return options.IncludeRestricted.Passes(state.IsRestricted) &&
            options.InCollection.Passes(state.IsInCollection);
    }

    /// <summary>
    /// Whether a read can return date-only entries at all. One counts as a
    /// <see cref="EpisodeAiringKind.Normal"/> <see cref="AiringKind.Original"/>
    /// showing in no particular language on no channel by no provider, so a
    /// filter on any of those leaves it out.
    /// </summary>
    /// <param name="options">The read's options.</param>
    /// <returns><c>true</c> when the read asked for date-only entries and can have them.</returns>
    private static bool AllowsDateOnly(EpisodeAiringFilteringOptions options)
        => options.IncludeDateOnly &&
            options.ProviderIDs is null &&
            options.ChannelIDs is null &&
            options.Languages is null &&
            (options.Kinds is null || options.Kinds.Contains(AiringKind.Original)) &&
            (options.EpisodeKinds is null || options.EpisodeKinds.Contains(EpisodeAiringKind.Normal));

    /// <summary>
    /// The date-only entries of a range read: the AniDB episodes whose air
    /// date, or the date linked to them before 1970, falls between the two
    /// calendar dates and that have no airing.
    /// </summary>
    /// <param name="context">The read the entries belong to.</param>
    /// <param name="firstDate">The first date of the range.</param>
    /// <param name="lastDate">The last date of the range.</param>
    /// <param name="options">The filters to apply.</param>
    /// <param name="anchor">The resolved entity anchor.</param>
    /// <returns>The entries.</returns>
    private IEnumerable<IEpisodeAiring> GetDateOnlyEntriesInRange(
        AiringReadContext context,
        DateOnly firstDate,
        DateOnly lastDate,
        EpisodeAiringFilteringOptions options,
        AiringEntityAnchor anchor
    )
    {
        // The stored date is the earliest showing, which the air date never
        // precedes, and an early showing is never as much as a year early.
        var earliest = AddDaysClamped(firstDate, -DateOnlyEarlyShowingSlackDays);
        var episodes = RepoFactory.AniDB_Episode.GetForDate(
            earliest.ToDateTime(TimeOnly.MinValue),
            lastDate.ToDateTime(TimeOnly.MaxValue)
        );
        foreach (var episode in episodes)
        {
            if (GetDateOnlyEntry(context, episode, options, anchor, firstDate, lastDate) is { } entry)
                yield return entry;
        }

        foreach (var episodeID in _linkedAirDates.GetEpisodesInRange(firstDate, lastDate))
        {
            if (RepoFactory.AniDB_Episode.GetByEpisodeID(episodeID) is { } episode &&
                GetDateOnlyEntry(context, episode, options, anchor, firstDate, lastDate) is { } entry)
                yield return entry;
        }
    }

    /// <summary>
    /// The date-only entry of an episode: its AniDB episode by its air date,
    /// or the date linked to it before 1970, when no provider has an airing
    /// for it at all and it passes the read's filters.
    /// </summary>
    /// <param name="context">The read the entry belongs to.</param>
    /// <param name="episode">The episode.</param>
    /// <param name="options">The filters to apply.</param>
    /// <param name="anchor">The resolved entity anchor.</param>
    /// <param name="firstDate">The first date the air date may fall on, or <c>null</c> for any.</param>
    /// <param name="lastDate">The last date the air date may fall on, or <c>null</c> for any.</param>
    /// <returns>The entry, or <c>null</c>.</returns>
    private EpisodeAirDateView? GetDateOnlyEntry(
        AiringReadContext context,
        IEpisode episode,
        EpisodeAiringFilteringOptions options,
        AiringEntityAnchor anchor,
        DateOnly? firstDate,
        DateOnly? lastDate
    )
    {
        var (anidbEpisode, shokoEpisode) = context.GetEpisodeViews(episode);
        if (anidbEpisode is null || (anidbEpisode.AirDate ?? GetLinkedAirDate(anidbEpisode)) is not { } airDate)
            return null;
        if (airDate < firstDate || airDate > lastDate)
            return null;
        if (anchor is AiringEntityAnchor.Shoko && shokoEpisode is null)
            return null;

        // Known only by its date means no provider knows the episode at all,
        // whatever the read filters its airings by.
        var known = ReadAirings(
            context,
            new(anidbEpisode, GetEpisodeTargetKey(context, anidbEpisode, linked: true)),
            new EpisodeAiringFilteringOptions
            {
                IncludeEstimates = options.IncludeEstimates,
                IncludeDisabled = options.IncludeDisabled,
                LinkedEntityAirings = true,
            },
            AiringEntityAnchor.Raw,
            preferredOnly: true,
            honourHiddenChannels: false
        );
        if (known.Count > 0)
            return null;

        var entry = new EpisodeAirDateView(context, anidbEpisode, shokoEpisode, airDate);
        return PassesAiringFilters(context, entry, options) ? entry : null;
    }

    /// <summary>
    /// The date linked to an undated regular AniDB episode of an anime
    /// starting by 1970-01-01, which AniDB gives no date or its 1970-01-01
    /// placeholder, when its anime's own date does not stand in: the
    /// earliest pre-1970 air date of the episodes linked to it.
    /// </summary>
    /// <param name="anidbEpisode">The AniDB episode, which has no air date.</param>
    /// <returns>
    /// The date, or <c>null</c> when the episode is dated, the anime is not
    /// known to start by 1970-01-01, or nothing linked has one.
    /// </returns>
    private DateOnly? GetLinkedAirDate(IAnidbEpisode anidbEpisode)
    {
        if (RepoFactory.AniDB_Episode.GetByEpisodeID(anidbEpisode.AnidbID) is not { } episode || !AnidbLinkedAirDateCache.IsUndatedPre1970(episode))
            return null;

        return _linkedAirDates.GetAirDate(episode.EpisodeID);
    }

    /// <summary>
    /// When an airing counts as being next: its current slot, when that is at
    /// or after the reference or still on air then, or the start of a
    /// date-only entry's day, when that is at or after the reference.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <param name="afterUtc">The reference instant.</param>
    /// <param name="afterDate">The reference date, for date-only entries.</param>
    /// <param name="offset">The offset a date-only entry's day is read in.</param>
    /// <returns>The time, or <c>null</c> when the airing is neither on air nor upcoming.</returns>
    private static DateTime? GetNextTime(IEpisodeAiring airing, DateTime afterUtc, DateOnly afterDate, TimeSpan offset)
    {
        if (airing is EpisodeAirDateView dateOnly)
            return dateOnly.AirDate >= afterDate ? dateOnly.GetDayStart(offset) : null;

        return airing.AiredAt is { } airedAt && (airedAt >= afterUtc || airing.IsAiringAt(afterUtc)) ? airedAt : null;
    }

    /// <summary>
    /// Whether a target has already premiered: a real
    /// <see cref="EpisodeAiringKind.Normal"/> airing of it, on any schedule of
    /// any provider and channel, whose slot ended by the reference. A next-only
    /// read skips such a target, so a delayed broadcast of it is never next.
    /// </summary>
    /// <param name="context">The read the check belongs to.</param>
    /// <param name="target">The target.</param>
    /// <param name="options">The read's options, for the links it walks.</param>
    /// <param name="beforeUtc">The reference instant.</param>
    /// <returns><c>true</c> when the target has premiered.</returns>
    private bool HasPremieredBefore(AiringReadContext context, AiringReadTarget target, EpisodeAiringFilteringOptions options, DateTime beforeUtc)
    {
        var shown = ReadAirings(
            context,
            target,
            new EpisodeAiringFilteringOptions
            {
                EpisodeKinds = _premiereKinds,
                IncludeEstimates = false,
                IncludeDisabled = true,
                LinkedEntityAirings = options.LinkedEntityAirings,
            },
            AiringEntityAnchor.Raw,
            preferredOnly: false,
            honourHiddenChannels: false
        );
        // The start is checked first, so no end is worked out for an airing still to come.
        return shown.Any(airing => airing.AiredAt is { } airedAt && airedAt < beforeUtc && airing.EndsAt is { } endsAt && endsAt <= beforeUtc);
    }

    /// <summary>
    /// Reduce a read to the next airing of each group: the earliest episode
    /// of the group, answered with its best airing by preference.
    /// </summary>
    /// <param name="airings">The airings, each episode's in preference order.</param>
    /// <param name="options">The read's options, for the grouping and the kinds.</param>
    /// <param name="afterUtc">The reference instant.</param>
    /// <param name="afterDate">The reference date, for date-only entries.</param>
    /// <param name="offset">The offset a date-only entry's day is read in.</param>
    /// <returns>The next airings, ordered by time.</returns>
    private static List<IEpisodeAiring> ReduceToNext(
        IEnumerable<IEpisodeAiring> airings,
        EpisodeAiringFilteringOptions options,
        DateTime afterUtc,
        DateOnly afterDate,
        TimeSpan offset
    )
    {
        var per = options.NextPer ?? _defaultNextPer;
        var episodeBest = new Dictionary<(NextGroupKey Group, AiringEpisodeKey Episode), (DateTime Time, IEpisodeAiring Airing)>();
        foreach (var airing in airings)
        {
            if (GetNextTime(airing, afterUtc, afterDate, offset) is not { } time)
                continue;

            // The first airing seen is the episode's best, and the earliest one
            // is what the episode is next by.
            var episodeKey = AiringEpisodeKey.For(airing);
            foreach (var group in GetNextGroupKeys(airing, per, options.Kinds))
            {
                episodeBest[(group, episodeKey)] = episodeBest.TryGetValue((group, episodeKey), out var seen)
                    ? (time < seen.Time ? time : seen.Time, seen.Airing)
                    : (time, airing);
            }
        }

        var groups = new Dictionary<NextGroupKey, (DateTime Time, int Number, IEpisodeAiring Airing)>();
        foreach (var ((group, _), (time, airing)) in episodeBest)
        {
            var number = ((IEpisode?)airing.AnidbEpisode ?? (IEpisode?)airing.ShokoEpisode ?? airing.Episode)?.EpisodeNumber ??
                airing.AnidbEpisodeNumber ??
                airing.EpisodeNumber ??
                int.MaxValue;
            if (!groups.TryGetValue(group, out var current) ||
                time < current.Time ||
                (time == current.Time && (number < current.Number || (number == current.Number && airing.ID.CompareTo(current.Airing.ID) < 0))))
                groups[group] = (time, number, airing);
        }

        return groups.Values
            .OrderBy(entry => entry.Time)
            .ThenBy(entry => entry.Number)
            .Select(entry => entry.Airing)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// The groups an airing counts for in a next-only read.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <param name="per">What to keep one airing per.</param>
    /// <param name="kinds">The kinds the read filters by, or <c>null</c> for every kind.</param>
    /// <returns>The groups.</returns>
    private static IEnumerable<NextGroupKey> GetNextGroupKeys(IEpisodeAiring airing, IReadOnlySet<AiringNextGrouping> per, IReadOnlySet<AiringKind>? kinds)
    {
        var series = per.Contains(AiringNextGrouping.Series) ? AiringReadContext.GetSeriesFor(airing)?.ID : null;
        var channel = per.Contains(AiringNextGrouping.Channel) ? airing.Channel?.ChannelID : null;
        if (!per.Contains(AiringNextGrouping.Kind))
        {
            yield return new(series, channel, null);
            yield break;
        }

        List<AiringKind> airingKinds = airing.IsDateOnly
            ? [AiringKind.Original]
            : [.. airing.Tracks.Select(track => track.Kind).Where(kind => kinds is null || kinds.Contains(kind)).Distinct()];
        if (airingKinds.Count is 0)
        {
            yield return new(series, channel, null);
            yield break;
        }

        foreach (var kind in airingKinds)
            yield return new(series, channel, kind);
    }

    /// <summary>
    /// One group of a next-only read. A part the read does not group by is
    /// <c>null</c>.
    /// </summary>
    /// <param name="Series">The series.</param>
    /// <param name="Channel">The channel.</param>
    /// <param name="Kind">The track kind.</param>
    private readonly record struct NextGroupKey(MetadataGuid? Series, Guid? Channel, AiringKind? Kind);

    #endregion

    #region Episode Airings | Anime by Channel

    /// <summary>
    /// The AniDB anime with a stored airing on one of the channels, hidden or
    /// not, from one pass over those channels' airings: the calendar quarters
    /// the airings fall in, and whether one is still to come.
    /// </summary>
    /// <remarks>
    /// An airing counts for every AniDB anime its episode is linked to, and an
    /// unresolved one for the anime its place stands for. Old seasons may have
    /// no airings left to count, since retention removes them.
    /// </remarks>
    /// <param name="channelIDs">The channels.</param>
    /// <param name="at">The time an airing is still to come after, or <c>null</c> for now.</param>
    /// <exception cref="ArgumentNullException"><paramref name="channelIDs"/> is <c>null</c>.</exception>
    /// <returns>The anime's airings on the channels, by AniDB anime ID.</returns>
    internal IReadOnlyDictionary<int, AnidbAnimeChannelAirings> GetAnidbAnimeOnChannels(IReadOnlySet<Guid> channelIDs, DateTime? at = null)
    {
        ArgumentNullException.ThrowIfNull(channelIDs);

        var context = new AiringReadContext(this, at: at);
        var options = new EpisodeAiringFilteringOptions { ChannelIDs = channelIDs };
        var now = context.Now;
        var found = new Dictionary<int, AnidbAnimeChannelAirings>();
        foreach (var channelID in channelIDs)
        {
            foreach (var row in RepoFactory.AiringSchedule.GetByChannelID(channelID))
            {
                var scheduleView = context.GetSchedule(row);
                if (!MatchesFilters(context, scheduleView, options))
                    continue;

                foreach (var entry in RepoFactory.EpisodeAiring.GetByScheduleID(row.AiringScheduleID))
                {
                    if (entry.AiredAt is not { } airedAt)
                        continue;

                    var view = new EpisodeAiringView(context, scheduleView, entry);
                    var animeIDs = GetAnidbAnimeIDs(view.Episode);
                    if (animeIDs.Count is 0 && view.AnidbAnimeID is { } anidbAnimeID)
                        animeIDs = [anidbAnimeID];

                    var quarter = SeasonCalendar.GetCalendarQuarter(DateOnly.FromDateTime(airedAt));
                    foreach (var animeID in animeIDs)
                    {
                        if (!found.TryGetValue(animeID, out var airings))
                            found[animeID] = airings = new AnidbAnimeChannelAirings();

                        airings.Seasons.Add(quarter);
                        airings.HasUpcoming |= airedAt >= now;
                    }
                }
            }
        }

        return found;
    }

    /// <summary>
    /// The AniDB anime an episode belongs to: its own, or those of the AniDB
    /// episodes behind its Shoko episodes.
    /// </summary>
    /// <param name="episode">The episode, or <c>null</c> when it could not be resolved.</param>
    /// <returns>The AniDB anime IDs.</returns>
    private static IReadOnlyList<int> GetAnidbAnimeIDs(IEpisode? episode)
        => episode switch
        {
            null => [],
            IAnidbEpisode anidbEpisode => [anidbEpisode.AnidbAnimeID],
            IShokoEpisode shokoEpisode => shokoEpisode.AnidbEpisode is { } anidbEpisode ? [anidbEpisode.AnidbAnimeID] : [],
            _ => [.. episode.ShokoEpisodes.Select(shokoEpisode => shokoEpisode.AnidbEpisode?.AnidbAnimeID).OfType<int>().Distinct()],
        };

    #endregion

    #region Episode Airings | Anime Airing Dates

    /// <summary>
    /// The local dates of an AniDB anime's stored normal airings, which place
    /// it in the yearly seasons alongside AniDB's own episode dates. Kept for
    /// every anime, and gathered again after any change that could move them.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime ID.</param>
    /// <returns>The dates, or <c>null</c> when it has none or the parts are not added yet.</returns>
    internal AnidbAnimeAiringDates? GetAnidbAiringDates(int anidbAnimeID)
        => _loaded ? AnidbAiringDates.Get(anidbAnimeID) : null;

    /// <summary>
    /// The cache of every AniDB anime's airing dates, made on first use.
    /// </summary>
    private AnidbAiringDateCache AnidbAiringDates
        => LazyInitializer.EnsureInitialized(ref _anidbAiringDates, () => new(this, metadataService, linkingService));

    /// <summary>
    /// Gathers the local dates of every AniDB anime's stored airings: only
    /// the normal ones with a slot, of enabled providers on schedules not
    /// detected as reruns, hidden channels included. Estimates are never
    /// stored, so they never count.
    /// </summary>
    /// <remarks>
    /// An airing counts for the AniDB episodes behind its episode: its own,
    /// or those of its Shoko episodes. An airing matched to none, unresolved
    /// ones among them, counts for the anime its number on the line stands
    /// for: the anime of an AniDB or Shoko schedule, or an anime linked to a
    /// plugin schedule's series whose episode numbering, learned from the
    /// linked episodes, carries on to it within the anime's episode count.
    /// </remarks>
    /// <returns>The dates, by AniDB anime ID.</returns>
    internal Dictionary<int, AnidbAnimeAiringDates> BuildAnidbAiringDates()
    {
        var context = new AiringReadContext(this, includeHiddenChannels: true);
        var options = new EpisodeAiringFilteringOptions();
        var matched = new Dictionary<int, Dictionary<int, DateOnly>>();
        var unmatched = new Dictionary<int, Dictionary<AiringEpisodeKey, DateOnly>>();
        foreach (var row in RepoFactory.AiringSchedule.GetAll())
        {
            if (!MatchesFilters(context, context.GetSchedule(row), options, honourHiddenChannels: false) || context.IsDetectedRerun(row))
                continue;

            var zone = row.TimeZoneID is { Length: > 0 } timeZoneID && TryResolveTimeZone(timeZoneID, out var resolved) ? resolved : TimeZoneInfo.Utc;
            foreach (var entry in RepoFactory.EpisodeAiring.GetByScheduleID(row.AiringScheduleID))
            {
                if (entry.Kind is not EpisodeAiringKind.Normal || entry.AiredAt is not { } airedAt)
                    continue;

                var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(airedAt, DateTimeKind.Utc), zone));
                var episode = context.ResolveEpisode(row, entry);
                var anidbEpisodes = GetAnidbEpisodes(episode);
                foreach (var anidbEpisode in anidbEpisodes)
                    AddEarliest(matched, anidbEpisode.AnidbAnimeID, anidbEpisode.AnidbID, date);

                if (anidbEpisodes.Count > 0)
                    continue;

                // An AniDB episode not cached yet still names itself.
                if (entry.IsPinned && entry.EpisodeSource == MetadataSource.AniDB && row.SeriesSource == MetadataSource.AniDB &&
                    int.TryParse(entry.EpisodeID, out var anidbEpisodeID) && int.TryParse(row.SeriesID, out var anidbAnimeID))
                {
                    AddEarliest(matched, anidbAnimeID, anidbEpisodeID, date);
                    continue;
                }

                var number = entry.SequenceNumber is { } sequenceNumber
                    ? row.FirstEpisodeNumber + sequenceNumber - 1
                    : episode is { Type: EpisodeType.Episode } ? episode.EpisodeNumber : (int?)null;
                if (number is null || context.GetAnidbPosition(row, number.Value) is not { } position)
                    continue;

                if (context.GetAnidbEpisode(position.AnimeID, position.EpisodeNumber) is { } listed)
                    AddEarliest(matched, position.AnimeID, listed.AnidbID, date);
                else
                    AddEarliest(unmatched, position.AnimeID, AiringEpisodeKey.ForAnidbPosition(position.AnimeID, position.EpisodeNumber), date);
            }
        }

        var result = new Dictionary<int, AnidbAnimeAiringDates>();
        foreach (var animeID in matched.Keys.Union(unmatched.Keys))
        {
            result[animeID] = new(
                matched.TryGetValue(animeID, out var episodes) ? episodes : new Dictionary<int, DateOnly>(),
                unmatched.TryGetValue(animeID, out var others) ? [.. others.Values.Order()] : []
            );
        }

        return result;
    }

    /// <summary>
    /// The AniDB episodes behind an episode: itself, or those of its Shoko
    /// episodes.
    /// </summary>
    /// <param name="episode">The episode, or <c>null</c> when it could not be resolved.</param>
    /// <returns>The AniDB episodes.</returns>
    private static IReadOnlyList<IAnidbEpisode> GetAnidbEpisodes(IEpisode? episode)
        => episode switch
        {
            null => [],
            IAnidbEpisode anidbEpisode => [anidbEpisode],
            IShokoEpisode shokoEpisode => shokoEpisode.AnidbEpisode is { } anidbEpisode ? [anidbEpisode] : [],
            _ =>
            [
                .. episode.ShokoEpisodes
                    .Select(shokoEpisode => shokoEpisode.AnidbEpisode)
                    .OfType<IAnidbEpisode>()
                    .DistinctBy(anidbEpisode => anidbEpisode.AnidbID),
            ],
        };

    /// <summary>
    /// Keeps the earliest date of a key for an anime.
    /// </summary>
    /// <typeparam name="TKey">The kind of key.</typeparam>
    /// <param name="dates">The dates, by anime and key.</param>
    /// <param name="animeID">The AniDB anime ID.</param>
    /// <param name="key">The key.</param>
    /// <param name="date">The date.</param>
    private static void AddEarliest<TKey>(Dictionary<int, Dictionary<TKey, DateOnly>> dates, int animeID, TKey key, DateOnly date) where TKey : notnull
    {
        if (!dates.TryGetValue(animeID, out var byKey))
            dates[animeID] = byKey = [];

        if (!byKey.TryGetValue(key, out var earliest) || date < earliest)
            byKey[key] = date;
    }

    #endregion

    #region Episode Airings | Preference

    /// <summary>
    /// Where an airing's tracks sit in the caller's track preference. An empty
    /// preference ranks everything the same, which skips the step.
    /// </summary>
    /// <param name="view">The airing.</param>
    /// <param name="preferences">The ordered track preference.</param>
    /// <returns>The rank, lower being better.</returns>
    private static int GetTrackRank(EpisodeAiringView view, IReadOnlyList<AiringTrackPreference> preferences)
    {
        for (var index = 0; index < preferences.Count; index++)
            if (view.Tracks.Any(track => MatchesPreference(track, preferences[index])))
                return index;

        return preferences.Count;
    }

    /// <summary>
    /// Where an airing's channel sits in the caller's channel preference. An
    /// empty preference ranks everything the same, which skips the step.
    /// </summary>
    /// <param name="view">The airing.</param>
    /// <param name="preferences">The ordered channel preference.</param>
    /// <returns>The rank, lower being better.</returns>
    private static int GetChannelRank(EpisodeAiringView view, IReadOnlyList<Guid> preferences)
    {
        if (view.ScheduleView.Row.ChannelID is not { } channelID)
            return preferences.Count;

        for (var index = 0; index < preferences.Count; index++)
            if (preferences[index] == channelID)
                return index;

        return preferences.Count;
    }

    /// <summary>
    /// Whether a track is what a preference entry asked for. A preference with
    /// no language matches every language of that kind.
    /// </summary>
    /// <param name="track">The track.</param>
    /// <param name="preference">The preference entry.</param>
    /// <returns><c>true</c> when the track matches.</returns>
    private static bool MatchesPreference(IAiringTrack track, AiringTrackPreference preference)
    {
        if (track.Kind != preference.Kind)
            return false;
        if (preference.LanguageCode is not { Length: > 0 } languageCode)
            return true;
        if (string.Equals(track.LanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))
            return true;

        return track.Language is not (TitleLanguage.Unknown or TitleLanguage.None) &&
            languageCode.TryGetTitleLanguage(out var language) &&
            track.Language == language;
    }

    /// <summary>
    /// What makes two airings on the same channel the same release for
    /// de-duplication: a shared track, matched by <see cref="TracksMatch"/> as
    /// everywhere else, so the same channel reported as <c>en</c> by one
    /// provider and <c>eng</c> by another collapses while a channel that
    /// genuinely carries two different releases doesn't.
    /// </summary>
    /// <param name="one">One airing.</param>
    /// <param name="other">The other airing.</param>
    /// <returns><c>true</c> when the two are the same release.</returns>
    private static bool IsSameRelease(EpisodeAiringView one, EpisodeAiringView other)
    {
        // A schedule with no visible track has nothing to match on, and only
        // reaches here on a read that asked for disabled data. Two of them are
        // still one slot on the channel, as they were when this compared whole
        // track sets.
        if (one.Tracks.Count is 0 || other.Tracks.Count is 0)
            return one.Tracks.Count == other.Tracks.Count;

        return one.Tracks.Any(track => other.Tracks.Any(entry => TracksMatch(track, entry)));
    }

    #endregion

    #region Estimates

    /// <summary>
    /// The estimate profile of a schedule, learned from its own airings the
    /// first time it is needed and kept until something changes it.
    /// </summary>
    /// <param name="row">The schedule to learn from.</param>
    /// <param name="context">The read the resolutions belong to.</param>
    /// <returns>The schedule's profile.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> or <paramref name="context"/> is <c>null</c>.</exception>
    internal AiringScheduleProfile GetProfile(AiringSchedule row, AiringReadContext context)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(context);

        if (_profiles.TryGetValue(row.AiringScheduleID, out var known))
            return known;

        var airings = RepoFactory.EpisodeAiring.GetByScheduleID(row.AiringScheduleID);
        var linkKeys = GetLinkKeys(airings);
        // A simulpub follows the broadcast, not its own channel's history, so a
        // schedule with nothing Original on it anchors on the broadcast instead.
        var anchored = row.Tracks.Count > 0 && row.Tracks.All(track => track.Kind is not AiringKind.Original);
        var scheduleView = context.GetSchedule(row);
        var samples = airings
            .Select(entry =>
            {
                var view = new EpisodeAiringView(context, scheduleView, entry);
                return new AiringProfileSample()
                {
                    EpisodeKey = GetLineKey(entry),
                    EpisodeNumber = view.EpisodeNumber ?? (view.Episode is { Type: EpisodeType.Episode } episode ? episode.EpisodeNumber : null),
                    AnidbAirDate = context.GetAnidbAirDate(view.Episode ?? view.AnidbEpisode),
                    AiredAt = entry.AiredAt,
                    OriginalAiredAt = entry.OriginalAiredAt,
                    IsDelayed = entry.IsDelayed,
                    Kind = entry.Kind,
                    LinkKey = linkKeys.GetValueOrDefault(entry.EpisodeAiringID),
                    FirstOriginalAiringAt = anchored ? context.GetFirstOriginalAiringAt(view.Target) : null,
                };
            })
            .ToList();
        var profile = AiringScheduleUtility.LearnProfile(samples, new AiringProfileOptions()
        {
            AnchorOnFirstOriginalAiring = anchored,
            IsFinished = row.IsFinished,
            LastEpisodeNumber = row.LastEpisodeNumber,
        });
        _profiles[row.AiringScheduleID] = profile;
        return profile;
    }

    #endregion

    #region Estimates | Unlinked Episodes

    /// <summary>
    /// Learn how an AniDB anime's regular episodes number on a schedule from
    /// the ones linked into its series, and into its season when it narrows to
    /// one. No episode on the schedule's source has to exist for the episodes
    /// the offset places: the schedule's own numbering and cadence do that.
    /// </summary>
    /// <remarks>
    /// The anime has to be linked to the schedule's series, and the regular
    /// episodes linked into it have to agree on one offset, as a run with no
    /// gaps. Anything else learns nothing, so nothing is estimated.
    /// </remarks>
    /// <param name="context">The read the lookups are cached in.</param>
    /// <param name="anidbAnimeID">The AniDB anime.</param>
    /// <param name="row">The schedule.</param>
    /// <returns>The offset, or <c>null</c> when the links give no consistent one.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="row"/> is <c>null</c>.</exception>
    internal static AiringEpisodeOffset? LearnEpisodeOffset(AiringReadContext context, int anidbAnimeID, AiringSchedule row)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(row);

        var source = row.SeriesSource;
        if (source.IsCore || string.IsNullOrEmpty(row.SeriesID))
            return null;

        var isSeriesLinked = context.GetSeriesLinks(anidbAnimeID).Any(link =>
            link.Source == source &&
            link.ProviderID is { } providerID &&
            providerID.EntityType == MetadataEntityType.Series &&
            providerID.ID == row.SeriesID
        );
        if (!isSeriesLinked)
            return null;

        var linkedEpisodeIDs = new HashSet<int>();
        var episodeNumbers = new HashSet<int>();
        var offset = default(int?);
        var lastScheduleNumber = 0;
        foreach (var link in context.GetEpisodeLinks(anidbAnimeID))
        {
            if (link.Source != source || link.ProviderID is not { } providerID)
                continue;

            linkedEpisodeIDs.Add(link.AnidbEpisodeID);
            if (context.GetEpisode(source, providerID.ID) is not { Type: EpisodeType.Episode } provider || provider.SeriesID.ID != row.SeriesID)
                continue;
            if (!string.IsNullOrEmpty(row.SeasonID) && !IsInSeason(context, row, (source, providerID.ID)))
                continue;

            lastScheduleNumber = Math.Max(lastScheduleNumber, provider.EpisodeNumber);
            if (context.GetEpisode(MetadataSource.AniDB, link.AnidbEpisodeID.ToString()) is not { Type: EpisodeType.Episode } anidbEpisode)
                continue;

            // Two links disagreeing, or one episode linked twice, leave the numbering unknown.
            var value = provider.EpisodeNumber - anidbEpisode.EpisodeNumber;
            if ((offset is { } known && known != value) || !episodeNumbers.Add(anidbEpisode.EpisodeNumber))
                return null;

            offset = value;
        }

        // So does a gap among the linked episodes.
        if (offset is not { } learned || episodeNumbers.Max() - episodeNumbers.Min() + 1 != episodeNumbers.Count)
            return null;

        return new(learned, episodeNumbers.Max(), lastScheduleNumber, linkedEpisodeIDs);
    }

    /// <summary>
    /// The AniDB episodes a schedule places by an <see cref="AiringEpisodeOffset"/>:
    /// the regular episodes of each anime linked to its series that continue
    /// past the last one linked into it, with their number on the schedule.
    /// </summary>
    /// <param name="context">The read the lookups are cached in.</param>
    /// <param name="row">The schedule.</param>
    /// <returns>The episodes and their numbers on the schedule.</returns>
    private static IEnumerable<(IAnidbEpisode Episode, int Number)> GetUnlinkedEpisodes(AiringReadContext context, AiringSchedule row)
    {
        foreach (var animeID in context.GetLinkedAnimeIDs(row.SeriesSource, row.SeriesID))
        {
            if (context.GetEpisodeOffset(animeID, row) is not { } offset)
                continue;

            foreach (var episode in RepoFactory.AniDB_Episode.GetByAnimeID(animeID))
            {
                if (offset.GetScheduleEpisodeNumber(episode) is { } number)
                    yield return (episode, number);
            }
        }
    }

    /// <summary>
    /// The schedules an AniDB episode is placed on by an <see cref="AiringEpisodeOffset"/>:
    /// those of the series its anime is linked to on the sources it has no
    /// episode link on, with its number on each.
    /// </summary>
    /// <param name="context">The read the lookups are cached in.</param>
    /// <param name="episode">The AniDB episode.</param>
    /// <returns>The schedules and the episode's number on each.</returns>
    private static IEnumerable<(AiringSchedule Schedule, int Number)> GetUnlinkedEpisodeSchedules(AiringReadContext context, IAnidbEpisode episode)
    {
        foreach (var link in context.GetSeriesLinks(episode.AnidbAnimeID))
        {
            if (link.Source.IsCore || link.ProviderID is not { } seriesID || seriesID.EntityType != MetadataEntityType.Series)
                continue;

            foreach (var row in RepoFactory.AiringSchedule.GetBySeriesID(link.Source, seriesID.ID))
            {
                if (context.GetEpisodeOffset(episode.AnidbAnimeID, row)?.GetScheduleEpisodeNumber(episode) is { } number)
                    yield return (row, number);
            }
        }
    }

    #endregion

    #region Invalidation

    /// <inheritdoc/>
    public void InvalidateForSeries(ISeries series)
    {
        ArgumentNullException.ThrowIfNull(series);

        foreach (var key in GetInvalidationKeys(series))
            InvalidateProfilesForSeries(key.Source, key.ID);
    }

    /// <inheritdoc/>
    public void InvalidateForSeason(ISeason season)
    {
        ArgumentNullException.ThrowIfNull(season);

        // A season clears the schedules attached to it and the series' schedules
        // that cover it, which is every schedule of the series either way.
        if (season.Series is { } series)
            InvalidateForSeries(series);
        else
            InvalidateProfilesForSeries(season.Source, season.SeriesID.ID);

        if (season.Series is IShokoSeries)
            foreach (var linked in season.LinkedSeasons)
                InvalidateProfilesForSeries(linked.Source, linked.SeriesID.ID);
    }

    /// <inheritdoc/>
    public void InvalidateForEpisode(IEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);

        var context = new AiringReadContext(this, includeDisabled: true);
        foreach (var (source, id) in context.GetLinkedEpisodeKeys(episode))
            if (context.GetEpisode(source, id) is { } entry)
                InvalidateProfilesForSeries(source, entry.SeriesID.ID);
    }

    /// <summary>
    /// The series keys an invalidation touches: the series itself, and the ones
    /// linked to it when it is a shoko series.
    /// </summary>
    /// <param name="series">The series being invalidated.</param>
    /// <returns>The keys to clear.</returns>
    private IEnumerable<(MetadataSource Source, string ID)> GetInvalidationKeys(ISeries series)
    {
        var keys = new HashSet<(MetadataSource, string)> { GetEntityKey(series) };
        foreach (var shokoSeries in series is IShokoSeries own ? [own] : series.ShokoSeries)
        {
            keys.Add(GetEntityKey(shokoSeries));
            foreach (var linked in shokoSeries.LinkedSeries)
                keys.Add(GetEntityKey(linked));
        }

        return keys;
    }

    /// <summary>
    /// Drop the cached profiles of every schedule for a series, which is what a
    /// write, a relink or a kind change makes stale.
    /// </summary>
    /// <param name="source">The source of the series.</param>
    /// <param name="id">The ID of the series within its source.</param>
    private void InvalidateProfilesForSeries(MetadataSource source, string id)
    {
        foreach (var row in RepoFactory.AiringSchedule.GetBySeriesID(source, id))
            _profiles.TryRemove(row.AiringScheduleID, out _);
    }

    #endregion

    #region Retention

    /// <inheritdoc/>
    public TimeSpan Retention
    {
        get
        {
            var now = UtcNow;
            return now - GetRetentionCutoff(LoadSettings(), now);
        }
    }

    /// <inheritdoc/>
    public DateTime? RetentionCutoff
    {
        get
        {
            var settings = LoadSettings();
            return settings.AutoCleanup ? GetRetentionCutoff(settings, UtcNow) : null;
        }
    }

    /// <summary>
    /// Remove the schedules whose run ended longer ago than the retention
    /// window, with their airings, and the schedules a provider created but
    /// never filled. A running schedule keeps every airing, however old, so a
    /// decades-long weekly show stays whole.
    /// </summary>
    /// <param name="progress">Told how far the sweep is, from 0 to 100.</param>
    /// <param name="cancellationToken">Stops the sweep between two schedules.</param>
    /// <returns>How many schedules were removed.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    internal int RunRetentionSweep(IProgress<decimal>? progress = null, CancellationToken cancellationToken = default)
    {
        var settings = LoadSettings();
        if (!settings.AutoCleanup)
            return 0;

        var now = UtcNow;
        var cutoff = GetRetentionCutoff(settings, now);
        var removed = 0;
        var rows = RepoFactory.AiringSchedule.GetAll().ToList();
        var items = new ItemProgress(progress, rows.Count);
        items.Report(0);
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            items.Increment();
            var airings = RepoFactory.EpisodeAiring.GetByScheduleID(row.AiringScheduleID);
            if (airings.Count is 0)
            {
                if (now - row.CreatedAt <= EmptyScheduleBuffer)
                    continue;

                RemoveSchedule(row);
                removed++;
                continue;
            }

            // A run ends at its last airing, not its first, so a finished cour
            // keeps its whole history until the lot ages out.
            if (airings.Max(entry => entry.AiredAt ?? entry.OriginalAiredAt) is not { } latest || latest >= cutoff)
                continue;

            RemoveSchedule(row);
            removed++;
        }

        if (removed > 0)
            logger.LogInformation("Removed {Count} airing schedules that ended before {Cutoff}.", removed, cutoff);

        return removed;
    }

    #endregion
}
