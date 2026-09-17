using System;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.Airing;
using Shoko.Server.Services;
using Shoko.Server.Settings;

#nullable enable
namespace Shoko.Server.Scheduling.Watchdog;

/// <summary>
/// The queue watchdog's threshold for <see cref="SweepAiringScheduleProviderJob"/>, which is the
/// global one until the sweep budget grows past it and half as long again as the budget after that.
/// </summary>
/// <remarks>
/// <para>
/// A chunk is allowed to run for <see cref="AiringScheduleServiceSettings.SweepBudgetSeconds"/> and
/// not a moment more, so a chunk still running well past that is a provider that is not observing
/// the token it was handed. That is worth a report rather than an exemption, and the margin is what
/// keeps the report to a real overrun: a chunk that spends its whole budget and returns is doing
/// exactly what it is meant to.
/// </para>
/// <para>
/// The budget is a setting, so the threshold is worked out on each poll rather than pinned at
/// startup, and a budget lowered or raised while the server runs is watched against at once.
/// </para>
/// </remarks>
/// <param name="airingScheduleService">The service that owns the budget.</param>
public class AiringScheduleSweepWatchdogThreshold(AiringScheduleService airingScheduleService) : IJobWatchdogThreshold
{
    /// <summary>
    /// How much longer than its budget a chunk may run before it is reported.
    /// </summary>
    private const double BudgetMargin = 1.5d;

    /// <inheritdoc/>
    public Type JobType => typeof(SweepAiringScheduleProviderJob);

    /// <inheritdoc/>
    public TimeSpan? GetThreshold(TimeSpan defaultThreshold)
    {
        var budget = airingScheduleService.GetSweepBudget();
        var threshold = budget * BudgetMargin;
        return threshold > defaultThreshold ? threshold : defaultThreshold;
    }
}
