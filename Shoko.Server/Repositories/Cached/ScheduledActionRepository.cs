using System;
using Shoko.Server.Databases;
using Shoko.Server.Models.Internal;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

/// <summary>
/// Cached repository for <see cref="ScheduledAction"/>, the schedule of the
/// global actions.
/// </summary>
/// <remarks>
/// One row per global action, so the table is as small as the action list and
/// is read on every tick of the scheduler. Cached rather than direct for that
/// reason.
/// </remarks>
/// <param name="databaseFactory">The database factory.</param>
public class ScheduledActionRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<ScheduledAction, int>(databaseFactory)
{
    private PocoIndex<int, ScheduledAction, Guid>? _actionIDs;

    /// <inheritdoc/>
    protected override int SelectKey(ScheduledAction entity)
        => entity.ScheduledActionID;

    /// <inheritdoc/>
    public override void PopulateIndexes()
    {
        _actionIDs = Cache.CreateIndex(schedule => schedule.ActionID);
    }

    /// <summary>
    /// Gets the schedule of an action.
    /// </summary>
    /// <param name="actionID">The ID of the action.</param>
    /// <returns>The schedule, or <c>null</c> when the scheduler never saw the action.</returns>
    public ScheduledAction? GetByActionID(Guid actionID)
        => _actionIDs!.GetOne(actionID);
}
