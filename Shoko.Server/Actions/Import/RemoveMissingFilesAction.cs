using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Remove entries in the Shoko database for files that are no longer
///   accessible, and from the user's AniDB MyList too. The configured delete
///   type still decides what removal means, so <c>DeleteLocalOnly</c> leaves
///   the MyList alone.
/// </summary>
public sealed class RemoveMissingFilesAction(ActionService actionService) : IScheduledAction
{
    public string Name => "Remove Missing Files";

    public string? Description => "Remove entries in the Shoko database for files that are no longer accessible.";

    public ActionCategory Category => ActionCategory.Import;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => actionService.RemoveRecordsWithoutPhysicalFiles(removeMylist: true);
}
