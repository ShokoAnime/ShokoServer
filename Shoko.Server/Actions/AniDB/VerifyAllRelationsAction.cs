using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Verify all unverified AniDB relations by fetching current data via the UDP API.
/// </summary>
/// <remarks>
///   Only queues the verifications, which run on their own; the progress covers
///   the queuing.
/// </remarks>
public sealed class VerifyAllRelationsAction(ActionService actionService) : IScheduledAction
{
    public string Name => "Verify All Relations";

    public string? Description => "Verify all unverified AniDB relations by fetching current data via the UDP API.";

    public ActionCategory Category => ActionCategory.AniDB;

    public TimeSpan? MinimumInterval => TimeSpan.FromHours(6);

    public bool ScheduleCountsManualRuns => true;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => actionService.VerifyAllUnverifiedRelations(progress, token);
}
