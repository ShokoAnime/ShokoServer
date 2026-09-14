using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable
namespace Shoko.Server.Utilities;

/// <summary>
/// Learns the broadcast time of a series from the episodes where a precise
/// air time is known, so it can be applied to the episodes where only the
/// AniDB air date is.
/// </summary>
public static class AirTimeUtility
{
    /// <summary>
    /// Default number of most recent samples to learn from, so a long-running
    /// series follows its current time slot rather than one it had years ago.
    /// </summary>
    public const int DefaultWindow = 10;

    /// <summary>
    /// Learn the offset between an AniDB air date, taken as midnight UTC, and
    /// the actual air time in UTC, from the most recent samples.
    /// </summary>
    /// <param name="samples">Pairs of the AniDB air date and the precise air time (UTC) for the same episode.</param>
    /// <param name="window">How many of the most recent samples to consider.</param>
    /// <param name="minimumSamples">How many samples are needed before an offset is trusted.</param>
    /// <returns>The median offset, or <see langword="null"/> with too few samples.</returns>
    public static TimeSpan? LearnAirTimeOffset(IEnumerable<(DateTime AnidbAirDate, DateTime AiredAtUtc)> samples, int window = DefaultWindow, int minimumSamples = 2)
    {
        var offsets = samples
            .OrderByDescending(sample => sample.AnidbAirDate)
            .Take(window)
            .Select(sample => sample.AiredAtUtc - DateTime.SpecifyKind(sample.AnidbAirDate.Date, DateTimeKind.Utc))
            .OrderBy(offset => offset)
            .ToList();
        if (offsets.Count < minimumSamples)
            return null;

        // The median, so a special aired in another slot or a one-off delay doesn't skew it.
        var middle = offsets.Count / 2;
        return offsets.Count % 2 is 1
            ? offsets[middle]
            : TimeSpan.FromTicks((offsets[middle - 1].Ticks + offsets[middle].Ticks) / 2);
    }

    /// <summary>
    /// Apply a learned offset to an AniDB air date.
    /// </summary>
    /// <param name="anidbAirDate">The AniDB air date.</param>
    /// <param name="offset">The learned offset from midnight UTC.</param>
    /// <returns>The estimated air time in UTC.</returns>
    public static DateTime EstimateAirTime(DateTime anidbAirDate, TimeSpan offset)
        => DateTime.SpecifyKind(anidbAirDate.Date, DateTimeKind.Utc) + offset;
}
