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
///   <c>removeFromMyList</c> toggle with two WebUI-visible entries — modeled as
///   two variant actions instead of one parameterized action, keeping
///   <see cref="Execute"/> genuinely parameterless.
/// </remarks>
public sealed class RemoveMissingFilesNoMyListSyncAction(ActionService actionService) : IExecutableAction
{
    public string Name => "Remove Missing Files (No MyList Sync)";

    public string? Description => "Remove entries in the Shoko database for files that are no longer accessible, without syncing their AniDB MyList state.";

    public ActionCategory Category => ActionCategory.Import;

    // The legacy RemoveMissingFiles endpoint had no admin gate; keep User-level to avoid regressing callers.
    public ActionPermission Permission => ActionPermission.User;

    public Task Execute(CancellationToken token = default)
        => actionService.RemoveRecordsWithoutPhysicalFiles(removeMyList: false);
}
