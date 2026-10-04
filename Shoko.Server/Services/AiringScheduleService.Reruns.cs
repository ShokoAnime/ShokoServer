using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Server.Models.Airing;
using Shoko.Server.Repositories;
using Shoko.Server.Services.Airing;

#nullable enable
namespace Shoko.Server.Services;

public partial class AiringScheduleService
{
    /// <summary>
    /// How long after the earliest showing elsewhere a schedule's first airing
    /// has to come for the whole run to be a rerun. A regional channel a few
    /// weeks behind the premiere stays well inside it.
    /// </summary>
    internal static readonly TimeSpan LateRerunThreshold = TimeSpan.FromDays(56);

    /// <summary>
    /// How long after the earliest showing elsewhere a marathon has to start to
    /// be a rerun, so a release several services drop the same day is never
    /// taken for a rerun of itself.
    /// </summary>
    internal static readonly TimeSpan MarathonRerunMinimumLead = TimeSpan.FromDays(1);

    /// <summary>
    /// How many episodes one local day of a schedule has to air, at the least,
    /// to be a marathon. The day also has to hold half the schedule's airings.
    /// </summary>
    internal const int MarathonMinimumEpisodes = 3;

    /// <summary>
    /// How many of a schedule's first episodes are looked up elsewhere. A rerun
    /// starts on episodes that aired before, so the first few are enough.
    /// </summary>
    internal const int RerunProbeEpisodes = 3;

    #region Episode Airings | Reruns

    /// <summary>
    /// Whether a schedule's normal airings are a rerun of an earlier run of the
    /// same episodes on another schedule sharing one of its tracks: its first
    /// airing comes <see cref="LateRerunThreshold"/> or more after the earliest
    /// of them, or it is a marathon that starts
    /// <see cref="MarathonRerunMinimumLead"/> or more after it.
    /// </summary>
    /// <remarks>
    /// Only stored <see cref="EpisodeAiringKind.Normal"/> airings count, on
    /// either side, and the episodes are matched through their links, so the
    /// verdict is the same whatever the read filters by. A marathon with no
    /// earlier showing is a batch release and stays normal.
    /// </remarks>
    /// <param name="context">The read the lookups are cached in.</param>
    /// <param name="row">The schedule.</param>
    /// <returns><c>true</c> when the schedule is detected as a rerun.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> or <paramref name="row"/> is <c>null</c>.</exception>
    internal static bool DetectRerun(AiringReadContext context, AiringSchedule row)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(row);

        var airings = RepoFactory.EpisodeAiring.GetByScheduleID(row.AiringScheduleID)
            .Where(entry => entry.Kind is EpisodeAiringKind.Normal && entry.AiredAt.HasValue)
            .OrderBy(entry => entry.AiredAt)
            .ToList();
        if (airings.Count is 0)
            return false;
        if (GetEarliestShowingElsewhere(context, row, airings) is not { } earliest)
            return false;

        var lead = airings[0].AiredAt!.Value - earliest;
        if (lead >= LateRerunThreshold)
            return true;

        return lead >= MarathonRerunMinimumLead && IsMarathon(row, airings);
    }

    /// <summary>
    /// The earliest normal airing of a schedule's first episodes on any other
    /// schedule sharing one of its tracks.
    /// </summary>
    /// <param name="context">The read the lookups are cached in.</param>
    /// <param name="row">The schedule.</param>
    /// <param name="airings">The schedule's normal airings with a slot, in slot order.</param>
    /// <returns>The earliest showing, or <c>null</c> when the episodes aired nowhere else.</returns>
    private static DateTime? GetEarliestShowingElsewhere(AiringReadContext context, AiringSchedule row, IReadOnlyList<EpisodeAiring> airings)
    {
        var probed = new HashSet<(MetadataSource Source, string ID)>();
        var earliest = default(DateTime?);
        foreach (var entry in airings)
        {
            if (!probed.Add((entry.EpisodeSource, entry.EpisodeID)))
                continue;

            foreach (var (schedule, airedAt) in context.GetFirstNormalAirings(entry.EpisodeSource, entry.EpisodeID))
            {
                if (schedule.AiringScheduleID == row.AiringScheduleID || earliest is { } current && airedAt >= current)
                    continue;
                if (!context.SharesTrack(row, schedule))
                    continue;

                earliest = airedAt;
            }

            if (probed.Count >= RerunProbeEpisodes)
                break;
        }

        return earliest;
    }

    /// <summary>
    /// Whether a schedule airs its run as a marathon: one local day holding at
    /// least <see cref="MarathonMinimumEpisodes"/> of its airings and at least
    /// half of them. The day is read in the schedule's time zone, else in UTC.
    /// </summary>
    /// <param name="row">The schedule.</param>
    /// <param name="airings">The schedule's normal airings with a slot.</param>
    /// <returns><c>true</c> when the run is a marathon.</returns>
    private static bool IsMarathon(AiringSchedule row, IReadOnlyList<EpisodeAiring> airings)
    {
        if (airings.Count < MarathonMinimumEpisodes)
            return false;

        var zone = row.TimeZoneID is { Length: > 0 } timeZoneID && TryResolveTimeZone(timeZoneID, out var resolved) ? resolved : TimeZoneInfo.Utc;
        var busiestDay = airings
            .GroupBy(entry => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(entry.AiredAt!.Value, DateTimeKind.Utc), zone)))
            .Max(group => group.Count());
        return busiestDay >= MarathonMinimumEpisodes && busiestDay * 2 >= airings.Count;
    }

    #endregion
}
