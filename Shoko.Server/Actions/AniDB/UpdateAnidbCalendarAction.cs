using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.AniDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Update the AniDB calendar data for use on the dashboard.
/// </summary>
public sealed class UpdateAnidbCalendarAction(IQueueScheduler scheduler) : IExecutableAction
{
    public string Name => "Update AniDB Calendar";

    public string? Description => "Update the AniDB calendar data for use on the dashboard.";

    public ActionCategory Category => ActionCategory.AniDB;

    // The legacy UpdateAniDBCalendar endpoint was admin-gated; keep Admin-level so the permission surface does not widen.
    public ActionPermission Permission => ActionPermission.Admin;

    public Task Execute(CancellationToken token = default)
        => scheduler.Enqueue<GetAniDBCalendarJob>(c => c.ForceRefresh = true, ct: token);
}
