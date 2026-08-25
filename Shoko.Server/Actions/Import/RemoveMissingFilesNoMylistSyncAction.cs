using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Remove entries in the Shoko database for files that are no longer
///   accessible, without syncing their AniDB MyList state.
/// </summary>
/// <remarks>
///   The legacy RemoveMissingFiles endpoint exposed a single
///   <c>removeFromMylist</c> toggle with two WebUI-visible entries — modeled as
///   two variant actions instead of one parameterized action, keeping
///   <see cref="Execute"/> genuinely parameterless.
/// </remarks>
public sealed class RemoveMissingFilesNoMylistSyncAction(ActionService actionService) : IExecutableAction
{
    public string Name => "Remove Missing Files (No Mylist Sync)";

    public string? Description => "Remove entries in the Shoko database for files that are no longer accessible, without syncing their AniDB Mylist state.";

    public ActionCategory Category => ActionCategory.Import;

    public ActionPermission Permission => ActionPermission.User;

    public Task Execute(CancellationToken token = default)
        => actionService.RemoveRecordsWithoutPhysicalFiles(removeMylist: false);
}
