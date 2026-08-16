using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Remove entries in the Shoko database for files that are no longer
///   accessible, including their AniDB MyList state.
/// </summary>
public sealed class RemoveMissingFilesAction(ActionService actionService) : IExecutableAction
{
    public string Name => "Remove Missing Files";

    public string? Description => "Remove entries in the Shoko database for files that are no longer accessible.";

    public ActionCategory Category => ActionCategory.Import;

    // The legacy RemoveMissingFiles endpoint had no admin gate; keep User-level to avoid regressing callers.
    public ActionPermission Permission => ActionPermission.User;

    public Task Execute(CancellationToken token = default)
        => actionService.RemoveRecordsWithoutPhysicalFiles(removeMyList: true);
}
