using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Download all missing AniDB creator data via the UDP API.
/// </summary>
public sealed class DownloadMissingAnidbCreatorsAction(ActionService actionService) : IScheduledAction
{
    public string Name => "Download Missing AniDB Creators";

    public string? Description => "Download all missing or incomplete AniDB creator data via the UDP API.";

    public ActionCategory Category => ActionCategory.AniDB;

    public TimeSpan? MinimumInterval => TimeSpan.FromHours(6);

    public bool ScheduleCountsManualRuns => true;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => actionService.ScheduleMissingAnidbCreators();
}
