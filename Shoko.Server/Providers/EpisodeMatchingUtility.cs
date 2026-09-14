using System;

#nullable enable
namespace Shoko.Server.Providers;

/// <summary>
/// Air-date helpers shared by the episode linking services, so every provider
/// scores dates the same way.
/// </summary>
public static class EpisodeMatchingUtility
{
    /// <summary>
    /// The strict window used for a confident air-date match.
    /// </summary>
    public const int MaxDifferenceInDays = 2;

    /// <summary>
    /// Upper bound for the nearest-air-date fallback, so a long hiatus or a
    /// special dated far from anything doesn't get confidently linked to an
    /// unrelated episode.
    /// </summary>
    public const int MaxFallbackDifferenceInDays = 120;

    /// <summary>
    /// Score how well two air dates agree within a strict window. Returns 1
    /// for the same day, decaying linearly to 0 at <paramref name="maxDifferenceInDays"/>.
    /// </summary>
    public static double CalculateAirDateProbability(DateOnly? firstDate, DateOnly? secondDate, int maxDifferenceInDays = MaxDifferenceInDays)
    {
        var difference = CalculateAirDateDistance(firstDate, secondDate);
        if (difference is null)
            return 0;

        if (difference == 0)
            return 1;

        if (difference <= maxDifferenceInDays)
            return (maxDifferenceInDays - difference.Value) / (double)maxDifferenceInDays;

        return 0;
    }

    /// <summary>
    /// Unbounded companion to <see cref="CalculateAirDateProbability"/>. Returns
    /// the raw day distance, or <see langword="null"/> if either date is unknown.
    /// </summary>
    public static int? CalculateAirDateDistance(DateOnly? firstDate, DateOnly? secondDate)
        => !firstDate.HasValue || !secondDate.HasValue ? null : Math.Abs(secondDate.Value.DayNumber - firstDate.Value.DayNumber);
}
