using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;

namespace Shoko.Server.Services.Actions.AniDB;

/// <summary>
///   Refresh all AniDB anime info from the remote API.
/// </summary>
internal sealed class UpdateAllAnidbInfoAction(ActionService actionService) : IExecutableGlobalAction
{
    public string Name => "Update All AniDB Info";
    public string? Description => "Refresh all AniDB anime information from the remote API.";
    public ActionCategory Category => ActionCategory.AniDB;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await actionService.RunImport_UpdateAllAniDB();
    }
}
