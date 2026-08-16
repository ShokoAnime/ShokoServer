using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Server.Services;

namespace Shoko.Server.Actions;

/// <summary>
///   Verify all unverified AniDB relations by fetching current data via the UDP API.
/// </summary>
public sealed class VerifyAllRelationsAction(ActionService actionService) : IExecutableAction
{
    public string Name => "Verify All Relations";

    public string? Description => "Verify all unverified AniDB relations by fetching current data via the UDP API.";

    public ActionCategory Category => ActionCategory.AniDB;

    public ActionPermission Permission => ActionPermission.Admin;

    public Task Execute(CancellationToken token = default)
        => actionService.VerifyAllUnverifiedRelations();
}
