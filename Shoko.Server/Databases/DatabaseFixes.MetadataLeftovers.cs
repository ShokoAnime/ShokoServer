using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Actions;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Scheduling;
using Shoko.Server.Services;

#pragma warning disable CS0618
namespace Shoko.Server.Databases;

public partial class DatabaseFixes
{
    #region Metadata Leftovers | Steps

    /// <summary>
    ///   Removes what the stores still hold for unlinked entries with no row
    ///   of their own, as the daily orphan purge does, and has that purge run
    ///   once soon after start for the linked ones, which it refreshes.
    /// </summary>
    public static void PurgeMetadataLeftovers()
    {
        var services = ISystemService.StaticServices;
        var purgeService = services.GetRequiredService<MetadataPurgeService>();
        var (storeRemoved, storeLinked) = purgeService.PurgeStoreLeftovers().GetAwaiter().GetResult();
        _logger.Info(
            $"Removed the leftovers of {storeRemoved} unlinked series; left {storeLinked.Count} linked series without a row of their own for a refresh."
        );
        if (storeLinked.Count is 0)
            return;

        var marked = RunScheduledActionAtStart(
            services.GetRequiredService<IScheduledActionSource>(),
            services.GetRequiredService<ScheduledActionRepository>(),
            typeof(PurgeExpiredOrphanedMetadataAction),
            DateTime.UtcNow
        );
        if (marked)
            _logger.Info("The orphaned metadata purge runs once soon after start, which refreshes them.");
        else
            _logger.Warn("The orphaned metadata purge is not registered; its daily run refreshes them once it is.");
    }

    #endregion

    #region Metadata Leftovers | Helpers

    /// <summary>
    ///   Makes a scheduled action's next run due, so the scheduler runs it
    ///   once at start-up as a run missed while the server was down, then
    ///   keeps to its triggers. One whose triggers the admin cleared stays off.
    /// </summary>
    /// <param name="source">The registered scheduled actions.</param>
    /// <param name="schedules">The schedule rows.</param>
    /// <param name="actionType">The scheduled action type.</param>
    /// <param name="now">The time now, in UTC.</param>
    /// <returns><see langword="true"/> when the action is registered and its row was saved.</returns>
    internal static bool RunScheduledActionAtStart(IScheduledActionSource source, ScheduledActionRepository schedules, Type actionType, DateTime now)
    {
        if (source.GetAction(actionType) is not { } action)
            return false;

        // The schedule counts from the last run it counts, or else from when
        // the row was made, so with neither in range of a trigger it is due.
        var anchor = now - ActionTrigger.MaximumInterval;
        var row = schedules.GetByActionID(action.ID) ?? new() { ActionID = action.ID, CreatedAt = anchor };
        if (row.CreatedAt > anchor)
            row.CreatedAt = anchor;
        row.LastScheduledRunAt = null;
        if (action.ScheduleCountsManualRuns)
            row.LastRunAt = null;
        schedules.Save(row);
        return true;
    }

    #endregion
}
