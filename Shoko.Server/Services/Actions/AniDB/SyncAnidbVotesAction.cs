using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Action;
using Shoko.Abstractions.Action.Enums;

namespace Shoko.Server.Services.Actions.AniDB;

/// <summary>
///   Sync local votes to AniDB.
/// </summary>
internal sealed class SyncAnidbVotesAction(ActionService actionService) : IExecutableGlobalSystemAction
{
    public string Name => "Sync AniDB Votes";
    public string? Description => "Export local votes to AniDB.";
    public ActionCategory Category => ActionCategory.AniDB;

    public Task Execute(CancellationToken cancellationToken = default)
        => actionService.RunImport_SyncVotes();
}
