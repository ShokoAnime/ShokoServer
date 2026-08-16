using System.Threading;
using System.Threading.Tasks;
using Shoko.Abstractions.Actions;
using Shoko.Abstractions.User;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Scheduling.Jobs.AniDB;

namespace Shoko.Server.Actions;

/// <summary>
///   Import votes from AniDB for the invoking user.
/// </summary>
public sealed class SyncVotesImportAction(IQueueScheduler scheduler) : IExecutableAction, IActionCaller
{
    private IUser _caller = null!;

    public string Name => "Sync Votes (Import)";

    public string? Description => "Import votes from AniDB for the invoking user.";

    public ActionCategory Category => ActionCategory.AniDB;

    public ActionPermission Permission => ActionPermission.User;

    void IActionCaller.SetCaller(IUser caller) => _caller = caller;

    public Task<ActionValidationResult?> Validate(CancellationToken token = default)
        => Task.FromResult(_caller.IsAnidbUser ? null : new ActionValidationResult("User is not an AniDB user. Nothing to do."));

    public Task Execute(CancellationToken token = default)
        => scheduler.Enqueue<SyncAniDBVotesJob>(c => (c.UserID, c.Export) = (_caller.ID, false), ct: token);
}
