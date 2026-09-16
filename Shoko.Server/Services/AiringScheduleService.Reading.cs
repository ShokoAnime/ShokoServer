using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
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
        return ReadAirings(context, episode, options);
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
        return series.Episodes.SelectMany(episode => ReadAirings(context, episode, options)).ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<IEpisodeAiring> GetAiringsForSeason(ISeason season, EpisodeAiringFilteringOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(season);
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        options ??= new EpisodeAiringFilteringOptions();
        var context = new AiringReadContext(this, options.IncludeDisabled);
        return season.Episodes.SelectMany(episode => ReadAirings(context, episode, options)).ToList();
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

        return Order(context, views, options).ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<IEpisodeAiring> GetAiringsInRange(DateTime fromUtc, DateTime toUtc, EpisodeAiringFilteringOptions? options = null)
    {
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");
        if (toUtc < fromUtc)
            throw new ArgumentException("The end of the range is before its start.", nameof(toUtc));

        options ??= new EpisodeAiringFilteringOptions();
        var context = new AiringReadContext(this, options.IncludeDisabled);
        var from = DateOnly.FromDateTime(fromUtc).AddDays(-RangeBucketSlackDays);
        var to = DateOnly.FromDateTime(toUtc).AddDays(RangeBucketSlackDays);

        // Everything already stored in the window, plus the airings that were
        // delayed out of it, which is what a calendar draws its gap from.
        var candidates = RepoFactory.EpisodeAiring.GetByDayRange(from, to)
            .Concat(options.IncludeDelayedOriginalSlots ? RepoFactory.EpisodeAiring.GetDelayedByOriginalDayRange(from, to) : [])
            .Select(entry => (entry.EpisodeSource, entry.EpisodeID))
            .ToHashSet();
        if (options.IncludeEstimates)
            foreach (var key in GetEstimableEpisodeKeys(context, fromUtc, toUtc, options))
                candidates.Add(key);

        var targets = new Dictionary<(DataSource, string), IEpisode>();
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

        return targets.Values
            .SelectMany(episode => ReadAirings(context, episode, options))
            .Where(airing => IsInRange(airing, fromUtc, toUtc, options.IncludeDelayedOriginalSlots))
            .OrderBy(airing => airing.AiredAt ?? airing.OriginalAiredAt ?? DateTime.MaxValue)
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
    /// <returns>The episode's airings, best first.</returns>
    private List<IEpisodeAiring> ReadAirings(AiringReadContext context, IEpisode episode, EpisodeAiringFilteringOptions options)
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

        return Order(context, views, options).ToList();
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
        IReadOnlyList<(DataSource Source, string ID)> keys,
        HashSet<int> covered,
        EpisodeAiringFilteringOptions options
    )
    {
        foreach (var key in keys)
        {
            if (context.GetEpisode(key.Source, key.ID) is not { Type: EpisodeType.Episode } target)
                continue;

            foreach (var row in RepoFactory.AiringSchedule.GetBySeriesID(key.Source, target.SeriesID.ToString()))
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
    /// The estimate one schedule makes for one episode, or <see langword="null"/>
    /// when its coverage, its season or its own hiatus says it makes none.
    /// </summary>
    /// <param name="context">The read the view belongs to.</param>
    /// <param name="row">The schedule doing the estimating.</param>
    /// <param name="scheduleView">The schedule's view.</param>
    /// <param name="target">The episode being estimated.</param>
    /// <param name="key">The stored key of the episode being estimated.</param>
    /// <param name="resolvedFor">The episode the read ran for.</param>
    /// <returns>The estimate, or <see langword="null"/>.</returns>
    private static EpisodeAiringView? Estimate(
        AiringReadContext context,
        AiringSchedule row,
        AiringScheduleView scheduleView,
        IEpisode target,
        (DataSource Source, string ID) key,
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
    /// <returns>The airings, best first.</returns>
    private IEnumerable<IEpisodeAiring> Order(AiringReadContext context, IReadOnlyList<EpisodeAiringView> views, EpisodeAiringFilteringOptions options)
    {
        var settings = LoadSettings();
        var preferredChannels = options.PreferredChannels ?? settings.PreferredChannels;
        var preferredTracks = options.PreferredTracks ?? settings.PreferredTracks;
        var deduplicated = views
            // Airings without a channel are never de-duplicated: there is nothing to say they are the same slot.
            .GroupBy(view => view.ScheduleView.Row.ChannelID is { } channelID ? (channelID, GetTrackSignature(view)) : ((Guid, string)?)null)
            .SelectMany(group => group.Key is null
                ? group
                : group
                    .OrderBy(view => view.IsEstimated)
                    .ThenBy(view => context.GetProvider(view.ProviderID)?.Priority ?? int.MaxValue)
                    .ThenBy(view => view.Key, StringComparer.Ordinal)
                    .Take(1))
            .ToList();

        var ordered = deduplicated
            .OrderBy(view => GetTrackRank(view, preferredTracks))
            .ThenBy(view => GetChannelRank(view, preferredChannels))
            .ThenBy(view => view.IsEstimated)
            .ThenBy(view => view.AiredAt ?? view.OriginalAiredAt ?? DateTime.MaxValue)
            .ThenBy(view => context.GetProvider(view.ProviderID)?.Priority ?? int.MaxValue)
            .ThenBy(view => view.Channel?.Name ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(view => view.Key, StringComparer.Ordinal)
            .Cast<IEpisodeAiring>();
        return options.PreferredOnly ? ordered.Take(1) : ordered;
    }

    /// <summary>
    /// Whether a schedule's airings are part of a read at all.
    /// </summary>
    /// <param name="context">The read the view belongs to.</param>
    /// <param name="view">The schedule's view.</param>
    /// <param name="options">The filters to apply.</param>
    /// <returns><see langword="true"/> when the schedule passes every hard filter.</returns>
    private static bool MatchesFilters(AiringReadContext context, AiringScheduleView view, EpisodeAiringFilteringOptions options)
    {
        if (!options.IncludeDisabled && (!context.IsProviderVisible(view.ProviderID) || !view.HasVisibleTracks))
            return false;
        if (options.ProviderID is { } providerID && view.ProviderID != providerID)
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
    /// Whether a stored airing is hidden from reads, which only happens to a
    /// slotless airing another airing of the same channel has since overtaken.
    /// </summary>
    /// <param name="row">The airing's schedule.</param>
    /// <param name="entry">The stored airing.</param>
    /// <param name="now">The current time, in UTC.</param>
    /// <returns><see langword="true"/> when the airing is hidden.</returns>
    private bool IsHidden(AiringSchedule row, EpisodeAiring entry, DateTime now)
        => entry.AiredAt is null && GetSupersededEpisodeKeys(row, [entry], now).Count > 0;

    /// <summary>
    /// Whether an episode belongs to the season a schedule narrows to.
    /// </summary>
    /// <param name="context">The read the resolution belongs to.</param>
    /// <param name="row">The schedule.</param>
    /// <param name="key">The stored key of the episode.</param>
    /// <returns><see langword="true"/> when the episode is in the season, or the season can't be resolved.</returns>
    private static bool IsInSeason(AiringReadContext context, AiringSchedule row, (DataSource Source, string ID) key)
    {
        // An unresolvable season is not fatal, so an episode isn't dropped over it.
        if (context.GetSeason(row.SeriesSource, row.SeasonID) is not { } season)
            return true;

        return season.Episodes.Any(episode => GetEntityKey(episode) == key);
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
    /// <returns><see langword="true"/> when the airing is part of the range.</returns>
    private static bool IsInRange(IEpisodeAiring airing, DateTime fromUtc, DateTime toUtc, bool includeDelayedOriginalSlots)
    {
        if ((airing.AiredAt ?? airing.OriginalAiredAt) is { } slot && slot >= fromUtc && slot <= toUtc)
            return true;

        return includeDelayedOriginalSlots && airing.IsDelayed && airing.OriginalAiredAt is { } original && original >= fromUtc && original <= toUtc;
    }

    /// <summary>
    /// The episodes a range read may need an estimate for: the ones a running
    /// schedule has no airing for whose AniDB date lands near the range, which
    /// is what puts a not-yet-scheduled episode on a calendar at all.
    /// </summary>
    /// <param name="context">The read the resolutions belong to.</param>
    /// <param name="fromUtc">The start of the range.</param>
    /// <param name="toUtc">The end of the range.</param>
    /// <param name="options">The filters to apply.</param>
    /// <returns>The candidate episode keys.</returns>
    private IEnumerable<(DataSource Source, string ID)> GetEstimableEpisodeKeys(
        AiringReadContext context,
        DateTime fromUtc,
        DateTime toUtc,
        EpisodeAiringFilteringOptions options
    )
    {
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
            if (airings.Count > 0 && airings.Max(entry => entry.AiredAt ?? entry.OriginalAiredAt) is { } latest && latest < fromUtc - _dormantScheduleWindow)
                continue;

            var profile = context.GetProfile(row);
            if ((profile.Offset ?? profile.AnidbOffset) is not { } offset)
                continue;

            // The window is widened by the slot itself and by any trailing
            // shift, since an estimate lands that far from the AniDB date.
            var slack = offset.Duration() + TimeSpan.FromDays(1 + Math.Abs(profile.TrailingShiftDays));
            var covered = airings.Select(entry => (entry.EpisodeSource, entry.EpisodeID)).ToHashSet();
            foreach (var episode in GetScheduleEpisodes(context, row))
            {
                if (episode.Type is not EpisodeType.Episode)
                    continue;

                var key = GetEntityKey(episode);
                if (covered.Contains(key))
                    continue;
                if (context.GetAnidbAirDate(episode) is not { } airDate || airDate < fromUtc - slack || airDate > toUtc + slack)
                    continue;

                yield return key;
            }
        }
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
    /// <returns><see langword="true"/> when the track matches.</returns>
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
    /// What makes two schedules the same release for de-duplication: the full
    /// track set, so a channel two providers both report collapses while a
    /// channel carrying two different track sets doesn't.
    /// </summary>
    /// <param name="view">The airing.</param>
    /// <returns>The signature.</returns>
    private static string GetTrackSignature(EpisodeAiringView view)
        => string.Join(
            "|",
            view.Tracks
                .Select(track => $"{track.Kind}/{track.LanguageCode}/{track.CountryCode}")
                .OrderBy(entry => entry, StringComparer.Ordinal)
        );

    #endregion

    #region Estimates

    /// <summary>
    /// The estimate profile of a schedule, learned from its own airings the
    /// first time it is needed and kept until something changes it.
    /// </summary>
    /// <param name="row">The schedule to learn from.</param>
    /// <param name="context">The read the resolutions belong to.</param>
    /// <returns>The schedule's profile.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="row"/> or <paramref name="context"/> is <see langword="null"/>.</exception>
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
            InvalidateProfilesForSeries(season.Source, season.SeriesID.ToString());

        if (season is IShokoSeason shokoSeason)
            foreach (var linked in shokoSeason.LinkedSeasons)
                InvalidateProfilesForSeries(linked.Source, linked.SeriesID.ToString());
    }

    /// <inheritdoc/>
    public void InvalidateForEpisode(IEpisode episode)
    {
        ArgumentNullException.ThrowIfNull(episode);

        var context = new AiringReadContext(this, includeDisabled: true);
        foreach (var (source, id) in context.GetLinkedEpisodeKeys(episode))
            if (context.GetEpisode(source, id) is { } entry)
                InvalidateProfilesForSeries(source, entry.SeriesID.ToString());
    }

    /// <summary>
    /// The series keys an invalidation touches: the series itself, and the ones
    /// linked to it when it is a shoko series.
    /// </summary>
    /// <param name="series">The series being invalidated.</param>
    /// <returns>The keys to clear.</returns>
    private IEnumerable<(DataSource Source, string ID)> GetInvalidationKeys(ISeries series)
    {
        var keys = new HashSet<(DataSource, string)> { GetEntityKey(series) };
        foreach (var shokoSeries in series is IShokoSeries own ? [own] : series.ShokoSeries)
        {
            keys.Add(GetEntityKey(shokoSeries));
            foreach (var linked in shokoSeries.LinkedSeries)
                keys.Add(GetEntityKey(linked));
            foreach (var linked in GetResolverLinks<ISeries>(shokoSeries))
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
    private void InvalidateProfilesForSeries(DataSource source, string id)
    {
        foreach (var row in RepoFactory.AiringSchedule.GetBySeriesID(source, id))
            _profiles.TryRemove(row.AiringScheduleID, out _);
    }

    #endregion

    #region Retention

    /// <summary>
    /// Remove the schedules whose run ended longer ago than the retention
    /// window, with their airings, and the schedules a provider created but
    /// never filled. A running schedule keeps every airing, however old, so a
    /// decades-long weekly show stays whole.
    /// </summary>
    /// <returns>How many schedules were removed.</returns>
    internal int RunRetentionSweep()
    {
        var settings = LoadSettings();
        if (!settings.AutoCleanup)
            return 0;

        var now = DateTime.UtcNow;
        var cutoff = now.AddMonths(-settings.RetentionMonths);
        var removed = 0;
        foreach (var row in RepoFactory.AiringSchedule.GetAll().ToList())
        {
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
