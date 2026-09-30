using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.Metadata.Anidb.Enums;
using Shoko.Abstractions.Metadata.Anidb.Models;
using Shoko.Abstractions.Metadata.Anidb.Services;
using Shoko.Abstractions.ScheduledActions;

namespace Shoko.Server.Actions;

/// <summary>
///   Sync all local state to the AniDB MyList. This overwrites AniDB data.
/// </summary>
public sealed class SyncAnidbMylistAction(IMylistService mylistService) : IScheduledAction
{
    public string Name => "Sync AniDB MyList";

    public string? Description => "Sync all local state to the AniDB MyList. This can overwrite AniDB data irreversibly.";

    public ActionCategory Category => ActionCategory.Sync;

    public TimeSpan? MinimumInterval => TimeSpan.FromHours(6);

    public bool ScheduleCountsManualRuns => true;

    public bool RequiresConfirmation => true;

    public string? ConfirmationMessage => "Are you sure you want to sync local state with the AniDB MyList for all series? This may take a while.";

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => mylistService.ScheduleSync(new MylistSyncOptions { FetchMode = MylistFetchMode.IgnoreTimeCheck });
}
