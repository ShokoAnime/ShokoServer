using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;

namespace Shoko.Server.Services.Actions.AniDB;

/// <summary>
///   Verify all unverified AniDB relations by fetching current data via the UDP API.
/// </summary>
internal sealed class VerifyAllRelationsAction(ActionService actionService) : IExecutableGlobalAction
{
    public string Name => "Verify All Relations";
    public string? Description => "Verify all unverified AniDB relations by fetching current data via the UDP API.";
    public ActionCategory Category => ActionCategory.AniDB;

    public async Task Execute(CancellationToken cancellationToken = default)
    {
        await actionService.VerifyAllUnverifiedRelations();
    }
}
