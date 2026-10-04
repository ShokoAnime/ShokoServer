using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Models.Airing;
using Shoko.Server.Repositories;
using Shoko.Server.Services.Airing;
using Shoko.Server.Utilities;
using Shoko.Server.Utilities.Airing;

#nullable enable
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

    #region Episode Airings | Reading

    /// <inheritdoc/>
    public IReadOnlyList<IEpisodeAiring> GetAiringsForEpisode(IEpisode episode, EpisodeAiringFilteringOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(episode);
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        options ??= new EpisodeAiringFilteringOptions();
        var context = new AiringReadContext(this, options.IncludeDisabled);
        context.Remember(episode);
        return ReadEntityAirings(context, [episode], options, ResolveAnchor(options.EntityAnchor, episode));
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
        var context = new AiringReadContext(this, options.IncludeDisabled);
        var anchor = ResolveAnchor(options.EntityAnchor, series);
        return ReadEntityAirings(context, series.Episodes, options, anchor);
    }

    /// <inheritdoc/>
    public IReadOnlyList<IEpisodeAiring> GetAiringsForSeason(ISeason season, EpisodeAiringFilteringOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(season);
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        options ??= new EpisodeAiringFilteringOptions();
        var context = new AiringReadContext(this, options.IncludeDisabled);
        var anchor = ResolveAnchor(options.EntityAnchor, season);
        return ReadEntityAirings(context, season.Episodes, options, anchor);
    }

    /// <inheritdoc/>
    public IReadOnlyList<IEpisodeAiring> GetAiringsForSchedule(Guid scheduleID, EpisodeAiringFilteringOptions? options = null)
    {
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        options ??= new EpisodeAiringFilteringOptions();
        var context = new AiringReadContext(this, options.IncludeDisabled);
        if (RepoFactory.AiringSchedule.GetByScheduleID(scheduleID) is not { } row)
            return [];

        // No entity was passed in, so there is nothing to infer an anchor from.
        var anchor = ResolveAnchor(options.EntityAnchor, null);
        var scheduleView = context.GetSchedule(row);
        if (!MatchesFilters(context, scheduleView, options))
            return [];

        var now = DateTime.UtcNow;
        var airings = RepoFactory.EpisodeAiring.GetByScheduleID(row.AiringScheduleID);
        var views = airings
            .Where(entry => !IsHidden(row, entry, now))
            .Select(entry => new EpisodeAiringView(context, scheduleView, entry))
            .ToList();
        if (options.IncludeEstimates)
        {
            var covered = airings.Select(entry => (entry.EpisodeSource, entry.EpisodeID)).ToHashSet();
            foreach (var episode in GetScheduleEpisodes(context, row))
            {
                var key = GetEntityKey(episode);
                if (covered.Contains(key))
                    continue;
                if (Estimate(context, row, scheduleView, episode, key) is { } estimate)
                    views.Add(estimate);
            }
        }

        ApplyAnchor(views, anchor);
        views.RemoveAll(view => !PassesAiringFilters(context, view, options));
        if (!options.NextOnly)
            return Order(context, views, options).ToList();

        var ordered = Order(context, views, options, preferredOnly: false)
            .Where(airing => GetNextTime(airing, now, DateOnly.FromDateTime(now), TimeSpan.Zero) is not null);
        return ReduceToNext(options.PreferredOnly ? TakePreferred(ordered) : ordered, options, now, DateOnly.FromDateTime(now), TimeSpan.Zero);
    }

    /// <inheritdoc/>
    public IReadOnlyList<IEpisodeAiring> GetAiringsInRange(DateTimeOffset from, DateTimeOffset to, EpisodeAiringFilteringOptions? options = null)
    {
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");
        if (to < from)
            throw new ArgumentException("The end of the range is before its start.", nameof(to));

        options ??= new EpisodeAiringFilteringOptions();
        var context = new AiringReadContext(this, options.IncludeDisabled);
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
        // it has been through the whole per-episode pipeline.
        var candidates = RepoFactory.EpisodeAiring.GetByDayRange(firstBucket, lastBucket)
            .Concat(includeGaps ? RepoFactory.EpisodeAiring.GetDelayedByOriginalDayRange(firstBucket, lastBucket) : [])
            .Where(entry => MatchesSchedule(entry.AiringScheduleID))
            .Select(entry => (entry.EpisodeSource, entry.EpisodeID))
            .ToHashSet();
        if (options.IncludeEstimates)
            foreach (var key in GetEstimableEpisodeKeys(context, fromUtc, toUtc, options))
                candidates.Add(key);

        var targets = new Dictionary<(MetadataSource, string), IEpisode>();
        foreach (var (source, id) in candidates)
        {
            if (context.GetEpisode(source, id) is not { } episode)
                continue;

            // One airing linked to two AniDB episodes is two views, but the same
            // episode reached through two keys is still one.
            var linked = options.LinkedEntityAirings ?? true;
            var resolved = linked && episode is not IShokoEpisode && episode.ShokoEpisodes.Count > 0
                ? episode.ShokoEpisodes.Cast<IEpisode>().ToList()
                : [episode];
            foreach (var target in resolved)
                targets.TryAdd(GetEntityKey(target), target);
        }

        // The window narrows first and the preference reduces afterwards, so an
        // episode is answered with the best airing it has in the window. The
        // other way round an episode whose best airing is elsewhere would drop
        // out of the day it actually airs on.
        var airings = targets.Values
            .SelectMany(episode =>
            {
                var inRange = ReadAirings(context, episode, options, anchor, preferredOnly: false)
                    .Where(airing => IsInRange(airing, fromUtc, toUtc, includeGaps));
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
    /// The one read every airing read runs: collect the episode's keys, load
    /// what is stored under them, filter, add the estimates the surviving
    /// schedules produce, de-duplicate channels and order by preference.
    /// </summary>
    /// <param name="context">The read the views belong to.</param>
    /// <param name="episode">The episode the read is for.</param>
    /// <param name="options">The filters and preference to read with.</param>
    /// <param name="anchor">The resolved entity anchor, never <see cref="AiringEntityAnchor.Auto"/>.</param>
    /// <param name="preferredOnly">Whether to reduce the answer to the episode's best airing, overriding <see cref="EpisodeAiringFilteringOptions.PreferredOnly"/>.</param>
    /// <returns>The episode's airings, best first.</returns>
    private List<IEpisodeAiring> ReadAirings(
        AiringReadContext context,
        IEpisode episode,
        EpisodeAiringFilteringOptions options,
        AiringEntityAnchor anchor,
        bool? preferredOnly = null
    )
    {
        var now = DateTime.UtcNow;
        var linked = options.LinkedEntityAirings ?? episode is IShokoEpisode;
        var keys = linked ? context.GetLinkedEpisodeKeys(episode) : [GetEntityKey(episode)];
        var views = new List<EpisodeAiringView>();
        var covered = new HashSet<int>();
        foreach (var (source, id) in keys)
        {
            foreach (var entry in RepoFactory.EpisodeAiring.GetByEpisodeID(source, id))
            {
                if (RepoFactory.AiringSchedule.GetByID(entry.AiringScheduleID) is not { } row)
                    continue;

                covered.Add(entry.AiringScheduleID);
                var scheduleView = context.GetSchedule(row);
                if (!MatchesFilters(context, scheduleView, options) || IsHidden(row, entry, now))
                    continue;

                views.Add(new EpisodeAiringView(context, scheduleView, entry, episode));
            }
        }

        if (options.IncludeEstimates)
            views.AddRange(GetEstimates(context, episode, keys, covered, options));

        // Before the ordering, so a preferred-only read is handed the best of
        // what the anchor and the filters kept rather than nothing at all.
        ApplyAnchor(views, anchor);
        views.RemoveAll(view => !PassesAiringFilters(context, view, options));
        return Order(context, views, options, preferredOnly).ToList();
    }

    /// <summary>
    /// The estimates every schedule that covers an episode, and has no airing
    /// for it, contributes. Filtering runs first, so no estimate is computed
    /// only to be thrown away.
    /// </summary>
    /// <param name="context">The read the views belong to.</param>
    /// <param name="episode">The episode the read is for.</param>
    /// <param name="keys">The episode's own key and its linked ones.</param>
    /// <param name="covered">The schedules that already have an airing for the episode.</param>
    /// <param name="options">The filters to apply.</param>
    /// <returns>One estimate per schedule that can produce one.</returns>
    private IEnumerable<EpisodeAiringView> GetEstimates(
        AiringReadContext context,
        IEpisode episode,
        IReadOnlyList<(MetadataSource Source, string ID)> keys,
        HashSet<int> covered,
        EpisodeAiringFilteringOptions options
    )
    {
        foreach (var key in keys)
        {
            if (context.GetEpisode(key.Source, key.ID) is not { Type: EpisodeType.Episode } target)
                continue;

            foreach (var row in RepoFactory.AiringSchedule.GetBySeriesID(key.Source, target.SeriesID.ID))
            {
                if (covered.Contains(row.AiringScheduleID))
                    continue;

                var scheduleView = context.GetSchedule(row);
                if (!MatchesFilters(context, scheduleView, options))
                    continue;

                if (Estimate(context, row, scheduleView, target, key, episode) is { } estimate)
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
    /// <param name="target">The episode being estimated.</param>
    /// <param name="key">The stored key of the episode being estimated.</param>
    /// <param name="resolvedFor">The episode the read ran for.</param>
    /// <returns>The estimate, or <c>null</c>.</returns>
    private static EpisodeAiringView? Estimate(
        AiringReadContext context,
        AiringSchedule row,
        AiringScheduleView scheduleView,
        IEpisode target,
        (MetadataSource Source, string ID) key,
        IEpisode? resolvedFor = null
    )
    {
        if (target.Type is not EpisodeType.Episode)
            return null;
        if (row.FirstEpisodeNumber is { } first && target.EpisodeNumber < first)
            return null;
        if (!string.IsNullOrEmpty(row.SeasonID) && !IsInSeason(context, row, key))
            return null;

        var estimate = AiringScheduleUtility.EstimateAiring(context.GetProfile(row), new AiringEstimateTarget()
        {
            EpisodeKey = AiringScheduleUtility.GetDerivedAiringKey(key.Source, key.ID),
            EpisodeNumber = target.EpisodeNumber,
            IsNormalEpisode = true,
            AnidbAirDate = context.GetAnidbAirDate(target),
            FirstOriginalAiringAt = context.GetFirstOriginalAiringAt(key.Source, key.ID),
        });
        if (estimate is null || (estimate.AiredAt is null && estimate.OriginalAiredAt is null))
            return null;

        return new EpisodeAiringView(context, scheduleView, key.Source, key.ID, estimate.EpisodeKey, estimate.AiredAt, estimate.OriginalAiredAt, resolvedFor ?? target);
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
            .GroupBy(view => (view.ScheduleView.Row.ChannelID, Episode: GetDeduplicationKey(view)))
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
        var seen = new HashSet<(MetadataSource Source, string ID)>();
        return ordered.Where(view => seen.Add(GetDeduplicationKey(view))).Cast<IEpisodeAiring>();
    }

    /// <summary>
    /// Which episode a view counts as, for de-duplication and for the one
    /// airing a preferred-only read keeps. A read that resolved the episode it
    /// ran for answers with that, so the same slot stored against two linked
    /// provider episodes is still one episode; a read that named none falls
    /// back to the airing's own stored episode.
    /// </summary>
    /// <param name="view">The airing.</param>
    /// <returns>The episode's key.</returns>
    private static (MetadataSource Source, string ID) GetDeduplicationKey(EpisodeAiringView view)
        => view.ResolvedFor is { } episode ? GetEntityKey(episode) : (view.EpisodeSource, view.EpisodeID);

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
    /// <returns><c>true</c> when the schedule passes every hard filter.</returns>
    private static bool MatchesFilters(AiringReadContext context, AiringScheduleView view, EpisodeAiringFilteringOptions options)
    {
        if (!options.IncludeDisabled && (!context.IsProviderVisible(view.ProviderID) || !view.HasVisibleTracks))
            return false;
        if (options.ProviderIDs is { } providerIDs && !providerIDs.Contains(view.ProviderID))
            return false;
        if (options.ChannelIDs is { } channelIDs && (view.Row.ChannelID is not { } channelID || !channelIDs.Contains(channelID)))
            return false;
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
    /// no shoko episode at all. A raw anchor keeps everything as the provider
    /// gave it.
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

        views.RemoveAll(view => view.ShokoEpisode is null);
    }

    /// <summary>
    /// Whether a stored airing is hidden from reads, which only happens to a
    /// slotless airing another airing of the same channel has since overtaken.
    /// </summary>
    /// <param name="row">The airing's schedule.</param>
    /// <param name="entry">The stored airing.</param>
    /// <param name="now">The current time, in UTC.</param>
    /// <returns><c>true</c> when the airing is hidden.</returns>
    private bool IsHidden(AiringSchedule row, EpisodeAiring entry, DateTime now)
        => entry.AiredAt is null && GetSupersededEpisodeKeys(row, [entry], now).Count > 0;

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
    private static IReadOnlyList<IEpisode> GetScheduleEpisodes(AiringReadContext context, AiringSchedule row)
    {
        if (!string.IsNullOrEmpty(row.SeasonID))
            return context.GetSeason(row.SeriesSource, row.SeasonID)?.Episodes ?? [];

        return context.GetSeries(row.SeriesSource, row.SeriesID)?.Episodes ?? [];
    }

    /// <summary>
    /// Whether an airing falls in a range, by its current slot or, for the one
    /// airing that caused a delay, by the slot it was moved out of.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <param name="fromUtc">The start of the range.</param>
    /// <param name="toUtc">The end of the range.</param>
    /// <param name="includeDelayedOriginalSlots">Whether a delayed airing matches by its original slot too.</param>
    /// <returns><c>true</c> when the airing is part of the range.</returns>
    private static bool IsInRange(IEpisodeAiring airing, DateTime fromUtc, DateTime toUtc, bool includeDelayedOriginalSlots)
    {
        if ((airing.AiredAt ?? airing.OriginalAiredAt) is { } slot && slot >= fromUtc && slot < toUtc)
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
    /// schedule has no airing for whose AniDB date, or whose Original anchor
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
            var covered = airings.Select(entry => (entry.EpisodeSource, entry.EpisodeID)).ToHashSet();
            foreach (var episode in GetScheduleEpisodes(context, row))
            {
                if (episode.Type is not EpisodeType.Episode)
                    continue;

                var key = GetEntityKey(episode);
                if (covered.Contains(key))
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
                    context.GetFirstOriginalAiringAt(key.Source, key.ID) is not { } anchor ||
                    anchor < windowStart || anchor > windowEnd)
                    continue;

                yield return key;
            }
        }
    }

    #endregion

    #region Episode Airings | Filters, Date-Only and Next

    /// <summary>
    /// The read every entity read runs: each episode's airings, a date-only
    /// entry for an episode with none when one was asked for, and the next
    /// airing of each group when only that was asked for.
    /// </summary>
    /// <param name="context">The read the views belong to.</param>
    /// <param name="episodes">The episodes the read is for.</param>
    /// <param name="options">The filters and preference to read with.</param>
    /// <param name="anchor">The resolved entity anchor.</param>
    /// <returns>The airings, each episode's best first.</returns>
    private List<IEpisodeAiring> ReadEntityAirings(
        AiringReadContext context,
        IEnumerable<IEpisode> episodes,
        EpisodeAiringFilteringOptions options,
        AiringEntityAnchor anchor
    )
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var airings = new List<IEpisodeAiring>();
        foreach (var episode in episodes)
        {
            // A next-only read reduces after it has dropped what already aired,
            // so an episode keeps the best airing it has still to come.
            IEnumerable<IEpisodeAiring> episodeAirings = options.NextOnly
                ? ReadAirings(context, episode, options, anchor, preferredOnly: false)
                    .Where(airing => GetNextTime(airing, now, today, TimeSpan.Zero) is not null)
                : ReadAirings(context, episode, options, anchor);
            if (options.NextOnly && options.PreferredOnly)
                episodeAirings = episodeAirings.Take(1);

            var count = airings.Count;
            airings.AddRange(episodeAirings);
            if (airings.Count == count && AllowsDateOnly(options) && GetDateOnlyEntry(context, episode, options, anchor, null, null) is { } entry)
                airings.Add(entry);
        }

        return options.NextOnly ? ReduceToNext(airings, options, now, today, TimeSpan.Zero) : airings;
    }

    /// <summary>
    /// Keep the first airing of each episode, the best by preference, out of a
    /// list ordered by preference within each episode.
    /// </summary>
    /// <param name="airings">The airings, each episode's best first.</param>
    /// <returns>One airing per episode.</returns>
    private static IEnumerable<IEpisodeAiring> TakePreferred(IEnumerable<IEpisodeAiring> airings)
    {
        var seen = new HashSet<(MetadataSource Source, string ID)>();
        return airings.Where(airing => seen.Add(GetEpisodeKeyFor(airing)));
    }

    /// <summary>
    /// Whether an airing passes the filters on its episode and its series: the
    /// episode types, the user, and the restricted, collection and missing
    /// filters. The series is only resolved when one of them asks for it.
    /// </summary>
    /// <param name="context">The read the airing belongs to.</param>
    /// <param name="airing">The airing.</param>
    /// <param name="options">The filters to apply.</param>
    /// <returns><c>true</c> when the airing passes every one of them.</returns>
    internal static bool PassesAiringFilters(AiringReadContext context, IEpisodeAiring airing, EpisodeAiringFilteringOptions options)
    {
        if (options.EpisodeTypes is { } episodeTypes)
        {
            var episodeType = ((IEpisode?)airing.AnidbEpisode ?? (IEpisode?)airing.ShokoEpisode ?? airing.Episode)?.Type;
            if (episodeType is not { } type || !episodeTypes.Contains(type))
                return false;
        }

        if (options is { User: null, IncludeRestricted: InclusionFilter.True, InCollection: InclusionFilter.True, IncludeMissing: InclusionFilter.True })
            return true;

        return PassesSeriesFilters(context.GetSeriesState(airing), options);
    }

    /// <summary>
    /// Whether a series passes the filters on it: the user, and the
    /// restricted, collection and missing filters.
    /// </summary>
    /// <param name="state">The series' state.</param>
    /// <param name="options">The filters to apply.</param>
    /// <returns><c>true</c> when the series passes every one of them.</returns>
    internal static bool PassesSeriesFilters(AiringSeriesState state, EpisodeAiringFilteringOptions options)
    {
        if (options.User is { } user && state.AnidbAnime is { } anime && !user.IsAllowedToSee(anime))
            return false;

        return options.IncludeRestricted.Passes(state.IsRestricted) &&
            options.InCollection.Passes(state.IsInCollection) &&
            options.IncludeMissing.Passes(state.IsMissing);
    }

    /// <summary>
    /// Whether a read can return date-only entries at all. One counts as an
    /// <see cref="AiringKind.Original"/> showing in no particular language on
    /// no channel by no provider, so a filter on any of those leaves it out.
    /// </summary>
    /// <param name="options">The read's options.</param>
    /// <returns><c>true</c> when the read asked for date-only entries and can have them.</returns>
    private static bool AllowsDateOnly(EpisodeAiringFilteringOptions options)
        => options.IncludeDateOnly &&
            options.ProviderIDs is null &&
            options.ChannelIDs is null &&
            options.Languages is null &&
            (options.Kinds is null || options.Kinds.Contains(AiringKind.Original));

    /// <summary>
    /// The date-only entries of a range read: the AniDB episodes whose air
    /// date falls between the two calendar dates and that have no airing.
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
    }

    /// <summary>
    /// The date-only entry of an episode: its AniDB episode by its air date,
    /// when no provider has an airing for it at all and it passes the read's
    /// filters.
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
        if (anidbEpisode?.AirDate is not { } airDate)
            return null;
        if (airDate < firstDate || airDate > lastDate)
            return null;
        if (anchor is AiringEntityAnchor.Shoko && shokoEpisode is null)
            return null;

        // Known only by its date means no provider knows the episode at all,
        // whatever the read filters its airings by.
        var known = ReadAirings(
            context,
            anidbEpisode,
            new EpisodeAiringFilteringOptions
            {
                IncludeEstimates = options.IncludeEstimates,
                IncludeDisabled = options.IncludeDisabled,
                LinkedEntityAirings = true,
            },
            AiringEntityAnchor.Raw,
            preferredOnly: true
        );
        if (known.Count > 0)
            return null;

        var entry = new EpisodeAirDateView(anidbEpisode, shokoEpisode, airDate);
        return PassesAiringFilters(context, entry, options) ? entry : null;
    }

    /// <summary>
    /// When an airing counts as being next: its current slot, or the start of
    /// a date-only entry's day, when that is at or after the reference.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <param name="afterUtc">The reference instant.</param>
    /// <param name="afterDate">The reference date, for date-only entries.</param>
    /// <param name="offset">The offset a date-only entry's day is read in.</param>
    /// <returns>The time, or <c>null</c> when the airing is not upcoming.</returns>
    private static DateTime? GetNextTime(IEpisodeAiring airing, DateTime afterUtc, DateOnly afterDate, TimeSpan offset)
    {
        if (airing is EpisodeAirDateView dateOnly)
            return dateOnly.AirDate >= afterDate ? dateOnly.GetDayStart(offset) : null;

        return airing.AiredAt is { } airedAt && airedAt >= afterUtc ? airedAt : null;
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
        var episodeBest = new Dictionary<(NextGroupKey Group, (MetadataSource Source, string ID) Episode), (DateTime Time, IEpisodeAiring Airing)>();
        foreach (var airing in airings)
        {
            if (GetNextTime(airing, afterUtc, afterDate, offset) is not { } time)
                continue;

            // The first airing seen is the episode's best, and the earliest one
            // is what the episode is next by.
            var episodeKey = GetEpisodeKeyFor(airing);
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
            var number = ((IEpisode?)airing.AnidbEpisode ?? (IEpisode?)airing.ShokoEpisode ?? airing.Episode)?.EpisodeNumber ?? int.MaxValue;
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
    /// Which episode an airing counts as: the one the read resolved it for, or
    /// its own stored episode.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <returns>The episode's key.</returns>
    private static (MetadataSource Source, string ID) GetEpisodeKeyFor(IEpisodeAiring airing)
        => airing is EpisodeAiringView view ? GetDeduplicationKey(view) : (airing.EpisodeID.Source, airing.EpisodeID.ID);

    /// <summary>
    /// One group of a next-only read. A part the read does not group by is
    /// <c>null</c>.
    /// </summary>
    /// <param name="Series">The series.</param>
    /// <param name="Channel">The channel.</param>
    /// <param name="Kind">The track kind.</param>
    private readonly record struct NextGroupKey(MetadataGuid? Series, Guid? Channel, AiringKind? Kind);

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
        var samples = airings
            .Select(entry =>
            {
                var episode = context.GetEpisode(entry.EpisodeSource, entry.EpisodeID);
                return new AiringProfileSample()
                {
                    EpisodeKey = AiringScheduleUtility.GetDerivedAiringKey(entry.EpisodeSource, entry.EpisodeID),
                    EpisodeNumber = episode is { Type: EpisodeType.Episode } ? episode.EpisodeNumber : null,
                    AnidbAirDate = context.GetAnidbAirDate(episode),
                    AiredAt = entry.AiredAt,
                    OriginalAiredAt = entry.OriginalAiredAt,
                    IsDelayed = entry.IsDelayed,
                    Kind = entry.Kind,
                    LinkKey = linkKeys.GetValueOrDefault(entry.EpisodeAiringID),
                    FirstOriginalAiringAt = anchored ? context.GetFirstOriginalAiringAt(entry.EpisodeSource, entry.EpisodeID) : null,
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
            var now = DateTime.UtcNow;
            return now - GetRetentionCutoff(LoadSettings(), now);
        }
    }

    /// <inheritdoc/>
    public DateTime? RetentionCutoff
    {
        get
        {
            var settings = LoadSettings();
            return settings.AutoCleanup ? GetRetentionCutoff(settings, DateTime.UtcNow) : null;
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

        var now = DateTime.UtcNow;
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
