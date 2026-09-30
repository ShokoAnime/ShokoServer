using System;
using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.ScheduledActions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Process any pending AniDB file-moved notifications.
/// </summary>
public sealed class RefreshAnidbMovedFilesAction(ActionService actionService) : IScheduledAction
{
    public string Name => "Refresh AniDB Moved Files";

    public string? Description => "Process pending AniDB file-moved notifications and update affected files.";

    public ActionCategory Category => ActionCategory.AniDB;

    public Task Execute(IProgress<decimal> progress, CancellationToken token)
        => actionService.RefreshAniDBMovedFiles(true);
}
