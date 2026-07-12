using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;

namespace Shoko.Server.Services.Actions.AniDB;

/// <summary>
///   Process any pending AniDB file-moved notifications.
/// </summary>
internal sealed class RefreshAnidbMovedFilesAction(ActionService actionService) : IExecutableGlobalAction
{
    public string Name => "Refresh AniDB Moved Files";
    public string? Description => "Process pending AniDB file-moved notifications and update affected files.";
    public ActionCategory Category => ActionCategory.AniDB;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await actionService.RefreshAniDBMovedFiles(true);
    }
}
