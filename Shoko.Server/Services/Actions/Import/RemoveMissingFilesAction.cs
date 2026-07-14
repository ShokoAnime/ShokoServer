using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;

namespace Shoko.Server.Services.Actions.Import;

/// <summary>
///   Remove database records for files that are no longer accessible on disk.
/// </summary>
internal sealed class RemoveMissingFilesAction(ActionService actionService) : IExecutableGlobalSystemAction
{
    public string Name => "Remove Missing Files";
    public string? Description => "Remove database records for files that are no longer accessible on disk.";
    public ActionCategory Category => ActionCategory.Import;
    public bool RequiresConfirmation => true;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await actionService.RemoveRecordsWithoutPhysicalFiles(true);
    }
}
