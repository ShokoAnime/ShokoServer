using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.ScheduledActions;

namespace Shoko.Server.Actions;

/// <summary>
///   Forcibly runs AddToMylist commands for all manually linked files.
/// </summary>
public sealed class AddAllManualLinksToMylistAction(IMylistService mylistService) : IScheduledAction
{
    public string Name => "Add All Manual Links to MyList";

    public string? Description => "Forcibly run AddToMylist commands for all files with manual links.";

    public ActionCategory Category => ActionCategory.Sync;

    public TimeSpan? MinimumInterval => TimeSpan.FromHours(6);

    public bool ScheduleCountsManualRuns => true;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => mylistService.ScheduleAddAllManualLinks();
}
