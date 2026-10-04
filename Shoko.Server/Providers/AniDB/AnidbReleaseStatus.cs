using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Providers.AniDB;

/// <summary>
///   Infers where an AniDB anime is in its release from its dates, type and
///   episodes, since AniDB does not say. Cancelled and on-hiatus works look
///   the same as releasing ones in AniDB's data, so those are never inferred.
/// </summary>
public static class AnidbReleaseStatus
{
    /// <summary>
    ///   Infers the release status of an anime.
    /// </summary>
    /// <param name="airDate">
    ///   When the anime started airing. Without one the status is
    ///   <see cref="ReleaseStatus.Unknown"/>.
    /// </param>
    /// <param name="endDate">
    ///   When the anime finished airing, if AniDB knows.
    /// </param>
    /// <param name="animeType">
    ///   The anime type. A movie is a single release, so it is finished once
    ///   it has started.
    /// </param>
    /// <param name="normalEpisodeCount">
    ///   The number of normal episodes AniDB expects, or 0 when unknown. One
    ///   normal episode is a single release, like a movie.
    /// </param>
    /// <param name="normalEpisodeAirDates">
    ///   The air dates of the normal episodes AniDB lists, <c>null</c>
    ///   for an episode without one. Only enumerated when the dates alone do
    ///   not decide.
    /// </param>
    /// <param name="today">The local date to judge against.</param>
    /// <returns>
    ///   <see cref="ReleaseStatus.NotYetReleased"/> before the air date,
    ///   <see cref="ReleaseStatus.Finished"/> once the end date has passed or
    ///   every expected episode has aired, otherwise
    ///   <see cref="ReleaseStatus.Releasing"/>.
    /// </returns>
    public static ReleaseStatus Infer(
        PartialDateOnly? airDate,
        PartialDateOnly? endDate,
        Abstractions.Metadata.Enums.AnimeType animeType,
        int normalEpisodeCount,
        IEnumerable<DateOnly?> normalEpisodeAirDates,
        DateOnly today
    )
    {
        if (airDate is not { } start)
            return ReleaseStatus.Unknown;

        if (!HasPassed(start, today))
            return ReleaseStatus.NotYetReleased;

        if (endDate is { } end)
            return HasPassed(end, today) ? ReleaseStatus.Finished : ReleaseStatus.Releasing;

        if (animeType is Abstractions.Metadata.Enums.AnimeType.Movie || normalEpisodeCount is 1)
            return ReleaseStatus.Finished;

        if (normalEpisodeCount > 0 && HaveAllAired(normalEpisodeAirDates, normalEpisodeCount, today))
            return ReleaseStatus.Finished;

        return ReleaseStatus.Releasing;
    }

    /// <summary>
    ///   Checks whether an end date has passed.
    /// </summary>
    /// <param name="endDate">The end date, if known.</param>
    /// <param name="today">The local date to judge against.</param>
    /// <returns>
    ///   <c>true</c> when the end date is known and falls on or
    ///   before <paramref name="today"/>, taking a partial date as the last
    ///   day it could mean.
    /// </returns>
    public static bool HasEnded(PartialDateOnly? endDate, DateOnly today)
        => endDate is { } end && HasPassed(end, today);

    /// <summary>
    ///   Checks whether a date is on or before today. A year-only or
    ///   year-month date counts as the last day it could mean, so a date in
    ///   the current year or month has not passed yet.
    /// </summary>
    /// <param name="date">The date to check.</param>
    /// <param name="today">The local date to judge against.</param>
    /// <returns>
    ///   <c>true</c> when the last day <paramref name="date"/> could
    ///   mean is on or before <paramref name="today"/>.
    /// </returns>
    public static bool HasPassed(PartialDateOnly date, DateOnly today)
        => LastDay(date) <= today;

    private static DateOnly LastDay(PartialDateOnly date)
    {
        var month = date.Month ?? 12;
        var day = date.Day ?? DateTime.DaysInMonth(date.Year, month);
        return new(date.Year, month, day);
    }

    private static bool HaveAllAired(IEnumerable<DateOnly?> airDates, int expectedCount, DateOnly today)
    {
        var aired = 0;
        foreach (var airDate in airDates)
        {
            if (airDate is not { } date || date > today)
                return false;

            aired++;
        }

        return aired >= expectedCount;
    }
}
