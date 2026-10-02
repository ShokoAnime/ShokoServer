using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Update AniDB release info for files with missing or incomplete group data.
/// </summary>
/// <remarks>
///   Only queues the release searches and release group fetches, which run on their own; the progress covers
///   the queuing.
/// </remarks>
public sealed class UpdateMissingAnidbFileInfoAction(ActionService actionService) : IScheduledAction
{
    public string Name => "Update Missing AniDB File Info";

    public string? Description => "Update AniDB release info for files with missing or incomplete group information.";

    public ActionCategory Category => ActionCategory.AniDB;

    public TimeSpan? MinimumInterval => TimeSpan.FromHours(6);

    public bool ScheduleCountsManualRuns => true;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => actionService.UpdateAnidbReleaseInfo(progress: progress, token: token);
}
