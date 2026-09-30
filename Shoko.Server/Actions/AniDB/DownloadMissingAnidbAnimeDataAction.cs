using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Download missing AniDB XML data for anime, and fix cross-references with
///   incomplete data.
/// </summary>
public sealed class DownloadMissingAnidbAnimeDataAction(ActionService actionService) : IScheduledAction
{
    public string Name => "Download Missing AniDB Anime Data";

    public string? Description => "Download missing AniDB XML data and fix cross-references with incomplete data.";

    public ActionCategory Category => ActionCategory.AniDB;

    public TimeSpan? MinimumInterval => TimeSpan.FromHours(6);

    public bool ScheduleCountsManualRuns => true;

    public async Task Execute(IProgress<decimal> progress, CancellationToken token)
    {
        await actionService.DownloadMissingAnidbAnimeXmls();
        await actionService.ScheduleMissingAnidbAnimeForFiles();
    }
}
