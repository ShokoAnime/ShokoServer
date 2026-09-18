using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.Airing;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Repositories.Cached.Airing;

/// <summary>
/// Cached repository for <see cref="EpisodeAiring"/>. Per-episode reads go
/// through the episode key index and range reads through the day buckets, so
/// no read scans the cache.
/// </summary>
/// <param name="databaseFactory">The database factory.</param>
public class EpisodeAiringRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<EpisodeAiring, int>(databaseFactory)
{
    private PocoIndex<int, EpisodeAiring, int>? _scheduleIDs;

    private PocoIndex<int, EpisodeAiring, (DataSource EpisodeSource, string EpisodeID)>? _episodeKeys;

    private PocoIndex<int, EpisodeAiring, int?>? _linkHeads;

    private PocoIndex<int, EpisodeAiring, DateOnly?>? _dayBuckets;

    private PocoIndex<int, EpisodeAiring, DateOnly?>? _delayedDayBuckets;

    /// <inheritdoc/>
    protected override int SelectKey(EpisodeAiring entity)
        => entity.EpisodeAiringID;

    /// <inheritdoc/>
    public override void PopulateIndexes()
    {
        _scheduleIDs = Cache.CreateIndex(a => a.AiringScheduleID);
        _episodeKeys = Cache.CreateIndex(a => (a.EpisodeSource, a.EpisodeID));
        _linkHeads = Cache.CreateIndex(a => a.LinkedToID);
        _dayBuckets = Cache.CreateIndex(a => a.DayBucket);
        _delayedDayBuckets = Cache.CreateIndex(a => a.DelayedDayBucket);
    }

    /// <summary>
    /// Gets every airing on a schedule.
    /// </summary>
    /// <param name="scheduleID">The local database ID of the schedule.</param>
    /// <returns>The airings, ordered by their current slot.</returns>
    public IReadOnlyList<EpisodeAiring> GetByScheduleID(int scheduleID)
        => _scheduleIDs!.GetMultiple(scheduleID)
            .OrderBy(a => a.AiredAt ?? a.OriginalAiredAt ?? DateTime.MaxValue)
            .ThenBy(a => a.Key, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Gets the airing on a schedule with the given key, which is the pair the
    /// store is unique on.
    /// </summary>
    /// <param name="scheduleID">The local database ID of the schedule.</param>
    /// <param name="key">The airing's key.</param>
    /// <returns>The airing, or <c>null</c> when there is none.</returns>
    public EpisodeAiring? GetByScheduleIDAndKey(int scheduleID, string key)
        => string.IsNullOrEmpty(key)
            ? null
            : _scheduleIDs!.GetMultiple(scheduleID)
                .FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.Ordinal));

    /// <summary>
    /// Gets every airing attached to an episode, across every schedule and
    /// provider.
    /// </summary>
    /// <param name="episodeSource">The source of the episode.</param>
    /// <param name="episodeID">The ID of the episode within its source.</param>
    /// <returns>The airings, ordered by their current slot.</returns>
    public IReadOnlyList<EpisodeAiring> GetByEpisodeID(DataSource episodeSource, string episodeID)
        => string.IsNullOrEmpty(episodeID)
            ? []
            : _episodeKeys!.GetMultiple((episodeSource, episodeID))
                .OrderBy(a => a.AiredAt ?? a.OriginalAiredAt ?? DateTime.MaxValue)
                .ThenBy(a => a.EpisodeAiringID)
                .ToList();

    /// <summary>
    /// Gets every member of a link set, the head included, in one lookup.
    /// </summary>
    /// <param name="linkedToID">The local database ID of the link head.</param>
    /// <returns>The members, ordered by their local database ID, so the head comes first.</returns>
    public IReadOnlyList<EpisodeAiring> GetByLinkedToID(int linkedToID)
        => _linkHeads!.GetMultiple(linkedToID)
            .OrderBy(a => a.EpisodeAiringID)
            .ToList();

    /// <summary>
    /// Gets every airing whose current slot, or the slot it was first scheduled
    /// for when it has no current one, falls on the given UTC day.
    /// </summary>
    /// <param name="day">The UTC day.</param>
    /// <returns>The airings, ordered by their current slot.</returns>
    public IReadOnlyList<EpisodeAiring> GetByDay(DateOnly day)
        => _dayBuckets!.GetMultiple(day)
            .OrderBy(a => a.AiredAt ?? a.OriginalAiredAt ?? DateTime.MaxValue)
            .ThenBy(a => a.EpisodeAiringID)
            .ToList();

    /// <summary>
    /// Gets every airing falling within a range of UTC days, both ends
    /// included.
    /// </summary>
    /// <param name="from">The first UTC day.</param>
    /// <param name="to">The last UTC day.</param>
    /// <returns>The airings, ordered by their current slot.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is before <paramref name="from"/>.</exception>
    public IReadOnlyList<EpisodeAiring> GetByDayRange(DateOnly from, DateOnly to)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(to, from);

        return EnumerateDays(from, to)
            .SelectMany(day => _dayBuckets!.GetMultiple(day))
            .DistinctBy(a => a.EpisodeAiringID)
            .OrderBy(a => a.AiredAt ?? a.OriginalAiredAt ?? DateTime.MaxValue)
            .ThenBy(a => a.EpisodeAiringID)
            .ToList();
    }

    /// <summary>
    /// Gets every delayed airing whose original slot fell on the given UTC day,
    /// so a week an episode was delayed out of still has something to draw its
    /// gap from.
    /// </summary>
    /// <param name="day">The UTC day.</param>
    /// <returns>The airings, ordered by their original slot.</returns>
    public IReadOnlyList<EpisodeAiring> GetDelayedByOriginalDay(DateOnly day)
        => _delayedDayBuckets!.GetMultiple(day)
            .OrderBy(a => a.OriginalAiredAt ?? DateTime.MaxValue)
            .ThenBy(a => a.EpisodeAiringID)
            .ToList();

    /// <summary>
    /// Gets every delayed airing whose original slot fell within a range of UTC
    /// days, both ends included.
    /// </summary>
    /// <param name="from">The first UTC day.</param>
    /// <param name="to">The last UTC day.</param>
    /// <returns>The airings, ordered by their original slot.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="to"/> is before <paramref name="from"/>.</exception>
    public IReadOnlyList<EpisodeAiring> GetDelayedByOriginalDayRange(DateOnly from, DateOnly to)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(to, from);

        return EnumerateDays(from, to)
            .SelectMany(day => _delayedDayBuckets!.GetMultiple(day))
            .DistinctBy(a => a.EpisodeAiringID)
            .OrderBy(a => a.OriginalAiredAt ?? DateTime.MaxValue)
            .ThenBy(a => a.EpisodeAiringID)
            .ToList();
    }

    /// <summary>
    /// Walks the days in a range, both ends included.
    /// </summary>
    /// <param name="from">The first UTC day.</param>
    /// <param name="to">The last UTC day.</param>
    /// <returns>Every day in the range.</returns>
    private static IEnumerable<DateOnly?> EnumerateDays(DateOnly from, DateOnly to)
    {
        // Walked by day number rather than `AddDays`, because a range ending on
        // `DateOnly.MaxValue` would otherwise step past it and throw.
        for (var dayNumber = from.DayNumber; dayNumber <= to.DayNumber; dayNumber++)
            yield return DateOnly.FromDayNumber(dayNumber);
    }
}
