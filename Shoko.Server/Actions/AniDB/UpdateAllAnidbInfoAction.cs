using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Refresh all AniDB anime info from the remote API.
/// </summary>
public sealed class UpdateAllAnidbInfoAction(ActionService actionService) : IScheduledAction
{
    public string Name => "Update All AniDB Info";

    public string? Description => "Refresh all AniDB anime information from the remote API.";

    public ActionCategory Category => ActionCategory.AniDB;

    public TimeSpan? MinimumInterval => TimeSpan.FromHours(6);

    public bool ScheduleCountsManualRuns => true;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => actionService.RunImport_UpdateAllAniDB();
}
